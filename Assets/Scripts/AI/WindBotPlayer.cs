// WindBot Ignite AI를 이 프로젝트의 플레이어(IDuelPlayer)로 쓴다. 네트워크 없이 같은 프로세스에서 돈다.
// 엔진 메시지를 해석 전 바이너리 그대로 WindBot의 메시지 처리(WindBotGameBehavior)에 넣고, WindBot이 만든 응답 바이트를 엔진에 돌려준다.
// EDOPro 서버가 하던 일은 여기서 한다: MSG_START 만들기, 응답 요청 전에 MSG_UPDATE_DATA로 카드 정보 보내기,
// 상대(사람)의 비공개 정보 가리기(WindBotMessageFilter).
// WindBot이 예외를 내거나 응답이 거절되면 그 요청만 SimpleAutoPlayer로 대신 답하고 Fallbacks로 센다.
using System;
using System.Collections.Generic;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using WindBot.Game;
using WBCardLocation = YGOSharp.OCGWrapper.Enums.CardLocation;
using WBGameMessage = YGOSharp.OCGWrapper.Enums.GameMessage;

namespace DuelRoguelike.AI
{
    public sealed class WindBotPlayer : IDuelPlayer, IDuelMessageObserver
    {
        /// <summary>WindBot의 ClientCard.Update가 읽을 줄 아는 조회 항목만 (모르는 항목은 잘못 건너뛴다).</summary>
        const OcgQuery WindBotQuery =
            OcgQuery.Code | OcgQuery.Position | OcgQuery.Alias | OcgQuery.Type | OcgQuery.Level | OcgQuery.Rank |
            OcgQuery.Attribute | OcgQuery.Race | OcgQuery.Attack | OcgQuery.Defense | OcgQuery.BaseAttack |
            OcgQuery.BaseDefense | OcgQuery.OverlayCard | OcgQuery.Owner | OcgQuery.Status |
            OcgQuery.LeftScale | OcgQuery.RightScale | OcgQuery.Link;

        static readonly OcgLocation[] UpdatedLocations =
        {
            OcgLocation.MonsterZone, OcgLocation.SpellTrapZone, OcgLocation.Hand,
            OcgLocation.Grave, OcgLocation.Removed, OcgLocation.Extra,
        };

        const int MaxProblems = 100;

        readonly byte _team;
        readonly CardDatabase _database;
        readonly int? _seed;
        readonly IDuelPlayer _fallback;
        readonly List<string> _problems = new List<string>();

        WindBotGameBehavior _behavior;
        IDuelQuery _query;
        OcgMessage? _pending;
        bool _rejected;
        int _turn;

        /// <summary>상대의 비공개 정보(뒷면 카드, 손패, 덱)를 가린다. 끄면 AI가 상대 카드를 모두 안다.</summary>
        public bool HideOpponentInfo { get; }

        /// <summary>WindBot이 직접 답한 요청 수.</summary>
        public int Answered { get; private set; }

        /// <summary>WindBot이 답하지 못해 SimpleAutoPlayer가 대신 답한 요청 수.</summary>
        public int Fallbacks { get; private set; }

        /// <summary>WindBot의 필드 모델이 엔진과 어긋나 맞춘 칸 수 (정상이면 0).</summary>
        public int ModelResyncs { get; private set; }

        /// <summary>요청이 아닌 메시지를 WindBot이 처리하다 예외를 낸 수.</summary>
        public int MessageErrors { get; private set; }

        /// <summary>
        /// 점검용: 응답할 때마다 센, WindBot이 코드를 알고 있던 상대 손패의 비공개 카드 장수 (누적).
        /// 가리기를 켰다면 0이어야 하고, 끄면 0보다 크다.
        /// </summary>
        public int KnownOpponentSecrets { get; private set; }

        /// <summary>대신 답한 이유, 메시지 처리 예외 등 (앞에서부터 최대 100개).</summary>
        public IReadOnlyList<string> Problems => _problems;

        /// <summary>WindBot 내부 로그 (디버그용).</summary>
        public event Action<string> DebugLog;

        /// <summary>대신 답하거나 메시지 처리 예외가 났을 때 (이유 문구).</summary>
        public event Action<string> ProblemReported;

        /// <param name="team">이 AI가 맡는 엔진 팀 번호 (0 또는 1)</param>
        /// <param name="seed">AI의 무작위 선택 시드. null이면 매번 다르다.</param>
        public WindBotPlayer(byte team, CardDatabase database, int? seed = null, bool hideOpponentInfo = true)
        {
            _team = team;
            _database = database;
            _seed = seed;
            HideOpponentInfo = hideOpponentInfo;
            _fallback = new SimpleAutoPlayer(activateEffects: true, seed: seed ?? Environment.TickCount);
        }

        public void OnDuelStart(DuelStartInfo info, IDuelQuery query)
        {
            WindBotCards.EnsureLoaded(_database);
            _query = query;
            _pending = null;
            _rejected = false;

            var duel = new Duel
            {
                IsNewRule = (info.Flags & OcgDuelFlags.EMZone) != 0,
                IsNewRule2020 = (info.Flags & OcgDuelFlags.FsxMMZone) != 0,
            };
            var ai = new GameAI(duel, "default", (message, forced) => { }, (message, level) => Debug(message), () => { }, "");
            var executor = new StarterDeckExecutor(ai, duel) { Deck = "Starter" };
            if (_seed.HasValue)
                executor.Rand = new Random(_seed.Value);
            ai.Executor = executor;
            _behavior = new WindBotGameBehavior(ai, Debug);
            _behavior.Start(_team, info.StartingLp, info.DeckCount, info.ExtraCount);
        }

        public void OnEngineMessage(OcgMessage message)
        {
            if (_behavior == null)
                return;
            if (message.Type == OcgMessageType.NewTurn)
                _turn++;
            if (message.Type == OcgMessageType.Retry)
                _rejected = true;

            if (WindBotMessageFilter.IsRequest(message.Type))
            {
                // 내 요청은 Respond에서 카드 정보를 보낸 뒤 넣는다. 상대 요청은 EDOPro 서버처럼 보내지 않는다.
                if (message.Body.Length > 0 && message.Body[0] == _team)
                {
                    _pending = message;
                    _rejected = false;
                }
                return;
            }

            byte[] body = WindBotMessageFilter.ForObserver(message, _team, HideOpponentInfo);
            var type = (WBGameMessage)message.Type;
            if (body == null || !_behavior.Handles(type))
                return;
            try
            {
                _behavior.HandleMessage(type, body);
            }
            catch (Exception e)
            {
                MessageErrors++;
                Problem($"메시지 처리 예외 ({message.Type}): {e.GetType().Name}: {e.Message}");
            }
        }

        public byte[] Respond(DuelRequest request, DuelState state)
        {
            if (_behavior == null || _pending == null)
                return Fallback(request, state, "WindBot이 시작되지 않았거나 요청 메시지를 받지 못함");
            var pending = _pending.Value;
            if (request.Player != _team || pending.Type != request.Type)
                return Fallback(request, state, $"요청이 맞지 않음: 받은 {pending.Type}, 물은 {request.Type} (플레이어 {request.Player})");
            if (_rejected)
                return Fallback(request, state, $"엔진이 WindBot의 응답을 거절함: {request.Type}");

            try
            {
                SendCardData();
                byte[] body = WindBotMessageFilter.HideInRequest(pending, _team, HideOpponentInfo);
                byte[] reply = _behavior.HandleMessage((WBGameMessage)pending.Type, body);
                if (reply == null)
                    return Fallback(request, state, $"WindBot이 응답하지 않음: {request.Type}");
                Answered++;
                return reply;
            }
            catch (Exception e)
            {
                return Fallback(request, state, $"{request.Type}: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>EDOPro 서버가 요청 전에 보내는 MSG_UPDATE_DATA를 양쪽 공개 영역에 대해 만든다.</summary>
        void SendCardData()
        {
            for (byte team = 0; team < 2; team++)
            {
                bool hide = HideOpponentInfo && team != _team;
                foreach (var location in UpdatedLocations)
                {
                    var flags = hide ? WindBotQuery | OcgQuery.IsPublic : WindBotQuery;
                    var cards = QueryBuffer.Parse(_query.QueryLocation(team, location, flags));
                    List<bool> secret = null;
                    if (team != _team && location == OcgLocation.Hand)
                    {
                        // 점검용: 상대 손패 중 공개되지 않은 카드 (가리기를 켰다면 WindBot이 몰라야 한다)
                        var publicFlags = QueryBuffer.Parse(_query.QueryLocation(team, location, OcgQuery.IsPublic));
                        secret = publicFlags.ConvertAll(card => card != null && !card.IsPublic);
                    }
                    if (hide)
                        WindBotMessageFilter.HideCards(cards, location, WindBotQuery);

                    var slots = new List<int?>(cards.Count);
                    foreach (var card in cards)
                        slots.Add(card == null ? (int?)null : (int)card.Position);
                    var wbLocation = (WBCardLocation)location;
                    int fixes = _behavior.SyncLocation(team, wbLocation, slots);
                    if (fixes > 0)
                    {
                        ModelResyncs += fixes;
                        Problem($"필드 모델을 엔진에 맞춤: 팀 {team} {location} {fixes}칸");
                    }
                    _behavior.UpdateData(team, wbLocation, QueryBuffer.Build(cards));
                    if (secret != null)
                        KnownOpponentSecrets += _behavior.CountKnownSecrets(team, wbLocation, secret);
                }
            }
        }

        byte[] Fallback(DuelRequest request, DuelState state, string reason)
        {
            Fallbacks++;
            Problem(reason);
            return _fallback.Respond(request, state);
        }

        void Problem(string text)
        {
            text = $"{_turn}턴: {text}";
            if (_problems.Count < MaxProblems)
                _problems.Add(text);
            ProblemReported?.Invoke(text);
            Debug(text);
        }

        void Debug(string text) => DebugLog?.Invoke(text);
    }
}

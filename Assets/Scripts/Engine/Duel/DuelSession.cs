// 듀얼 하나를 진행한다: 엔진을 돌려 메시지를 해석하고, 상태를 맞추고, 응답이 필요하면 플레이어에게 묻는다.
using System;
using System.Collections.Generic;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine.Duel
{
    /// <summary>요청에 답하는 쪽. 사람이 답해야 하면 null을 돌려준다.</summary>
    public interface IDuelPlayer
    {
        byte[] Respond(DuelRequest request, DuelState state);
    }

    public sealed class DuelSessionOptions
    {
        public ulong[] Seed = { 1, 2, 3, 4 };
        /// <summary>
        /// 룰 플래그. 마스터 룰 5에서 엑스트라 몬스터 존만 뺐다 (설계: 엑스트라 몬스터 존 없음, 엑스트라 덱 몬스터는 메인 몬스터 존에 바로 소환).
        /// </summary>
        public OcgDuelFlags Flags = OcgDuelFlags.ModeMR5 & ~OcgDuelFlags.EMZone;
        public OcgPlayerOptions Team1 = OcgPlayerOptions.Default;
        public OcgPlayerOptions Team2 = OcgPlayerOptions.Default;
        /// <summary>플레이어별 메인·엑스트라 덱 카드 코드. 엔진은 덱을 섞지 않으므로 DeckShuffleSeed로 섞어서 넣는다.</summary>
        public IReadOnlyList<uint>[] Decks = new IReadOnlyList<uint>[2];
        public int DeckShuffleSeed = 1000;
    }

    public sealed class DuelSession : IDisposable
    {
        /// <summary>같은 요청에 자동 응답이 연달아 거절되면 멈춘다 (자동 응답 버그로 인한 무한 반복 방지).</summary>
        const int MaxAutoRetries = 3;

        readonly OcgDuel _duel;
        readonly IDuelPlayer[] _players;
        DuelRequest _lastRequest;
        int _retries;

        public DuelState State { get; }
        public CardDatabase Database { get; }

        /// <summary>사람이 답해야 하는 요청. 없으면 null.</summary>
        public DuelRequest PendingRequest { get; private set; }

        /// <summary>바로 앞 응답이 엔진에 거절되어 같은 요청을 다시 묻는 중인지.</summary>
        public bool IsRetry { get; private set; }

        public int EngineErrors { get; private set; }

        /// <summary>듀얼을 만드는 동안(덱 넣기 전까지) 나온 엔진 로그. 이벤트를 연결하기 전이라 따로 모아 둔다.</summary>
        public IReadOnlyList<(OcgLogType Type, string Text)> StartupLog { get; private set; }

        /// <summary>해석한 메시지마다 (로그·연출용). 상태(State)는 메시지를 반영한 뒤, 조회로 다시 맞추기 전이다.</summary>
        public event Action<DuelMessage> MessageReceived;

        /// <summary>엔진 로그 (종류, 내용).</summary>
        public event Action<OcgLogType, string> EngineLog;

        DuelSession(OcgDuel duel, IDuelPlayer[] players, CardDatabase database, uint startingLp)
        {
            _duel = duel;
            _players = players;
            Database = database;
            State = new DuelState(startingLp);
        }

        public static DuelSession Create(DuelSessionOptions options, CardDatabase database, CardScriptSource scripts,
            IDuelPlayer player0, IDuelPlayer player1)
        {
            DuelSession session = null;
            var startupLog = new List<(OcgLogType, string)>();
            var duelOptions = new OcgDuelOptions
            {
                Seed = options.Seed,
                Flags = options.Flags,
                Team1 = options.Team1,
                Team2 = options.Team2,
                Log = (type, text) =>
                {
                    if (session != null) session.OnEngineLog(type, text);
                    else startupLog.Add((type, text));
                },
            };
            var duel = DuelSetup.CreateDuel(duelOptions, database, scripts);
            session = new DuelSession(duel, new[] { player0, player1 }, database, options.Team1.StartingLP);
            session.State.Players[1].Lp = (int)options.Team2.StartingLP;
            session.StartupLog = startupLog;
            foreach (var (type, _) in startupLog)
                if (type == OcgLogType.Error)
                    session.EngineErrors++;
            session._options = options;
            return session;
        }

        DuelSessionOptions _options;
        bool _started;

        /// <summary>
        /// 덱을 섞어 넣고 듀얼을 시작해 첫 입력까지 진행한다.
        /// 카드 스크립트는 덱에 넣을 때 불러오므로, 이벤트(MessageReceived, EngineLog)는 이 전에 연결한다.
        /// </summary>
        public void Start()
        {
            if (_started)
                throw new InvalidOperationException("이미 시작한 듀얼입니다.");
            _started = true;
            for (byte team = 0; team < 2; team++)
            {
                var deck = new List<uint>(_options.Decks[team] ?? Array.Empty<uint>());
                Shuffle(deck, new Random(_options.DeckShuffleSeed + team));
                foreach (uint code in deck)
                    _duel.AddDeckCard(team, code);
            }
            _duel.Start();
            Run();
        }

        /// <summary>사람의 응답이 필요하거나 듀얼이 끝날 때까지 진행한다.</summary>
        public void Run(int maxProcessCalls = 100000)
        {
            for (int call = 0; call < maxProcessCalls; call++)
            {
                if (State.IsFinished || PendingRequest != null)
                    return;

                var status = _duel.Process();
                DuelRequest request = null;
                bool retried = false;
                foreach (var raw in _duel.ReadMessages())
                {
                    DuelMessage message;
                    try
                    {
                        message = DuelMessageParser.Parse(raw);
                    }
                    catch (Exception e)
                    {
                        OnEngineLog(OcgLogType.Error, $"메시지 해석 실패 ({raw.Type}, {raw.Body.Length}바이트): {e.Message}");
                        continue;
                    }

                    if (message.Type == OcgMessageType.Retry)
                        retried = true;
                    State.Apply(message);
                    MessageReceived?.Invoke(message);
                    if (message is DuelRequest r)
                        request = r;
                }
                State.Refresh(_duel);

                if (State.IsFinished)
                    return;
                if (status == OcgDuelStatus.End)
                {
                    State.IsFinished = true;
                    return;
                }
                if (status != OcgDuelStatus.Awaiting)
                    continue;

                // 응답 대기: 새 요청이 없으면(거절 후 다시 묻기) 바로 앞 요청을 다시 쓴다.
                if (request == null)
                    request = _lastRequest ?? throw new InvalidOperationException("엔진이 응답을 기다리지만 요청 메시지가 없습니다.");
                _retries = retried ? _retries + 1 : 0;
                _lastRequest = request;
                IsRetry = retried;

                byte[] response = _players[request.Player]?.Respond(request, State);
                if (response == null)
                {
                    PendingRequest = request;
                    return;
                }
                if (_retries >= MaxAutoRetries)
                    throw new InvalidOperationException($"자동 응답이 계속 거절됩니다: {request.Type} (플레이어 {request.Player})");
                _duel.SetResponse(response);
            }
            throw new InvalidOperationException($"엔진 처리 횟수 한도({maxProcessCalls})를 넘었습니다.");
        }

        /// <summary>PendingRequest에 답하고 다음 입력이 필요할 때까지 진행한다.</summary>
        public void Respond(byte[] response)
        {
            if (PendingRequest == null)
                throw new InvalidOperationException("답할 요청이 없습니다.");
            PendingRequest = null;
            _duel.SetResponse(response);
            Run();
        }

        public void Dispose() => _duel.Dispose();

        void OnEngineLog(OcgLogType type, string text)
        {
            if (type == OcgLogType.Error)
                EngineErrors++;
            EngineLog?.Invoke(type, text);
        }

        static void Shuffle(List<uint> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

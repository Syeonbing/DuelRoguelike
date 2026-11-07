// 화면 없이 자동으로 듀얼을 끝까지 돌려, 메시지 해석·응답·상태 갱신과 상대 AI가 끝까지 문제없는지 확인한다.
// 유니티 타입을 쓰지 않으므로 에디터 밖에서도 그대로 돌릴 수 있다 (tools/headless-duel.ps1).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.AI;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.UI;

namespace DuelRoguelike.Dev
{
    /// <summary>화면 밖 대전에서 한쪽을 맡을 플레이어.</summary>
    public enum HeadlessPlayerKind
    {
        /// <summary>SimpleAutoPlayer (효과 발동 켬, 무작위)</summary>
        Auto,
        /// <summary>WindBot Ignite AI (상대 비공개 정보 가림)</summary>
        WindBot,
        /// <summary>화면 조작 모델(InteractionTestPlayer)로만 답한다: 화면에서 고를 수 없는 선택이 있는지 확인. 플레이어 0만.</summary>
        Interaction,
    }

    public sealed class HeadlessDuelRun
    {
        public sealed class Result
        {
            public int Duels;
            public int Finished;
            public int EngineErrors;
            public int Exceptions;
            public int TotalTurns;
            public readonly Dictionary<string, int> Winners = new Dictionary<string, int>();
            public readonly Dictionary<OcgMessageType, int> UnparsedTypes = new Dictionary<OcgMessageType, int>();
            public readonly Dictionary<OcgMessageType, int> RequestTypes = new Dictionary<OcgMessageType, int>();
            /// <summary>플레이어 0이 사람(ManualPlayer, 체인 확인 Smart)이었다면 실제로 물었을 요청 수.</summary>
            public readonly Dictionary<OcgMessageType, int> HumanPrompts = new Dictionary<OcgMessageType, int>();
            /// <summary>플레이어 0을 화면 조작 모델(InteractionTestPlayer)로 돌렸을 때: 모델로 답한 수, 자동 응답으로 대신한 수.</summary>
            public int InteractionAnswered;
            public int InteractionFallbacks;
            public readonly Dictionary<string, int> FallbackReasons = new Dictionary<string, int>();

            /// <summary>WindBot: 직접 답한 수, 대신 답한 수, 필드 모델을 맞춘 칸 수, 메시지 처리 예외 수.</summary>
            public int WindBotAnswered;
            public int WindBotFallbacks;
            public int WindBotResyncs;
            public int WindBotMessageErrors;
            /// <summary>WindBot이 코드를 알고 있던 상대 손패의 비공개 카드 (응답마다 센 누적, 가리기를 켰다면 0).</summary>
            public int WindBotKnownSecrets;
            public bool WindBotHidesInfo = true;
            public readonly List<string> WindBotProblems = new List<string>();

            /// <summary>플레이어별 일반·특수·반전 소환, 효과 발동(체인), 공격 선언 수.</summary>
            public readonly int[] Summons = new int[2];
            public readonly int[] Activations = new int[2];
            public readonly int[] Attacks = new int[2];

            public bool Ok => Finished == Duels && EngineErrors == 0 && Exceptions == 0 && InteractionFallbacks == 0
                              && WindBotFallbacks == 0 && WindBotMessageErrors == 0 && WindBotResyncs == 0
                              && (!WindBotHidesInfo || WindBotKnownSecrets == 0);
        }

        const int MaxProblemsKept = 40;

        readonly CardDatabase _db;
        readonly CardScriptSource _scripts;
        readonly SystemStrings _strings;
        readonly Action<string> _log;

        public HeadlessDuelRun(CardDataSet data, Action<string> log)
        {
            _db = data.Database;
            _scripts = data.Scripts;
            _strings = data.Strings;
            _log = log;
        }

        /// <param name="printLogSeed">이 시드의 듀얼은 로그 전체를 출력한다 (-1이면 출력하지 않음).</param>
        public Result Run(int duels, int firstSeed = 1, int printLogSeed = -1, int maxTurns = 60,
            HeadlessPlayerKind player0 = HeadlessPlayerKind.Auto, HeadlessPlayerKind player1 = HeadlessPlayerKind.Auto,
            bool windBotHidesInfo = true)
        {
            if (player1 == HeadlessPlayerKind.Interaction)
                throw new ArgumentException("화면 조작 모델은 플레이어 0만 맡을 수 있습니다.", nameof(player1));

            var result = new Result { WindBotHidesInfo = windBotHidesInfo };
            var deck = CardDataSmokeRun.TestDeck.SelectMany(e => Enumerable.Repeat(e.Code, e.Count)).ToList();
            var text = new DuelText(_db, _strings);
            var formatter = new DuelLogFormatter(text);
            var manual = new ManualPlayer();

            for (int seed = firstSeed; seed < firstSeed + duels; seed++)
            {
                result.Duels++;
                bool print = seed == printLogSeed;
                var options = new DuelSessionOptions
                {
                    Seed = new ulong[] { (ulong)seed, 2, 3, 4 },
                    Decks = new IReadOnlyList<uint>[] { deck, deck },
                    DeckShuffleSeed = seed * 100,
                };
                var interactionPlayer = player0 == HeadlessPlayerKind.Interaction ? new InteractionTestPlayer(text, seed) : null;
                var windBots = new List<WindBotPlayer>();
                IDuelPlayer p0 = (IDuelPlayer)interactionPlayer ?? CreatePlayer(player0, 0, seed, windBotHidesInfo, windBots);
                IDuelPlayer p1 = CreatePlayer(player1, 1, seed + 7777, windBotHidesInfo, windBots);
                if (print)
                    foreach (var bot in windBots)
                        bot.DebugLog += line => _log($"    [WindBot] {line}");
                try
                {
                    using (var session = DuelSession.Create(options, _db, _scripts, p0, p1))
                    {
                        session.EngineLog += (type, msg) =>
                        {
                            if (type == OcgLogType.Error) _log($"[시드 {seed}] 엔진 오류: {msg}");
                        };
                        session.MessageReceived += message =>
                        {
                            if (message is RawMessage)
                                Count(result.UnparsedTypes, message.Type);
                            CountAction(result, message);
                            if (message is DuelRequest request)
                            {
                                Count(result.RequestTypes, message.Type);
                                if (request.Player == 0 && manual.Respond(request, session.State) == null)
                                    Count(result.HumanPrompts, message.Type);
                            }
                            if (print)
                            {
                                string line = formatter.Format(message, session.State);
                                if (line != null) _log(line);
                            }
                            if (session.State.Turn > maxTurns)
                                throw new TimeoutException($"{maxTurns}턴 안에 끝나지 않음");
                        };
                        session.Start();
                        result.EngineErrors += session.EngineErrors;
                        result.TotalTurns += session.State.Turn;
                        if (session.State.IsFinished)
                        {
                            result.Finished++;
                            string winner = session.State.Winner == OcgConstants.PlayerNone ? "무승부" : $"플레이어 {session.State.Winner}";
                            Count(result.Winners, $"{winner} ({session.State.WinReason})");
                        }
                        else
                        {
                            _log($"[시드 {seed}] 끝나지 않음: 대기 중인 요청 {session.PendingRequest?.Type}");
                        }
                    }
                }
                catch (Exception e)
                {
                    result.Exceptions++;
                    _log($"[시드 {seed}] 예외: {e.GetType().Name}: {e.Message}");
                }
                if (interactionPlayer != null)
                {
                    result.InteractionAnswered += interactionPlayer.Answered;
                    result.InteractionFallbacks += interactionPlayer.Fallbacks;
                    foreach (string reason in interactionPlayer.FallbackReasons)
                        Count(result.FallbackReasons, reason);
                }
                foreach (var bot in windBots)
                {
                    result.WindBotAnswered += bot.Answered;
                    result.WindBotFallbacks += bot.Fallbacks;
                    result.WindBotResyncs += bot.ModelResyncs;
                    result.WindBotMessageErrors += bot.MessageErrors;
                    result.WindBotKnownSecrets += bot.KnownOpponentSecrets;
                    foreach (string problem in bot.Problems)
                        if (result.WindBotProblems.Count < MaxProblemsKept)
                            result.WindBotProblems.Add($"[시드 {seed}] {problem}");
                }
            }
            return result;
        }

        IDuelPlayer CreatePlayer(HeadlessPlayerKind kind, byte team, int seed, bool hides, List<WindBotPlayer> windBots)
        {
            if (kind != HeadlessPlayerKind.WindBot)
                return new SimpleAutoPlayer(activateEffects: true, seed: seed);
            var bot = new WindBotPlayer(team, _db, seed, hides);
            windBots.Add(bot);
            return bot;
        }

        static void CountAction(Result result, DuelMessage message)
        {
            switch (message)
            {
                case SummoningMessage m when m.Location.Controller < 2:
                    result.Summons[m.Location.Controller]++;
                    break;
                case ChainingMessage m when m.Location.Controller < 2:
                    result.Activations[m.Location.Controller]++;
                    break;
                case AttackMessage m when m.Attacker.Controller < 2:
                    result.Attacks[m.Attacker.Controller]++;
                    break;
            }
        }

        static void Count<T>(Dictionary<T, int> counts, T key) =>
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
    }
}

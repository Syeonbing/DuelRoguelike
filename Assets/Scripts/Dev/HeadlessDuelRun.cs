// 화면 없이 양쪽 다 자동 응답으로 듀얼을 끝까지 돌려, 메시지 해석·응답·상태 갱신이 끝까지 문제없는지 확인한다.
// 유니티 타입을 쓰지 않으므로 에디터 밖에서도 그대로 돌릴 수 있다.
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.UI;

namespace DuelRoguelike.Dev
{
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
            public bool Ok => Finished == Duels && EngineErrors == 0 && Exceptions == 0 && InteractionFallbacks == 0;
        }

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
        /// <param name="viaInteraction">true면 플레이어 0이 화면 조작 모델로만 답한다 (화면에서 고를 수 없는 선택이 있는지 확인).</param>
        public Result Run(int duels, int firstSeed = 1, int printLogSeed = -1, int maxTurns = 60, bool viaInteraction = false)
        {
            var result = new Result();
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
                var interactionPlayer = viaInteraction ? new InteractionTestPlayer(text, seed) : null;
                IDuelPlayer player0 = (IDuelPlayer)interactionPlayer ?? new SimpleAutoPlayer(activateEffects: true, seed: seed);
                try
                {
                    using (var session = DuelSession.Create(options, _db, _scripts,
                               player0,
                               new SimpleAutoPlayer(activateEffects: true, seed: seed + 7777)))
                    {
                        session.EngineLog += (type, msg) =>
                        {
                            if (type == OcgLogType.Error) _log($"[시드 {seed}] 엔진 오류: {msg}");
                        };
                        session.MessageReceived += message =>
                        {
                            if (message is RawMessage)
                                Count(result.UnparsedTypes, message.Type);
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
            }
            return result;
        }

        static void Count<T>(Dictionary<T, int> counts, T key) =>
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
    }
}

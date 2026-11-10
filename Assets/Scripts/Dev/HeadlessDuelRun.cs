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
            /// <summary>그중 칸 고르기(MSG_SELECT_PLACE / MSG_SELECT_DISFIELD)를 화면 조작 모델로 답한 수.</summary>
            public int InteractionPlaces;
            /// <summary>그중 순서 정하기(MSG_SORT_CARD / MSG_SORT_CHAIN)를 화면 조작 모델로 답한 수.</summary>
            public int InteractionSorts;
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
            /// <summary>플레이어별로 엔진이 거절한 응답 수 (MSG_RETRY, 바로 앞 요청을 받은 플레이어 기준).</summary>
            public readonly int[] Retries = new int[2];
            /// <summary>거절된 요청의 "플레이어 요청 종류"별 수.</summary>
            public readonly Dictionary<string, int> RetryTypes = new Dictionary<string, int>();
            /// <summary>배치 끝마다 메시지로 바로 반영한 상태와 엔진 조회를 비교한 수와 어긋난 수 (화면이 배치 중간에 그리는 필드 점검).</summary>
            public int StateChecks;
            public int StateMismatches;
            /// <summary>어긋난 항목 → 그 배치의 메시지 종류별 횟수.</summary>
            public readonly Dictionary<string, int> StateMismatchKinds = new Dictionary<string, int>();

            /// <summary>
            /// 플레이어 0에게 온 요청 중 화면에서 사람이 고를 수 없는 것 (카운터는 자동 분배, 카드 선언은 숫자 입력).
            /// 키는 "요청 종류: 마지막으로 발동한 카드". 통과 조건에는 넣지 않고 보고만 한다.
            /// </summary>
            public readonly Dictionary<string, int> ScreenlessChoices = new Dictionary<string, int>();

            public string Deck0;
            public string Deck1;

            /// <summary>카드 코드별 효과 발동(체인) 수 [플레이어 0, 플레이어 1]. 발동한 플레이어 기준.</summary>
            public readonly Dictionary<uint, int[]> CardActivations = new Dictionary<uint, int[]>();
            /// <summary>카드 코드별 일반·특수·반전 소환 수 [플레이어 0, 플레이어 1].</summary>
            public readonly Dictionary<uint, int[]> CardSummons = new Dictionary<uint, int[]>();

            public bool Ok => Finished == Duels && EngineErrors == 0 && Exceptions == 0 && InteractionFallbacks == 0
                              && WindBotFallbacks == 0 && WindBotMessageErrors == 0 && WindBotResyncs == 0
                              && (!WindBotHidesInfo || WindBotKnownSecrets == 0)
                              && Retries[0] == 0 && Retries[1] == 0 && UnparsedTypes.Count == 0 && StateMismatches == 0;
        }

        const int MaxProblemsKept = 40;

        readonly CardDatabase _db;
        readonly CardScriptSource _scripts;
        readonly SystemStrings _strings;
        readonly Action<string> _log;
        readonly DuelText _text;
        readonly DuelLogFormatter _formatter;
        readonly ManualPlayer _manual = new ManualPlayer();

        public HeadlessDuelRun(CardDataSet data, Action<string> log)
        {
            _db = data.Database;
            _scripts = data.Scripts;
            _strings = data.Strings;
            _log = log;
            _text = new DuelText(_db, _strings);
            _formatter = new DuelLogFormatter(_text);
        }

        public CardDatabase Database => _db;

        /// <summary>카드 코드 → 표시할 카드명.</summary>
        public string CardName(uint code) => _text.CardName(code);

        /// <summary>
        /// 레시피가 덱 규칙(DeckRecipe.Validate)에 맞고, 일반 몬스터가 아닌 카드마다 카드 스크립트가 있는지 확인한다.
        /// 문제가 없으면 빈 목록.
        /// </summary>
        public List<string> CheckRecipe(DeckRecipe recipe)
        {
            var problems = recipe.Validate(_db);
            foreach (var (code, _) in recipe.Entries)
            {
                if (!_db.TryGet(code, out var record))
                    continue;
                if (!IsVanilla(record) && !_scripts.TryGetPath($"c{code}.lua", out _))
                    problems.Add($"{code} {record.Name}: 카드 스크립트 없음");
            }
            return problems;
        }

        /// <summary>효과도 소환 절차도 없는 일반 몬스터 (카드 스크립트가 필요 없다).</summary>
        static bool IsVanilla(CardRecord record)
        {
            const OcgCardType procedures = OcgCardType.Fusion | OcgCardType.Ritual | OcgCardType.Synchro | OcgCardType.Xyz
                                           | OcgCardType.Link | OcgCardType.Pendulum;
            return (record.Type & OcgCardType.Normal) != 0 && (record.Type & procedures) == 0;
        }

        /// <summary>
        /// 시험한 덱의 카드마다 효과 발동·소환 수를 모든 조합에서 합쳐, 확인이 덜 된 카드를 돌려준다 (일반 몬스터는 뺀다).
        /// - 한 번도 나오지 않음: 마법·함정은 발동 0, 몬스터는 발동 0이고 소환 0.
        /// - 소환만 됨: 효과 몬스터인데 소환은 됐고 효과 발동은 0 (지속 효과만 있으면 정상).
        /// - 플레이어 0은 쓴 적 없음: 플레이어 0의 덱에 있는데 플레이어 0(화면 조작 모델 등)은 발동·소환한 적 없고 상대만 썼다.
        /// </summary>
        public List<string> CoverageReport(IEnumerable<Result> results, IEnumerable<DeckRecipe> decks0, IEnumerable<DeckRecipe> decks1)
        {
            var activations = new Dictionary<uint, int[]>();
            var summons = new Dictionary<uint, int[]>();
            foreach (var r in results)
            {
                Merge(activations, r.CardActivations);
                Merge(summons, r.CardSummons);
            }
            var player0Codes = new HashSet<uint>(decks0.SelectMany(d => d.Entries.Select(e => e.Code)));
            var lines = new List<string>();
            var seen = new HashSet<uint>();
            foreach (var deck in decks0.Concat(decks1).Distinct())
            {
                var notSeen = new List<string>();
                var summonOnly = new List<string>();
                var opponentOnly = new List<string>();
                foreach (var (code, _) in deck.Entries)
                {
                    if (!seen.Add(code) || !_db.TryGet(code, out var record) || IsVanilla(record))
                        continue;
                    int[] act = activations.TryGetValue(code, out var a) ? a : new int[2];
                    int[] sum = summons.TryGetValue(code, out var s) ? s : new int[2];
                    bool monster = (record.Type & OcgCardType.Monster) != 0;
                    string name = $"{record.Name} (발동 {act[0]}/{act[1]}{(monster ? $", 소환 {sum[0]}/{sum[1]}" : "")})";
                    if (act[0] + act[1] == 0 && sum[0] + sum[1] == 0)
                        notSeen.Add(name);
                    else if (monster && (record.Type & OcgCardType.Effect) != 0 && act[0] + act[1] == 0)
                        summonOnly.Add(name);
                    else if (player0Codes.Contains(code) && act[0] == 0 && sum[0] == 0)
                        opponentOnly.Add(name);
                }
                if (notSeen.Count + summonOnly.Count + opponentOnly.Count == 0)
                    continue;
                lines.Add($"{deck.Name} (앞서 나온 덱과 겹치는 카드는 뺀다, 숫자는 플레이어 0/1)");
                if (notSeen.Count > 0) lines.Add($"  한 번도 나오지 않음: {string.Join(", ", notSeen)}");
                if (summonOnly.Count > 0) lines.Add($"  소환만 되고 효과 발동 없음: {string.Join(", ", summonOnly)}");
                if (opponentOnly.Count > 0) lines.Add($"  플레이어 0은 쓴 적 없음: {string.Join(", ", opponentOnly)}");
            }
            return lines;
        }

        static void Merge(Dictionary<uint, int[]> into, Dictionary<uint, int[]> from)
        {
            foreach (var pair in from)
            {
                if (!into.TryGetValue(pair.Key, out var counts))
                    into[pair.Key] = counts = new int[2];
                counts[0] += pair.Value[0];
                counts[1] += pair.Value[1];
            }
        }

        /// <param name="printLogSeed">이 시드의 듀얼은 로그 전체를 출력한다 (-1이면 출력하지 않음).</param>
        /// <param name="deck0">플레이어 0의 덱. null이면 스타터 덱 —유희 편—.</param>
        /// <param name="deck1">플레이어 1의 덱. null이면 스타터 덱 —유희 편—.</param>
        public Result Run(int duels, int firstSeed = 1, int printLogSeed = -1, int maxTurns = 60,
            HeadlessPlayerKind player0 = HeadlessPlayerKind.Auto, HeadlessPlayerKind player1 = HeadlessPlayerKind.Auto,
            bool windBotHidesInfo = true, DeckRecipe deck0 = null, DeckRecipe deck1 = null)
        {
            if (player1 == HeadlessPlayerKind.Interaction)
                throw new ArgumentException("화면 조작 모델은 플레이어 0만 맡을 수 있습니다.", nameof(player1));
            deck0 = deck0 ?? StarterDecks.Yugi;
            deck1 = deck1 ?? StarterDecks.Yugi;

            var result = new Result { WindBotHidesInfo = windBotHidesInfo, Deck0 = deck0.Id, Deck1 = deck1.Id };
            var cards0 = deck0.ToCardList();
            var cards1 = deck1.ToCardList();

            for (int seed = firstSeed; seed < firstSeed + duels; seed++)
            {
                var options = new DuelSessionOptions
                {
                    Seed = new ulong[] { (ulong)seed, 2, 3, 4 },
                    Decks = new IReadOnlyList<uint>[] { cards0, cards1 },
                    DeckShuffleSeed = seed * 100,
                };
                PlayDuel(result, options, $"[{deck0.Id} 대 {deck1.Id}, 시드 {seed}]", seed, seed == printLogSeed, maxTurns,
                    player0, player1, windBotHidesInfo);
            }
            return result;
        }

        /// <summary>듀얼 한 판의 결과 (런 시험이 쓴다).</summary>
        public sealed class DuelOutcome
        {
            public bool Finished;
            /// <summary>승자. OcgConstants.PlayerNone이면 무승부.</summary>
            public byte Winner = OcgConstants.PlayerNone;
            /// <summary>듀얼이 끝났을 때 플레이어별 LP.</summary>
            public readonly int[] Lp = new int[2];
            public int Turns;
            /// <summary>끝난 듀얼의 상태 (끝나지 않았으면 null).</summary>
            public DuelState State;
        }

        /// <summary>
        /// 듀얼 한 판을 끝까지 돌리고 점검 수치를 result에 더한다 (판 수, 엔진 오류, 예외, 거절, 대체 응답 등).
        /// playerSeed는 시험 플레이어의 난수 시드 (플레이어 1은 playerSeed + 7777).
        /// </summary>
        public DuelOutcome PlayDuel(Result result, DuelSessionOptions options, string tag, int playerSeed, bool print, int maxTurns,
            HeadlessPlayerKind player0, HeadlessPlayerKind player1, bool windBotHidesInfo)
        {
            if (player0 == HeadlessPlayerKind.Interaction && player1 == HeadlessPlayerKind.Interaction)
                throw new ArgumentException("화면 조작 모델은 한쪽만 맡을 수 있습니다.", nameof(player1));
            var outcome = new DuelOutcome();
            result.Duels++;
            // 화면 조작 모델의 점검 수치(화면에서 고를 수 없는 선택 등)는 그 모델이 앉은 쪽의 요청만 센다 (런에서 후공이면 플레이어 1).
            int watched = player1 == HeadlessPlayerKind.Interaction ? 1 : 0;
            var interactionPlayer = player0 == HeadlessPlayerKind.Interaction || player1 == HeadlessPlayerKind.Interaction
                ? new InteractionTestPlayer(_text, watched == 0 ? playerSeed : playerSeed + 7777) : null;
            var windBots = new List<WindBotPlayer>();
            IDuelPlayer p0 = watched == 0 && interactionPlayer != null ? interactionPlayer : CreatePlayer(player0, 0, playerSeed, windBotHidesInfo, windBots);
            IDuelPlayer p1 = watched == 1 ? interactionPlayer : CreatePlayer(player1, 1, playerSeed + 7777, windBotHidesInfo, windBots);
            if (print)
                foreach (var bot in windBots)
                    bot.DebugLog += line => _log($"    [WindBot] {line}");
            try
            {
                using (var session = DuelSession.Create(options, _db, _scripts, p0, p1))
                {
                    byte lastRequestPlayer = 0;
                    OcgMessageType lastRequestType = OcgMessageType.Retry;
                    uint lastChainCode = 0;
                    session.EngineLog += (type, msg) =>
                    {
                        if (type == OcgLogType.Error) _log($"{tag} 엔진 오류: {msg}");
                    };
                    session.MessageReceived += message =>
                    {
                        if (message is RawMessage)
                        {
                            Count(result.UnparsedTypes, message.Type);
                            if (result.UnparsedTypes[message.Type] <= 3)
                                _log($"{tag} 해석하지 않은 메시지: {message.Type}");
                        }
                        CountAction(result, message);
                        if (message is ChainingMessage chaining)
                            lastChainCode = chaining.Code;
                        if (message.Type == OcgMessageType.Retry && lastRequestPlayer < 2)
                        {
                            result.Retries[lastRequestPlayer]++;
                            string key = $"플레이어 {lastRequestPlayer} {lastRequestType}";
                            Count(result.RetryTypes, key);
                            if (result.RetryTypes[key] <= 3)
                                _log($"{tag} 엔진이 응답을 거절함: {key} (마지막 발동: {_text.CardName(lastChainCode)})");
                        }
                        if (message is DuelRequest request)
                        {
                            lastRequestPlayer = request.Player;
                            lastRequestType = request.Type;
                            Count(result.RequestTypes, message.Type);
                            if (request.Player == watched && _manual.Respond(request, session.State) == null)
                                Count(result.HumanPrompts, message.Type);
                            if (request.Player == watched && IsScreenless(request))
                                Count(result.ScreenlessChoices, $"{request.Type}: {_text.CardName(lastChainCode)}");
                        }
                        if (print)
                        {
                            string line = _formatter.Format(message, session.State);
                            if (line != null) _log(line);
                        }
                        if (session.State.Turn > maxTurns)
                            throw new TimeoutException($"{maxTurns}턴 안에 끝나지 않음");
                    };
                    session.CheckMessageState = true;
                    session.StateMismatched += (diff, types) =>
                    {
                        // 줄 이름: "P0 패: …" → "P0 패", "P0 LP 8000, 덱 …" → "P0 LP·장수", "체인: …" → "체인"
                        var labels = string.Join(", ", diff.Select(d =>
                        {
                            int colon = d.IndexOf(':'), comma = d.IndexOf(',');
                            return colon > 0 && (comma < 0 || colon < comma) ? d.Substring(0, colon) : d.Substring(0, 2) + " LP·장수";
                        }).Distinct());
                        string key = $"{labels} | {string.Join(" ", types.Distinct())}";
                        Count(result.StateMismatchKinds, key);
                        if (result.StateMismatchKinds[key] <= 2)
                            _log($"{tag} 메시지 반영과 엔진이 다름 ({string.Join(" ", types)}):\n      " + string.Join("\n      ", diff));
                    };
                    session.Start();
                    result.EngineErrors += session.EngineErrors;
                    result.StateChecks += session.StateChecks;
                    result.StateMismatches += session.StateMismatches;
                    result.TotalTurns += session.State.Turn;
                    outcome.Turns = session.State.Turn;
                    if (session.State.IsFinished)
                    {
                        result.Finished++;
                        string winner = session.State.Winner == OcgConstants.PlayerNone ? "무승부" : $"플레이어 {session.State.Winner}";
                        Count(result.Winners, $"{winner} ({session.State.WinReason})");
                        outcome.Finished = true;
                        outcome.Winner = session.State.Winner;
                        outcome.Lp[0] = session.State.Players[0].Lp;
                        outcome.Lp[1] = session.State.Players[1].Lp;
                        outcome.State = session.State;
                    }
                    else
                    {
                        _log($"{tag} 끝나지 않음: 대기 중인 요청 {session.PendingRequest?.Type}");
                    }
                }
            }
            catch (Exception e)
            {
                result.Exceptions++;
                _log($"{tag} 예외: {e.GetType().Name}: {e.Message}");
            }
            if (interactionPlayer != null)
            {
                result.InteractionAnswered += interactionPlayer.Answered;
                result.InteractionFallbacks += interactionPlayer.Fallbacks;
                result.InteractionPlaces += interactionPlayer.PlacesAnswered;
                result.InteractionSorts += interactionPlayer.SortsAnswered;
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
                        result.WindBotProblems.Add($"{tag} {problem}");
            }
            return outcome;
        }

        /// <summary>화면이 사람에게 실제 선택을 주지 않는 요청 (DuelInteraction·ManualPlayer 기준).</summary>
        static bool IsScreenless(DuelRequest request) =>
            request is SelectCounterRequest || request is AnnounceCardRequest;

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
                    CountCard(result.CardSummons, m.Code, m.Location.Controller);
                    break;
                case ChainingMessage m when m.Location.Controller < 2:
                    result.Activations[m.Location.Controller]++;
                    CountCard(result.CardActivations, m.Code, m.TriggerController < 2 ? m.TriggerController : m.Location.Controller);
                    break;
                case AttackMessage m when m.Attacker.Controller < 2:
                    result.Attacks[m.Attacker.Controller]++;
                    break;
            }
        }

        static void CountCard(Dictionary<uint, int[]> counts, uint code, byte player)
        {
            if (!counts.TryGetValue(code, out var perPlayer))
                counts[code] = perPlayer = new int[2];
            perPlayer[player]++;
        }

        static void Count<T>(Dictionary<T, int> counts, T key) =>
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
    }
}

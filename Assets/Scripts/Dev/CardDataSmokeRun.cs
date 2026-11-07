// 카드 DB·카드 스크립트 연결 확인 절차. 유니티 타입을 쓰지 않으므로 에디터 밖에서도 그대로 돌릴 수 있다.
// 플레이 모드에서는 CardDataSmokeTest가 이 클래스를 부른다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DuelRoguelike.Engine;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Dev
{
    public sealed class CardDataSmokeRun
    {
        /// <summary>양쪽 플레이어가 쓰는 덱: 스타터 덱 —유희 편—.</summary>
        public static DeckRecipe TestDeck => StarterDecks.Yugi;

        readonly Action<string> _info;
        readonly Action<string> _warn;

        public int EngineErrors { get; private set; }

        public CardDataSmokeRun(Action<string> info, Action<string> warn)
        {
            _info = info;
            _warn = warn;
        }

        /// <returns>엔진 오류 없이 메인 페이즈 1 입력 대기까지 가면 true</returns>
        public bool Run(string cdbPath, string scriptsRoot, int maxProcessCalls = 50)
        {
            // 1. 카드 DB
            var database = new CardDatabase();
            var watch = Stopwatch.StartNew();
            int loaded = database.Load(cdbPath);
            _info($"SQLite {CardDatabase.SqliteVersion}, {System.IO.Path.GetFileName(cdbPath)}에서 카드 {loaded}장 읽음 ({watch.ElapsedMilliseconds} ms)");

            // 2. 카드 스크립트 색인
            watch.Restart();
            var scripts = new CardScriptSource(scriptsRoot);
            _info($"CardScripts 스크립트 {scripts.Count}개 색인 ({watch.ElapsedMilliseconds} ms)");

            // 3. 듀얼 생성 + 기본 스크립트
            var loadedScripts = new List<string>();
            var normalWithoutScript = new HashSet<uint>();
            int missingEffectScripts = 0;

            var options = new OcgDuelOptions
            {
                Seed = new ulong[] { 1, 2, 3, 4 },
                // 임시 값: 마스터 룰에서 엑스트라 몬스터 존만 뺐다. 정식 룰 플래그는 최소 듀얼 단계에서 정한다.
                Flags = OcgDuelFlags.ModeMR5 & ~OcgDuelFlags.EMZone,
                Log = (type, text) =>
                {
                    if (type == OcgLogType.Error)
                    {
                        EngineErrors++;
                        _warn($"엔진 오류: {text}");
                    }
                    else
                    {
                        _info($"엔진 로그({type}): {text}");
                    }
                },
            };

            void OnScriptRequest(string name, bool found)
            {
                if (found)
                {
                    loadedScripts.Add(name);
                    return;
                }
                if (!TryParseCardScriptName(name, out uint code))
                {
                    _warn($"스크립트 없음: {name}");
                    missingEffectScripts++;
                    return;
                }
                if (code == 0)
                    return; // 엔진이 듀얼을 만들 때 c0.lua를 요청한다. 이런 파일은 없는 것이 정상이다.
                if (database.TryGet(code, out var record) && (record.Type & OcgCardType.Normal) != 0)
                {
                    normalWithoutScript.Add(code); // 일반 몬스터는 스크립트가 없다.
                    return;
                }
                _warn($"효과 카드 스크립트 없음: {name} ({database.GetName(code)})");
                missingEffectScripts++;
            }

            using (var duel = DuelSetup.CreateDuel(options, database, scripts, OnScriptRequest))
            {
                _info($"듀얼 생성, 기본 스크립트 불러옴: {string.Join(", ", CardScriptSource.StartupScripts)}");

                // 4. 덱 넣기 (엔진은 덱을 섞지 않으므로 넣기 전에 섞는다)
                for (byte team = 0; team < 2; team++)
                {
                    var deck = ExpandDeck(database);
                    Shuffle(deck, new Random(1000 + team));
                    foreach (uint code in deck)
                        duel.AddDeckCard(team, code);
                    _info($"플레이어 {team} 덱 {deck.Count}장 넣음");
                }
                _info($"불러온 스크립트 {loadedScripts.Count}개: {string.Join(", ", loadedScripts.Except(CardScriptSource.StartupScripts))}");
                if (normalWithoutScript.Count > 0)
                    _info($"스크립트가 없는 일반 몬스터 {normalWithoutScript.Count}종 (정상)");

                // 5. 듀얼 진행: 메인 페이즈 1 입력 대기까지
                duel.Start();
                OcgMessageType lastMessage = 0;
                OcgDuelStatus status = OcgDuelStatus.Continue;
                for (int call = 1; call <= maxProcessCalls && status == OcgDuelStatus.Continue; call++)
                {
                    status = duel.Process();
                    foreach (var message in duel.ReadMessages())
                    {
                        lastMessage = message.Type;
                        if (message.Type == OcgMessageType.Draw)
                            _info(DescribeDraw(message, database));
                        else
                            _info($"메시지 {message.Type} ({(int)message.Type}), 본문 {message.Body.Length}바이트");
                    }
                }
                _info($"처리 상태 {status}, 마지막 메시지 {lastMessage}");

                bool reachedIdle = status == OcgDuelStatus.Awaiting && lastMessage == OcgMessageType.SelectIdleCmd;
                bool ok = reachedIdle && EngineErrors == 0 && missingEffectScripts == 0;
                string summary = $"확인 결과: 메인 페이즈 1 입력 대기 {(reachedIdle ? "도달" : "못 함")}, " +
                                 $"엔진 오류 {EngineErrors}건, 없는 효과 카드 스크립트 {missingEffectScripts}건";
                if (ok) _info(summary); else _warn(summary);
                return ok;
            }
        }

        List<uint> ExpandDeck(CardDatabase database)
        {
            var deck = new List<uint>();
            foreach (var (code, count) in TestDeck.Entries)
            {
                if (!database.TryGet(code, out _))
                    _warn($"카드 DB에 없는 카드: {code}");
                for (int i = 0; i < count; i++)
                    deck.Add(code);
            }
            return deck;
        }

        static void Shuffle(List<uint> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // MSG_DRAW 본문: [플레이어 u8][장수 u32] + 장수 × ([카드 코드 u32][표시 형식 u32])
        static string DescribeDraw(OcgMessage message, CardDatabase database)
        {
            byte[] b = message.Body;
            int player = b[0];
            int count = BitConverter.ToInt32(b, 1);
            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                uint code = BitConverter.ToUInt32(b, 5 + i * 8);
                names.Add(database.GetName(code));
            }
            return $"플레이어 {player} 드로우 {count}장: {string.Join(", ", names)}";
        }

        static bool TryParseCardScriptName(string name, out uint code)
        {
            code = 0;
            string file = System.IO.Path.GetFileNameWithoutExtension(name);
            return file.Length > 1 && file[0] == 'c' && uint.TryParse(file.Substring(1), out code);
        }
    }
}

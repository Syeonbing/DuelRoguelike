// 저장 (설계 문서 "로그라이크 진행": 진행 상황은 층 단위로 저장). 파일 하나에 다음을 담는다:
// - 컬렉션: 한 번이라도 얻은 카드와 장수 (영구). 처음에는 스타터 덱 4개를 지급한 것으로 시작한다.
// - 포인트: 끝난 런들의 포인트 대체 횟수를 등급별로 더한 것 (지급량은 미정이라 횟수만).
// - 마지막 시작 설정: 마지막으로 고른 시작 덱, 시작 덱별로 마지막에 고른 시작 사이드 덱.
// - 진행 중인 런 (있으면). 듀얼 사이 상태만 저장한다 (RunStateSave.cs).
// 파일 형식은 SaveText.cs (줄 단위 텍스트). 파일 위치는 부르는 쪽이 정한다 (게임: 유니티 persistentDataPath).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    public sealed class GameSave
    {
        /// <summary>저장 파일 형식 버전. 형식을 바꾸면 올리고, 이전 버전 파일을 읽는 길을 남긴다.</summary>
        public const int FormatVersion = 1;

        static readonly string[] ProfileKeys = { "format", "points", "last-start-deck" };
        static readonly string[] RepeatableProfileKeys = { "card", "last-side-deck" };

        readonly int[] _points = new int[RarityTable.RarityCount];
        readonly Dictionary<string, List<uint>> _lastSideDecks = new Dictionary<string, List<uint>>(StringComparer.Ordinal);

        GameSave(CardCollection collection) => Collection = collection;

        public CardCollection Collection { get; }

        /// <summary>등급별 포인트 대체 누적 횟수 (끝난 런의 SubstitutedRewards를 더한 것).</summary>
        public IReadOnlyList<int> Points => _points;

        /// <summary>진행 중인 런. 없으면 null. 끝난 런은 CloseEndedRun으로 정리한다.</summary>
        public RunState Run { get; private set; }

        /// <summary>마지막으로 런을 시작한 시작 덱 Id (없으면 null).</summary>
        public string LastStartDeckId { get; private set; }

        /// <summary>이 시작 덱으로 마지막에 고른 시작 사이드 덱 (없으면 빈 목록).</summary>
        public IReadOnlyList<uint> LastSideDeck(string startDeckId) =>
            startDeckId != null && _lastSideDecks.TryGetValue(startDeckId, out var side) ? side : (IReadOnlyList<uint>)Array.Empty<uint>();

        /// <summary>처음 하는 플레이어의 저장: 스타터 덱 4개를 지급한 컬렉션, 진행 중인 런 없음.</summary>
        public static GameSave NewGame() => new GameSave(CardCollection.FromRecipes(StarterDecks.All));

        /// <summary>런을 시작한다. 진행 중인 런이 있으면 InvalidOperationException (먼저 AbandonRun).</summary>
        public RunState StartRun(RunSetup setup, CardDatabase database, RarityTable rarities)
        {
            if (Run != null)
                throw new InvalidOperationException("진행 중인 런이 있습니다. 먼저 포기(AbandonRun)하거나 끝난 런을 정리(CloseEndedRun)하세요.");
            Run = RunState.Start(setup, database, rarities, Collection);
            LastStartDeckId = setup.StartDeck.Id;
            _lastSideDecks[setup.StartDeck.Id] = (setup.SideDeck ?? Array.Empty<uint>()).ToList();
            return Run;
        }

        /// <summary>진행 중인 런을 포기하고 정리한다 (런 종료로 처리). 런이 없으면 아무것도 하지 않는다.</summary>
        public void AbandonRun()
        {
            if (Run == null)
                return;
            if (Run.Phase != RunPhase.Ended)
                Run.Abandon();
            CloseEndedRun();
        }

        /// <summary>런이 끝났으면 포인트를 누적에 더하고 진행 중인 런을 비운다. 정리했으면 true.</summary>
        public bool CloseEndedRun()
        {
            if (Run == null || Run.Phase != RunPhase.Ended)
                return false;
            for (int i = 0; i < _points.Length; i++)
                _points[i] += Run.SubstitutedRewards[i];
            Run = null;
            return true;
        }

        // ---------------- 쓰기 ----------------

        /// <summary>저장 파일 내용. 듀얼 중인 런이 있으면 InvalidOperationException.</summary>
        public string Write()
        {
            var w = new SaveWriter();
            w.Comment("DuelRoguelike 저장 파일. 게임이 저절로 쓴다 (직접 고치면 읽지 못할 수 있음).");
            w.Line("format", FormatVersion);
            w.Line("points", _points.Cast<object>());
            if (LastStartDeckId != null)
                w.Line("last-start-deck", LastStartDeckId);
            foreach (var pair in _lastSideDecks.OrderBy(p => p.Key, StringComparer.Ordinal))
                w.Line("last-side-deck", new object[] { pair.Key }.Concat(pair.Value.Cast<object>()));
            w.Comment($"컬렉션 {Collection.Cards.Count}종 {Collection.TotalCount}장: card 카드코드 장수");
            foreach (var pair in Collection.Cards.OrderBy(p => p.Key))
                w.Line("card", pair.Key, pair.Value);
            if (Run != null)
            {
                w.Comment("진행 중인 런 (듀얼 사이 상태)");
                w.Line("run");
                Run.WriteTo(w);
                w.Line("end-run");
            }
            return w.ToString();
        }

        // ---------------- 읽기 ----------------

        /// <summary>저장 파일 내용을 읽는다. 형식이 틀리면 FormatException (파일 이름과 줄 번호 포함).</summary>
        public static GameSave Parse(string text, CardDatabase database, RarityTable rarities, string sourceName = GameSaveFile.FileName)
        {
            var r = new SaveReader(sourceName);
            var lines = SaveReader.Split(text ?? "");
            if (lines.Count == 0 || lines[0].Key != "format")
                throw r.Error("첫 줄이 \"format 버전\"이 아님");
            r.ExpectCount(lines[0], 1);
            int version = r.Int(lines[0], lines[0].Values[0], 1);
            if (version > FormatVersion)
                throw r.Error(lines[0], $"이 게임보다 새 버전이 쓴 저장 파일 (형식 {version}, 이 게임은 {FormatVersion}까지 읽음)");

            // 런 묶음(run … end-run)을 먼저 떼어 낸다. 런은 컬렉션을 들고 있으므로 컬렉션을 다 읽은 뒤에 만든다.
            var profile = new List<SaveLine>();
            List<SaveLine> runLines = null;
            SaveLine runStart = default;
            bool inRun = false;
            foreach (var line in lines)
            {
                if (inRun)
                {
                    if (line.Key == "end-run")
                    {
                        r.ExpectCount(line, 0);
                        inRun = false;
                    }
                    else
                        runLines.Add(line);
                    continue;
                }
                if (line.Key == "run")
                {
                    r.ExpectCount(line, 0);
                    if (runLines != null)
                        throw r.Error(line, "진행 중인 런이 두 번 나옴");
                    runLines = new List<SaveLine>();
                    runStart = line;
                    inRun = true;
                    continue;
                }
                if (line.Key == "end-run")
                    throw r.Error(line, "run 없이 end-run이 나옴");
                profile.Add(line);
            }
            if (inRun)
                throw r.Error(runStart, "end-run이 없음");

            var map = r.Unique(profile, ProfileKeys, RepeatableProfileKeys);
            var collection = new CardCollection();
            foreach (var line in profile.Where(l => l.Key == "card"))
            {
                r.ExpectCount(line, 2);
                uint code = r.Code(line, line.Values[0]);
                if (collection.Count(code) > 0)
                    throw r.Error(line, $"카드 {code}가 두 번 나옴");
                collection.Add(code, r.Int(line, line.Values[1], 1));
            }

            var save = new GameSave(collection);
            if (map.TryGetValue("points", out var points))
            {
                r.ExpectCount(points, RarityTable.RarityCount);
                for (int i = 0; i < RarityTable.RarityCount; i++)
                    save._points[i] = r.Int(points, points.Values[i], 0);
            }
            if (map.TryGetValue("last-start-deck", out var lastDeck))
            {
                r.ExpectCount(lastDeck, 1);
                save.LastStartDeckId = lastDeck.Values[0];
            }
            foreach (var line in profile.Where(l => l.Key == "last-side-deck"))
            {
                r.ExpectAtLeast(line, 1);
                string id = line.Values[0];
                if (save._lastSideDecks.ContainsKey(id))
                    throw r.Error(line, $"시작 덱 {id}가 두 번 나옴");
                save._lastSideDecks[id] = r.Codes(line, 1);
            }
            if (runLines != null)
                save.Run = RunState.ReadFrom(runLines, r, database, rarities, collection);
            return save;
        }
    }

    /// <summary>저장 파일 읽기·쓰기 (폴더 하나에 save.txt, 바로 전 저장 save.txt.bak).</summary>
    public static class GameSaveFile
    {
        public const string FileName = "save.txt";

        public static string FilePath(string directory) => Path.Combine(directory, FileName);

        /// <summary>바로 전에 저장했던 파일 (쓸 때마다 이전 save.txt를 여기로 옮긴다).</summary>
        public static string BackupPath(string directory) => FilePath(directory) + ".bak";

        /// <summary>
        /// 저장을 읽는다. 파일이 없으면 새 게임(notice는 null).
        /// 파일이 깨져서 못 읽으면 save-broken-날짜.txt로 옮겨 두고, 바로 전 저장(.bak)을 읽을 수 있으면 그것으로, 아니면 새 게임으로 시작한다.
        /// 그때 notice에 무슨 일이 있었는지 적는다 (화면에 보여 준다).
        /// </summary>
        public static GameSave Load(string directory, CardDatabase database, RarityTable rarities, out string notice)
        {
            notice = null;
            string path = FilePath(directory);
            if (!File.Exists(path))
                return GameSave.NewGame();
            try
            {
                return GameSave.Parse(File.ReadAllText(path, Encoding.UTF8), database, rarities, FileName);
            }
            catch (FormatException e)
            {
                string broken = BrokenPath(directory);
                File.Move(path, broken);
                string backup = BackupPath(directory);
                if (File.Exists(backup))
                {
                    try
                    {
                        var save = GameSave.Parse(File.ReadAllText(backup, Encoding.UTF8), database, rarities, Path.GetFileName(backup));
                        notice = $"저장 파일을 읽지 못해 {Path.GetFileName(broken)}(으)로 옮겨 두고, 바로 전 저장({Path.GetFileName(backup)})으로 이어 갑니다. ({e.Message})";
                        return save;
                    }
                    catch (FormatException)
                    {
                    }
                }
                notice = $"저장 파일을 읽지 못해 {Path.GetFileName(broken)}(으)로 옮겨 두고 새로 시작합니다. ({e.Message})";
                return GameSave.NewGame();
            }
        }

        /// <summary>저장한다. 임시 파일에 다 쓴 뒤 바꿔 끼우므로, 쓰다가 꺼져도 이전 파일이 남는다.</summary>
        public static void Save(GameSave save, string directory)
        {
            string text = save.Write();
            Directory.CreateDirectory(directory);
            string path = FilePath(directory);
            string temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, BackupPath(directory));
            else
                File.Move(temp, path);
        }

        static string BrokenPath(string directory)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(directory, $"save-broken-{stamp}.txt");
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(directory, $"save-broken-{stamp}-{i}.txt");
            return path;
        }
    }
}

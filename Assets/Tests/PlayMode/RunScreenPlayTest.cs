// 플레이 모드 시험: Run 씬을 열고 화면의 버튼만 눌러 로그라이크 런을 처음부터 끝까지 간다.
// 런 시작(시작 덱·시드·시작 사이드 덱) → [층 사이 화면 → 다음 듀얼 시작 → 듀얼 화면 "자동으로 끝까지" → "결과 보기" → (보충 고르기)] 반복
// → 런 종료 → 새 런. 듀얼은 사람 대신 임시 자동 응답(개발용 버튼)이 답하고, 상대는 WindBot이다.
// 저장도 본다 (저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다):
// - "처음 화면으로" → "이어하기", 씬을 다시 불러(게임을 다시 켠 것처럼) "이어하기"를 해도 층 사이 화면과 저장 파일이 그대로인지
// - 듀얼 도중에 씬을 다시 불러도 그 듀얼을 시작하기 전으로 돌아가는지
// - 런이 끝나면 진행 중인 런이 없어지고, 시작 사이드 덱은 다음 런에 그대로 채워지는지
// - 진행 중인 런이 있을 때 새 런: 한 번 더 눌러야 포기하고, 취소할 수 있는지
// 층 사이 화면의 교환(사이드 덱 → 덱, 덱 → 사이드 덱)도 한 번 해 본다. 보충 고르기를 한 번 이상 거칠 때까지 런을 더 한다.
// 오류 로그(Debug.LogError·LogException)가 하나라도 나오면 실패한다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 처음 한 번 1920×1080 PNG로 저장한다 (화면 모양 확인용).
//
// 실행: 에디터의 Test Runner 창(Window > General > Test Runner) PlayMode 탭, 또는 에디터를 닫고 배치 모드로
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testResults C:\Dev\playmode-results.xml -logFile C:\Dev\playmode.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class RunScreenPlayTest
    {
        const string RunUi = "RunUI";
        const string DuelUi = "DuelUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const int SidePicks = 3;
        const int MaxExtraRuns = 3;
        const int MaxDuelsPerRun = 300;
        const float LoadTimeout = 120;
        const float DuelTimeout = 300;

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        int _refills;
        int _duels;
        bool _exchanged;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-playtest-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(SaveDirVariable, _saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(SaveDirVariable, null);
            if (Directory.Exists(_saveDir))
                Directory.Delete(_saveDir, true);
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator RunFromStartToEnd()
        {
            yield return LoadRunScene();
            Assert.IsNull(Find<Button>(RunUi, "continue-run"), "처음 켰는데 진행 중인 런이 있다");
            Assert.IsFalse(File.Exists(SaveFile), "아무것도 안 했는데 저장 파일이 있다");

            // ---- 첫 런: 시작 덱, 시드, 시작 사이드 덱 3장 ----
            Click(Find<Button>(RunUi, "deck-Yugi"));
            yield return null;
            SetSeed(1);
            var picked = new List<string>();
            for (int i = 0; i < SidePicks; i++)
            {
                var row = Root(RunUi).Query<Button>().Where(b => b.name.StartsWith("pick-") && !picked.Contains(b.name.Substring(5))).First();
                Assert.IsNotNull(row, "컬렉션 목록에 고를 카드가 없다");
                string code = row.name.Substring("pick-".Length);
                Click(row);
                yield return null;
                Click(Find<Button>(RunUi, "side-add"));
                yield return null;
                picked.Add(code);
            }
            Assert.AreEqual($"시작 사이드 덱 {SidePicks}/10장", Find<Label>(RunUi, "side-count").text);
            // 빼고 다시 넣기
            Click(Find<Button>(RunUi, "side-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "side-remove"));
            yield return null;
            Assert.AreEqual($"시작 사이드 덱 {SidePicks - 1}/10장", Find<Label>(RunUi, "side-count").text, "사이드 덱에서 빼기");
            Assert.IsNull(Find<Button>(RunUi, "side-" + picked[0]), "뺀 카드가 사이드 덱 목록에 남아 있다");
            Click(Find<Button>(RunUi, "pick-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "side-add"));
            yield return null;
            foreach (string code in picked)
                Assert.IsNotNull(Find<Button>(RunUi, "side-" + code), $"고른 카드 {code}가 사이드 덱 목록에 없다");
            yield return Shot(RunUi, "1-start");

            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "층 사이 화면");
            foreach (string code in picked)
                Assert.IsNotNull(Find<Button>(RunUi, "card-side-" + code), $"시작 사이드 덱 카드 {code}가 층 사이 화면 사이드 덱에 없다");
            Assert.IsTrue(File.Exists(SaveFile), "런을 시작했는데 저장 파일이 없다");

            yield return Exchange();
            for (int i = 0; i < 2 && Find<Button>(RunUi, "new-run") == null; i++)
                yield return Step(1);
            Assert.IsNotNull(Find<Button>(RunUi, "next-duel"), "듀얼 2판 뒤에 층 사이 화면이 아니다 (런이 벌써 끝남?)");

            // ---- 처음 화면으로 → 이어하기 ----
            string hub = HubText();
            string saved = File.ReadAllText(SaveFile);
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "continue-run"), "처음 화면에 이어하기가 없다");
            yield return Shot(RunUi, "7-continue");
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.AreEqual(hub, HubText(), "이어하기 뒤 층 사이 화면이 다르다");

            // ---- 게임을 다시 켠 것처럼 씬을 다시 불러 이어하기 ----
            yield return LoadRunScene();
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.AreEqual(hub, HubText(), "다시 켠 뒤 이어하기: 층 사이 화면이 다르다");
            Assert.AreEqual(saved, File.ReadAllText(SaveFile), "읽기·이어하기만 했는데 저장 파일이 바뀌었다");

            // ---- 듀얼 도중에 끄기 → 그 듀얼을 시작하기 전으로 ----
            Click(Find<Button>(RunUi, "next-duel"));
            yield return WaitFor(() => Find<Button>(DuelUi, "auto-to-end") != null || Find<Button>(DuelUi, "leave-duel") != null, LoadTimeout, "듀얼 화면");
            var auto = Find<Button>(DuelUi, "auto-to-end");
            if (auto != null)
                Click(auto);
            for (int i = 0; i < 5; i++)
                yield return null;
            yield return LoadRunScene();
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.AreEqual(hub, HubText(), "듀얼 도중에 끈 뒤 이어하기: 듀얼을 시작하기 전과 다르다");
            Assert.AreEqual(saved, File.ReadAllText(SaveFile), "듀얼 도중에 저장 파일이 바뀌었다");
            Debug.Log("[런 화면 시험] 저장: 처음 화면으로·다시 켜기·듀얼 도중 끄기 뒤 이어하기 모두 같은 상태");

            // ---- 이 런을 끝까지 ----
            yield return PlayToEnd(1);
            Click(Find<Button>(RunUi, "new-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "새 런의 런 시작 화면");
            Assert.IsNull(Find<Button>(RunUi, "continue-run"), "끝난 런이 진행 중인 런으로 남아 있다");
            Assert.AreEqual($"시작 사이드 덱 {SidePicks}/10장", Find<Label>(RunUi, "side-count").text, "지난번 시작 사이드 덱을 기억하지 못한다");

            // ---- 진행 중인 런이 있을 때 새 런: 취소, 그리고 포기 ----
            SetSeed(2);
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "층 사이 화면");
            yield return Step(2);
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            SetSeed(3);
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "start-cancel"), "진행 중인 런이 있는데 포기 확인 없이 지나갔다");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "한 번 눌렀는데 새 런이 시작됐다");
            yield return Shot(RunUi, "8-abandon-confirm");
            Click(Find<Button>(RunUi, "start-cancel"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "continue-run"), "취소했는데 진행 중인 런이 없다");
            SetSeed(3);
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "포기 뒤 새 런의 층 사이 화면");
            StringAssert.Contains("끝낸 듀얼 0", Find<Label>(RunUi, "run-summary").text, "포기 뒤 새 런이 처음부터가 아니다");
            Assert.IsFalse(File.ReadAllText(SaveFile).Contains("seed 2\n"), "포기한 런(시드 2)이 저장 파일에 남아 있다");
            Debug.Log("[런 화면 시험] 포기: 확인·취소·포기 뒤 새 런");
            yield return PlayToEnd(3);

            // ---- 보충 고르기를 거칠 때까지 런을 더 한다 ----
            for (int seed = 4; seed < 4 + MaxExtraRuns && _refills == 0; seed++)
            {
                Click(Find<Button>(RunUi, "new-run"));
                yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "새 런의 런 시작 화면");
                SetSeed(seed);
                Click(Find<Button>(RunUi, "start-run"));
                yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "층 사이 화면");
                yield return PlayToEnd(seed);
            }

            Debug.Log($"[런 화면 시험] 듀얼 {_duels}판, 보충 {_refills}번, 교환 {(_exchanged ? "함" : "못 함")}");
            Assert.Greater(_refills, 0, "보충 고르기를 한 번도 거치지 않았다");
            Assert.IsTrue(_exchanged, "사이드 덱 카드를 한 번도 교환하지 못했다");
        }

        IEnumerator LoadRunScene()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, LoadTimeout, "런 시작 화면");
        }

        void SetSeed(int seed)
        {
            var field = Find<TextField>(RunUi, "seed");
            Assert.IsNotNull(field, "시드 칸이 없다");
            field.value = seed.ToString();
        }

        /// <summary>층 사이 화면의 상태 줄과 덱 목록 글 (이어하기 전후 비교용).</summary>
        static string HubText()
        {
            var root = Root(RunUi);
            Assert.IsNotNull(root, "런 화면이 보이지 않는다");
            string Texts(string name)
            {
                var e = root.Q(name);
                Assert.IsNotNull(e, $"{name}이(가) 없다 (층 사이 화면이 아님)");
                return string.Join("\n", e.Query<Label>().ToList().Select(l => l.text));
            }
            return Texts("run-status") + "\n----\n" + Texts("run-decks");
        }

        IEnumerator PlayToEnd(int seed)
        {
            int duels = 0;
            while (Find<Button>(RunUi, "new-run") == null)
            {
                Assert.Less(duels, MaxDuelsPerRun, $"시드 {seed}: 듀얼 {MaxDuelsPerRun}판 안에 런이 끝나지 않았다");
                if (Find<VisualElement>(RunUi, "refill") == null)
                    duels++;
                yield return Step(seed);
            }
            Debug.Log($"[런 화면 시험] 시드 {seed}: 런 종료, 보충 누적 {_refills}번");
            yield return Shot(RunUi, "6-end");
        }

        /// <summary>보충 창이 떠 있으면 한 장 넣고, 아니면 듀얼 하나를 끝까지 한다.</summary>
        IEnumerator Step(int seed)
        {
            if (Find<VisualElement>(RunUi, "refill") != null)
            {
                var candidate = Root(RunUi).Query<Button>().Where(b => b.name.StartsWith("refill-") && b.name != "refill-confirm").First();
                Assert.IsNotNull(candidate, "보충 창에 고를 카드가 없다");
                Click(candidate);
                yield return null;
                yield return Shot(RunUi, "5-refill");
                Click(Find<Button>(RunUi, "refill-confirm"));
                _refills++;
                yield return null;
                yield break;
            }

            if (!_exchanged)
                yield return Exchange();

            Click(Find<Button>(RunUi, "next-duel"));
            yield return WaitFor(() => Find<Button>(DuelUi, "auto-to-end") != null || Find<Button>(DuelUi, "leave-duel") != null,
                LoadTimeout, "듀얼 화면");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "듀얼 중에 런 화면이 보인다");
            yield return Shot(DuelUi, "2-duel");
            float until = Time.realtimeSinceStartup + DuelTimeout;
            while (Find<Button>(DuelUi, "leave-duel") == null)
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"시드 {seed} 듀얼: {DuelTimeout}초 안에 끝나지 않았다");
                var auto = Find<Button>(DuelUi, "auto-to-end");
                if (auto != null)
                    Click(auto); // 처음, 또는 자동 응답이 멈췄으면 다시 켠다
                yield return null;
            }
            yield return Shot(DuelUi, "3-duel-end");
            Click(Find<Button>(DuelUi, "leave-duel"));
            _duels++;
            yield return null;
            Assert.IsNull(Find<Button>(DuelUi, "leave-duel"), "결과 보기 뒤에도 듀얼 화면이 보인다");
            Assert.IsTrue(Find<Button>(RunUi, "next-duel") != null || Find<Button>(RunUi, "new-run") != null,
                "결과 보기 뒤에 층 사이 화면이나 런 종료 화면이 아니다");
            if (Find<Button>(RunUi, "next-duel") != null)
                yield return Shot(RunUi, "4-hub");
        }

        /// <summary>교환 (한 번만): 사이드 덱 카드를 덱에 넣고 다시 뺀다.</summary>
        IEnumerator Exchange()
        {
            if (_exchanged || Find<Button>(RunUi, "next-duel") == null)
                yield break;
            var side = Root(RunUi).Query<Button>().Where(b => b.name.StartsWith("card-side-")).First();
            if (side == null)
                yield break;
            string code = side.name.Substring("card-side-".Length);
            Click(side);
            yield return null;
            var toDeck = Find<Button>(RunUi, "move-to-deck");
            if (toDeck == null || !toDeck.enabledSelf)
                yield break;
            Click(toDeck);
            yield return null;
            Assert.IsTrue(Find<Button>(RunUi, "card-main-" + code) != null || Find<Button>(RunUi, "card-extra-" + code) != null,
                "덱에 넣은 카드가 메인·엑스트라 덱 목록에 없다");
            var toSide = Find<Button>(RunUi, "move-to-side");
            Assert.IsNotNull(toSide, "덱에 넣은 카드에 사이드 덱으로 빼기 버튼이 없다");
            Click(toSide);
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "card-side-" + code), "사이드 덱으로 뺀 카드가 사이드 덱 목록에 없다");
            _exchanged = true;
            Debug.Log($"[런 화면 시험] 교환: {code} 사이드 덱 → 덱 → 사이드 덱");
        }

        /// <summary>DUELROGUELIKE_SCREENSHOT_DIR이 있으면 화면(UIDocument) 하나를 텍스처에 그려 PNG로 저장한다. 같은 이름은 한 번만.</summary>
        IEnumerator Shot(string documentName, string name)
        {
            string dir = Environment.GetEnvironmentVariable("DUELROGUELIKE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(dir) || !_shots.Add(name))
                yield break;
            var settings = GameObject.Find(documentName).GetComponent<UIDocument>().panelSettings;
            var texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previous = settings.targetTexture;
            settings.targetTexture = texture;
            for (int i = 0; i < 3; i++)
                yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            settings.targetTexture = previous;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            texture.Release();
            yield return null;
        }

        static VisualElement Root(string documentName)
        {
            var go = GameObject.Find(documentName);
            var root = go != null ? go.GetComponent<UIDocument>()?.rootVisualElement : null;
            if (root == null || root.style.display.value == DisplayStyle.None)
                return null;
            return root;
        }

        /// <summary>보이는 화면(UIDocument)에서 이름으로 찾는다. 화면이 숨겨져 있으면 null.</summary>
        static T Find<T>(string documentName, string name) where T : VisualElement => Root(documentName)?.Q<T>(name);

        /// <summary>버튼을 누른다 (제출 이벤트 = 클릭과 같은 동작). 꺼진 버튼이면 실패.</summary>
        static void Click(Button button)
        {
            Assert.IsNotNull(button, "누를 버튼이 없다");
            Assert.IsTrue(button.enabledInHierarchy, $"{button.name} 버튼이 꺼져 있다");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = button;
                button.SendEvent(e);
            }
        }

        static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"{seconds}초 안에 {what}이(가) 나오지 않았다");
                yield return null;
            }
        }
    }
}

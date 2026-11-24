// 플레이 모드 시험 (짧은 전용 시험): 탑 이벤트 (2026-11-25에 더한 이벤트). docs/DESIGN.md "이벤트 시스템", docs/UI.md "나머지 창 > 이벤트".
// 시작 덱 —기계의 반란— 하나를 가진 새 게임에서 런을 시작하고, 저장 파일을 고쳐 지금 층을 이벤트 층으로 만든 뒤 이어하기로 그 이벤트를 띄운다. 듀얼은 하지 않는다.
// 1. 멈춘 태엽 병사: 기계족 5장 조건을 채워 조건 선택지가 켜져 있다 → 고르면 「기계 복제술」이 사이드 덱으로, 칩 +30.
// 2. 바람의 둥지: 바람 속성 몬스터가 없어 조건 선택지가 잠겨 있다 (조건은 오른쪽 꼬리표) → 벽을 타면 LP -1000, 칩 +40.
// 3. 피의 계약: 서명하면 LP -2000, 칩 +80.
// 4. 백룡의 전설: 덱 전용이 아니게 되어 기계의 반란 런에도 나온다. 「푸른 눈의 백룡」이 없어 조건 선택지가 잠겨 있다.
// 5. 길 잃은 아이: 카드를 건네면 몬스터 1장(사이드 덱에는 몬스터가 없어 메인 덱에서)을 이번 런에서 잃고 골드 +6. 메인 덱이 39장이라 보충 창이 뜬다.
// 이벤트 창과 결과 창은 처음 고르는 선택지의 결과를 숨기고, 고른 뒤 결과 창에 일어난 일을 보여 준다.
// 소리는 끈다 (AudioListener.volume = 0). 저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1~2분)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.TowerEventPlayTest -testResults C:\Dev\towerevent-results.xml -logFile C:\Dev\towerevent.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class TowerEventPlayTest
    {
        const string RunUi = "RunUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const float LoadTimeout = 120;
        const uint MachineDuplication = 63995093; // 기계 복제술

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        float _listenerVolume = 1f;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-towerevent-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(SaveDirVariable, _saveDir);
            // 시작 덱 —기계의 반란— 하나만 가진 새 게임 (기계족이 많고 바람 속성 몬스터는 없다)
            Directory.CreateDirectory(_saveDir);
            File.WriteAllText(GameSaveFile.FilePath(_saveDir), GameSave.NewGame(StructureDecks.MachineReVoltStart).Write(), new UTF8Encoding(false));
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            AudioListener.volume = _listenerVolume;
            Environment.SetEnvironmentVariable(SaveDirVariable, null);
            if (Directory.Exists(_saveDir))
                Directory.Delete(_saveDir, true);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NewEvents()
        {
            // 런 시작 (탑 앞)
            yield return LoadRun();
            Click(Find<Button>("title-new-run"));
            yield return WaitFor(() => Find<Button>("start-run") != null, 10, "새 런 창");
            Click(Find<Button>("start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");

            // ---- 1. 멈춘 태엽 병사: 조건 선택지가 켜져 있다 ----
            yield return ToEvent("clockwork-soldier");
            Assert.AreEqual("[ 멈춘 태엽 병사. ]", Text("event-title"), "이벤트 제목");
            Assert.AreEqual("부품을 맞춰 고친다", Text("event-choice-title-0"), "선택지 글에는 할 일만");
            Assert.IsTrue(Find<Button>("event-choice-0").enabledSelf, "기계의 반란인데 기계족 조건 선택지가 잠겼다");
            Assert.AreEqual("기계족 몬스터 5장 이상 · 덱·사이드 덱", Text("event-choice-note-0"), "조건 꼬리표");
            Assert.IsNull(Find<Label>("event-choice-note-1"), "조건 없는 선택지에 꼬리표가 있다");
            Assert.IsNull(Find<Label>("event-choice-info-0"), "처음 고르는 선택지의 결과가 보인다");
            yield return Shot("event-1-clockwork");
            int chips = RunNumber("chips");
            yield return Choose(0);
            StringAssert.Contains("기계 복제술", ResultText(), "결과 창에 받은 카드가 없다");
            yield return Shot("event-1b-clockwork-result");
            Assert.Contains(MachineDuplication.ToString(), RunCodes("side"), "「기계 복제술」이 사이드 덱에 들어가지 않았다");
            Assert.AreEqual(chips + 30, RunNumber("chips"), "칩 +30");
            yield return CloseResult();

            // ---- 2. 바람의 둥지: 바람 속성 몬스터가 없어 조건 선택지가 잠겨 있다 ----
            yield return ToEvent("wind-nest");
            Assert.AreEqual("[ 바람의 둥지. ]", Text("event-title"), "이벤트 제목");
            Assert.IsFalse(Find<Button>("event-choice-0").enabledSelf, "바람 속성 몬스터가 없는데 조건 선택지가 켜져 있다");
            Assert.AreEqual("잠김 · 바람 속성 몬스터 4장 이상 · 덱", Text("event-choice-note-0"), "잠긴 조건 꼬리표");
            Assert.IsTrue(Find<Button>("event-choice-1").ClassListContains("selected"), "처음에는 고를 수 있는 첫 선택지를 가리켜야 한다");
            yield return Shot("event-2-wind-nest");
            int lp = RunNumber("lp");
            chips = RunNumber("chips");
            yield return Choose(1);
            yield return Shot("event-2b-wind-nest-result");
            Assert.AreEqual(lp - 1000, RunNumber("lp"), "LP -1000");
            Assert.AreEqual(chips + 40, RunNumber("chips"), "칩 +40");
            yield return CloseResult();

            // ---- 3. 피의 계약 ----
            yield return ToEvent("blood-contract");
            Assert.AreEqual("[ 피의 계약. ]", Text("event-title"), "이벤트 제목");
            Assert.AreEqual("계약서에 서명한다", Text("event-choice-title-0"));
            yield return Shot("event-3-blood-contract");
            lp = RunNumber("lp");
            chips = RunNumber("chips");
            yield return Choose(0);
            yield return Shot("event-3b-blood-contract-result");
            Assert.AreEqual(Math.Max(1, lp - 2000), RunNumber("lp"), "LP -2000");
            Assert.AreEqual(chips + 80, RunNumber("chips"), "칩 +80");
            yield return CloseResult();

            // ---- 4. 백룡의 전설: 「푸른 눈의 백룡」이 없으면 조건 선택지가 잠긴다 ----
            yield return ToEvent("white-dragon-legend");
            Assert.AreEqual("[ 백룡의 전설. ]", Text("event-title"), "이벤트 제목");
            Assert.IsFalse(Find<Button>("event-choice-0").enabledSelf, "「푸른 눈의 백룡」이 없는데 조건 선택지가 켜져 있다");
            Assert.AreEqual("잠김 · 「푸른 눈의 백룡」 1장 · 덱", Text("event-choice-note-0"), "잠긴 조건 꼬리표");
            yield return Shot("event-4-white-dragon");
            yield return Choose(1);
            yield return CloseResult();

            // ---- 5. 길 잃은 아이: 사이드 덱에는 몬스터가 없어 메인 덱의 몬스터를 잃는다 → 메인 덱 39장이라 보충 창이 뜬다 ----
            yield return ToEvent("lost-child");
            Assert.AreEqual("[ 길 잃은 아이. ]", Text("event-title"), "이벤트 제목");
            yield return Shot("event-5-lost-child");
            int main = RunCodes("main").Length, side = RunCodes("side").Length;
            yield return Choose(0);
            StringAssert.Contains("골드 +6", ResultText(), "결과 창에 골드가 없다");
            StringAssert.Contains("잃음", ResultText(), "결과 창에 잃은 카드가 없다");
            yield return Shot("event-5b-lost-child-result");
            Assert.AreEqual(main - 1, RunCodes("main").Length, "메인 덱의 몬스터 1장을 이번 런에서 잃는다");
            Assert.AreEqual(side, RunCodes("side").Length, "사이드 덱(마법 카드뿐)은 그대로다");
            Assert.IsTrue(RunLines("event").Any(l => l.StartsWith("EventGold ") && l.Contains("count=6")), "골드 +6이 저장되지 않았다");
            yield return CloseResult();
            Assert.IsNotNull(Find<VisualElement>("refill"), "메인 덱이 39장이 됐는데 보충 창이 뜨지 않았다");
            Debug.Log("[탑 이벤트 시험] 통과: 멈춘 태엽 병사, 바람의 둥지, 피의 계약, 백룡의 전설, 길 잃은 아이");
        }

        // ================= 도우미 =================

        IEnumerator LoadRun()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>("title-new-run") != null, LoadTimeout, "처음 화면");
        }

        /// <summary>처음 화면으로 나가 저장 파일의 지금 층을 이 이벤트의 이벤트 층으로 고치고, 이어하기로 올라가 이벤트 창을 띄운다.</summary>
        IEnumerator ToEvent(string id)
        {
            Click(Find<Button>("to-title"));
            yield return null;
            string text = SaveText();
            // 그 층이 칩교환소였으면 진열(shop 줄)도 지운다 (칩교환소가 아닌 층에 진열이 있으면 저장 파일을 읽지 못한다)
            text = Regex.Replace(text, @"\n(floor-event|shop) [^\n]*", "");
            var kind = new Regex(@"\nfloor-kind [^\n]*");
            Assert.IsTrue(kind.IsMatch(text), "저장 파일에 floor-kind 줄이 없다");
            text = kind.Replace(text, "\nfloor-kind Event\nfloor-event " + id, 1);
            File.WriteAllText(SaveFile, text, new UTF8Encoding(false));
            yield return LoadRun();
            Click(Find<Button>("continue-run"));
            yield return null;
            Assert.IsTrue(IsHub(), "이어하기를 했는데 층 사이 화면이 아니다");
            Click(Find<Button>("climb"));
            yield return null;
            var skip = Find<Button>("climb-skip");
            if (skip != null)
            {
                Click(skip);
                yield return null;
            }
            yield return WaitFor(() => Find<VisualElement>("tower-event") != null, 10, $"이벤트 창 ({id})");
        }

        IEnumerator Choose(int index)
        {
            Click(Find<Button>($"event-choice-{index}"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("event-result"), "선택지를 고른 뒤 결과 창이 뜨지 않았다");
        }

        IEnumerator CloseResult()
        {
            Click(Find<Button>("event-result-ok"));
            yield return null;
            Assert.IsNull(Find<VisualElement>("event-result"), "결과 창이 닫히지 않았다");
            Assert.IsTrue(IsHub(), "결과 창을 닫은 뒤 층 사이 화면이 아니다");
        }

        static bool IsHub() => Find<VisualElement>("run-status") != null;

        static string Text(string name) => Find<Label>(name)?.text;

        static string ResultText() =>
            string.Join("\n", Find<VisualElement>("event-result")?.Query<Label>().ToList().Select(l => l.text) ?? Enumerable.Empty<string>());

        string SaveText() => File.ReadAllText(SaveFile).Replace("\r\n", "\n");

        /// <summary>저장 파일의 진행 중인 런 부분에서 key로 시작하는 줄들의 값 (key 뒤).</summary>
        List<string> RunLines(string key)
        {
            var run = Regex.Match(SaveText(), @"\nrun\n(.*?)\nend-run", RegexOptions.Singleline);
            Assert.IsTrue(run.Success, "저장 파일에 진행 중인 런이 없다");
            return run.Groups[1].Value.Split('\n').Where(l => l.StartsWith(key + " ")).Select(l => l.Substring(key.Length + 1)).ToList();
        }

        /// <summary>런의 숫자 줄 (chips, lp 등)의 첫 값.</summary>
        int RunNumber(string key)
        {
            var lines = RunLines(key);
            Assert.AreEqual(1, lines.Count, $"런의 {key} 줄");
            return int.Parse(lines[0].Split(' ')[0]);
        }

        /// <summary>런의 카드 줄 (main, extra, side): 카드 코드들. 비어 있으면 빈 배열.</summary>
        string[] RunCodes(string key)
        {
            var lines = RunLines(key);
            return lines.Count == 0 ? new string[0] : lines[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>DUELROGUELIKE_SCREENSHOT_DIR이 있으면 런 화면을 텍스처에 그려 PNG로 저장한다. 같은 이름은 한 번만.</summary>
        IEnumerator Shot(string name)
        {
            string dir = Environment.GetEnvironmentVariable("DUELROGUELIKE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(dir) || !_shots.Add(name))
                yield break;
            var settings = GameObject.Find(RunUi).GetComponent<UIDocument>().panelSettings;
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

        static VisualElement Root()
        {
            var go = GameObject.Find(RunUi);
            var root = go != null ? go.GetComponent<UIDocument>()?.rootVisualElement : null;
            return root == null || root.style.display.value == DisplayStyle.None ? null : root;
        }

        static T Find<T>(string name) where T : VisualElement => Root()?.Q<T>(name);

        /// <summary>버튼을 누른다 (제출 이벤트 = 클릭과 같은 동작). 꺼져 있으면 실패.</summary>
        static void Click(VisualElement button)
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

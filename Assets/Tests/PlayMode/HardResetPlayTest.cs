// 플레이 모드 시험 (짧은 전용 시험): 게임 데이터 초기화 (하드 리셋). 설정 창 맨 아래 줄 → 확인 창 → 새 게임.
// Run 씬의 처음 화면에서 버튼과 가상 키보드만으로 다룬다. 듀얼은 하지 않는다.
// 1. 지울 것을 만든다: 컬렉션 전부 분해(골드가 생김), 새 런 시작(진행 중인 런). 저장을 여러 번 해서 바로 전 저장(.bak)도 생긴다.
//    탑(허브)에서 연 설정 창에는 초기화 줄이 없다.
// 2. 다시 켠 처음 화면의 설정 창: 아래 키로 초기화 줄을 고르고 결정 키 → 확인 창 (지우는 것에 골드와 진행 중인 런).
//    처음에는 "그만둔다"가 골라져 있어 결정 키로는 지워지지 않고 설정 창의 초기화 줄로 돌아온다. 취소 키도 같다.
// 3. "초기화한다"를 누르면 처음 화면: 초기화 알림, 이어하기 없음, 골드 0. 저장 폴더에는 save.txt 하나만 남고
//    (바로 전 저장, 깨진 저장 파일도 지움) 컬렉션은 새 게임과 같다.
// 4. 씬을 다시 불러도(게임을 다시 켠 것처럼) 새 게임 그대로다.
// 소리는 끈다 (AudioListener.volume = 0). 저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1~2분)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.HardResetPlayTest -testResults C:\Dev\hardreset-results.xml -logFile C:\Dev\hardreset.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class HardResetPlayTest
    {
        const string RunUi = "RunUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const float LoadTimeout = 120;
        /// <summary>새 게임의 컬렉션 장수: 스타터 덱 4개 × 50장.</summary>
        const int NewGameCards = 200;

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        float _listenerVolume = 1f;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-hardreset-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(SaveDirVariable, _saveDir);
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
        public IEnumerator HardReset()
        {
            // ---- 1. 지울 것을 만든다: 골드(전부 분해)와 진행 중인 런 ----
            yield return LoadTitle();
            Click(Find<Button>("open-gold-shop"));
            yield return null;
            Click(Find<Button>("gold-shop-disassemble"));
            yield return null;
            Click(Find<Button>("disassemble-all"));
            yield return null;
            Click(Find<Button>("disassemble-all-confirm"));
            yield return null;
            int gold = SavedGold();
            Assert.Greater(gold, 0, "전부 분해했는데 골드가 없다");
            Click(Find<Button>("gold-shop-close"));
            yield return null;
            Click(Find<Button>("title-new-run"));
            yield return null;
            Click(Find<Button>("recipe-Yugi"));
            yield return null;
            Click(Find<Button>("start-run"));
            yield return WaitFor(() => Find<VisualElement>("run-status") != null, 20, "층 사이 화면");
            StringAssert.Contains("\nrun\n", SaveText(), "런을 시작했는데 저장 파일에 런이 없다");
            Click(Find<Button>("hub-settings"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("settings"), "탑에서 설정 창이 열리지 않았다");
            Assert.IsNull(Find<VisualElement>("settings-reset"), "탑에서 연 설정 창에 초기화 줄이 있다");
            Click(Find<Button>("settings-close"));
            yield return null;
            Assert.IsTrue(File.Exists(SaveFile + ".bak"), "여러 번 저장했는데 바로 전 저장(.bak)이 없다");
            File.WriteAllText(Path.Combine(_saveDir, "save-broken-20260101-000000.txt"), "예전에 깨진 저장");

            // ---- 2. 다시 켠 처음 화면의 설정 창: 확인 창은 결정 키·취소 키로 그만둘 수 있다 ----
            yield return LoadTitle();
            Assert.IsNotNull(Find<Button>("continue-run"), "다시 켠 처음 화면에 이어하기가 없다");
            Click(Find<Button>("open-settings"));
            yield return null;
            var row = Find<VisualElement>("settings-reset");
            Assert.IsNotNull(row, "처음 화면에서 연 설정 창에 초기화 줄이 없다");
            yield return Shot("reset-0-settings");
            yield return Tap(Key.DownArrow);
            yield return Tap(Key.DownArrow);
            Assert.IsTrue(row.ClassListContains("selected"), "아래 키 두 번으로 초기화 줄을 고르지 못했다");
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<VisualElement>("hard-reset-confirm"), "초기화 줄에서 결정 키를 눌렀는데 확인 창이 없다");
            string lost = Text("hard-reset-lost");
            StringAssert.Contains($"골드 {gold}", lost, "확인 창의 지우는 것에 골드가 없다");
            StringAssert.Contains("진행 중인 런", lost, "확인 창의 지우는 것에 진행 중인 런이 없다");
            yield return Shot("reset-1-confirm");
            yield return Tap(Key.Z);
            Assert.IsNull(Find<VisualElement>("hard-reset-confirm"), "그만둔다를 골랐는데 확인 창이 그대로다");
            Assert.IsTrue(Find<VisualElement>("settings-reset")?.ClassListContains("selected") == true, "그만둔 뒤 설정 창의 초기화 줄로 돌아오지 않았다");
            Assert.AreEqual(gold, SavedGold(), "그만뒀는데 저장 파일이 바뀌었다");
            Click(Find<Button>("settings-reset-open"));
            yield return null;
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("hard-reset-confirm"), "취소 키를 눌렀는데 확인 창이 그대로다");
            Assert.IsNotNull(Find<VisualElement>("settings-reset"), "취소 키로 그만둔 뒤 설정 창으로 돌아오지 않았다");
            Assert.AreEqual(gold, SavedGold(), "취소 키로 그만뒀는데 저장 파일이 바뀌었다");

            // ---- 3. 초기화한다 ----
            Click(Find<Button>("settings-reset-open"));
            yield return null;
            Click(Find<Button>("hard-reset-confirm-button"));
            yield return WaitFor(() => Find<Button>("title-new-run") != null, 10, "처음 화면");
            Assert.IsNull(Find<VisualElement>("hard-reset-confirm"), "초기화한 뒤에도 확인 창이 남아 있다");
            StringAssert.Contains("초기화했습니다", Text("reset-notice") ?? "", "초기화 알림이 없다");
            Assert.IsNull(Find<Button>("continue-run"), "초기화했는데 이어하기가 있다");
            Assert.AreEqual("골드 0", Text("title-gold"), "초기화한 뒤 메뉴의 골드");
            yield return Shot("reset-2-title");
            var files = Directory.GetFiles(_saveDir).Select(Path.GetFileName).ToArray();
            Assert.AreEqual(new[] { "save.txt" }, files, "초기화한 뒤 저장 폴더에는 save.txt만 남아야 한다");
            Assert.AreEqual(0, SavedGold(), "초기화한 뒤 저장 파일의 골드");
            Assert.IsFalse(SaveText().Contains("\nrun\n"), "초기화한 뒤 저장 파일에 런이 남아 있다");
            Assert.AreEqual(NewGameCards, SavedCollection().Values.Sum(), "초기화한 뒤 컬렉션 장수 (새 게임)");

            // ---- 4. 다시 켜도 새 게임 ----
            yield return LoadTitle();
            Assert.AreEqual("골드 0", Text("title-gold"), "다시 켠 뒤의 골드");
            Assert.IsNull(Find<Button>("continue-run"), "다시 켠 뒤에 이어하기가 있다");
            Assert.IsNull(Find<Label>("reset-notice"), "다시 켠 뒤에도 초기화 알림이 남아 있다");
            Debug.Log("[하드 리셋 시험] 통과: 탑에는 없음, 확인 창 그만두기, 초기화, 다시 켜기");
        }

        // ================= 도우미 (GoldShopPlayTest와 같다) =================

        IEnumerator LoadTitle()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>("title-new-run") != null, LoadTimeout, "처음 화면");
        }

        static string Text(string name) => Find<Label>(name)?.text;

        string SaveText() => File.ReadAllText(SaveFile).Replace("\r\n", "\n");

        /// <summary>저장 파일의 프로필 gold 줄.</summary>
        int SavedGold()
        {
            var match = Regex.Match(SaveText(), @"\ngold (-?\d+)\n");
            Assert.IsTrue(match.Success, "저장 파일에 gold 줄이 없다");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>저장 파일의 컬렉션: 카드 코드 → 장수.</summary>
        Dictionary<string, int> SavedCollection() =>
            Regex.Matches(SaveText(), @"\ncard (\d+) (\d+)(?=\n)").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));

        /// <summary>시험용 가상 키보드. 배치 모드는 창에 포커스가 없어 키 입력이 버려질 수 있어서, 쓰는 동안만 포커스를 무시하게 한다.</summary>
        sealed class VirtualKeyboard : IDisposable
        {
            readonly InputSettings.BackgroundBehavior _background;
            readonly InputSettings.EditorInputBehaviorInPlayMode _editor;
            public readonly Keyboard Device;

            public VirtualKeyboard()
            {
                var settings = InputSystem.settings;
                _background = settings.backgroundBehavior;
                _editor = settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Device = InputSystem.AddDevice<Keyboard>();
                Device.MakeCurrent();
            }

            public void Hold(params Key[] keys) => InputSystem.QueueStateEvent(Device, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));

            public void Dispose()
            {
                InputSystem.RemoveDevice(Device);
                var settings = InputSystem.settings;
                settings.backgroundBehavior = _background;
                settings.editorInputBehaviorInPlayMode = _editor;
            }
        }

        /// <summary>키를 한 번 눌렀다 뗀다 (누른 프레임에 화면이 받는다). 화면이 다시 그려질 틈을 한 프레임 더 준다.</summary>
        static IEnumerator Tap(Key key)
        {
            using (var keys = new VirtualKeyboard())
            {
                keys.Hold(key);
                yield return null;
                keys.Hold();
                yield return null;
            }
            yield return null;
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

        /// <summary>버튼이나 카드를 누른다 (제출 이벤트 = 클릭과 같은 동작). 꺼져 있으면 실패.</summary>
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

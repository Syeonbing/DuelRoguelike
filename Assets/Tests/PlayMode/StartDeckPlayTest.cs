// 플레이 모드 시험 (짧은 전용 시험): 시작 덱 고르기. 새 저장(빈 저장 폴더)으로 Run 씬을 열어 버튼과 가상 키보드로 다룬다. 듀얼은 하지 않는다.
// 1. 처음 켜면 처음 화면 위에 고르기 창이 바로 뜬다: 세 덱의 돌판, 가운데(기계의 반란)가 골라져 있고 고른 돌판에만 버튼이 있다. 저장 파일은 아직 없다.
//    ←→로 고르기 (끝에서 멈춤).
// 2. E: 카드 40장 창. 같은 카드는 한 칸(장수 합 40), 이동 키로 카드를 옮겨 다니고, Q·E로 다른 덱. X로 고르기 창.
// 3. X: 고르지 않고 닫으면 처음 화면의 메뉴는 시작 덱 고르기·설정·끝내기뿐. 결정 키로 다시 연다.
// 4. 확인 창: 취소 키와 "다시 고른다"는 고르지 않는다. 결정 키("이 덱으로 한다")로 고른다.
// 5. 고른 뒤: 처음 화면에 띠 알림, 새 런이 맨 위에 골라져 있다. 저장 파일에는 그 덱의 카드 40장과 레시피 하나, 골드 0.
//    새 런 창에 그 레시피가 있고 런을 시작할 수 있다. 씬을 다시 불러도(게임을 다시 켠 것처럼) 고르기 창은 뜨지 않는다.
// 소리는 끈다 (AudioListener.volume = 0). 저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1~2분)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.StartDeckPlayTest -testResults C:\Dev\startdeck-results.xml -logFile C:\Dev\startdeck.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DuelRoguelike.Engine.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class StartDeckPlayTest
    {
        const string RunUi = "RunUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const float LoadTimeout = 120;

        static readonly string[] DeckIds = StructureDecks.StartVersions.Select(d => d.Id).ToArray();

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        float _listenerVolume = 1f;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-startdeck-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator ChooseStartDeck()
        {
            // ---- 1. 처음 켜면 고르기 창 ----
            yield return LoadRun();
            Assert.IsNotNull(Find<VisualElement>("start-deck"), "새 저장인데 시작 덱 고르기 창이 뜨지 않았다");
            Assert.IsNull(Find<Button>("title-new-run"), "시작 덱을 고르기 전인데 새 런이 보인다");
            Assert.IsFalse(File.Exists(SaveFile), "아무것도 고르지 않았는데 저장 파일이 있다");
            foreach (string id in DeckIds)
                Assert.IsNotNull(Slab(id), $"덱 돌판 {id}가 없다");
            Assert.AreEqual(DeckIds[1], ChosenDeck(), "처음에는 가운데 덱이 골라져 있어야 한다");
            AssertButtonsOnlyOnChosen();
            Assert.AreEqual("기계의 반란", Slab(DeckIds[1]).Q<Label>("start-deck-name").text);
            StringAssert.Contains("몬스터", Slab(DeckIds[1]).Q<Label>("start-deck-kinds").text, "돌판에 몬스터·마법·함정 수가 없다");
            StringAssert.Contains("장", Slab(DeckIds[0]).Q<Label>("start-deck-type").text, "돌판에 많은 속성·종족이 없다");
            yield return Shot("start-0-choose");
            yield return Tap(Key.LeftArrow);
            Assert.AreEqual(DeckIds[0], ChosenDeck(), "왼쪽 키로 고르기");
            yield return Tap(Key.LeftArrow);
            Assert.AreEqual(DeckIds[0], ChosenDeck(), "맨 왼쪽에서 왼쪽 키: 그대로");
            yield return Tap(Key.D);
            yield return Tap(Key.D);
            yield return Tap(Key.D);
            Assert.AreEqual(DeckIds[2], ChosenDeck(), "오른쪽 키(D)로 고르기, 맨 오른쪽에서 멈춤");
            AssertButtonsOnlyOnChosen();

            // ---- 2. 카드 40장 창 ----
            yield return Tap(Key.E);
            Assert.IsNotNull(Find<VisualElement>("start-deck-list"), "E 키로 카드 40장 창이 열리지 않았다");
            Assert.AreEqual(40, ListCardCount(), "카드 40장 창의 장수 합");
            var slots = GridSlots();
            Assert.AreEqual(slots.Count, slots.Select(s => s.name).Distinct().Count(), "같은 카드가 두 칸에 있다");
            Assert.AreEqual(0, SelectedSlotIndex(), "처음에는 첫 카드가 골라져 있어야 한다");
            StringAssert.Contains("이 덱에", Text("start-deck-card-info"), "상세 카드 아래에 이 덱의 장수가 없다");
            yield return Shot("start-1-list");
            yield return Tap(Key.RightArrow);
            Assert.AreEqual(1, SelectedSlotIndex(), "오른쪽 키로 다음 카드");
            yield return Tap(Key.DownArrow);
            Assert.AreEqual(Math.Min(1 + 11, slots.Count - 1), SelectedSlotIndex(), "아래 키로 한 줄(11장) 아래");
            yield return Tap(Key.E);
            Assert.IsTrue(Find<VisualElement>("start-deck-grid").Q("card-startdeck-14989021") != null, "E 키로 열풍의 패왕으로 넘어가지 않았다 (신조 시무르그가 없다)");
            Assert.AreEqual(40, ListCardCount(), "열풍의 패왕 카드 40장 창의 장수 합");
            yield return Tap(Key.Q);
            Assert.IsTrue(Find<VisualElement>("start-deck-grid").Q("card-startdeck-29424328") != null, "Q 키로 암흑의 주술로 돌아오지 않았다 (마왕 디아블로스가 없다)");
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("start-deck-list"), "X 키로 카드 40장 창이 닫히지 않았다");
            Assert.AreEqual(DeckIds[2], ChosenDeck(), "카드 40장 창에서 돌아온 뒤 고른 덱");

            // ---- 3. 고르지 않고 닫기 → 처음 화면의 "시작 덱 고르기" ----
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("start-deck"), "X 키로 고르기 창이 닫히지 않았다");
            var menu = Find<VisualElement>("title-menu");
            Assert.IsNotNull(menu, "처음 화면의 메뉴가 없다");
            var items = menu.Query<Button>().ToList().Select(b => b.name).ToArray();
            Assert.AreEqual(new[] { "title-start-deck", "open-settings", "title-quit" }, items, "고르기 전의 처음 화면 메뉴");
            Assert.IsNotNull(Find<Label>("title-no-deck"), "고르기 전의 처음 화면에 안내가 없다");
            Assert.IsFalse(File.Exists(SaveFile), "닫기만 했는데 저장 파일이 생겼다");
            yield return Shot("start-2-title-closed");
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<VisualElement>("start-deck"), "메뉴의 시작 덱 고르기로 창이 다시 열리지 않았다");
            Assert.AreEqual(DeckIds[2], ChosenDeck(), "다시 연 창에서 고른 덱");

            // ---- 4. 확인 창 ----
            Click(Slab(DeckIds[2]).Q<Button>("start-deck-pick"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("start-deck-confirm"), "이 덱으로 시작을 눌렀는데 확인 창이 없다");
            Assert.AreEqual("암흑의 주술", Text("start-deck-confirm-name"));
            yield return Shot("start-3-confirm");
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("start-deck-confirm"), "취소 키로 확인 창이 닫히지 않았다");
            Assert.IsNotNull(Find<VisualElement>("start-deck"), "확인 창을 닫으면 고르기 창으로 돌아와야 한다");
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<VisualElement>("start-deck-confirm"), "결정 키로 확인 창이 열리지 않았다");
            yield return Tap(Key.RightArrow);
            yield return Tap(Key.Z); // "다시 고른다"
            Assert.IsNull(Find<VisualElement>("start-deck-confirm"), "다시 고른다를 골랐는데 확인 창이 그대로다");
            Assert.IsFalse(File.Exists(SaveFile), "고르지 않았는데 저장 파일이 생겼다");
            yield return Tap(Key.Z);
            yield return Tap(Key.Z); // 처음에 골라져 있는 "이 덱으로 한다"

            // ---- 5. 고른 뒤 ----
            yield return WaitFor(() => Find<Button>("title-new-run") != null, 10, "고른 뒤의 처음 화면");
            Assert.IsNull(Find<VisualElement>("start-deck"), "고른 뒤에도 고르기 창이 남아 있다");
            StringAssert.Contains("「시작 덱 —암흑의 주술—」 카드 40장을 받았다.", Text("start-deck-banner-text") ?? "", "고른 뒤의 띠 알림");
            Assert.IsTrue(Find<Button>("title-new-run").ClassListContains("selected"), "고른 뒤에는 새 런이 골라져 있어야 한다");
            yield return Shot("start-4-title-chosen");
            var deck = StructureDecks.CurseOfDarknessStart;
            var saved = SavedCollection();
            Assert.AreEqual(40, saved.Values.Sum(), "저장 파일의 컬렉션 장수");
            foreach (var (code, count) in deck.Entries)
                Assert.AreEqual(count, saved.TryGetValue(code.ToString(), out int n) ? n : 0, $"저장 파일의 카드 {code} 장수");
            Assert.AreEqual(1, Regex.Matches(SaveText(), @"\nrecipe ").Count, "저장 파일의 레시피 수");
            StringAssert.Contains($"\nrecipe {deck.Id} ", SaveText(), "고른 덱의 레시피가 저장 파일에 없다");
            StringAssert.Contains("\ngold 0\n", SaveText(), "고른 뒤의 골드");
            CollectionAssert.Contains(LabelTexts("title-edit"), "레시피 1개", "메뉴의 덱 편집 옆 레시피 수");
            Click(Find<Button>("title-new-run"));
            yield return null;
            Assert.IsNotNull(Find<Button>("recipe-" + deck.Id), "새 런 창에 고른 덱의 레시피가 없다");
            StringAssert.Contains("시작할 수 있다", Text("start-recipe-status"), "고른 덱으로 런을 시작할 수 없다고 나온다");
            yield return Tap(Key.X);

            yield return LoadRun();
            Assert.IsNull(Find<VisualElement>("start-deck"), "고른 뒤 다시 켰는데 시작 덱 고르기 창이 떴다");
            Assert.IsNotNull(Find<Button>("title-new-run"), "고른 뒤 다시 켠 처음 화면에 새 런이 없다");
            Debug.Log("[시작 덱 시험] 통과: 처음 켜면 고르기 창, 카드 40장 창, 닫기·다시 열기, 확인 창, 고른 뒤 저장, 다시 켜기");
        }

        // ================= 도우미 (HardResetPlayTest와 같다) =================

        IEnumerator LoadRun()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>("title-new-run") != null || Find<VisualElement>("start-deck") != null, LoadTimeout, "처음 화면");
        }

        static VisualElement Slab(string deckId) => Find<VisualElement>("start-deck-" + deckId);

        /// <summary>금색 테로 떠오른 돌판의 덱 Id (없으면 null).</summary>
        static string ChosenDeck() => DeckIds.FirstOrDefault(id => Slab(id)?.ClassListContains("selected") == true);

        /// <summary>고른 돌판에만 켜진 버튼(카드 40장, 이 덱으로 시작)이 있다.</summary>
        static void AssertButtonsOnlyOnChosen()
        {
            string chosen = ChosenDeck();
            foreach (string id in DeckIds)
            {
                var pick = Slab(id).Q<Button>("start-deck-pick");
                Assert.IsNotNull(pick, $"{id} 돌판에 이 덱으로 시작 버튼 자리가 없다");
                Assert.AreEqual(id == chosen, pick.enabledInHierarchy && pick.resolvedStyle.visibility == Visibility.Visible,
                    $"{id} 돌판의 이 덱으로 시작 버튼은 고른 돌판에만 보여야 한다");
            }
        }

        static List<VisualElement> GridSlots() => Find<VisualElement>("start-deck-grid").Children().ToList();

        /// <summary>테두리 색이 있는 칸 = 고른 카드 (없으면 -1).</summary>
        static int SelectedSlotIndex() => GridSlots().FindIndex(s => s.style.borderTopColor.value.a > 0.5f);

        /// <summary>카드 40장 창의 장수 합: 칸마다 ×n 표시가 있으면 n장, 없으면 1장.</summary>
        static int ListCardCount() =>
            GridSlots().Sum(s => s.Query<Label>().ToList().Select(l => Regex.Match(l.text ?? "", @"^×(\d+)$")).Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(1).First());

        static string[] LabelTexts(string name) => Find<VisualElement>(name).Query<Label>().ToList().Select(l => l.text).ToArray();

        static string Text(string name) => Find<Label>(name)?.text;

        string SaveText() => File.ReadAllText(SaveFile).Replace("\r\n", "\n");

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

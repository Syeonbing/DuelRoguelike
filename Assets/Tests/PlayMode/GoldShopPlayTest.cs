// 플레이 모드 시험 (짧은 전용 시험): 골드 상점. docs/UI.md "나머지 창 > 골드 상점", docs/ECONOMY.md "골드 가격".
// Run 씬의 처음 화면에서 버튼과 가상 키보드만으로 골드 상점을 다뤄 본다. 듀얼은 하지 않는다.
// 1. 스타터 덱 4개를 가진 새 게임 (골드 0. 시험 폴더에 써 둔다: TestSaves): 메뉴의 "골드 상점", 사이드 덱 확장 줄은 골드가 모자라 꺼져 있고, 팩·스트럭처 덱은 "준비 중"으로 꺼져 있다.
// 2. 컬렉션 분해: 3장을 넘게 가진 카드만 나온다. 금색 버튼과 결정 키로 1장씩 분해 → 골드가 늘고 저장 파일에 남는다.
//    "전부 분해"는 한 번 묻는다 (처음에는 "그만둔다": 결정 키로는 분해되지 않는다). 분해하면 띠 알림이 뜨고, 넘는 카드가 없어진다.
// 3. 저장 파일을 고쳐 골드 130, 유희 편 레시피의 시작 사이드 덱 5장(영구 칸 3 + 임시 칸 2)으로 다시 켠다.
//    새 런 창의 임시 확장: 살 수 있는 만큼(2장)으로 시작하고, − 버튼과 좌우 키로 바꾼다. 사지 않은 카드는 "두고 감".
// 4. 사이드 덱 확장: 확인 창에서 취소 키는 사지 않고 돌아오고, 결정 키("산다")는 산다 → 골드 130 → 80, 한도 4장, 띠 알림,
//    저장 파일의 side-limit 4. 다음 확장(100)은 골드가 모자라 꺼진다.
// 5. 씬을 다시 불러도(게임을 다시 켠 것처럼) 골드와 한도가 그대로다. 덱 편집 화면의 시작 사이드 덱 칸(영구 칸 4, 임시 칸 5),
//    임시 칸의 카드를 영구 칸으로 옮기기. 임시 칸 1장을 사서 런을 시작하면 골드 80 → 72, 런의 사이드 덱 5장.
// 소리는 끈다 (AudioListener.volume = 0). 저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1~2분)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.GoldShopPlayTest -testResults C:\Dev\goldshop-results.xml -logFile C:\Dev\goldshop.log
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
    public sealed class GoldShopPlayTest
    {
        const string RunUi = "RunUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const float LoadTimeout = 120;
        /// <summary>분해할 때 카드 ID마다 남기는 장수 (RunRules.DisassemblyKeep).</summary>
        const int Keep = 3;
        /// <summary>사이드 덱 임시 칸 1장의 값 (RunRules.RunSideDeckSlotPrice).</summary>
        const int SlotPrice = 8;

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        float _listenerVolume = 1f;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-goldshop-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(SaveDirVariable, _saveDir);
            TestSaves.WriteStarterSave(_saveDir); // 스타터 덱 4개를 가진 새 게임 (4장인 카드가 있어 분해할 수 있다)
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
        public IEnumerator GoldShop()
        {
            // ---- 1. 새 게임: 골드 0 ----
            yield return LoadTitle();
            Assert.AreEqual("골드 0", Text("title-gold"), "새 게임의 메뉴에 보이는 골드");
            yield return Shot("gold-0-title");
            yield return OpenShop();
            Assert.AreEqual(0, ShopGold(), "새 게임의 가진 골드");
            Assert.IsFalse(Find<Button>("gold-shop-side").enabledSelf, "골드 0인데 사이드 덱 확장 줄이 켜져 있다");
            StringAssert.Contains("모자라다", Text("gold-shop-side-note"), "골드가 모자란 까닭이 없다");
            Assert.AreEqual("골드 50", Text("gold-shop-side-price"), "첫 확장의 값");
            Assert.AreEqual(3, Find<VisualElement>("gold-shop-side-slots").Query("slot-owned").ToList().Count, "처음에 가진 사이드 덱 칸");
            Assert.IsFalse(Find<Button>("gold-shop-pack").enabledSelf || Find<Button>("gold-shop-structure").enabledSelf, "준비 중인 상품이 켜져 있다");
            Assert.AreEqual("준비 중", Text("gold-shop-pack-tag"));
            Assert.IsTrue(Find<Button>("gold-shop-disassemble").enabledSelf, "새 게임에는 4장인 카드가 있는데 분해 줄이 꺼져 있다");
            Assert.IsTrue(Find<Button>("gold-shop-disassemble").ClassListContains("selected"), "켜진 첫 줄(컬렉션 분해)을 가리키고 있지 않다");
            yield return Shot("gold-1-shop-empty");

            // ---- 2. 컬렉션 분해 ----
            int allGold = PlusGold(Text("gold-shop-disassemble-gold"));
            Click(Find<Button>("gold-shop-disassemble"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("gold-disassemble"), "분해 창이 열리지 않았다");
            var cards = Named("card-disassemble-").Select(e => e.name.Substring("card-disassemble-".Length)).ToList();
            Assert.Greater(cards.Count, 1, "분해할 수 있는 카드가 둘 이상이어야 한다 (새 게임에는 4장인 카드가 여럿 있다)");
            Assert.IsNull(Find<Button>("disassemble-one"), "카드를 고르기 전인데 분해 버튼이 있다");
            // 마우스: 카드를 고르고 금색 버튼으로 1장
            string first = cards[0];
            int ownedFirst = ShownCount("card-disassemble-" + first);
            Assert.Greater(ownedFirst, Keep, "분해 창의 카드는 3장을 넘게 가진 카드여야 한다");
            Click(Find<VisualElement>("card-disassemble-" + first));
            yield return null;
            var one = Find<Button>("disassemble-one");
            Assert.IsNotNull(one, "카드를 골랐는데 분해 버튼이 없다");
            int firstGold = PlusGold(one.text);
            Assert.Greater(firstGold, 0, $"분해 버튼의 골드: {one.text}");
            yield return Shot("gold-2-disassemble");
            Click(one);
            yield return null;
            Assert.AreEqual(firstGold, int.Parse(Text("disassemble-gold")), "1장 분해한 뒤 가진 골드");
            StringAssert.Contains("분해했다", Text("disassemble-notice"), "분해 알림이 없다");
            Assert.AreEqual(firstGold, SavedGold(), "분해로 얻은 골드가 저장 파일에 없다");
            Assert.AreEqual(ownedFirst - 1, OwnedInSave(first), "분해한 카드의 컬렉션 장수");
            Assert.AreEqual(ownedFirst - 1 > Keep, Find<VisualElement>("card-disassemble-" + first) != null,
                "넘는 분이 남은 카드만 격자에 남아야 한다");
            // 키보드: 첫 키는 첫 카드를 가리키고, 결정 키는 1장 분해
            string second = Named("card-disassemble-")[0].name.Substring("card-disassemble-".Length);
            int ownedSecond = ShownCount("card-disassemble-" + second);
            yield return Tap(Key.RightArrow);
            Assert.IsNotNull(Find<Button>("disassemble-one"), "이동 키로 카드를 가리켰는데 분해 버튼이 없다");
            int secondGold = PlusGold(Find<Button>("disassemble-one").text);
            yield return Tap(Key.Z);
            Assert.AreEqual(firstGold + secondGold, int.Parse(Text("disassemble-gold")), "결정 키로 1장 분해한 뒤 가진 골드");
            Assert.AreEqual(ownedSecond - 1, OwnedInSave(second), "결정 키로 분해한 카드의 컬렉션 장수");
            // 전부 분해: 처음에는 "그만둔다"가 골라져 있어 결정 키로는 분해되지 않는다
            int rest = allGold - firstGold - secondGold;
            var all = Find<Button>("disassemble-all");
            StringAssert.Contains($"골드 +{rest}", all.text, "전부 분해 버튼의 골드");
            Click(all);
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("gold-disassemble-confirm"), "전부 분해 확인 창이 뜨지 않았다");
            Assert.AreEqual($"받을 골드 +{rest}", Text("disassemble-all-gold"));
            yield return Shot("gold-3-disassemble-all");
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<VisualElement>("gold-disassemble"), "확인 창에서 결정 키(그만둔다)를 눌렀는데 분해 창으로 돌아오지 않았다");
            Assert.AreEqual(firstGold + secondGold, SavedGold(), "그만뒀는데 골드가 바뀌었다");
            Click(Find<Button>("disassemble-all"));
            yield return null;
            Click(Find<Button>("disassemble-all-confirm"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("gold-shop"), "전부 분해한 뒤 골드 상점으로 돌아오지 않았다");
            StringAssert.Contains("분해했다", Text("gold-banner-text") ?? "", "전부 분해한 뒤 띠 알림이 없다");
            Assert.AreEqual(allGold, ShopGold(), "전부 분해한 뒤 가진 골드");
            Assert.AreEqual(allGold, SavedGold(), "전부 분해한 골드가 저장 파일에 없다");
            Assert.IsTrue(SavedCollection().Values.All(n => n <= Keep), "전부 분해했는데 3장을 넘는 카드가 남아 있다");
            Assert.IsFalse(Find<Button>("gold-shop-disassemble").enabledSelf, "넘는 카드가 없는데 분해 줄이 켜져 있다");
            yield return Shot("gold-4-shop-after-disassemble");
            Debug.Log($"[골드 상점 시험] 분해: 카드 {cards.Count}종, 골드 +{allGold}");
            Click(Find<Button>("gold-shop-close"));
            yield return null;
            Assert.AreEqual($"골드 {allGold}", Text("title-gold"), "상점을 닫은 뒤 메뉴의 골드");

            // ---- 3. 저장 파일을 고쳐 골드 130, 유희 편 레시피의 시작 사이드 덱 5장 ----
            string text = File.ReadAllText(SaveFile).Replace("\r\n", "\n");
            var yugi = RecipeCodes(text, "recipe-deck", "Yugi");
            var side = RecipeCodes(text, "recipe-deck", "Kaiba").Distinct().Where(c => !yugi.Contains(c)).Take(5).ToList();
            Assert.AreEqual(5, side.Count, "카이바 편에만 있는 카드가 5종보다 적다");
            text = new Regex(@"\ngold \d+\n").Replace(text, "\ngold 130\n", 1);
            text = new Regex(@"\nrecipe-side Yugi[^\n]*\n").Replace(text, $"\nrecipe-side Yugi {string.Join(" ", side)}\n", 1);
            File.WriteAllText(SaveFile, text);
            yield return LoadTitle();
            Assert.AreEqual("골드 130", Text("title-gold"), "고친 저장 파일의 골드");

            // 새 런 창: 임시 칸의 카드 2장, 살 수 있는 만큼(2장)으로 시작
            yield return OpenNewRun("Yugi");
            Assert.IsNotNull(Find<VisualElement>("run-side-slots"), "임시 칸의 카드가 있는데 임시 확장 칸이 없다");
            Assert.AreEqual("2장", Text("run-side-count"));
            Assert.AreEqual("골드 16", Text("run-side-price"));
            Click(Find<Button>("start-tab-side"));
            yield return null;
            Assert.IsNull(SlotTag(side[2]), "영구 칸의 카드에 표시가 붙었다");
            Assert.AreEqual("임시", SlotTag(side[3]));
            Assert.AreEqual("임시", SlotTag(side[4]));
            yield return Shot("gold-5-new-run");
            Click(Find<Button>("run-side-less"));
            yield return null;
            Assert.AreEqual("1장", Text("run-side-count"));
            Assert.AreEqual("골드 8", Text("run-side-price"));
            Assert.AreEqual("임시", SlotTag(side[3]));
            Assert.AreEqual("두고 감", SlotTag(side[4]), "사지 않은 임시 칸의 카드");
            yield return Tap(Key.RightArrow);
            Assert.AreEqual("2장", Text("run-side-count"), "오른쪽 키로 임시 칸 늘리기");
            Assert.IsFalse(Find<Button>("run-side-more").enabledSelf, "임시 칸의 카드보다 많이 살 수 있게 켜져 있다");
            yield return Tap(Key.LeftArrow);
            yield return Tap(Key.LeftArrow);
            Assert.AreEqual("0장", Text("run-side-count"), "왼쪽 키로 임시 칸 줄이기");
            Assert.IsFalse(Find<Button>("run-side-less").enabledSelf);
            Assert.AreEqual("두고 감", SlotTag(side[3]));
            Click(Find<Button>("start-back"));
            yield return null;

            // ---- 4. 사이드 덱 확장 ----
            yield return OpenShop();
            var sideRow = Find<Button>("gold-shop-side");
            Assert.IsTrue(sideRow.enabledSelf, "골드 130인데 사이드 덱 확장 줄이 꺼져 있다");
            Assert.IsTrue(sideRow.ClassListContains("selected"), "켜진 첫 줄(사이드 덱 확장)을 가리키고 있지 않다");
            StringAssert.Contains("4장째", Text("gold-shop-side-tag"));
            yield return Shot("gold-6-shop");
            Click(sideRow);
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("gold-shop-confirm"), "구매 확인 창이 뜨지 않았다");
            Assert.AreEqual("골드 50", Text("gold-confirm-price"));
            Assert.AreEqual("가진 골드 130 → 80", Text("gold-confirm-after"));
            Assert.AreEqual(1, Find<VisualElement>("gold-confirm-slots").Query("slot-next").ToList().Count, "확인 창에 새로 생길 칸이 없다");
            yield return Shot("gold-7-confirm");
            yield return Tap(Key.X);
            Assert.IsNotNull(Find<VisualElement>("gold-shop"), "취소 키로 골드 상점에 돌아오지 않았다");
            Assert.AreEqual(130, ShopGold(), "취소했는데 골드가 줄었다");
            yield return Tap(Key.Z); // 가리키는 줄(사이드 덱 확장)을 연다
            Assert.IsNotNull(Find<VisualElement>("gold-shop-confirm"), "결정 키로 구매 확인 창이 뜨지 않았다");
            yield return Tap(Key.Z); // 산다 (처음에 골라져 있다)
            Assert.IsNotNull(Find<VisualElement>("gold-shop"), "산 뒤 골드 상점으로 돌아오지 않았다");
            Assert.AreEqual("사이드 덱이 4장으로 늘었다.", Text("gold-banner-text"), "산 뒤 띠 알림");
            Assert.AreEqual(80, ShopGold(), "산 뒤 가진 골드");
            Assert.AreEqual(80, SavedGold());
            Assert.AreEqual(4, SavedNumber("side-limit"), "늘린 한도가 저장 파일에 없다");
            Assert.AreEqual(4, Find<VisualElement>("gold-shop-side-slots").Query("slot-owned").ToList().Count, "산 뒤 가진 칸");
            Assert.AreEqual("골드 100", Text("gold-shop-side-price"), "다음 확장의 값");
            Assert.IsFalse(Find<Button>("gold-shop-side").enabledSelf, "골드 80으로 100짜리 확장이 켜져 있다");
            StringAssert.Contains("20 모자라다", Text("gold-shop-side-note"));
            yield return Shot("gold-8-bought");
            Click(Find<Button>("gold-shop-close"));
            yield return null;
            Assert.AreEqual("골드 80", Text("title-gold"));

            // ---- 5. 다시 켜도 그대로, 덱 편집의 칸, 임시 칸을 사서 런 시작 ----
            yield return LoadTitle();
            Assert.AreEqual("골드 80", Text("title-gold"), "다시 켠 뒤의 골드");
            yield return OpenNewRun("Yugi");
            Assert.AreEqual("1장", Text("run-side-count"), "한도가 4장이 되면 임시 칸의 카드는 1장");
            Click(Find<Button>("edit-recipe"));
            yield return WaitFor(() => Find<VisualElement>("editor-bar") != null, 10, "덱 편집 화면");
            Assert.AreEqual("시작 사이드 덱 4/4장", Text("editor-side-count"));
            Assert.AreEqual("임시 칸 1/5장", Text("editor-side-temp-count"));
            Assert.AreEqual(0, Named("editor-side-empty").Count(e => e.name == "editor-side-empty"), "영구 칸이 다 찼는데 빈 칸이 있다");
            Assert.AreEqual(4, Named("editor-side-temp-empty").Count, "빈 임시 칸");
            Assert.AreEqual("임시", SlotTag(side[4]));
            Assert.AreEqual(side, SideOrder(), "덱 편집의 시작 사이드 덱 순서");
            yield return Shot("gold-9-editor");
            Click(Find<VisualElement>("deck-side-" + side[4]));
            yield return null;
            Click(Find<Button>("editor-side-front"));
            yield return null;
            Assert.AreEqual(new[] { side[4], side[0], side[1], side[2], side[3] }, SideOrder().ToArray(), "임시 칸의 카드를 영구 칸으로 옮긴 뒤 순서");
            Assert.AreEqual("임시", SlotTag(side[3]), "밀려난 카드가 임시 칸에 없다");
            Click(Find<Button>("editor-back"));
            yield return null;
            Click(Find<Button>("editor-leave")); // 저장하지 않고 나간다
            yield return WaitFor(() => Find<Button>("start-run") != null, 10, "새 런 창");
            Assert.AreEqual("1장", Text("run-side-count"));
            Click(Find<Button>("start-run"));
            yield return WaitFor(() => Find<VisualElement>("run-status") != null, 20, "층 사이 화면");
            Assert.AreEqual(80 - SlotPrice, SavedGold(), "임시 칸 1장의 값을 낸 뒤 골드");
            var runSide = Regex.Match(File.ReadAllText(SaveFile).Replace("\r\n", "\n"), @"\nrun\n(?:[^\n]*\n)*?side( [^\n]*)?\n").Groups[1].Value
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(side, runSide.ToList(), "런의 사이드 덱: 영구 칸 4장 + 산 임시 칸 1장");
            Debug.Log("[골드 상점 시험] 통과: 분해, 사이드 덱 확장, 임시 칸, 저장");
        }

        // ================= 도우미 =================

        IEnumerator LoadTitle()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>("title-new-run") != null, LoadTimeout, "처음 화면");
            Assert.IsNotNull(Find<Button>("open-gold-shop"), "처음 화면의 메뉴에 골드 상점이 없다");
        }

        static IEnumerator OpenShop()
        {
            Click(Find<Button>("open-gold-shop"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("gold-shop"), "골드 상점 창이 열리지 않았다");
        }

        static IEnumerator OpenNewRun(string recipe)
        {
            Click(Find<Button>("title-new-run"));
            yield return null;
            Assert.IsNotNull(Find<Button>("start-run"), "새 런 창이 열리지 않았다");
            Click(Find<Button>("recipe-" + recipe));
            yield return null;
        }

        static string Text(string name) => Find<Label>(name)?.text;

        static int ShopGold() => int.Parse(Text("gold-shop-gold"));

        /// <summary>"… 골드 +12 …"의 12.</summary>
        static int PlusGold(string text)
        {
            var match = Regex.Match(text ?? "", @"골드 \+(\d+)");
            Assert.IsTrue(match.Success, $"골드 +N을 찾을 수 없다: {text}");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>카드 칸(deck-side-코드)의 그림 칸 왼쪽 아래 표시 ("임시", "두고 감"). 없으면 null.</summary>
        static string SlotTag(string code)
        {
            var slot = Find<VisualElement>("deck-side-" + code);
            Assert.IsNotNull(slot, $"시작 사이드 덱에 카드 {code}가 없다");
            return slot.Query<Label>().ToList().Select(l => l.text).FirstOrDefault(t => t == "임시" || t == "두고 감" || t == "부족");
        }

        /// <summary>화면에 놓인 시작 사이드 덱 카드의 순서.</summary>
        static List<string> SideOrder() => Named("deck-side-").Select(e => e.name.Substring("deck-side-".Length)).ToList();

        string SaveText() => File.ReadAllText(SaveFile).Replace("\r\n", "\n");

        int SavedGold() => SavedNumber("gold");

        /// <summary>저장 파일의 첫 "key 숫자" 줄 (프로필의 gold, side-limit).</summary>
        int SavedNumber(string key)
        {
            var match = Regex.Match(SaveText(), $@"\n{Regex.Escape(key)} (-?\d+)\n");
            Assert.IsTrue(match.Success, $"저장 파일에 {key} 줄이 없다");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>저장 파일의 컬렉션: 카드 코드 → 장수.</summary>
        Dictionary<string, int> SavedCollection() =>
            Regex.Matches(SaveText(), @"\ncard (\d+) (\d+)(?=\n)").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));

        /// <summary>저장 파일의 컬렉션에서 이 카드의 장수.</summary>
        int OwnedInSave(string code) => SavedCollection().TryGetValue(code, out int n) ? n : 0;

        /// <summary>카드 칸의 그림 칸 오른쪽 아래에 보이는 장수 ("×4"의 4). 표시가 없으면 1.</summary>
        static int ShownCount(string slotName)
        {
            var slot = Find<VisualElement>(slotName);
            Assert.IsNotNull(slot, $"카드 칸 {slotName}이(가) 없다");
            string text = slot.Query<Label>().ToList().Select(l => l.text).FirstOrDefault(t => t != null && t.StartsWith("×"));
            return text == null ? 1 : int.Parse(text.Substring(1));
        }

        /// <summary>저장 파일의 "key Id 카드 …" 줄의 카드 코드들 (recipe-deck, recipe-side).</summary>
        static List<string> RecipeCodes(string text, string key, string id)
        {
            var match = Regex.Match(text, $@"\n{Regex.Escape(key)} {Regex.Escape(id)}( [^\n]*)?\n");
            Assert.IsTrue(match.Success, $"저장 파일에 {key} {id} 줄이 없다");
            return match.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

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

        /// <summary>런 화면에서 이름이 prefix로 시작하는 요소들 (화면 순서).</summary>
        static List<VisualElement> Named(string prefix) =>
            Root()?.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(prefix)).ToList() ?? new List<VisualElement>();

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

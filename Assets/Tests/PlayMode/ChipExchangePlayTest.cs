// 플레이 모드 시험 (짧은 전용 시험): 칩교환소 장면. docs/UI.md "상점 > 칩교환소".
// 유희 편 레시피로 런을 시작하고, 저장 파일을 고쳐 지금 층을 칩교환소로 만든 뒤 이어하기로 올라선다. 듀얼은 하지 않는다.
// 1. Counter: 칩 400, 봉인 3장. 올라서면 장면이 저절로 펴진다 (계산대의 봉인된 카드 3장과 아이템 카드, 아래 판의 방법 다섯 가지).
//    키보드로 카드 → 방법 → 무작위 교체를 사고 결과(풀린 카드, 대신 봉인된 카드)를 본다. 마우스로 직접 고르는 교체(대신 봉인할 카드 고르기 창,
//    그만두기와 치르기), 봉인 해제, 아이템 카드를 산다. 덱 보기로 덱 창에 다녀오고, 나갔다가 메뉴로 다시 들어오고, 올라간다.
// 2. Poor: 칩 45. 살 수 없는 방법은 꺼지고 까닭이 보인다 (칩이 얼마 모자란지, 이번 칩교환소에 없는 봉인 해제).
//    봉인된 카드가 없으면 아이템 카드만 가리킨다. 키보드로 아래 버튼 줄까지 내려가 올라간다.
// 소리는 끈다 (AudioListener.volume = 0). 저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1~2분)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.ChipExchangePlayTest -testResults C:\Dev\chipexchange-results.xml -logFile C:\Dev\chipexchange.log
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
    public sealed class ChipExchangePlayTest
    {
        const string RunUi = "RunUI";
        const string SaveDirVariable = "DUELROGUELIKE_SAVE_DIR";
        const float LoadTimeout = 120;
        const string Telescope = "17092736"; // 고대의 망원경
        static readonly string[] Methods = { "SwapSameRarity", "SwapSameRarityChosen", "SwapAny", "SwapAnyChosen", "Unseal" };

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        float _listenerVolume = 1f;

        string SaveFile => Path.Combine(_saveDir, "save.txt");

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "DuelRoguelike-chipexchange-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(SaveDirVariable, _saveDir);
            TestSaves.WriteStarterSave(_saveDir);
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
        public IEnumerator Counter()
        {
            yield return StartRun();
            yield return ToChipExchange(400, sealedCards: 3, unseal: true);

            // ---- 올라서면 장면이 저절로 펴진다 ----
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "칩교환소 층에 올라섰는데 장면이 펴지지 않았다");
            Assert.IsNotNull(Find<VisualElement>("run-status"), "칩교환소에서 위 막대가 보이지 않는다");
            Assert.IsNull(Find<VisualElement>("hub-menu"), "칩교환소 장면 뒤에 허브의 명령 메뉴가 남아 있다");
            Assert.AreEqual("400", Text("shop-chips"), "가진 칩");
            Assert.AreEqual("봉인된 카드 3장", Text("shop-sealed-sign-text"), "계산대의 팻말");
            var cards = Named("card-shoprelease-");
            Assert.AreEqual(3, cards.Count, "계산대의 봉인된 카드");
            Assert.IsTrue(cards[0].ClassListContains("selected"), "처음에는 첫 카드를 가리켜야 한다");
            Assert.IsNotNull(Find<Button>("shop-item"), "계산대에 아이템 카드가 없다");
            foreach (string method in Methods)
                Assert.IsTrue(Find<Button>("shop-buy-" + method)?.enabledSelf == true, $"칩 400인데 {method}를 살 수 없다");
            Assert.AreEqual("30", Text("shop-price-SwapSameRarity"), "같은 레어도·무작위 교체의 값");
            Assert.AreEqual("150", Text("shop-price-Unseal"), "봉인 해제의 값");
            yield return Shot("shop-1-counter");

            // ---- 키보드: 좌우로 카드, 결정 키로 방법 줄, 좌우로 방법 ----
            yield return Tap(Key.RightArrow);
            Assert.IsTrue(Named("card-shoprelease-")[1].ClassListContains("selected"), "오른쪽 키로 다음 카드를 가리키지 않았다");
            yield return Tap(Key.LeftArrow);
            Assert.IsTrue(Named("card-shoprelease-")[0].ClassListContains("selected"), "왼쪽 키로 앞 카드를 가리키지 않았다");
            Assert.AreEqual("", Text("shop-after"), "방법을 고르기 전인데 사면 남는 칩이 보인다");
            yield return Tap(Key.Z);
            Assert.IsTrue(Find<Button>("shop-buy-SwapSameRarity").ClassListContains("selected"), "결정 키로 방법 줄에 내려가지 않았다");
            Assert.AreEqual("사면 370", Text("shop-after"), "방법을 가리켰을 때 사면 남는 칩");
            yield return Shot("shop-2-methods");
            yield return Tap(Key.RightArrow);
            Assert.IsTrue(Find<Button>("shop-buy-SwapSameRarityChosen").ClassListContains("selected"), "오른쪽 키로 다음 방법을 가리키지 않았다");
            Assert.AreEqual("사면 350", Text("shop-after"));
            yield return Tap(Key.LeftArrow);

            // ---- 무작위 교체: 결정 키로 바로 산다. 결과가 아래 판에 남는다 ----
            int side = RunCodes("side").Length;
            string freed = Named("card-shoprelease-")[0].name.Substring("card-shoprelease-".Length);
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<VisualElement>("shop-result"), "산 뒤 결과가 보이지 않는다");
            StringAssert.Contains("봉인을 풀었다", Text("shop-result-text"));
            Assert.IsNotNull(Find<Label>("shop-result-seal"), "무작위 교체인데 대신 봉인된 카드가 결과에 없다");
            StringAssert.Contains("가진 칩 400 → 370", Text("shop-result-paid"));
            Assert.AreEqual("370", Text("shop-chips"), "같은 레어도 교체(30) 뒤 칩");
            Assert.AreEqual("370", Text("stat-chips"), "위 막대의 칩");
            Assert.AreEqual("3", Text("stat-sealed"), "교체 뒤 봉인 장수가 바뀌었다");
            Assert.AreEqual(side + 1, RunCodes("side").Length, "풀린 카드가 사이드 덱에 더해지지 않았다");
            Assert.Contains(freed, RunCodes("side"), "풀린 카드가 사이드 덱에 없다");
            StringAssert.Contains("SwapSameRarity:30:1", SaveText(), "산 상품이 저장되지 않았다");
            Assert.IsTrue(Named("card-shoprelease-").Any(c => c.ClassListContains("selected")), "산 뒤 가리키는 카드가 없다");
            yield return Shot("shop-3-result");
            yield return Tap(Key.Z); // 계속
            Assert.IsNull(Find<VisualElement>("shop-result"), "결정 키로 결과가 접히지 않았다");
            Assert.AreEqual("샀음", Text("shop-price-SwapSameRarity"), "산 방법의 값 자리");
            Assert.IsFalse(Find<Button>("shop-buy-SwapSameRarity").enabledSelf, "산 방법을 또 살 수 있다");

            // ---- 직접 고르는 교체 (마우스): 대신 봉인할 카드를 고르는 창이 장면 위에 뜬다 ----
            Click(Named("card-shoprelease-")[0]);
            yield return null;
            Click(Find<Button>("shop-buy-SwapAnyChosen"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("shop-pick"), "대신 봉인할 카드를 고르는 창이 열리지 않았다");
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "고르기 창 뒤에 칩교환소 장면이 없다");
            Assert.IsNotNull(Find<VisualElement>("shop-pick-release"), "고르기 창에 풀기로 한 카드가 없다");
            Assert.AreEqual("80", Text("shop-pick-price"), "고르기 창의 값");
            var seal = Named("card-shopseal-");
            Assert.Greater(seal.Count, 0, "대신 봉인할 카드 후보가 없다");
            yield return Shot("shop-4-pick");
            Click(seal[0]);
            yield return null;
            Assert.IsNotNull(Find<Button>("shop-confirm"), "카드를 골랐는데 실행 버튼이 없다");
            yield return Shot("shop-4b-pick-chosen");
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("shop-pick"), "취소 키로 고르기 창이 닫히지 않았다");
            Assert.AreEqual("370", Text("shop-chips"), "그만뒀는데 칩이 줄었다");
            Click(Find<Button>("shop-buy-SwapAnyChosen"));
            yield return null;
            seal = Named("card-shopseal-");
            string chosen = seal[seal.Count - 1].name.Substring("card-shopseal-".Length);
            Click(seal[seal.Count - 1]);
            yield return null;
            Click(Find<Button>("shop-confirm"));
            yield return null;
            Assert.IsNull(Find<VisualElement>("shop-pick"), "치른 뒤 고르기 창이 닫히지 않았다");
            Assert.IsNotNull(Find<VisualElement>("shop-result"), "직접 고르는 교체의 결과가 보이지 않는다");
            Assert.AreEqual("290", Text("shop-chips"), "직접 고르는 교체(80) 뒤 칩");
            Assert.Contains(chosen, RunCodes("sealed-main"), "고른 카드가 봉인되지 않았다");
            Assert.IsNotNull(Find<VisualElement>("card-shoprelease-" + chosen), "새로 봉인된 카드가 계산대에 없다");

            // ---- 봉인 해제: 대신 봉인되는 카드가 없다 ----
            yield return Tap(Key.Z);
            Click(Named("card-shoprelease-")[0]);
            yield return null;
            Click(Find<Button>("shop-buy-Unseal"));
            yield return null;
            Assert.AreEqual("140", Text("shop-chips"), "봉인 해제(150) 뒤 칩");
            Assert.AreEqual("2", Text("stat-sealed"), "봉인 해제 뒤 봉인 장수");
            Assert.IsNull(Find<Label>("shop-result-seal"), "봉인 해제인데 대신 봉인된 카드가 결과에 있다");
            Assert.AreEqual("봉인된 카드 2장", Text("shop-sealed-sign-text"));
            yield return Shot("shop-5-unseal-result");

            // ---- 아이템 카드 ----
            Click(Find<Button>("shop-item"));
            yield return null;
            Assert.IsTrue(Find<Button>("shop-item").ClassListContains("selected"), "아이템 카드를 눌렀는데 가리키지 않는다");
            StringAssert.Contains("탑 미리보기", Text("shop-item-effect"), "아이템 카드의 탑 효과");
            Assert.IsTrue(Find<Button>("shop-buy-ItemCard").enabledSelf, "칩 140인데 아이템 카드(60)를 살 수 없다");
            yield return Shot("shop-6-item");
            Click(Find<Button>("shop-buy-ItemCard"));
            yield return null;
            Assert.AreEqual("80", Text("shop-chips"), "아이템 카드(60) 뒤 칩");
            StringAssert.Contains("샀다", Text("shop-result-text"));
            StringAssert.Contains("\nshop-deck " + Telescope + "\n", SaveText(), "상점 덱이 저장되지 않았다");
            yield return Shot("shop-6b-item-bought");
            yield return Tap(Key.Z);
            Click(Find<Button>("shop-item"));
            yield return null;
            Assert.IsFalse(Find<Button>("shop-buy-ItemCard").enabledSelf, "산 아이템 카드를 또 살 수 있다");
            Assert.AreEqual("샀음", Text("shop-price-ItemCard"));

            // ---- 덱 보기: 덱 창을 닫으면 칩교환소로 돌아온다 ----
            Click(Find<Button>("shop-deck"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("run-decks"), "덱 보기로 덱 창이 열리지 않았다");
            Assert.IsNull(Find<VisualElement>("chip-exchange"), "덱 창을 보는 중인데 칩교환소 장면이 그려져 있다");
            StringAssert.Contains("칩교환소", Text("deck-from-duel") ?? "", "덱 창에 돌아갈 곳이 쓰여 있지 않다");
            Click(Find<Button>("close-deck"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "덱 창을 닫았는데 칩교환소로 돌아오지 않았다");

            // ---- 나갔다가 메뉴로 다시 들어온다 ----
            yield return Tap(Key.X);
            Assert.IsNull(Find<VisualElement>("chip-exchange"), "취소 키로 칩교환소에서 나가지 않았다");
            Assert.IsNotNull(Find<VisualElement>("hub-menu"), "나온 뒤 허브의 명령 메뉴가 없다");
            Click(Find<Button>("open-shop"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "메뉴의 칩교환소로 다시 들어가지 못했다");
            Click(Find<Button>("shop-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>("chip-exchange"), "나간다 버튼으로 나가지 않았다");
            yield return Shot("shop-7-hub");
            Click(Find<Button>("open-shop"));
            yield return null;

            // ---- 올라간다 ----
            Click(Find<Button>("shop-leave"));
            yield return null;
            Assert.IsNotNull(Find<Button>("climb-skip"), "올라간다를 눌렀는데 올라가는 연출이 시작되지 않았다");
            Assert.IsNull(Find<VisualElement>("chip-exchange"), "올라가는 중인데 칩교환소 장면이 남아 있다");
            Click(Find<Button>("climb-skip"));
            yield return null;
            Assert.AreEqual("6층", Text("stand-floor"), "칩교환소 다음 층에 서 있지 않다");
            Debug.Log("[칩교환소 시험] 통과: 무작위 교체, 직접 고르는 교체, 봉인 해제, 아이템 카드, 덱 보기, 나가기·다시 들어오기, 올라가기");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Poor()
        {
            yield return StartRun();

            // ---- 칩 45, 봉인 3장, 봉인 해제는 이번에 진열되지 않았다 ----
            yield return ToChipExchange(45, sealedCards: 3, unseal: false);
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "칩교환소 장면이 펴지지 않았다");
            Assert.IsTrue(Find<Button>("shop-buy-SwapSameRarity").enabledSelf, "칩 45인데 30짜리 교체를 살 수 없다");
            Assert.IsFalse(Find<Button>("shop-buy-SwapSameRarityChosen").enabledSelf, "칩 45인데 50짜리 교체가 켜져 있다");
            Assert.AreEqual("칩 5 모자람", Text("shop-reason-SwapSameRarityChosen"), "칩이 모자란 까닭");
            Assert.AreEqual("칩 35 모자람", Text("shop-reason-SwapAnyChosen"));
            Assert.IsFalse(Find<Button>("shop-buy-Unseal").enabledSelf, "진열되지 않은 봉인 해제가 켜져 있다");
            Assert.AreEqual("이번 칩교환소에는 없다", Text("shop-reason-Unseal"), "진열되지 않은 봉인 해제의 까닭");
            yield return Tap(Key.Z);
            Assert.IsTrue(Find<Button>("shop-buy-SwapSameRarity").ClassListContains("selected"));
            yield return Tap(Key.RightArrow); // 살 수 있는 방법이 하나뿐이라 그대로다
            Assert.IsTrue(Find<Button>("shop-buy-SwapSameRarity").ClassListContains("selected"), "꺼진 방법으로 넘어갔다");
            yield return Shot("shop-8-poor");
            // 아이템 카드도 칩이 모자라다
            Click(Find<Button>("shop-item"));
            yield return null;
            Assert.IsFalse(Find<Button>("shop-buy-ItemCard").enabledSelf, "칩 45인데 아이템 카드(60)가 켜져 있다");
            Assert.AreEqual("칩 15 모자람", Text("shop-reason-ItemCard"));
            yield return Shot("shop-8b-poor-item");

            // ---- 봉인된 카드가 없으면 아이템 카드만 가리킨다 ----
            yield return ToChipExchange(45, sealedCards: 0, unseal: true);
            Assert.IsNotNull(Find<VisualElement>("chip-exchange"), "칩교환소 장면이 펴지지 않았다");
            Assert.AreEqual(0, Named("card-shoprelease-").Count, "봉인된 카드가 없는데 계산대에 카드가 있다");
            Assert.IsNotNull(Find<Label>("shop-no-sealed"), "봉인된 카드가 없다는 글이 없다");
            Assert.AreEqual("봉인된 카드 0장", Text("shop-sealed-sign-text"));
            Assert.IsTrue(Find<Button>("shop-item").ClassListContains("selected"), "봉인된 카드가 없으면 아이템 카드를 가리켜야 한다");
            yield return Shot("shop-9-no-sealed");

            // ---- 키보드로 아래 버튼 줄까지: 살 것이 없으면 아래 키가 바로 버튼 줄로 간다 ----
            yield return Tap(Key.DownArrow);
            Assert.IsTrue(Find<Button>("shop-leave").parent.ClassListContains("selected"), "아래 키로 버튼 줄(올라간다)에 가지 않았다");
            yield return Tap(Key.LeftArrow);
            Assert.IsTrue(Find<Button>("shop-deck").parent.ClassListContains("selected"), "왼쪽 키로 덱 보기를 가리키지 않았다");
            yield return Tap(Key.RightArrow);
            yield return Shot("shop-9b-buttons");
            yield return Tap(Key.Z);
            Assert.IsNotNull(Find<Button>("climb-skip"), "버튼 줄에서 결정 키로 올라가지 않았다");
            Click(Find<Button>("climb-skip"));
            yield return null;
            Assert.AreEqual("6층", Text("stand-floor"));
            Debug.Log("[칩교환소 시험] 통과: 칩이 모자란 방법, 진열되지 않은 봉인 해제, 봉인된 카드 없음, 아래 버튼 줄");
        }

        // ================= 도우미 =================

        IEnumerator LoadTitle()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>("title-new-run") != null, LoadTimeout, "처음 화면");
        }

        IEnumerator StartRun()
        {
            yield return LoadTitle();
            Click(Find<Button>("title-new-run"));
            yield return WaitFor(() => Find<Button>("start-run") != null, 10, "새 런 창");
            Click(Find<Button>("recipe-Yugi"));
            yield return null;
            Click(Find<Button>("start-run"));
            yield return WaitFor(IsHub, 20, "층 사이 화면");
        }

        /// <summary>
        /// 처음 화면으로 나가 저장 파일의 런을 5층 칩교환소로 고치고 (칩, 봉인된 카드 장수, 봉인 해제를 진열할지), 이어하기로 올라선다.
        /// 봉인된 카드는 메인 덱 앞에서 옮긴다 (이미 봉인된 카드는 메인 덱으로 되돌린 뒤 다시 센다).
        /// </summary>
        IEnumerator ToChipExchange(int chips, int sealedCards, bool unseal)
        {
            if (Find<VisualElement>("chip-exchange") != null)
            {
                // 칩교환소 장면에서는 허브의 메뉴가 없다: 먼저 나간다
                Click(Find<Button>("shop-close"));
                yield return null;
            }
            Click(Find<Button>("to-title"));
            yield return null;
            string text = SaveText();
            text = SetLine(text, "floor", "5");
            text = SetLine(text, "floor-kind", "ChipExchange");
            text = Regex.Replace(text, @"\n(floor-event|shop) [^\n]*", "");
            string stock = "shop SwapSameRarity:30:0 SwapSameRarityChosen:50:0 SwapAny:50:0 SwapAnyChosen:80:0" + (unseal ? " Unseal:150:0" : "") + " ItemCard:60:0:" + Telescope;
            text = SetLine(text, "chips", $"{chips} 400\n{stock}");
            var main = (Regex.Match(text, @"\nsealed-main( [^\n]*)?\n").Groups[1].Value + " " + Regex.Match(text, @"\nmain ([^\n]*)\n").Groups[1].Value)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            text = SetLine(text, "main", string.Join(" ", main.Skip(sealedCards)));
            text = new Regex(@"\nsealed-main( [^\n]*)?\n").Replace(text, _ => "\nsealed-main" + string.Concat(main.Take(sealedCards).Select(c => " " + c)) + "\n", 1);
            File.WriteAllText(SaveFile, text);
            yield return LoadTitle();
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
        }

        static string SetLine(string text, string key, string value)
        {
            var pattern = new Regex($@"\n{Regex.Escape(key)}( [^\n]*)?\n");
            Assert.IsTrue(pattern.IsMatch(text), $"저장 파일에 {key} 줄이 없다");
            return pattern.Replace(text, _ => $"\n{key} {value}\n", 1);
        }

        static bool IsHub() => Find<VisualElement>("run-status") != null;

        static string Text(string name) => Find<Label>(name)?.text;

        string SaveText() => File.ReadAllText(SaveFile).Replace("\r\n", "\n");

        /// <summary>저장 파일의 진행 중인 런에서 카드 줄 (main, side, sealed-main)의 카드 코드들. 비어 있으면 빈 배열.</summary>
        string[] RunCodes(string key)
        {
            var run = Regex.Match(SaveText(), @"\nrun\n(.*?)\nend-run", RegexOptions.Singleline);
            Assert.IsTrue(run.Success, "저장 파일에 진행 중인 런이 없다");
            var line = run.Groups[1].Value.Split('\n').FirstOrDefault(l => l == key || l.StartsWith(key + " "));
            return line == null ? new string[0] : line.Substring(key.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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

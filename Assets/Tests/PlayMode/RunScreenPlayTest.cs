// 플레이 모드 시험: Run 씬을 열고 화면의 버튼만 눌러 로그라이크 런을 처음부터 끝까지 간다.
// 덱 편집(레시피 고치기·저장·지우기) → 런 시작(레시피·시드) → [층 사이 화면(탑) → "N층으로 올라간다" → 듀얼 창의 "듀얼 시작" → 코인 토스 → 듀얼 화면 "자동으로 끝까지" → "결과 보기" → (승리 보상 고르기·보충 고르기)] 반복
// → 런 종료 → 새 런. 듀얼은 사람 대신 임시 자동 응답(개발용 버튼)이 답하고, 상대는 WindBot이다.
// 저장도 본다 (저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다):
// - "처음 화면으로" → "이어하기", 씬을 다시 불러(게임을 다시 켠 것처럼) "이어하기"를 해도 층 사이 화면과 저장 파일이 그대로인지
// - 듀얼을 시작하면 듀얼 중 상태로 저장되고, 듀얼 도중에 씬을 다시 부르면 그 듀얼이 패배로 처리되며 알림이 한 번 나오는지
// - 런이 끝나면 진행 중인 런이 없어지고, 마지막으로 시작한 레시피(시작 사이드 덱 포함)가 다음 런에 그대로 골라져 있는지
// - 레시피를 저장·지우면 저장 파일에 바로 쓰이는지, 층 사이 화면의 "지금 덱을 레시피로"가 새 레시피를 만드는지
// 덱 편집: 시작 사이드 덱 3장(꽉 참)까지 넣기·빼기(버튼·두 번 누르기·끌어다 놓기), 덱 ↔ 사이드 덱 옮기기, 같은 카드명 4장째 막기,
// 컬렉션보다 많이 넣으면 저장은 되고 런 시작은 막히는지, 메인 덱 39장은 저장할 수 없는지, 저장하지 않고 나가기 확인, 복사·지우기·빈 새 레시피.
// - 진행 중인 런이 있을 때 새 런: 한 번 더 눌러야 포기하고, 취소할 수 있는지
// 덱 창의 교환(사이드 덱 → 덱, 덱 → 사이드 덱)도 버튼·두 번 누르기·끌어다 놓기로 한 번씩 해 본다 (포인터 이벤트를 직접 보낸다).
// 허브: 첫 층은 올라가는 연출을 끝까지 보고(그 밖에는 눌러서 건너뛴다), 듀얼 창 → 덱 조정 → 듀얼 창, 가상 키보드로 메뉴·덱 창을 다뤄 본다.
// 런 로그 창 열고 닫기, 덱 편집 화면의 거르기, 꽉 찬 시작 사이드 덱(3장)에 더 넣을 때의 알림도 본다.
// 승리 보상 창은 자리 3개를 확인하고, 누르고 받기·두 번 누르기를 번갈아 하며, 빈 자리(골드)가 나오면 한 번 골드를 고른다.
// 보충 고르기를 한 번 이상 거칠 때까지 런을 더 한다. 비전투 층(칩교환소·휴식·이벤트)은 다음 층으로 지나가고, 보스 보상 창은 첫 카드를 고른다.
// 탑·칩교환소 시험(TowerAndChipExchange): 저장 파일을 고쳐 5층 칩교환소로 가서 교체·봉인 해제를 사고, 이벤트 창, 휴식 창(LP 3000 → 8000) → 보스, 보스 보상 창을 본다.
// 듀얼 화면은 한 번, 6턴쯤에 자동 진행을 멈추고 카드에 마우스 올리기(왼쪽 카드 정보)와 묘지 누르기(카드 목록 창)를 해 본다.
// 오류 로그(Debug.LogError·LogException)가 하나라도 나오면 실패한다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 화면마다 처음 한 번 1920×1080 PNG로 저장한다 (화면 모양 확인용).
// DUELROGUELIKE_SCREEN_THOROUGH=1이면 한 단계씩 보여 주기를 더 오래(더 많은 듀얼에서) 지켜보며 가끔만 생기는 장면(체인 2 이상, 상대 체인, 선택 창)도 기다린다.
//
// 실행: 에디터의 Test Runner 창(Window > General > Test Runner) PlayMode 탭, 또는 에디터를 닫고 배치 모드로
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testResults C:\Dev\playmode-results.xml -logFile C:\Dev\playmode.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        /// <summary>시작 사이드 덱 최대 장수 (RunRules.StartingSideDeckMax). 이만큼 골라 꽉 채운다.</summary>
        const int SidePicks = 3;
        /// <summary>승리 보상 창의 자리 수 (RunRules.RewardChoices).</summary>
        const int RewardChoices = 3;
        const int MaxExtraRuns = 3;
        const int MaxDuelsPerRun = 300;
        const float LoadTimeout = 120;
        const float DuelTimeout = 300;
        const float PlaybackTimeout = 100;

        readonly HashSet<string> _shots = new HashSet<string>();
        string _saveDir;
        int _refills;
        int _rewards;
        bool _goldChosen;
        int _duels;
        bool _exchanged;
        int _floorsPassed;
        int _unseals;
        int _coinWins, _coinLosses, _secondDuels;
        bool _coinFlipWatched;
        bool _duelViewChecked;
        int _playbackTries;
        bool _playbackDone;
        bool _previewSeen;
        bool _fieldViewSeen;
        bool _chainSeen;
        bool _speedSeen;
        bool _lpSeen;
        // 필드 보기는 선택 창(묘지·덱에서 고르기, 예/아니오 등)이 뜰 때만 있어서 기다리지 않는다. 뜨면 확인한다.
        bool PlaybackAllSeen => _previewSeen && _skipSeen && _chainSeen && _speedSeen && _lpSeen && _enterSeen;
        bool _enterSeen;
        // 체인 목록: 체인 2 이상, "처리 중", 상대가 발동한 줄 (생길 때만 본다)
        bool _chain2Seen, _chainSolvingSeen, _chainOpponentSeen;
        bool _skipSeen;
        static readonly bool Thorough = Environment.GetEnvironmentVariable("DUELROGUELIKE_SCREEN_THOROUGH") == "1";
        static readonly int MaxPlaybackTries = Thorough ? 8 : 3;
        bool RareSeen => _chain2Seen && _chainOpponentSeen && _fieldViewSeen;

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
            yield return CardGallery();

            // ---- 덱 레시피: 새 게임에는 스타터 덱 4개 ----
            foreach (string id in new[] { "Yugi", "Kaiba", "Joey", "Pegasus" })
                Assert.IsNotNull(Find<Button>(RunUi, "recipe-" + id), $"새 게임에 스타터 덱 레시피 {id}가 없다");
            Click(Find<Button>(RunUi, "recipe-Yugi"));
            yield return null;
            StringAssert.Contains("시작할 수 있다", Find<Label>(RunUi, "start-recipe-status").text, "스타터 덱 레시피로 런을 시작할 수 없다고 나온다");

            // ---- 덱 편집: 유희 편 레시피의 시작 사이드 덱 3장 (꽉 참), 빼기·두 번 누르기·끌어다 놓기 ----
            yield return OpenEditor("edit-recipe");
            Assert.AreEqual("스타터 덱 —유희 편—", Find<TextField>(RunUi, "editor-name").value, "덱 편집 화면의 레시피 이름");
            Assert.IsFalse(Find<Button>(RunUi, "editor-save").enabledSelf, "고치지 않았는데 저장 버튼이 켜져 있다");
            StringAssert.Contains("규칙에 맞습니다", EditorStatus(), "스타터 덱 레시피가 규칙에 맞지 않다고 나온다");
            var picked = new List<string>();
            for (int i = 0; i < SidePicks; i++)
                yield return PickToSide(picked);
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "editor-side-count").text);
            Assert.IsNull(Find<VisualElement>(RunUi, "editor-deck").Q<ScrollView>().Q("editor-side"), "시작 사이드 덱이 덱 스크롤 밖 아래에 고정돼 있지 않다");
            Click(Find<VisualElement>(RunUi, "deck-side-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "editor-remove"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks - 1), Find<Label>(RunUi, "editor-side-count").text, "시작 사이드 덱에서 빼기");
            Assert.IsNull(Find<VisualElement>(RunUi, "deck-side-" + picked[0]), "뺀 카드가 시작 사이드 덱에 남아 있다");
            Click(Find<VisualElement>(RunUi, "pick-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "editor-add-side"));
            yield return null;
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + code), $"고른 카드 {code}가 시작 사이드 덱에 없다");
            DoubleClick(Find<VisualElement>(RunUi, "deck-side-" + picked[0]));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks - 1), Find<Label>(RunUi, "editor-side-count").text, "시작 사이드 덱 카드를 두 번 눌러 빼기");
            Drag(Find<VisualElement>(RunUi, "pick-" + picked[0]), Find<VisualElement>(RunUi, "editor-side"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "editor-side-count").text, "컬렉션 카드를 시작 사이드 덱으로 끌어다 놓기");
            // 시작 사이드 덱 → 메인 덱 → 시작 사이드 덱 (끌어다 놓기)
            int mainBefore = EditorMainCount();
            Drag(Find<VisualElement>(RunUi, "deck-side-" + picked[0]), Find<VisualElement>(RunUi, "editor-main"));
            yield return null;
            Assert.AreEqual(mainBefore + 1, EditorMainCount(), "시작 사이드 덱 카드를 메인 덱으로 끌어다 놓기");
            Assert.AreEqual(SideCountText(SidePicks - 1), Find<Label>(RunUi, "editor-side-count").text, "메인 덱으로 옮긴 뒤 시작 사이드 덱 장수");
            Drag(Find<VisualElement>(RunUi, "deck-main-" + picked[0]), Find<VisualElement>(RunUi, "editor-side"));
            yield return null;
            Assert.AreEqual(mainBefore, EditorMainCount(), "메인 덱 카드를 시작 사이드 덱으로 끌어다 놓기");
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "editor-side-count").text, "시작 사이드 덱으로 되돌린 뒤 장수");
            // 꽉 찬 시작 사이드 덱에 하나 더 끌어다 놓기: 들어가지 않고 까닭 알림
            string extra = Named("pick-").Select(e => e.name.Substring("pick-".Length)).FirstOrDefault(c => !picked.Contains(c));
            Assert.IsNotNull(extra, "컬렉션 목록에 끌어다 놓을 카드가 없다");
            Drag(Find<VisualElement>(RunUi, "pick-" + extra), Find<VisualElement>(RunUi, "editor-side"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "editor-side-count").text, "꽉 찬 시작 사이드 덱에 더 들어갔다");
            Assert.IsNull(Find<VisualElement>(RunUi, "deck-side-" + extra), "꽉 찬 시작 사이드 덱에 끌어다 놓은 카드가 들어갔다");
            string full = EditorNotice();
            StringAssert.StartsWith("넣을 수 없습니다", full, "꽉 찬 시작 사이드 덱에 넣을 때 까닭 알림이 없다");
            StringAssert.Contains($"{SidePicks}장까지", full, "꽉 찬 시작 사이드 덱 알림에 장수 제한이 없다");
            Debug.Log($"[런 화면 시험] 꽉 찬 시작 사이드 덱: {full}");
            Click(Find<Button>(RunUi, "filter-spell"));
            yield return null;
            Assert.IsTrue(Named("pick-").Count > 0, "마법 거르기에 카드가 없다");
            Click(Find<Button>(RunUi, "filter-all"));
            yield return null;
            yield return CollectionSearchAndSort();
            Assert.AreEqual(DisplayStyle.Flex, Find<Label>(RunUi, "editor-dirty").style.display.value, "고쳤는데 저장하지 않은 변경 표시가 없다");
            yield return Shot(RunUi, "1a-editor");
            Click(Find<Button>(RunUi, "editor-save"));
            yield return null;
            StringAssert.Contains("저장했습니다", EditorNotice(), "저장 알림이 없다");
            Assert.AreEqual(DisplayStyle.None, Find<Label>(RunUi, "editor-dirty").style.display.value, "저장했는데 변경 표시가 남아 있다");
            string sideLine = File.ReadAllText(SaveFile).Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.StartsWith("recipe-side Yugi "));
            Assert.IsNotNull(sideLine, "저장 파일에 레시피의 시작 사이드 덱이 없다");
            CollectionAssert.AreEquivalent(picked, sideLine.Split(' ').Skip(2).ToList(), "저장 파일의 시작 사이드 덱이 고른 카드와 다르다");

            // ---- 같은 카드명 3장, 컬렉션보다 많이 넣기 (저장은 되고 런 시작은 안 됨) ----
            string once = Named("pick-").Where(e => !e.Query<Label>().ToList().Any(l => l.text.StartsWith("×")))
                .Select(e => e.name.Substring("pick-".Length))
                .FirstOrDefault(c => Find<VisualElement>(RunUi, "deck-main-" + c) == null && Find<VisualElement>(RunUi, "deck-extra-" + c) == null
                                     && !picked.Contains(c));
            Assert.IsNotNull(once, "컬렉션에 1장 있고 레시피에 없는 카드가 없다");
            for (int i = 0; i < 3; i++)
            {
                DoubleClick(Find<VisualElement>(RunUi, "pick-" + once));
                yield return null;
            }
            Assert.AreEqual(3, CountInDeck(once), "컬렉션 카드를 두 번 눌러 덱에 3장 넣기");
            StringAssert.Contains("런을 시작할 수 없음", EditorStatus(), "컬렉션보다 많이 넣었는데 시작 불가 알림이 없다");
            Assert.IsTrue(Find<VisualElement>(RunUi, "pick-" + once).Query<Label>().ToList().Any(l => l.text == "부족"), "컬렉션보다 많이 넣은 카드에 부족 표시가 없다");
            DoubleClick(Find<VisualElement>(RunUi, "pick-" + once));
            yield return null;
            Assert.AreEqual(3, CountInDeck(once), "같은 카드명 4장째가 들어갔다");
            StringAssert.Contains("3장까지", EditorNotice(), "같은 카드명 4장째를 넣을 때 까닭 알림이 없다");
            Click(Find<Button>(RunUi, "editor-save"));
            yield return null;
            StringAssert.Contains("저장했습니다", EditorNotice(), "컬렉션보다 많은 레시피가 저장되지 않았다");
            Click(Find<Button>(RunUi, "editor-back"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "런 시작 화면");
            StringAssert.Contains("시작 불가", HubTextOf("recipe-Yugi"), "컬렉션보다 많은 레시피에 시작 불가 표시가 없다");
            var shortSlot = Find<VisualElement>(RunUi, "deck-main-" + once) ?? Find<VisualElement>(RunUi, "deck-extra-" + once);
            Assert.IsNotNull(shortSlot, "런 시작 화면의 레시피 보기에 컬렉션보다 많이 넣은 카드가 없다");
            Assert.IsTrue(shortSlot.Query<Label>().ToList().Any(l => l.text == "부족"), "런 시작 화면의 레시피 보기에 부족 표시가 없다");
            yield return StartTab("side");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + picked[0]), "새 런 창의 시작 사이드 탭에 시작 사이드 덱 카드가 없다");
            yield return StartTab("main");
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Assert.IsFalse(IsHub(), "컬렉션보다 많은 레시피로 런이 시작됐다");
            StringAssert.Contains("런을 시작할 수 없다", Find<Label>(RunUi, "start-error").text, "런 시작을 막은 까닭 알림이 없다");
            yield return Shot(RunUi, "1b-start-blocked");
            Debug.Log($"[런 화면 시험] 컬렉션 초과 레시피: {Find<Label>(RunUi, "start-error").text}");

            // 다시 열어 빼고 저장
            yield return OpenEditor("edit-recipe");
            for (int i = 0; i < 3; i++)
            {
                DoubleClick(Find<VisualElement>(RunUi, "deck-main-" + once) ?? Find<VisualElement>(RunUi, "deck-extra-" + once));
                yield return null;
            }
            Assert.AreEqual(0, CountInDeck(once), "덱 카드를 두 번 눌러 빼기");
            StringAssert.Contains("규칙에 맞습니다", EditorStatus(), "넘친 카드를 뺐는데 규칙에 맞지 않다고 나온다");
            Click(Find<Button>(RunUi, "editor-save"));
            yield return null;

            // ---- 메인 덱 39장은 저장할 수 없음, 저장하지 않고 나가기 ----
            while (EditorMainCount() >= 40)
            {
                DoubleClick(Named("deck-main-").First());
                yield return null;
            }
            StringAssert.Contains("저장할 수 없음", EditorStatus(), "메인 덱 39장인데 저장할 수 없다는 알림이 없다");
            StringAssert.Contains("39장", EditorStatus(), "저장할 수 없는 까닭에 장수가 없다");
            Assert.IsFalse(Find<Button>(RunUi, "editor-save").enabledSelf, "메인 덱 39장인데 저장 버튼이 켜져 있다");
            yield return Shot(RunUi, "1c-editor-39");
            Click(Find<Button>(RunUi, "editor-back"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "editor-leave"), "저장하지 않은 변경이 있는데 나가기 확인이 없다");
            Click(Find<Button>(RunUi, "editor-leave"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "런 시작 화면");
            StringAssert.Contains("시작할 수 있다", Find<Label>(RunUi, "start-recipe-status").text, "저장하지 않고 나갔는데 레시피가 바뀌었다");

            // ---- 복사해서 새 레시피, 지우기, 빈 새 레시피 ----
            yield return OpenEditor("copy-recipe");
            StringAssert.Contains("사본", Find<TextField>(RunUi, "editor-name").value, "복사한 레시피 이름");
            Click(Find<Button>(RunUi, "editor-save"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "editor-delete"), "저장한 새 레시피에 지우기 버튼이 없다");
            Click(Find<Button>(RunUi, "editor-back"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "런 시작 화면");
            Assert.IsNotNull(Find<Button>(RunUi, "recipe-deck-1"), "복사해서 저장한 레시피(deck-1)가 목록에 없다");
            yield return OpenEditor("edit-recipe");
            Click(Find<Button>(RunUi, "editor-delete"));
            yield return null;
            Click(Find<Button>(RunUi, "editor-delete-confirm"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "런 시작 화면");
            Assert.IsNull(Find<Button>(RunUi, "recipe-deck-1"), "지운 레시피가 목록에 남아 있다");
            Assert.IsFalse(File.ReadAllText(SaveFile).Contains("recipe deck-1 "), "지운 레시피가 저장 파일에 남아 있다");
            yield return OpenEditor("new-recipe");
            StringAssert.Contains("저장할 수 없음", EditorStatus(), "빈 새 레시피가 저장할 수 있다고 나온다");
            Click(Find<Button>(RunUi, "editor-back"));
            yield return null;
            Click(Find<Button>(RunUi, "editor-leave"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "런 시작 화면");
            Debug.Log("[런 화면 시험] 덱 편집: 시작 사이드 덱, 같은 카드명 3장, 컬렉션 초과(저장·시작 불가), 39장 저장 불가, 복사·지우기·빈 레시피");

            // ---- 첫 런: 유희 편 레시피, 시드 1 ----
            Click(Find<Button>(RunUi, "recipe-Yugi"));
            yield return null;
            SetSeed(1);
            yield return StartTab("side");
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + code), $"런 시작 화면의 레시피 보기에 시작 사이드 덱 카드 {code}가 없다");
            yield return StartTab("main");
            Click(Named("deck-main-").First()); // 화면 확인용: 카드 하나를 고른 상태
            yield return null;
            yield return Shot(RunUi, "1-start");

            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");
            Assert.IsTrue(File.Exists(SaveFile), "런을 시작했는데 저장 파일이 없다");
            // 새 런: 내 말은 탑 앞에 서 있고, 메뉴의 첫 항목은 "1층으로 올라간다"
            Assert.AreEqual("탑 앞", Find<Label>(RunUi, "stand-floor")?.text, "새 런인데 탑 앞에 서 있지 않다");
            Assert.IsNotNull(Find<Button>(RunUi, "climb"), "새 런의 메뉴에 올라가기가 없다");
            Assert.IsNull(Find<VisualElement>(RunUi, "run-decks"), "덱 창이 처음부터 열려 있다");
            yield return Shot(RunUi, "4a-hub-start");
            yield return HubKeyboard();
            yield return OpenDeck("side");
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "card-side-" + code), $"시작 사이드 덱 카드 {code}가 덱 창의 사이드 탭에 없다");
            yield return CloseDeck();

            // 런 로그 창
            Click(Find<Button>(RunUi, "open-log"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-events"), "런 로그 창이 열리지 않았다");
            yield return Shot(RunUi, "9-log");
            Click(Find<Button>(RunUi, "close-log"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "run-events"), "런 로그 창이 닫히지 않았다");

            yield return Exchange();

            // ---- 지금 덱을 레시피로 저장 (덱 창의 버튼) ----
            yield return OpenDeck();
            Click(Find<Button>(RunUi, "save-recipe"));
            yield return null;
            var recipeName = Find<TextField>(RunUi, "recipe-name");
            Assert.IsNotNull(recipeName, "지금 덱을 레시피로 저장하는 창이 열리지 않았다");
            StringAssert.Contains("1층", recipeName.value, "레시피 이름 기본값에 층이 없다");
            recipeName.value = "시험 런 덱";
            yield return Shot(RunUi, "9b-save-recipe");
            Click(Find<Button>(RunUi, "recipe-save-confirm"));
            yield return null;
            Assert.IsNull(Find<TextField>(RunUi, "recipe-name"), "저장했는데 창이 닫히지 않았다");
            StringAssert.Contains("시험 런 덱", Find<Label>(RunUi, "move-notice").text, "레시피 저장 알림이 없다");
            string runRecipe = File.ReadAllText(SaveFile).Replace("\r\n", "\n");
            StringAssert.Contains("\nrecipe deck-1 시험%20런%20덱\n", runRecipe, "지금 덱이 레시피로 저장되지 않았다");
            StringAssert.Contains("\nrecipe-side deck-1\n", runRecipe, "지금 덱을 저장한 레시피에 사이드 덱이 들어갔다");
            yield return CloseDeck();

            yield return FirstClimb();

            for (int target = _duels + 2; _duels < target && Find<Button>(RunUi, "new-run") == null;)
                yield return Step(1);
            yield return Settle(1);
            Assert.IsTrue(IsHub(), "듀얼 2판 뒤에 층 사이 화면이 아니다 (런이 벌써 끝남?)");

            // ---- 처음 화면으로 → 이어하기 ----
            string hub = HubText();
            string saved = File.ReadAllText(SaveFile);
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "continue-run"), "처음 화면에 이어하기가 없다");
            yield return OpenNewRun();
            StringAssert.Contains("시험 런 덱", HubTextOf("recipe-deck-1"), "지금 덱으로 저장한 레시피가 새 런 창의 목록에 없다");
            yield return Shot(RunUi, "7a-new-run-continue");
            yield return CloseNewRun();
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

            // ---- 듀얼 도중에 끄기 → 그 듀얼은 패배 ----
            yield return ToBattleFloor(1);
            int played = DuelsPlayed();
            yield return BeginDuelWithCoinToss();
            yield return WaitFor(() => Find<Button>(DuelUi, "auto-to-end") != null || Find<Button>(DuelUi, "leave-duel") != null, LoadTimeout, "듀얼 화면");
            StringAssert.Contains("\nphase InDuel\n", File.ReadAllText(SaveFile), "듀얼을 시작했는데 듀얼 중 상태로 저장되지 않았다");
            var auto = Find<Button>(DuelUi, "auto-to-end");
            if (auto != null)
                Click(auto);
            for (int i = 0; i < 5; i++)
                yield return null;
            yield return LoadRunScene();
            Assert.IsNotNull(Find<VisualElement>(RunUi, "forfeit-notice"), "듀얼 도중에 끈 뒤 다시 켰는데 패배 처리 알림이 없다");
            string forfeited = File.ReadAllText(SaveFile);
            StringAssert.Contains("event DuelForfeited ", forfeited, "듀얼 도중에 끈 듀얼이 패배로 저장되지 않았다");
            Assert.IsFalse(forfeited.Contains("\nphase InDuel\n"), "패배로 처리했는데 저장 파일이 아직 듀얼 중이다");
            yield return Shot(RunUi, "7b-forfeit");
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "forfeit-notice"), "이어하기 뒤 층 사이 화면에 패배 처리 알림이 없다");
            Assert.AreEqual(played + 1, DuelsPlayed(), "듀얼 도중에 끈 듀얼이 끝낸 듀얼로 세어지지 않았다");
            // 다시 켜면 알림은 없고 저장 파일도 그대로
            yield return LoadRunScene();
            Assert.IsNull(Find<VisualElement>(RunUi, "forfeit-notice"), "한 번 알린 패배 처리 알림이 다시 나온다");
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.AreEqual(played + 1, DuelsPlayed(), "다시 켠 뒤 끝낸 듀얼 수가 바뀌었다");
            Assert.AreEqual(forfeited, File.ReadAllText(SaveFile), "패배 처리 뒤 읽기·이어하기만 했는데 저장 파일이 바뀌었다");
            Debug.Log("[런 화면 시험] 저장: 처음 화면으로·다시 켜기 뒤 이어하기는 같은 상태, 듀얼 도중 끄기는 패배 처리");

            // ---- 이 런을 끝까지 ----
            yield return PlayToEnd(1);
            Click(Find<Button>(RunUi, "new-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "새 런의 런 시작 화면");
            Assert.IsNull(Find<Button>(RunUi, "continue-run"), "끝난 런이 진행 중인 런으로 남아 있다");
            StringAssert.Contains("유희 편", Find<Label>(RunUi, "start-recipe-name").text, "마지막으로 시작한 레시피를 고른 상태가 아니다");
            yield return StartTab("side");
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + code), $"레시피의 시작 사이드 덱 카드 {code}가 남아 있지 않다");
            yield return StartTab("main");

            // ---- 진행 중인 런이 있을 때 새 런: 취소, 그리고 포기 ----
            SetSeed(2);
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");
            yield return Step(2);
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            yield return OpenNewRun();
            SetSeed(3);
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "start-cancel"), "진행 중인 런이 있는데 포기 확인 없이 지나갔다");
            Assert.IsFalse(IsHub(), "한 번 눌렀는데 새 런이 시작됐다");
            yield return Shot(RunUi, "8-abandon-confirm");
            Click(Find<Button>(RunUi, "start-cancel"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "continue-run"), "취소했는데 진행 중인 런이 없다");
            SetSeed(3);
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "포기 뒤 새 런의 층 사이 화면");
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
                yield return WaitFor(IsHub, 10, "층 사이 화면");
                yield return PlayToEnd(seed);
            }

            Debug.Log($"[런 화면 시험] 듀얼 {_duels}판, 보상 {_rewards}번 (빈 자리 골드 {(_goldChosen ? "고름" : "나오지 않음")}), 보충 {_refills}번, 보스 보상 봉인 해제 {_unseals}번, 비전투 층 {_floorsPassed}개, 교환 {(_exchanged ? "함" : "못 함")}");
            Assert.Greater(_rewards, 0, "승리 보상을 한 번도 고르지 않았다");
            Assert.Greater(_refills, 0, "보충 고르기를 한 번도 거치지 않았다");
            Assert.IsTrue(_exchanged, "사이드 덱 카드를 한 번도 교환하지 못했다");
        }

        /// <summary>
        /// 탑·칩교환소·이벤트·휴식·보스 보상 화면. 런을 시작한 뒤 저장 파일을 고쳐 5층 칩교환소(칩 400, 메인 덱 카드 3장 봉인)로 만들고,
        /// 교체(같은 레어도·무작위), 교체(레어도 상관없음·직접 고름), 봉인 해제를 산 뒤, 6층 이벤트, 9층 휴식 → 10층 보스,
        /// 보스 보상 봉인 해제 창(저장 파일을 고쳐 보스를 이긴 상태로)을 거친다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TowerAndChipExchange()
        {
            yield return LoadRunScene();
            Click(Find<Button>(RunUi, "recipe-Yugi"));
            yield return null;
            SetSeed(7);
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-floor-1"), "층 사이 화면의 탑에 1층이 없다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-floor-10"), "층 사이 화면의 탑에 10층이 없다");
            Assert.AreEqual("0", Find<Label>(RunUi, "stat-chips")?.text, "위 막대에 칩이 없다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "floor-plaque-1"), "탑 옆에 1층 팻말이 없다");

            yield return EditSave(text =>
            {
                text = SetLine(text, "floor", "5");
                text = SetLine(text, "floor-kind", "ChipExchange");
                text = SetLine(text, "chips", "400 400\nshop SwapSameRarity:30:0 SwapSameRarityChosen:50:0 SwapAny:50:0 SwapAnyChosen:80:0 Unseal:150:0 ItemCard:60:0:17092736");
                text = Regex.Replace(text, @"\nfloor-event [^\n]*", "");
                var main = Regex.Match(text, @"\nmain ([^\n]*)\n").Groups[1].Value.Split(' ');
                text = SetLine(text, "main", string.Join(" ", main.Skip(3)));
                return SetLine(text, "sealed-main", string.Join(" ", main.Take(3)));
            });
            Assert.AreEqual("5층 · 칩교환소", Find<Label>(RunUi, "floor-label")?.text, "5층 칩교환소가 아니다");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "칩교환소에 다음 듀얼 버튼이 있다");
            Assert.IsNotNull(Find<Button>(RunUi, "leave-floor"), "칩교환소에 다음 층으로 버튼이 없다");
            Assert.AreEqual("5층", Find<Label>(RunUi, "stand-floor")?.text, "칩교환소에 올라섰는데 서 있는 층이 5층이 아니다");
            Assert.AreEqual("3", Find<Label>(RunUi, "stat-sealed")?.text, "봉인 3장이 보이지 않는다");
            Assert.AreEqual("400", Find<Label>(RunUi, "stat-chips")?.text, "위 막대에 칩이 없다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 층에 왔는데 칩교환소 창이 저절로 뜨지 않았다");
            Click(Find<Button>(RunUi, "shop-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 닫히지 않았다");
            yield return Shot(RunUi, "11-tower");

            Click(Find<Button>(RunUi, "open-shop"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 열리지 않았다");
            Assert.AreEqual("400", Find<Label>(RunUi, "shop-chips")?.text, "칩교환소의 보유 칩");
            StringAssert.Contains("고대의 망원경", HubTextOf("shop-buy-ItemCard"), "칩교환소에 아이템 카드(고대의 망원경) 상품이 없다");
            foreach (string product in new[] { "SwapSameRarity", "SwapSameRarityChosen", "SwapAny", "SwapAnyChosen", "Unseal", "ItemCard" })
                Assert.IsTrue(Find<Button>(RunUi, "shop-buy-" + product)?.enabledSelf == true, $"칩 400인데 {product}를 살 수 없다");
            yield return Shot(RunUi, "12-shop");

            yield return BuyInShop("SwapSameRarity", chooseSeal: false);
            Assert.AreEqual("370", Find<Label>(RunUi, "shop-chips")?.text, "같은 레어도 교체(30) 뒤 칩");
            var sold = Find<Button>(RunUi, "shop-buy-SwapSameRarity");
            Assert.IsTrue(sold != null && !sold.enabledSelf && Find<Label>(RunUi, "shop-price-SwapSameRarity")?.text == "샀음", "산 상품을 또 살 수 있다");
            Assert.AreEqual("3", Find<Label>(RunUi, "stat-sealed")?.text, "교체 뒤 봉인 장수가 바뀌었다");
            yield return BuyInShop("SwapAnyChosen", chooseSeal: true);
            Assert.AreEqual("290", Find<Label>(RunUi, "shop-chips")?.text, "직접 고르는 완전 교체(80) 뒤 칩");
            yield return BuyInShop("Unseal", chooseSeal: false);
            Assert.AreEqual("140", Find<Label>(RunUi, "shop-chips")?.text, "봉인 해제(150) 뒤 칩");
            Assert.AreEqual("2", Find<Label>(RunUi, "stat-sealed")?.text, "봉인 해제 뒤 봉인 장수");
            string saved = File.ReadAllText(SaveFile).Replace("\r\n", "\n");
            StringAssert.Contains("SwapSameRarity:30:1", saved, "산 상품이 저장되지 않았다");
            StringAssert.Contains("\nchips 140 400\n", saved, "칩이 저장되지 않았다");

            // 아이템 카드 「고대의 망원경」(60): 상점 덱으로 가고, 상점 덱에서 적용하면 탑 미리보기가 2층이 되어 7층(확률 이벤트 층)이 보인다.
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-preview-7"), "아이템 없이 7층이 미리 보인다");
            Click(Find<Button>(RunUi, "shop-buy-ItemCard"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), "아이템 카드를 산 뒤 칩교환소 창으로 돌아오지 않았다");
            Assert.AreEqual("80", Find<Label>(RunUi, "shop-chips")?.text, "아이템 카드(60) 뒤 칩");
            StringAssert.StartsWith("칩교환소: 아이템 카드", Find<Label>(RunUi, "move-notice")?.text ?? "", "아이템 카드를 산 뒤 알림");
            var soldItem = Find<Button>(RunUi, "shop-buy-ItemCard");
            Assert.IsTrue(soldItem != null && !soldItem.enabledSelf && Find<Label>(RunUi, "shop-price-ItemCard")?.text == "샀음", "산 아이템 카드를 또 살 수 있다");
            yield return Shot(RunUi, "12b-shop-item");
            Click(Find<Button>(RunUi, "shop-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 닫히지 않았다");
            yield return OpenDeck("shop");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "card-shop-17092736"), "덱 창의 상점 덱 탭에 산 아이템 카드가 없다");
            StringAssert.Contains("상점 덱 1/10", HubTextOf("zone-shop"), "상점 덱 탭 제목");
            if (Find<Button>(RunUi, "apply-item") == null)
            {
                Click(Find<VisualElement>(RunUi, "card-shop-17092736"));
                yield return null;
            }
            var apply = Find<Button>(RunUi, "apply-item");
            Assert.IsTrue(apply != null && apply.enabledSelf, "상점 덱의 망원경에 적용 버튼이 없거나 잠겼다");
            Click(apply);
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-preview-7"), "망원경을 적용했는데 7층이 미리 보이지 않는다");
            StringAssert.Contains("바로 위 2층", Find<Label>(RunUi, "tower-preview-note")?.text ?? "", "탑 미리보기 안내");
            StringAssert.StartsWith("아이템 효과 적용", Find<Label>(RunUi, "move-notice")?.text ?? "", "적용 뒤 알림");
            saved = File.ReadAllText(SaveFile).Replace("\r\n", "\n");
            StringAssert.Contains("\nshop-deck 17092736\n", saved, "상점 덱이 저장되지 않았다");
            StringAssert.Contains("\nitems Shop:17092736\n", saved, "적용한 아이템이 저장되지 않았다");
            yield return Shot(RunUi, "12c-item-applied");
            yield return CloseDeck();

            // 6층 이벤트: 올라서면 창이 한 번 뜬다. 저장 파일을 고쳐 「용의 사당」으로: 조건 선택지는 조건이 보이고, 처음 고르는 선택지는 결과가 숨겨진다.
            Click(Find<Button>(RunUi, "leave-floor"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "climb-skip"), "다음 층으로를 눌렀는데 올라가는 연출이 시작되지 않았다");
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-event"), "올라가는 중인데 이벤트 창이 벌써 떴다");
            yield return SkipClimb();
            Assert.AreEqual("6층 · 이벤트", Find<Label>(RunUi, "floor-label")?.text, "칩교환소 다음이 6층 이벤트가 아니다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-event"), "이벤트 층에 들어갔는데 이벤트 창이 뜨지 않았다");
            yield return EditSave(text => Regex.Replace(text, @"\nfloor-event [^\n]*", "\nfloor-event dragon-shrine"));
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-event"), "이어하기로 이벤트 층에 왔는데 이벤트 창이 뜨지 않았다");
            Assert.AreEqual("[ 용의 사당. ]", Find<Label>(RunUi, "event-title")?.text, "이벤트 제목");
            Assert.IsTrue(Named("event-choice-").OfType<Button>().Any(b => b.ClassListContains("selected")), "이벤트 창이 떴는데 가리키는 선택지가 없다");
            StringAssert.Contains("드래곤족 몬스터 2장 이상", Find<Label>(RunUi, "event-choice-note-0")?.text ?? "", "조건 선택지에 조건이 보이지 않는다");
            Assert.IsTrue(Find<Button>(RunUi, "event-choice-1").enabledSelf, "칩 30 이상인데 시주 선택지가 잠겼다");
            // 선택지 글에는 조건·비용을 괄호로 넣지 않는다: 버튼에는 할 일만, 조건은 버튼 오른쪽 끝에 따로
            Assert.AreEqual("시주한다", Find<Label>(RunUi, "event-choice-title-1")?.text, "선택지 글에 괄호로 비용이 들어 있다");
            Assert.AreEqual("칩 30 이상", Find<Label>(RunUi, "event-choice-note-1")?.text, "시주 선택지의 조건 표시");
            Assert.IsNull(Find<Label>(RunUi, "event-choice-note-2"), "조건이 없는 선택지(지나간다)에 조건 표시가 있다");
            StringAssert.DoesNotContain("결과:", Find<Label>(RunUi, "event-choice-note-1")?.text ?? "", "처음 고르는 선택지의 결과가 보인다");
            Assert.IsNull(Find<Button>(RunUi, "event-pass"), "고를 수 있는 선택지가 있는데 그냥 지나가기 버튼이 있다");
            yield return Shot(RunUi, "14-event");
            Click(Find<Button>(RunUi, "event-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-event"), "이벤트 창이 닫히지 않았다");
            Click(Find<Button>(RunUi, "open-event"));
            yield return null;
            Click(Find<Button>(RunUi, "event-choice-2"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "event-result"), "이벤트 선택지를 고른 뒤 결과 창이 뜨지 않았다");
            yield return Shot(RunUi, "14b-event-result");
            Click(Find<Button>(RunUi, "event-result-ok"));
            yield return null;
            StringAssert.StartsWith("7층", Find<Label>(RunUi, "floor-label")?.text ?? "", "이벤트 선택지를 고른 뒤 7층이 아니다");
            string afterEvent = File.ReadAllText(SaveFile);
            StringAssert.Contains("event-id=dragon-shrine", afterEvent, "이벤트 결과가 저장되지 않았다");
            StringAssert.Contains("\nevent-seen dragon-shrine 2\n", afterEvent.Replace("\r\n", "\n"), "이벤트 도감이 저장되지 않았다");

            // 8층(저장 파일을 고쳐 이벤트 층, LP 3000) → 9층 휴식: 들어가면 LP를 8000으로 맞추고 휴식 창이 뜬다 → 10층 보스
            yield return EditSave(text =>
            {
                text = Regex.Replace(SetLine(text, "floor", "8"), @"\nfloor-event [^\n]*", "");
                text = SetLine(text, "lp", "3000");
                return SetLine(text, "floor-kind", "Event\nfloor-event quiet-floor");
            });
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-event"), "8층 이벤트 창이 뜨지 않았다");
            Click(Find<Button>(RunUi, "event-choice-0"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "event-result"), "8층 이벤트 결과 창이 뜨지 않았다");
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-rest"), "이벤트 결과 창보다 휴식 창이 먼저 떴다");
            Click(Find<Button>(RunUi, "event-result-ok"));
            yield return null;
            Assert.AreEqual("9층 · 휴식", Find<Label>(RunUi, "floor-label")?.text, "8층 다음이 9층 휴식이 아니다");
            StringAssert.Contains("휴식", Find<Label>(RunUi, "floor-note")?.text ?? "", "휴식 층 안내가 없다");
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-rest"), "아직 올라가지 않았는데 휴식 창이 떴다");
            yield return Climb();
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-rest"), "휴식 층에 올라섰는데 휴식 창이 뜨지 않았다");
            Assert.AreEqual("LP 3000 → 8000", Find<Label>(RunUi, "rest-lp-from")?.text + " → " + Find<Label>(RunUi, "rest-lp-to")?.text, "휴식 창의 LP");
            Assert.AreEqual("8000", Find<Label>(RunUi, "stat-lp")?.text, "휴식 뒤 LP");
            yield return Shot(RunUi, "15-rest");
            // 휴식 창의 키보드: 좌우로 버튼을 고른다. 덱 정리로 연 덱 창을 닫으면 휴식 창으로 돌아온다. 취소 키는 창을 닫는다.
            yield return Tap(UnityEngine.InputSystem.Key.RightArrow);
            yield return Tap(UnityEngine.InputSystem.Key.Z);
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-decks"), "휴식 창의 덱 정리를 골랐는데 덱 창이 열리지 않았다");
            StringAssert.Contains("휴식 창", Find<Label>(RunUi, "deck-from-duel")?.text ?? "", "휴식 창에서 연 덱 창의 안내");
            yield return CloseDeck();
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-rest"), "덱 창을 닫았는데 휴식 창으로 돌아오지 않았다");
            yield return Tap(UnityEngine.InputSystem.Key.X);
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-rest"), "휴식 창이 닫히지 않았다");
            Click(Find<Button>(RunUi, "open-rest"));
            yield return null;
            Click(Find<Button>(RunUi, "rest-leave"));
            yield return null;
            yield return SkipClimb();
            Assert.AreEqual("10층 · 보스", Find<Label>(RunUi, "floor-label")?.text, "휴식 다음이 10층 보스가 아니다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "duel-window"), "보스 층에 올라섰는데 듀얼 창이 뜨지 않았다");
            Assert.AreEqual("보스 듀얼 시작", Find<Button>(RunUi, "next-duel")?.text, "보스 층의 듀얼 시작 버튼");
            var bossFloor = Find<VisualElement>(RunUi, "floor-plaque-10");
            Assert.IsTrue(bossFloor != null && bossFloor.Query<Label>().ToList().Any(l => l.text == "지금"), "탑에 지금 층(10층) 표시가 없다");
            yield return Shot(RunUi, "15b-boss");

            // 보스를 이기고 봉인된 카드가 있으면: 보스 보상 봉인 해제 창
            yield return EditSave(text => SetLine(text, "phase", "ChoosingUnseal"));
            Assert.IsNotNull(Find<VisualElement>(RunUi, "unseal"), "보스 보상(봉인 해제) 창이 뜨지 않았다");
            Assert.IsFalse(Find<Button>(RunUi, "open-duel").enabledSelf, "보스 보상을 고르기 전인데 메뉴의 듀얼이 켜져 있다");
            yield return ChooseUnseal();
            StringAssert.StartsWith("11층", Find<Label>(RunUi, "floor-label")?.text ?? "", "보스 보상 뒤 11층이 아니다");
            Assert.AreEqual("1", Find<Label>(RunUi, "stat-sealed")?.text, "보스 보상 봉인 해제 뒤 봉인 장수");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-floor-20"), "11층인데 탑이 11~20층 묶음이 아니다");

            // 높은 층의 탑 장면 (화면 확인용): 24층은 아래에 구름 바다, 46층은 밤
            yield return EditSave(text => SetLine(SetLine(text, "floor", "24"), "floor-kind", "Battle"), climb: false);
            Assert.AreEqual("23층", Find<Label>(RunUi, "stand-floor")?.text, "24층으로 이어 했는데 23층에 서 있지 않다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-floor-30"), "24층인데 위 막대가 21~30층 묶음이 아니다");
            yield return Shot(RunUi, "11b-tower-24");
            yield return EditSave(text => SetLine(text, "floor", "46"), climb: false);
            yield return Shot(RunUi, "11c-tower-46");

            // 이벤트 도감: 처음 화면에서 연다. 만난 이벤트는 이름이, 골라 본 선택지는 결과가 보인다.
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            Click(Find<Button>(RunUi, "open-codex"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "event-codex"), "이벤트 도감 창이 열리지 않았다");
            var shrine = Find<VisualElement>(RunUi, "codex-dragon-shrine");
            Assert.IsTrue(shrine != null && shrine.Query<Label>().ToList().Any(l => l.text == "용의 사당"), "도감에 만난 이벤트(용의 사당)가 없다");
            yield return Shot(RunUi, "17-codex");
            Click(Find<Button>(RunUi, "event-codex-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "event-codex"), "이벤트 도감 창이 닫히지 않았다");
            Debug.Log("[런 화면 시험] 탑·칩교환소: 교체 2가지와 봉인 해제 구매, 이벤트 창·결과, 휴식 → 보스, 보스 보상 봉인 해제, 이벤트 도감");
        }

        /// <summary>칩교환소에서 상품 하나를 산다: 사기 → 풀 카드 → (직접 고르는 교체면 대신 봉인할 카드) → 사기. 산 뒤 칩교환소 창으로 돌아온다.</summary>
        IEnumerator BuyInShop(string product, bool chooseSeal)
        {
            int sideBefore = SavedCards("side").Count;
            Click(Find<Button>(RunUi, "shop-buy-" + product));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "shop-pick"), $"{product}: 카드를 고르는 창이 열리지 않았다");
            var release = Named("card-shoprelease-");
            Assert.Greater(release.Count, 0, $"{product}: 풀 카드 후보가 없다");
            Click(release[0]);
            yield return null;
            yield return Shot(RunUi, "13-shop-pick");
            Click(Find<Button>(RunUi, "shop-confirm"));
            yield return null;
            if (chooseSeal)
            {
                var seal = Named("card-shopseal-");
                Assert.Greater(seal.Count, 0, $"{product}: 대신 봉인할 카드 후보가 없다");
                Click(seal[seal.Count - 1]);
                yield return null;
                yield return Shot(RunUi, "13b-shop-pick-seal");
                Click(Find<Button>(RunUi, "shop-confirm"));
                yield return null;
            }
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), $"{product}: 산 뒤 칩교환소 창으로 돌아오지 않았다");
            StringAssert.StartsWith("칩교환소:", Find<Label>(RunUi, "move-notice")?.text ?? "", $"{product}: 산 뒤 알림");
            Assert.AreEqual(sideBefore + 1, SavedCards("side").Count, $"{product}: 풀린 카드가 사이드 덱에 더해지지 않았다");
            Assert.AreEqual($"사이드 {sideBefore + 1}", Regex.Match(Find<Label>(RunUi, "deck-counts")?.text ?? "", @"사이드 \d+").Value,
                $"{product}: 메뉴의 사이드 덱 장수가 저장 파일과 다르다");
        }

        /// <summary>
        /// 처음 화면으로 가서 저장 파일을 고치고, 씬을 다시 불러 이어하기.
        /// 이어 하면 내 말은 아래층에 서 있다. climb이면 고친 층으로 올라가서(연출은 건너뛴다) 그 층의 창이 뜬 상태로 만든다.
        /// </summary>
        IEnumerator EditSave(Func<string, string> edit, bool climb = true)
        {
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            File.WriteAllText(SaveFile, edit(File.ReadAllText(SaveFile).Replace("\r\n", "\n")));
            yield return LoadRunScene();
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.IsTrue(IsHub(), "고친 저장 파일로 이어하기를 했는데 층 사이 화면이 아니다");
            if (climb && Find<Button>(RunUi, "climb") != null && Find<Button>(RunUi, "climb").enabledSelf)
                yield return Climb();
        }

        /// <summary>올라가는 연출 중이면 화면을 눌러 건너뛴다. 끝나면 그 층의 창(듀얼 창, 칩교환소 …)이 떠 있다.</summary>
        static IEnumerator SkipClimb()
        {
            var skip = Find<Button>(RunUi, "climb-skip");
            if (skip == null)
                yield break;
            Click(skip);
            yield return null;
            Assert.IsNull(Find<Button>(RunUi, "climb-skip"), "연출을 건너뛰었는데 끝나지 않았다");
            Assert.IsTrue(IsHub(), "연출을 건너뛴 뒤 층 사이 화면이 아니다");
        }

        /// <summary>메뉴의 "N층으로 올라간다"를 누르고 연출을 건너뛴다.</summary>
        static IEnumerator Climb()
        {
            var climb = Find<Button>(RunUi, "climb");
            Assert.IsNotNull(climb, "메뉴에 올라가기가 없다");
            Click(climb);
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "climb-skip"), "올라가기를 눌렀는데 연출이 시작되지 않았다");
            yield return SkipClimb();
        }

        /// <summary>
        /// 첫 층은 올라가는 연출을 건너뛰지 않고 끝까지 본다: 메뉴가 사라지고, 끝나면 1층에 서 있고 듀얼 창이 뜬다.
        /// 듀얼 창에서 "덱 조정"으로 덱 창을 열었다 닫으면 듀얼 창으로 돌아오는지도 본다.
        /// </summary>
        IEnumerator FirstClimb()
        {
            StringAssert.Contains("1층으로 올라간다", HubTextOf("climb"), "메뉴의 올라가기 글");
            Click(Find<Button>(RunUi, "climb"));
            yield return null;
            Assert.IsNotNull(Find<Button>(RunUi, "climb-skip"), "올라가기를 눌렀는데 연출이 시작되지 않았다");
            Assert.IsNull(Find<VisualElement>(RunUi, "hub-menu"), "올라가는 중인데 명령 메뉴가 보인다");
            yield return WaitFor(() => Find<Label>(RunUi, "stand-floor")?.text == "1층", 15, "1층 도착");
            yield return Shot(RunUi, "4b-arrive");
            yield return WaitFor(() => Find<Button>(RunUi, "climb-skip") == null, 20, "올라가는 연출의 끝");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "duel-window"), "1층에 올라섰는데 듀얼 창이 뜨지 않았다");
            Assert.AreEqual("듀얼 시작", Find<Button>(RunUi, "next-duel")?.text, "듀얼 창의 듀얼 시작 버튼");
            Assert.AreEqual("상대 · ???", Find<Label>(RunUi, "duel-opponent")?.text, "듀얼 창에서 상대 덱 이름을 가리지 않았다");
            Assert.AreEqual("1층 · 전투", Find<Label>(RunUi, "floor-label")?.text, "1층에 올라선 뒤 메뉴의 층 표시");
            yield return Shot(RunUi, "4c-duel-window");

            // 듀얼 창 → 덱 조정 → 닫기 → 듀얼 창
            Click(Find<Button>(RunUi, "duel-deck"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-decks"), "덱 조정을 눌렀는데 덱 창이 열리지 않았다");
            Assert.IsNull(Find<VisualElement>(RunUi, "duel-window"), "덱 창이 열렸는데 듀얼 창이 남아 있다");
            Assert.IsNotNull(Find<Label>(RunUi, "deck-from-duel"), "듀얼 전 덱 조정 중이라는 안내가 없다");
            yield return Shot(RunUi, "4d-deck-from-duel");
            yield return CloseDeck();
            Assert.IsNotNull(Find<VisualElement>(RunUi, "duel-window"), "덱 창을 닫았는데 듀얼 창으로 돌아오지 않았다");
            Debug.Log("[런 화면 시험] 올라가는 연출(끝까지) → 듀얼 창 → 덱 조정 → 듀얼 창");
        }

        /// <summary>
        /// 허브의 키보드 (가상 키보드): S로 메뉴 아래 항목, J로 덱 창 열기, E로 다음 탭, D로 카드 고르기, K로 닫기, 방향키와 Z·X 자리의 키 표시.
        /// </summary>
        IEnumerator HubKeyboard()
        {
            bool Selected(string name) => Find<Button>(RunUi, name)?.ClassListContains("selected") == true;
            Assert.IsTrue(Selected("climb"), "허브에 들어왔을 때 메뉴의 첫 항목이 골라져 있지 않다");
            StringAssert.Contains("Z", HubTextOf("hub-menu"), "처음에는 방향키 자리(Z)의 키 표시여야 한다");
            yield return Tap(UnityEngine.InputSystem.Key.S);
            Assert.IsTrue(Selected("open-deck"), "S를 눌렀는데 메뉴의 다음 항목(덱)이 골라지지 않았다");
            Assert.IsFalse(Selected("climb"), "S를 눌렀는데 첫 항목이 그대로 골라져 있다");
            StringAssert.Contains("J", HubTextOf("hub-menu"), "W A S D를 썼는데 키 표시가 J로 바뀌지 않았다");
            yield return Tap(UnityEngine.InputSystem.Key.J);
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-decks"), "J를 눌렀는데 덱 창이 열리지 않았다");
            Assert.IsTrue(Selected("zone-main"), "덱 창이 메인 탭으로 열리지 않았다");
            yield return Tap(UnityEngine.InputSystem.Key.D);
            yield return Tap(UnityEngine.InputSystem.Key.D);
            var cards = Named("card-main-");
            Assert.Greater(cards.Count, 1, "덱 창의 메인 탭에 카드가 없다");
            Assert.Greater(cards[1].resolvedStyle.scale.value.x, 1.01f, "D를 두 번 눌렀는데 둘째 카드가 골라지지 않았다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "card-detail-face"), "키보드로 카드를 골랐는데 카드 정보가 나오지 않았다");
            yield return Shot(RunUi, "4e-deck");
            yield return Tap(UnityEngine.InputSystem.Key.E);
            Assert.IsTrue(Selected("zone-extra"), "E를 눌렀는데 다음 탭(엑스트라)으로 가지 않았다");
            yield return Tap(UnityEngine.InputSystem.Key.K);
            Assert.IsNull(Find<VisualElement>(RunUi, "run-decks"), "K를 눌렀는데 덱 창이 닫히지 않았다");
            yield return Tap(UnityEngine.InputSystem.Key.UpArrow);
            Assert.IsTrue(Selected("climb"), "↑를 눌렀는데 메뉴의 첫 항목으로 돌아가지 않았다");
            StringAssert.Contains("Z", HubTextOf("hub-menu"), "방향키를 썼는데 키 표시가 Z로 돌아오지 않았다");
            Debug.Log("[런 화면 시험] 허브 키보드: W A S D + J·K, 방향키, 덱 창의 탭·카드 고르기");
        }

        /// <summary>
        /// 덱 창을 연다 (허브 메뉴의 "덱", 듀얼 창이 떠 있으면 "덱 조정"). tab을 주면 그 탭으로 바꾼다 (main, extra, side, sealed, shop).
        /// </summary>
        static IEnumerator OpenDeck(string tab = null)
        {
            if (Find<VisualElement>(RunUi, "run-decks") == null)
            {
                Click(Find<Button>(RunUi, "duel-deck") ?? Find<Button>(RunUi, "open-deck"));
                yield return null;
            }
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-decks"), "덱 창이 열리지 않았다");
            if (tab != null && Find<Button>(RunUi, "zone-" + tab)?.ClassListContains("selected") != true)
            {
                Click(Find<Button>(RunUi, "zone-" + tab));
                yield return null;
                Assert.IsTrue(Find<Button>(RunUi, "zone-" + tab).ClassListContains("selected"), $"덱 창의 {tab} 탭으로 바뀌지 않았다");
            }
        }

        /// <summary>덱 창을 닫는다 (듀얼 창에서 열었으면 듀얼 창으로 돌아간다). 열려 있지 않으면 아무 일도 하지 않는다.</summary>
        static IEnumerator CloseDeck()
        {
            var close = Find<Button>(RunUi, "close-deck");
            if (close == null)
                yield break;
            Click(close);
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "run-decks"), "덱 창이 닫히지 않았다");
        }

        /// <summary>저장 파일의 "key 카드 …" 줄에 든 카드 코드들 (main, extra, side, sealed-main …). 층 사이 화면에서 덱 창을 열지 않고 장수를 본다.</summary>
        List<string> SavedCards(string key)
        {
            var match = Regex.Match(File.ReadAllText(SaveFile).Replace("\r\n", "\n"), $@"\n{Regex.Escape(key)}( [^\n]*)?\n");
            return match.Success ? match.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
        }

        /// <summary>저장 파일의 "key 값…" 줄을 바꾼다 (값이 없는 줄도). 그 줄이 없으면 실패.</summary>
        static string SetLine(string text, string key, string value)
        {
            var pattern = new Regex($@"\n{Regex.Escape(key)}( [^\n]*)?\n");
            Assert.IsTrue(pattern.IsMatch(text), $"저장 파일에 {key} 줄이 없다");
            return pattern.Replace(text, _ => $"\n{key} {value}\n", 1);
        }

        IEnumerator LoadRunScene()
        {
            SceneManager.LoadScene("Run");
            yield return null;
            yield return null;
            yield return WaitFor(() => Find<Button>(RunUi, "title-new-run") != null, LoadTimeout, "처음 화면");
            yield return Shot(RunUi, "0a-title");
            // 처음 화면의 명령 메뉴: 위아래 키로 고른다. 시험의 대부분은 새 런 창에서 시작한다.
            Assert.IsTrue(Named("title-").Concat(Named("continue-run")).OfType<Button>().Any(b => b.ClassListContains("selected")), "처음 화면의 메뉴에 가리키는 항목이 없다");
            yield return OpenNewRun();
        }

        /// <summary>처음 화면에서 새 런 창을 연다 (이미 열려 있으면 그대로).</summary>
        static IEnumerator OpenNewRun()
        {
            if (Find<Button>(RunUi, "start-run") == null)
            {
                Click(Find<Button>(RunUi, "title-new-run"));
                yield return null;
            }
            Assert.IsNotNull(Find<Button>(RunUi, "start-run"), "새 런 창이 열리지 않았다");
        }

        /// <summary>새 런 창을 닫고 처음 화면으로 돌아간다.</summary>
        static IEnumerator CloseNewRun()
        {
            Click(Find<Button>(RunUi, "start-back"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "recipe-window"), "새 런 창이 닫히지 않았다");
            Assert.IsNotNull(Find<Button>(RunUi, "title-new-run"), "새 런 창을 닫았는데 처음 화면의 메뉴가 없다");
        }

        /// <summary>새 런 창의 탭을 바꾼다 ("main", "extra", "side").</summary>
        static IEnumerator StartTab(string tab)
        {
            var button = Find<Button>(RunUi, "start-tab-" + tab);
            Assert.IsNotNull(button, $"새 런 창에 {tab} 탭이 없다");
            Click(button);
            yield return null;
        }

        /// <summary>
        /// 카드 견본 화면(RunScreen.ToggleCardGallery)을 띄워 카드가 그려지는지 보고 스크린샷을 남긴 뒤 닫는다.
        /// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 이름으로 찾아 부른다.
        /// </summary>
        IEnumerator CardGallery()
        {
            const string gallery = "CardGalleryUI";
            var screen = GameObject.Find(RunUi).transform.parent.GetComponent("RunScreen");
            Assert.IsNotNull(screen, "RunScreen을 찾지 못했다");
            var toggle = screen.GetType().GetMethod("ToggleCardGallery");
            Assert.IsNotNull(toggle, "RunScreen.ToggleCardGallery가 없다");
            toggle.Invoke(screen, null);
            yield return null;
            yield return null;
            var faces = Root(gallery)?.Query("card-face").ToList() ?? new List<VisualElement>();
            Assert.GreaterOrEqual(faces.Count, 20, "카드 견본에 작은 카드가 모자란다");
            Assert.GreaterOrEqual(Root(gallery).Query("card-detail-face").ToList().Count, 3, "카드 견본에 상세 카드가 모자란다");
            Assert.IsNotNull(Root(gallery).Q("card-back"), "카드 견본에 뒷면이 없다");
            // 카드명 줄과 수치 줄의 높이는 카드마다 같아야 한다 (긴 이름이 아래 칸을 밀면 안 된다).
            // 배치는 화면 픽셀에 맞춰 반올림되므로 작은 창에서는 1픽셀만큼(기준 단위로는 2쯤) 어긋날 수 있다.
            foreach (var face in faces)
            {
                var card = face[0];
                Assert.AreEqual(3, card.childCount, "작은 카드는 이름 줄, 그림 칸, 수치 줄 세 칸이다");
                Assert.AreEqual(24f, card[0].layout.height, 2.5f, "카드명 줄의 높이가 다르다");
                Assert.AreEqual(24f, card[2].layout.height, 2.5f, "수치 줄의 높이가 다르다");
                Assert.AreEqual(94f, card[1].layout.height, 4f, "그림 칸의 높이가 다르다 (이름이 길어 밀렸을 수 있다)");
            }
            yield return Shot(gallery, "0-card-gallery");
            toggle.Invoke(screen, null);
            yield return null;
            Assert.IsNull(GameObject.Find(gallery), "카드 견본이 닫히지 않았다");
        }

        static string SideCountText(int count) => $"시작 사이드 덱 {count}/{SidePicks}장";

        /// <summary>덱 편집 화면: 컬렉션 목록에서 아직 고르지 않았고 넣을 수 있는 카드를 하나 골라 "시작 사이드 덱에 넣기"를 누른다.</summary>
        IEnumerator PickToSide(List<string> picked)
        {
            // 누를 때마다 화면을 다시 그리므로 이름만 먼저 모아 두고 매번 새로 찾는다.
            var codes = Named("pick-").Select(e => e.name.Substring("pick-".Length)).Where(c => !picked.Contains(c)).ToList();
            foreach (string code in codes)
            {
                Click(Find<VisualElement>(RunUi, "pick-" + code));
                yield return null;
                var add = Find<Button>(RunUi, "editor-add-side");
                if (add == null || !add.enabledSelf)
                    continue; // 같은 카드명이 덱에 이미 3장 있는 등
                Click(add);
                yield return null;
                picked.Add(code);
                yield break;
            }
            Assert.Fail("컬렉션 목록에 시작 사이드 덱에 넣을 수 있는 카드가 없다");
        }

        /// <summary>런 시작 화면의 버튼(edit-recipe, copy-recipe, new-recipe)을 눌러 덱 편집 화면을 연다.</summary>
        static IEnumerator OpenEditor(string button)
        {
            var b = Find<Button>(RunUi, button);
            Assert.IsNotNull(b, $"런 시작 화면에 {button} 버튼이 없다");
            Click(b);
            yield return WaitFor(() => Find<VisualElement>(RunUi, "editor-bar") != null, 10, "덱 편집 화면");
        }

        static string EditorStatus() => Find<Label>(RunUi, "editor-status")?.text ?? "(규칙 확인 줄 없음)";

        static string EditorNotice()
        {
            var notice = Find<Label>(RunUi, "editor-notice");
            Assert.IsNotNull(notice, "덱 편집 화면에 알림 줄이 없다");
            Assert.AreEqual(DisplayStyle.Flex, notice.style.display.value, "덱 편집 화면의 알림이 보이지 않는다");
            return notice.text;
        }

        /// <summary>덱 편집 화면 "메인 덱 n장"의 n.</summary>
        static int EditorMainCount()
        {
            var label = Find<Label>(RunUi, "editor-main-count");
            Assert.IsNotNull(label, "덱 편집 화면에 메인 덱 장수가 없다");
            var match = Regex.Match(label.text, @"메인 덱 (\d+)장");
            Assert.IsTrue(match.Success, $"메인 덱 장수 글: {label.text}");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>덱 편집 화면의 메인·엑스트라 덱에 든 이 카드의 장수.</summary>
        static int CountInDeck(string code) => CountNamed("deck-main-" + code) + CountNamed("deck-extra-" + code);

        /// <summary>층 사이 화면 "끝낸 듀얼 n"의 n.</summary>
        static int DuelsPlayed()
        {
            var summary = Find<Label>(RunUi, "run-summary");
            Assert.IsNotNull(summary, "층 사이 화면에 런 요약 줄이 없다");
            var match = Regex.Match(summary.text, @"끝낸 듀얼 (\d+)");
            Assert.IsTrue(match.Success, $"런 요약 줄에 끝낸 듀얼 수가 없다: {summary.text}");
            return int.Parse(match.Groups[1].Value);
        }

        void SetSeed(int seed)
        {
            var field = Find<TextField>(RunUi, "seed");
            Assert.IsNotNull(field, "시드 칸이 없다");
            field.value = seed.ToString();
        }

        /// <summary>이름이 name인 요소 안의 모든 글을 이어 붙인다 (없으면 빈 글).</summary>
        static string HubTextOf(string name)
        {
            var e = Root(RunUi)?.Q(name);
            return e == null ? "" : string.Join(" ", e.Query<TextElement>().ToList().Select(t => t.text));
        }

        /// <summary>층 사이 화면의 위 막대, 명령 메뉴, 직전 듀얼 판의 글 (이어하기 전후 비교용).</summary>
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
            return Texts("run-status") + "\n----\n" + Texts("hub-menu") + "\n----\n" + Texts("last-duel-panel");
        }

        IEnumerator PlayToEnd(int seed)
        {
            int duels = 0;
            while (Find<Button>(RunUi, "new-run") == null)
            {
                Assert.Less(duels, MaxDuelsPerRun, $"시드 {seed}: 듀얼 {MaxDuelsPerRun}판 안에 런이 끝나지 않았다");
                if (NoModal() && Find<Button>(RunUi, "next-duel") != null)
                    duels++;
                yield return Step(seed);
            }
            Debug.Log($"[런 화면 시험] 시드 {seed}: 런 종료, 보상 누적 {_rewards}번, 보충 누적 {_refills}번, 지나간 비전투 층 {_floorsPassed}개");
            StringAssert.Contains("골드 정산", Find<Label>(RunUi, "run-settlement")?.text ?? "", "런 종료 화면에 골드 정산이 없다");
            Debug.Log($"[런 화면 시험] 코인 토스: 이김 {_coinWins}번, 짐 {_coinLosses}번, 후공 듀얼 {_secondDuels}판");
            Assert.GreaterOrEqual(_coinWins + _coinLosses, _duels, "듀얼마다 코인 토스 창을 거치지 않았다");
            yield return Shot(RunUi, "6-end");
        }

        /// <summary>승리 보상 창·보충 창·보스 보상 창이 떠 있으면 처리해 층 사이 화면(또는 런 종료)으로 만든다.</summary>
        IEnumerator Settle(int seed)
        {
            for (int i = 0; i < 10 && (Find<VisualElement>(RunUi, "reward") != null || Find<VisualElement>(RunUi, "refill") != null
                                       || Find<VisualElement>(RunUi, "unseal") != null); i++)
                yield return Step(seed);
            Assert.IsNull(Find<VisualElement>(RunUi, "reward"), "승리 보상 창이 닫히지 않는다");
            Assert.IsNull(Find<VisualElement>(RunUi, "refill"), "보충 창이 닫히지 않는다");
            Assert.IsNull(Find<VisualElement>(RunUi, "unseal"), "보스 보상 창이 닫히지 않는다");
        }

        /// <summary>층 사이 화면이 보이는지 (전투 층이 아니면 다음 듀얼 버튼이 없어서 상태 줄로 본다).</summary>
        static bool IsHub() => Find<VisualElement>(RunUi, "run-status") != null;

        /// <summary>층 사이 화면 위에 창(보상·보충·보스 보상·칩교환소·이벤트)이 떠 있지 않은지.</summary>
        static bool NoModal() =>
            new[] { "reward", "refill", "unseal", "chip-exchange", "shop-pick", "tower-event", "tower-rest", "coin-toss", "run-log", "event-result", "event-battle", "event-codex" }.All(n => Find<VisualElement>(RunUi, n) == null);

        /// <summary>전투 층(다음 듀얼 버튼이 있는 층)이 될 때까지 Step으로 창을 처리하고 비전투 층을 지나간다 (듀얼은 하지 않는다).</summary>
        IEnumerator ToBattleFloor(int seed)
        {
            for (int i = 0; i < 20 && !(NoModal() && Find<Button>(RunUi, "next-duel") != null); i++)
                yield return Step(seed);
            Assert.IsNotNull(Find<Button>(RunUi, "next-duel"), "전투 층에 닿지 않았다");
        }

        /// <summary>보스 보상 창: 봉인된 카드 하나를 누르고 봉인 풀기.</summary>
        IEnumerator ChooseUnseal()
        {
            var cards = Named("card-unseal-");
            Assert.Greater(cards.Count, 0, "보스 보상 창에 봉인된 카드가 없다");
            var next = Find<Button>(RunUi, "next-duel");
            Assert.IsTrue(next == null || !next.enabledSelf, "보스 보상을 고르기 전인데 듀얼 시작 버튼이 켜져 있다");
            Click(cards[0]);
            yield return null;
            yield return Shot(RunUi, "16-unseal");
            Click(Find<Button>(RunUi, "unseal-confirm"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "unseal"), "봉인 해제를 골랐는데 보스 보상 창이 닫히지 않았다");
            StringAssert.StartsWith("보스 보상", Find<Label>(RunUi, "move-notice")?.text ?? "", "봉인 해제 뒤 알림");
            _unseals++;
        }

        /// <summary>
        /// "다음 듀얼 시작" → 코인 토스 창. 이겼으면 선공·후공을 번갈아 고르고, 졌으면 듀얼 시작.
        /// 첫 번째는 "나중에"로 닫았다가 다시 열어 같은 결과인지 본다.
        /// </summary>
        IEnumerator BeginDuelWithCoinToss()
        {
            Click(Find<Button>(RunUi, "next-duel"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "coin-toss"), "듀얼 시작을 눌렀는데 코인 토스 창이 뜨지 않았다");
            string result = Find<Label>(RunUi, "coin-result")?.text;
            if (_coinWins + _coinLosses == 0)
            {
                yield return Shot(RunUi, "2a-coin-toss");
                // 창의 공통 키보드: 첫 키는 금색 테를 켜고, 취소 키는 "나중에"를 누른다 (듀얼 창으로 돌아간다)
                var keyRing = Find<VisualElement>(RunUi, "run-key-ring");
                Assert.IsTrue(keyRing != null && keyRing.style.display.value == DisplayStyle.None, "키를 누르기 전인데 금색 테가 보인다");
                Assert.IsNotNull(Find<VisualElement>(RunUi, "modal-hints"), "창 아래에 키 도움말이 없다");
                yield return Tap(UnityEngine.InputSystem.Key.DownArrow);
                Assert.AreEqual(DisplayStyle.Flex, keyRing.style.display.value, "코인 토스 창에서 이동 키를 눌렀는데 금색 테가 켜지지 않았다");
                yield return Tap(UnityEngine.InputSystem.Key.X);
                Assert.IsNull(Find<VisualElement>(RunUi, "coin-toss"), "코인 토스 창이 닫히지 않았다");
                Assert.IsNotNull(Find<Button>(RunUi, "next-duel"), "코인 토스 창을 닫았는데 듀얼 시작 버튼이 없다");
                Click(Find<Button>(RunUi, "next-duel"));
                yield return null;
                Assert.AreEqual(result, Find<Label>(RunUi, "coin-result")?.text, "코인 토스를 다시 열었는데 결과가 바뀌었다");
            }
            // 코인 토스 연출: 동전이 도는 동안에는 결과 글이 보이지 않고 고르는 버튼이 꺼져 있다.
            // 한 번은 끝까지 지켜보고, 그 밖에는 동전을 눌러 바로 결과를 낸다.
            Button Choose() => Find<Button>(RunUi, "coin-first") ?? Find<Button>(RunUi, "coin-start");
            Assert.IsNotNull(Choose(), "코인 토스 창에 선공·후공이나 듀얼 시작 버튼이 없다");
            if (!Choose().enabledSelf)
            {
                Assert.Less(Find<Label>(RunUi, "coin-result").resolvedStyle.opacity, 0.5f, "동전이 도는 중인데 결과 글이 보인다");
                if (!_coinFlipWatched)
                {
                    _coinFlipWatched = true;
                    yield return WaitFor(() => Choose().enabledSelf, 6, "코인 토스 연출의 끝");
                    yield return null;
                    Assert.Greater(Find<Label>(RunUi, "coin-result").resolvedStyle.opacity, 0.9f, "동전이 멈췄는데 결과 글이 보이지 않는다");
                    yield return Shot(RunUi, "2a2-coin-landed");
                    Debug.Log("[런 화면 시험] 코인 토스 연출: 동전이 돌다 멈춘 뒤 결과와 버튼이 나옴");
                }
                else
                {
                    Click(Find<Button>(RunUi, "coin-flip"));
                    yield return null;
                }
                Assert.IsTrue(Choose().enabledSelf, "동전이 멈췄는데 고르는 버튼이 꺼져 있다");
            }
            var first = Find<Button>(RunUi, "coin-first");
            if (first != null)
            {
                Assert.IsNotNull(Find<Button>(RunUi, "coin-second"), "코인 토스에 이겼는데 후공 버튼이 없다");
                StringAssert.Contains("내가 이겼습니다", result ?? "", "코인 토스 승리 표시");
                bool second = _coinWins % 2 == 1;
                _coinWins++;
                if (second) _secondDuels++;
                Click(second ? Find<Button>(RunUi, "coin-second") : first);
            }
            else
            {
                StringAssert.Contains("상대가 이겼습니다", result ?? "", "코인 토스 패배 표시");
                var start = Find<Button>(RunUi, "coin-start");
                Assert.IsNotNull(start, "코인 토스에 졌는데 듀얼 시작 버튼이 없다");
                _coinLosses++;
                if (Find<VisualElement>(RunUi, "coin-toss").Query<Label>().ToList().Any(l => l.text.Contains("나는 후공")))
                    _secondDuels++;
                Click(start);
            }
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "coin-toss"), "듀얼을 시작했는데 코인 토스 창이 남아 있다");
        }

        /// <summary>
        /// 승리 보상 창이 떠 있으면 하나 고르고, 보충 창이 떠 있으면 한 장 넣고, 보스 보상 창이면 봉인을 풀고, 이벤트 창이면 첫 선택지,
        /// 칩교환소 창이면 닫는다. 비전투 층(칩교환소·휴식·이벤트)이면 다음 층으로 가고, 전투 층이면 듀얼 하나를 끝까지 한다.
        /// </summary>
        IEnumerator Step(int seed)
        {
            if (Find<Button>(RunUi, "climb-skip") != null)
            {
                // 올라가는 중 (다음 층으로·휴식 창의 다음 층으로를 누른 뒤): 연출을 건너뛴다
                yield return SkipClimb();
                yield break;
            }
            if (Find<VisualElement>(RunUi, "unseal") != null)
            {
                yield return ChooseUnseal();
                yield break;
            }
            if (Find<VisualElement>(RunUi, "tower-rest") != null)
            {
                Click(Find<Button>(RunUi, "rest-leave"));
                _floorsPassed++;
                yield return null;
                yield break;
            }
            if (Find<VisualElement>(RunUi, "tower-event") != null)
            {
                // 고를 수 있는 첫 선택지, 없으면 그냥 지나가기
                var option = Named("event-choice-").OfType<Button>().FirstOrDefault(b => b.enabledSelf) ?? Find<Button>(RunUi, "event-pass");
                Assert.IsNotNull(option, "이벤트 창에 고를 수 있는 선택지가 없다");
                Click(option);
                _floorsPassed++;
                yield return null;
                yield break;
            }
            if (Find<VisualElement>(RunUi, "event-result") != null)
            {
                Click(Find<Button>(RunUi, "event-result-ok"));
                yield return null;
                yield break;
            }
            if (Find<VisualElement>(RunUi, "chip-exchange") != null || Find<VisualElement>(RunUi, "shop-pick") != null)
            {
                CloseShopForStep();
                yield return null;
                yield break;
            }
            if (Find<VisualElement>(RunUi, "reward") != null)
            {
                yield return ChooseReward();
                yield break;
            }
            if (Find<VisualElement>(RunUi, "refill") != null)
            {
                var candidate = Named("refill-").FirstOrDefault(b => b.name != "refill-confirm" && b.name != "refill-detail");
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

            // 아직 아래층에 서 있으면 올라간다 (연출은 건너뛴다). 올라서면 그 층의 창이 뜬다 (전투 층은 듀얼 창).
            if (Find<Button>(RunUi, "climb") != null)
            {
                yield return Climb();
                yield break;
            }
            // 전투 층에 서 있는데 듀얼 창이 닫혀 있으면 (같은 층에서 지고 돌아옴) 메뉴에서 연다
            var openDuel = Find<Button>(RunUi, "open-duel");
            if (openDuel != null && Find<Button>(RunUi, "next-duel") == null)
            {
                Click(openDuel);
                yield return null;
                Assert.IsNotNull(Find<VisualElement>(RunUi, "duel-window"), "메뉴의 듀얼을 눌렀는데 듀얼 창이 뜨지 않았다");
                yield break;
            }

            var openEvent = Find<Button>(RunUi, "open-event");
            if (openEvent != null)
            {
                Click(openEvent);
                yield return null;
                yield break;
            }
            var leave = Find<Button>(RunUi, "leave-floor");
            if (leave != null)
            {
                Click(leave);
                _floorsPassed++;
                yield return null;
                yield break;
            }

            yield return BeginDuelWithCoinToss();
            yield return WaitFor(() => Find<Button>(DuelUi, "auto-to-end") != null || Find<Button>(DuelUi, "leave-duel") != null,
                LoadTimeout, "듀얼 화면");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "듀얼 중에 런 화면이 보인다");
            yield return Shot(DuelUi, "2-duel");
            float until = Time.realtimeSinceStartup + DuelTimeout;
            while (Find<Button>(DuelUi, "leave-duel") == null)
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"시드 {seed} 듀얼: {DuelTimeout}초 안에 끝나지 않았다");
                if (!_playbackDone && _playbackTries < MaxPlaybackTries && TurnOf(Find<Label>(DuelUi, "turn-label")) >= 2)
                    yield return CheckPlayback();
                if (!_duelViewChecked && TurnOf(Find<Label>(DuelUi, "turn-label")) >= 6)
                    yield return CheckDuelView();
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
            Assert.IsTrue(IsHub() || Find<Button>(RunUi, "new-run") != null,
                "결과 보기 뒤에 층 사이 화면이나 런 종료 화면이 아니다");
            if (IsHub() && NoModal())
                yield return Shot(RunUi, "4-hub");
        }

        /// <summary>
        /// 승리 보상 창: 자리 3개(후보 카드 + 빈 자리 골드)인지, 고르기 전에는 받기 버튼과 다음 듀얼 시작 버튼이 꺼져 있는지 본다.
        /// 빈 자리가 있으면 처음 한 번은 골드를 고르고, 그 밖에는 누르고 받기·두 번 누르기를 번갈아 한다.
        /// </summary>
        IEnumerator ChooseReward()
        {
            var cards = Named("reward-card-").Select(e => e.name).ToList();
            var golds = Named("reward-gold-").Select(e => e.name).ToList();
            Assert.AreEqual(RewardChoices, cards.Count + golds.Count, $"보상 창 자리 수 (카드 {cards.Count}, 빈 자리 {golds.Count})");
            Assert.Greater(cards.Count, 0, "보상 창에 후보 카드가 없다");
            var confirm = Find<Button>(RunUi, "reward-confirm");
            Assert.IsNotNull(confirm, "보상 창에 받기 버튼이 없다");
            Assert.IsFalse(confirm.enabledSelf, "후보를 고르기 전인데 받기 버튼이 켜져 있다");
            var next = Find<Button>(RunUi, "open-duel") ?? Find<Button>(RunUi, "climb");
            Assert.IsTrue(next != null && !next.enabledSelf, "보상을 고르기 전인데 메뉴의 첫 항목(듀얼·올라가기)이 켜져 있다");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "보상을 고르기 전인데 듀얼 창이 떠 있다");
            int sideBefore = SavedCards("side").Count;
            bool gold = golds.Count > 0 && !_goldChosen;
            string how;
            if (gold)
            {
                Click(Find<VisualElement>(RunUi, golds[0]));
                yield return null;
                confirm = Find<Button>(RunUi, "reward-confirm");
                StringAssert.StartsWith("골드", confirm.text, "빈 자리를 골랐는데 받기 버튼이 골드가 아니다");
                yield return Shot(RunUi, "10b-reward-gold");
                Click(confirm);
                how = "빈 자리(골드) 누르고 받기";
            }
            else if (_rewards % 3 == 0)
            {
                Click(Find<VisualElement>(RunUi, cards[0]));
                yield return null;
                confirm = Find<Button>(RunUi, "reward-confirm");
                Assert.IsTrue(confirm.enabledSelf, "후보를 골랐는데 받기 버튼이 꺼져 있다");
                StringAssert.EndsWith("받기", confirm.text, "후보를 골랐는데 받기 버튼 글이 이상하다");
                yield return null;
                // 고른 후보는 올라오고 (22) 상세 카드로 보인다
                var chosen = Find<VisualElement>(RunUi, cards[0]);
                Assert.Less(chosen.resolvedStyle.translate.y, -10f, "고른 후보가 올라오지 않았다");
                Assert.IsNotNull(chosen.Q("card-detail-face"), "보상 후보가 상세 카드로 보이지 않는다");
                yield return Shot(RunUi, "10-reward");
                Click(confirm);
                how = "카드 누르고 받기";
            }
            else if (_rewards % 3 == 2)
            {
                // 키보드: 좌우로 후보를 고르고 결정 키로 받는다
                yield return Tap(UnityEngine.InputSystem.Key.D);
                confirm = Find<Button>(RunUi, "reward-confirm");
                Assert.IsTrue(confirm != null && confirm.enabledSelf, "D를 눌렀는데 후보가 골라지지 않았다");
                string first = confirm.text;
                yield return Tap(UnityEngine.InputSystem.Key.D);
                if (cards.Count + golds.Count > 1)
                    Assert.AreNotEqual(first, Find<Button>(RunUi, "reward-confirm")?.text, "D를 한 번 더 눌렀는데 다음 후보로 가지 않았다");
                gold = Find<Button>(RunUi, "reward-confirm").text.StartsWith("골드");
                yield return Tap(UnityEngine.InputSystem.Key.J);
                how = "키보드로 고르고 받기";
            }
            else
            {
                DoubleClick(Find<VisualElement>(RunUi, cards[cards.Count - 1]));
                how = "카드 두 번 누르기";
            }
            yield return null;
            // 보스 층에서 봉인된 카드가 없으면 보스 보상으로 승리 보상 창이 한 번 더 뜬다 (제목이 "보스 보상"으로 시작).
            var again = Find<VisualElement>(RunUi, "reward");
            bool bonus = again != null && again.Query<Label>().ToList().Any(l => l.text.StartsWith("보스 보상"));
            Assert.IsTrue(again == null || bonus, $"보상을 골랐는데({how}) 보상 창이 닫히지 않았다");
            if (bonus)
                yield return Shot(RunUi, "10c-reward-bonus");
            Assert.AreEqual(sideBefore + (gold ? 0 : 1), SavedCards("side").Count, $"보상({how}) 뒤 사이드 덱 장수");
            var notice = Find<Label>(RunUi, "move-notice");
            Assert.IsNotNull(notice, "보상을 고른 뒤 층 사이 화면에 알림 줄이 없다");
            StringAssert.StartsWith(gold ? "보상으로 골드" : "획득", notice.text, $"보상({how}) 뒤 알림");
            if (gold)
                _goldChosen = true;
            _rewards++;
            Debug.Log($"[런 화면 시험] 승리 보상: 카드 {cards.Count}자리, 빈 자리 {golds.Count}곳 → {how}: {notice.text}");
        }

        /// <summary>
        /// 덱 편집의 컬렉션 검색과 정렬: 띄어쓰기를 빼고 카드명 검색, 효과문까지 검색, 없는 카드, 정렬 바꾸기.
        /// 끝나면 검색어를 비우고 기본 정렬로 되돌린다.
        /// </summary>
        IEnumerator CollectionSearchAndSort()
        {
            const string blackMagician = "pick-46986414";
            List<string> Shown() => Named("pick-").Select(e => e.name).ToList();
            string CountText() => Find<Label>(RunUi, "collection-count")?.text ?? "";
            var all = Shown();
            var search = Find<TextField>(RunUi, "collection-search");
            Assert.IsNotNull(search, "컬렉션 검색 칸이 없다");
            Assert.Contains(blackMagician, all, "시험 전제: 컬렉션에 블랙 매지션이 있어야 한다");

            search.value = "블랙매지션";
            yield return null;
            var found = Shown();
            Assert.Contains(blackMagician, found, "띄어쓰기를 빼고 검색했는데 블랙 매지션이 없다");
            Assert.Less(found.Count, all.Count, "검색해도 카드가 줄지 않았다");
            StringAssert.Contains("찾은 카드", CountText(), "검색 결과 장수 표시가 없다");
            Assert.AreSame(search, Find<TextField>(RunUi, "collection-search"), "글자를 칠 때 검색 칸이 다시 만들어졌다 (입력 포커스가 사라진다)");
            yield return Shot(RunUi, "1b-editor-search");

            search.value = "공격력";
            yield return null;
            int nameOnly = Shown().Count;
            Click(Find<Button>(RunUi, "search-scope"));
            yield return null;
            int withText = Shown().Count;
            Assert.Greater(withText, nameOnly, "효과문까지 검색해도 결과가 늘지 않았다");
            Click(Find<Button>(RunUi, "search-scope"));
            yield return null;
            Debug.Log($"[런 화면 시험] 「공격력」 검색: 카드명만 {nameOnly}종, 효과문 포함 {withText}종");

            search.value = "없는카드이름zz";
            yield return null;
            Assert.AreEqual(0, Shown().Count, "없는 카드를 검색했는데 카드가 보인다");
            Assert.IsTrue(Find<VisualElement>(RunUi, "editor-collection").Query<Label>().ToList().Any(l => l.text == "찾는 카드가 없다."), "검색 결과가 없다는 표시가 없다");

            search.value = "";
            yield return null;
            CollectionAssert.AreEqual(all, Shown(), "검색어를 지웠는데 처음 목록으로 돌아오지 않았다");

            Click(Find<Button>(RunUi, "sort-name"));
            yield return null;
            var byName = Shown();
            CollectionAssert.AreEquivalent(all, byName, "정렬을 바꿨는데 카드 종류가 달라졌다");
            CollectionAssert.AreNotEqual(all, byName, "이름 순으로 바꿨는데 순서가 그대로다");
            Click(Find<Button>(RunUi, "sort-attack"));
            yield return null;
            StringAssert.Contains("공격력 높은 순", CountText(), "공격력 정렬 표시가 없다");
            Click(Find<Button>(RunUi, "sort-default"));
            yield return null;
            CollectionAssert.AreEqual(all, Shown(), "기본 정렬로 되돌렸는데 처음 순서가 아니다");
        }

        /// <summary>
        /// 교환 (한 번만): 사이드 덱 카드를 덱에 넣고 다시 뺀다. 버튼으로 한 번, 두 번 누르기로 한 번, 끌어다 놓기로 한 번.
        /// 그리고 메인 덱 카드 하나를 두 번 눌러 본다 (메인 덱이 최소 장수면 옮길 수 없다는 알림, 아니면 옮겼다가 되돌림).
        /// </summary>
        IEnumerator Exchange()
        {
            if (_exchanged || !IsHub() || !NoModal() || SavedCards("side").Count == 0)
                yield break;
            // 덱 창을 연다 (듀얼 창이 떠 있으면 "덱 조정"으로). 덱 창은 고른 탭의 카드만 보여 주므로 장수는 저장 파일로 본다.
            yield return OpenDeck("side");
            var side = Named("card-side-").FirstOrDefault();
            Assert.IsNotNull(side, "사이드 덱에 카드가 있는데 덱 창의 사이드 탭이 비어 있다");
            string code = side.name.Substring("card-side-".Length);
            int sideStart = SideCount(code), deckStart = DeckCount(code);
            ParkPointer();
            yield return null;
            void AssertMoved(bool inDeck, string how)
            {
                Assert.AreEqual(inDeck ? sideStart - 1 : sideStart, SideCount(code), $"{how}: 사이드 덱 장수");
                Assert.AreEqual(inDeck ? deckStart + 1 : deckStart, DeckCount(code), $"{how}: 메인·엑스트라 덱 장수");
                Assert.AreEqual(SideCount(code), CountNamed("card-side-" + code), $"{how}: 사이드 탭에 보이는 장수가 저장 파일과 다르다");
            }
            Click(side);
            yield return null;
            var toDeck = Find<Button>(RunUi, "move-to-deck");
            if (toDeck == null || !toDeck.enabledSelf)
            {
                yield return CloseDeck();
                yield break;
            }
            Click(toDeck);
            yield return null;
            AssertMoved(true, "버튼으로 덱에 넣기");
            string deckTab = SavedCards("extra").Contains(code) ? "extra" : "main";
            // 옮긴 카드는 사이드 탭에서 빠지고, 카드 정보는 그 카드를 계속 보여 준다 (되돌리는 버튼)
            var toSide = Find<Button>(RunUi, "move-to-side");
            Assert.IsNotNull(toSide, "덱에 넣은 카드에 사이드 덱으로 빼기 버튼이 없다");
            Click(toSide);
            yield return null;
            AssertMoved(false, "버튼으로 사이드 덱으로 빼기");

            // 두 번 누르기
            DoubleClick(Find<VisualElement>(RunUi, "card-side-" + code));
            yield return null;
            AssertMoved(true, "두 번 눌러 덱에 넣기");
            StringAssert.Contains("→", Find<Label>(RunUi, "move-notice").text, "옮긴 결과 알림이 없다");
            yield return OpenDeck(deckTab);
            DoubleClick(InDeck(code));
            yield return null;
            yield return OpenDeck("side");
            AssertMoved(false, "두 번 눌러 사이드 덱으로 빼기");

            // 끌어다 놓기: 사이드 탭의 카드를 덱 탭에, 덱 탭의 카드를 사이드 탭에
            Drag(Find<VisualElement>(RunUi, "card-side-" + code), Find<VisualElement>(RunUi, "zone-" + deckTab));
            yield return null;
            AssertMoved(true, "덱 탭으로 끌어다 놓기");
            yield return OpenDeck(deckTab);
            Drag(InDeck(code), Find<VisualElement>(RunUi, "zone-side"));
            yield return null;
            yield return OpenDeck("side");
            AssertMoved(false, "사이드 탭으로 끌어다 놓기");

            // 메인 덱 카드 두 번 누르기: 옮길 수 없으면 알림, 옮겼으면 되돌린다
            yield return OpenDeck("main");
            var main = Named("card-main-").FirstOrDefault();
            Assert.IsNotNull(main, "덱 창의 메인 탭에 카드가 없다");
            string mainCode = main.name.Substring("card-main-".Length);
            int mainSideBefore = SideCount(mainCode), mainDeckBefore = DeckCount(mainCode);
            DoubleClick(main);
            yield return null;
            var notice = Find<Label>(RunUi, "move-notice");
            Assert.AreEqual(DisplayStyle.Flex, notice.style.display.value, "옮기기 알림이 보이지 않는다");
            if (SideCount(mainCode) > mainSideBefore)
            {
                yield return OpenDeck("side");
                DoubleClick(Find<VisualElement>(RunUi, "card-side-" + mainCode));
                yield return null;
                Assert.AreEqual(mainDeckBefore, DeckCount(mainCode), "사이드 덱으로 뺐다가 되돌린 메인 덱 카드 장수");
            }
            else
            {
                StringAssert.StartsWith("옮길 수 없습니다", notice.text, "옮기지 못했는데 까닭 알림이 없다");
                Assert.AreEqual(mainDeckBefore, DeckCount(mainCode), "옮길 수 없다는데 메인 덱 장수가 바뀌었다");
            }
            Debug.Log($"[런 화면 시험] 메인 덱 카드 두 번 누르기: {notice.text}");
            yield return Shot(RunUi, "4-hub-moved");
            yield return CloseDeck();

            _exchanged = true;
            Debug.Log($"[런 화면 시험] 교환: {code} 사이드 덱 → 덱 → 사이드 덱 (버튼, 두 번 누르기, 끌어다 놓기)");
        }

        /// <summary>
        /// 듀얼 도중 (한 번): 자동 진행을 멈추고, 내 필드(없으면 손패)의 카드에 마우스를 올려 왼쪽 카드 정보에 카드가 나오는지,
        /// 내 묘지를 눌러 카드 목록 창이 열리는지 본다. 그 뒤 루프가 "자동으로 끝까지"를 다시 누른다.
        /// </summary>
        IEnumerator CheckDuelView()
        {
            var stop = Find<Button>(DuelUi, "auto-stop");
            if (stop != null)
            {
                Click(stop);
                yield return null;
            }
            if (Find<Button>(DuelUi, "leave-duel") != null)
                yield break; // 그 사이에 끝났다. 다음 듀얼에서 다시 본다.
            _duelViewChecked = true;
            var root = Root(DuelUi);
            // 앞면 카드: 내 필드(묘지·제외 더미의 맨 위 카드도 앞면이다)와 손패. 그중 필드 칸·손패 칸에 든 것을 쓴다.
            var faces = new[] { "my-field", "my-hand" }.SelectMany(n => root.Q(n)?.Query("card-face").ToList() ?? new List<VisualElement>()).ToList();
            Assert.IsNotEmpty(faces, "6턴인데 내 필드와 손패에 앞면 카드가 없다");
            // 내 자리: 선공이면 0, 후공이면 1 (내 필드는 늘 아래 "my-field").
            string me = root.Q("my-field").Query<VisualElement>().ToList().Any(e => e.name != null && e.name.StartsWith("zone-1-")) ? "1" : "0";
            var chain = new List<string>();
            VisualElement CellOf(VisualElement face, bool record)
            {
                var at = face.parent;
                while (at != null && (at.name == null || !(at.name.StartsWith($"zone-{me}-") || at.name.StartsWith($"hand-{me}-"))))
                {
                    if (record)
                        chain.Add(string.IsNullOrEmpty(at.name) ? "?" : at.name);
                    at = at.parent;
                }
                return at;
            }
            var cell = faces.Select(f => CellOf(f, false)).FirstOrDefault(c => c != null);
            if (cell == null)
                CellOf(faces[0], true);
            Assert.IsNotNull(cell, $"카드 앞면이 필드 칸·손패 칸 안에 있지 않다 (나 = {me}, 앞면 {faces.Count}장, 첫 장 위로: {string.Join(" < ", chain)})");
            Hover(cell);
            yield return null;
            Assert.IsNotNull(root.Q("card-detail")?.Q("card-detail-face"), $"{cell.name}에 마우스를 올렸는데 왼쪽 상세 카드가 없다");
            yield return Shot(DuelUi, "2b-duel-mid");

            // 점대칭: 내 쪽과 상대 쪽의 같은 칸은 필드 중심에 대해 마주 본다 (칸마다 두 칸의 가운데가 같은 점이어야 한다).
            string opp = me == "0" ? "1" : "0";
            Vector2 Middle(string mine, string theirs)
            {
                var a = root.Q(mine);
                var b = root.Q(theirs);
                Assert.IsTrue(a != null && b != null, $"{mine} 또는 {theirs} 칸이 없다");
                return (a.worldBound.center + b.worldBound.center) / 2;
            }
            var center = Middle($"zone-{me}-m2", $"zone-{opp}-m2");
            foreach (string zone in new[] { "zone-{0}-m0", "zone-{0}-s4", "zone-{0}-field", "extra-{0}", "grave-{0}", "deck-{0}", "removed-{0}" })
            {
                var middle = Middle(string.Format(zone, me), string.Format(zone, opp));
                Assert.Less((middle - center).magnitude, 1.5f, $"{string.Format(zone, me)}: 상대 쪽 칸과 점대칭이 아니다 (가운데 {middle}, 필드 중심 {center})");
            }
            Assert.Greater(root.Q($"zone-{me}-m2").worldBound.center.y, center.y, "내 몬스터 존이 필드 중심보다 위에 있다");
            Assert.Greater(root.Q($"removed-{me}").worldBound.center.x, center.x, "내 제외 칸이 오른쪽 끝에 있지 않다");

            // 로그 서랍: "로그 전체"로 열고, 같은 자리의 "로그 닫기"로 닫는다 (마우스를 옮기지 않고 다시 누를 수 있어야 한다).
            var drawer = Find<VisualElement>(DuelUi, "log-drawer");
            var openLog = Find<Button>(DuelUi, "open-log");
            var closeLog = Find<Button>(DuelUi, "close-log");
            Assert.IsTrue(drawer != null && openLog != null && closeLog != null, "로그 서랍이나 여닫는 버튼이 없다");
            Assert.AreEqual(DisplayStyle.None, drawer.style.display.value, "로그 서랍이 처음부터 열려 있다");
            Click(openLog);
            yield return WaitFor(() => drawer.style.display.value == DisplayStyle.Flex
                                       && (closeLog.worldBound.center - openLog.worldBound.center).magnitude < 2f, 5, "로그 서랍이 열려 닫기 버튼이 여는 버튼 자리에 오기");
            Assert.Greater(drawer.Q<ScrollView>().contentContainer.childCount, 0, "로그 서랍에 로그가 없다");
            yield return Shot(DuelUi, "2q-log-drawer");
            Click(closeLog);
            yield return WaitFor(() => drawer.style.display.value == DisplayStyle.None, 5, "로그 서랍 닫힘");

            yield return DuelKeyboard(root, me);

            Click(Find<VisualElement>(DuelUi, "grave-" + me));
            yield return null;
            var close = Find<Button>(DuelUi, "modal-close");
            Assert.IsNotNull(close, "묘지를 눌렀는데 카드 목록 창이 없다");
            yield return Shot(DuelUi, "2c-duel-grave");
            Click(close);
            yield return null;
            Assert.IsNull(Find<Button>(DuelUi, "modal-close"), "닫기를 눌렀는데 카드 목록 창이 남아 있다");
            Debug.Log($"[런 화면 시험] 듀얼 화면: {cell.name}에 마우스 올리기 → 카드 정보, 묘지 누르기 → 카드 목록 창");
        }

        /// <summary>
        /// 듀얼의 키보드 커서 (가상 키보드): 첫 키는 커서를 켜고(금색 테), W로 패 → 내 마법·함정 줄 → 내 몬스터 줄로 올라간다.
        /// F는 패와 필드 사이를 건너뛰고, 오른쪽 끝 열의 내 묘지에서 결정 키를 누르면 카드 목록 창이 열리고 취소 키로 닫힌다.
        /// 키 표시는 마지막에 쓴 이동 키의 자리(W A S D면 J·K, 방향키면 Z·X)를 따른다.
        /// </summary>
        IEnumerator DuelKeyboard(VisualElement root, string me)
        {
            // 키를 누른 뒤 두 프레임을 더 기다린다: 패의 카드는 올라오며 자리가 바뀌고, 금색 테는 그 다음 프레임에 따라간다.
            IEnumerator PressKey(UnityEngine.InputSystem.Key key)
            {
                yield return Tap(key);
                yield return null;
                yield return null;
            }
            var ring = Find<VisualElement>(DuelUi, "key-ring");
            Assert.IsNotNull(ring, "키보드 커서의 금색 테가 없다");
            Assert.AreEqual(DisplayStyle.None, ring.style.display.value, "키를 누르기 전인데 키보드 커서가 보인다");
            // 금색 테가 두른 것: 가운데가 테의 가운데와 같은 칸·패 카드·더미의 이름
            string Under()
            {
                if (ring.style.display.value != DisplayStyle.Flex)
                    return "(없음)";
                Vector2 at = ring.worldBound.center;
                var best = root.Query<VisualElement>().Where(e => e.name != null && (e.name.StartsWith("zone-") || e.name.StartsWith("hand-")
                        || e.name.StartsWith("extra-") || e.name.StartsWith("grave-") || e.name.StartsWith("deck-") || e.name.StartsWith("removed-")))
                    .ToList().OrderBy(e => (e.worldBound.center - at).magnitude).FirstOrDefault();
                return best != null && (best.worldBound.center - at).magnitude < 3f ? best.name : "(칸이 아님)";
            }
            bool InRow(string name, params string[] prefixes) => prefixes.Any(name.StartsWith);
            string[] spellRow = { $"zone-{me}-s", $"extra-{me}", $"deck-{me}" };
            string[] monsterRow = { $"zone-{me}-m", $"zone-{me}-field", $"grave-{me}" };
            string CapsText() => string.Join(" ", root.Query("key-hints").ToList().SelectMany(h => h.Query<Label>().ToList()).Select(l => l.text));

            yield return PressKey(UnityEngine.InputSystem.Key.W);
            string start = Under();
            Assert.AreNotEqual("(없음)", start, "이동 키를 눌렀는데 키보드 커서가 켜지지 않았다");
            StringAssert.Contains("W A S D", CapsText(), "W A S D를 썼는데 키 표시가 바뀌지 않았다");
            StringAssert.Contains("J", CapsText(), "W A S D를 썼는데 결정 키 표시가 J가 아니다");
            bool fromHand = start.StartsWith($"hand-{me}-");
            if (fromHand)
            {
                Assert.Greater(root.Q(start).resolvedStyle.scale.value.x, 1.5f, "키보드로 가리킨 패의 카드가 올라오며 커지지 않았다");
                yield return PressKey(UnityEngine.InputSystem.Key.W);
                Assert.IsTrue(InRow(Under(), spellRow), $"패에서 위로 갔는데 내 마법·함정 줄이 아니다: {Under()}");
            }
            else
                Assert.IsTrue(InRow(start, monsterRow) || InRow(start, spellRow), $"패가 없을 때 커서가 내 필드에서 시작하지 않았다: {start}");
            if (InRow(Under(), spellRow))
                yield return PressKey(UnityEngine.InputSystem.Key.W);
            string onMonsterRow = Under();
            Assert.IsTrue(InRow(onMonsterRow, monsterRow), $"위로 갔는데 내 몬스터 줄이 아니다: {onMonsterRow}");
            yield return Shot(DuelUi, "2r-key-cursor");
            if (fromHand)
            {
                yield return PressKey(UnityEngine.InputSystem.Key.F);
                StringAssert.StartsWith($"hand-{me}-", Under(), "F를 눌렀는데 패로 건너뛰지 않았다");
                yield return PressKey(UnityEngine.InputSystem.Key.F);
                Assert.AreEqual(onMonsterRow, Under(), "F를 다시 눌렀는데 있던 필드 칸으로 돌아오지 않았다");
            }
            // 오른쪽 끝 열(내 묘지)까지 가서, 끝에서 더 눌러도 그대로인지
            for (int i = 0; i < 8 && Under() != $"grave-{me}"; i++)
                yield return PressKey(UnityEngine.InputSystem.Key.RightArrow);
            Assert.AreEqual($"grave-{me}", Under(), "오른쪽 끝까지 갔는데 내 묘지 칸이 아니다");
            StringAssert.Contains("Z", CapsText(), "방향키를 썼는데 결정 키 표시가 Z로 돌아오지 않았다");
            yield return PressKey(UnityEngine.InputSystem.Key.RightArrow);
            Assert.AreEqual($"grave-{me}", Under(), "오른쪽 끝에서 더 눌렀는데 커서가 움직였다");
            // 결정 키: 묘지의 카드 목록 창 (진행을 보여 주는 중이면 결정 키는 단계를 넘기는 데 쓰이므로 그때는 넘어간다)
            yield return PressKey(UnityEngine.InputSystem.Key.Z);
            if (Find<Button>(DuelUi, "modal-close") != null)
            {
                yield return PressKey(UnityEngine.InputSystem.Key.DownArrow);
                Assert.AreEqual(DisplayStyle.Flex, ring.style.display.value, "카드 목록 창에서 키보드 커서가 보이지 않는다");
                yield return PressKey(UnityEngine.InputSystem.Key.X);
                Assert.IsNull(Find<Button>(DuelUi, "modal-close"), "취소 키를 눌렀는데 카드 목록 창이 닫히지 않았다");
                Debug.Log("[런 화면 시험] 듀얼 키보드: 커서 이동, F, 묘지 창 열기(Z)·닫기(X)");
            }
            else
                Debug.Log("[런 화면 시험] 듀얼 키보드: 커서 이동, F (진행을 보여 주는 중이라 결정 키로 묘지 창은 열지 않음)");
        }

        /// <summary>
        /// 한 단계씩 보여 주기: "자동"(한 번)으로 답하면 멈추며 진행한다. 멈춘 단계(가운데 줄)가 보이는지 보고,
        /// 상대의 공격 선언과 그 뒤 데미지 계산 단계를 찾아 찍는다. 그 사이 상대가 공격하지 않으면 다음 듀얼에서 다시 해 본다
        /// (MaxPlaybackTries번까지, 못 보면 실패로 하지 않고 기록만 남긴다).
        /// </summary>
        IEnumerator CheckPlayback()
        {
            _playbackTries++;
            var stop = Find<Button>(DuelUi, "auto-stop");
            if (stop != null)
            {
                Click(stop);
                yield return null;
            }
            float until = Time.realtimeSinceStartup + PlaybackTimeout;
            int steps = 0;
            bool attack = false, battle = false;
            string last = null;
            while (!(battle && PlaybackAllSeen && (!Thorough || RareSeen)) && Time.realtimeSinceStartup < until && Find<Button>(DuelUi, "leave-duel") == null)
            {
                // 내가 공격 대상을 고를 때: 후보 몬스터 위의 예상 결과
                var note = Root(DuelUi)?.Query<Label>("attack-preview").Where(l => l.style.display.value == DisplayStyle.Flex && !string.IsNullOrEmpty(l.text)).First();
                if (!_previewSeen && note != null)
                {
                    _previewSeen = true;
                    Debug.Log($"[런 화면 시험] 공격 대상 예상 결과: {note.text.Replace('\n', ' ')}");
                    yield return Shot(DuelUi, "2g-attack-preview");
                }
                if (Find<Button>(DuelUi, "skip-playback") != null)
                {
                    string line = Find<Label>(DuelUi, "prompt-label")?.text ?? "";
                    if (line != "진행 중..." && line != last)
                    {
                        last = line;
                        steps++;
                        if (steps == 1)
                            yield return Shot(DuelUi, "2d-duel-step");
                        if (!attack && line.StartsWith("상대:") && line.Contains("공격"))
                        {
                            attack = true;
                            // 연출: 공격하는 카드에서 대상으로 뻗는 화살이 연출 층에 있어야 한다
                            Assert.Greater(Find<VisualElement>(DuelUi, "fx-layer")?.childCount ?? 0, 0, "상대가 공격을 선언했는데 연출(화살)이 없다");
                            yield return Shot(DuelUi, "2e-duel-attack");
                        }
                        else if (attack && line.StartsWith("데미지 계산"))
                        {
                            battle = true;
                            yield return Shot(DuelUi, "2f-duel-battle");
                        }
                    }
                }
                // LP가 바뀌는 단계: LP 옆에 바뀐 양 (−1450 / +1000)
                if (!_lpSeen)
                {
                    var delta = new[] { "lp-delta-0", "lp-delta-1" }.Select(n => Find<Label>(DuelUi, n))
                        .FirstOrDefault(l => l != null && !string.IsNullOrEmpty(l.text));
                    if (delta != null)
                    {
                        _lpSeen = true;
                        StringAssert.IsMatch("^[+−][0-9]+$", delta.text, "LP 옆 바뀐 양 표시가 이상하다");
                        // 연출: 같은 숫자가 연출 층에 크게 떠오른다
                        Assert.IsTrue(Find<VisualElement>(DuelUi, "fx-layer").Query<Label>().ToList().Any(l => l.text == delta.text),
                            $"LP가 바뀌었는데 떠오르는 숫자({delta.text})가 없다");
                        Debug.Log($"[런 화면 시험] LP 변화: {delta.name} {delta.text}");
                        yield return Shot(DuelUi, "2l-lp-delta");
                    }
                }
                // 체인이 쌓이면 로그 위 체인 목록 (체인이 생길 때만)
                var chainPanel = Find<VisualElement>(DuelUi, "chain-panel");
                if (!_chainSeen && chainPanel != null && chainPanel.style.display.value == DisplayStyle.Flex && chainPanel.Q("chain-1") != null)
                {
                    _chainSeen = true;
                    // 체인 상자: 필드 한가운데에 뜨고, 체인에 오른 카드가 번호를 달고 들어 있다
                    var chainBox = Find<VisualElement>(DuelUi, "chain-box");
                    Assert.IsTrue(chainBox != null && chainBox.style.display.value == DisplayStyle.Flex, "체인이 쌓였는데 필드 한가운데의 체인 상자가 없다");
                    Assert.IsNotNull(chainBox.Q("chain-card-1"), "체인 상자에 체인 1의 카드가 없다");
                    Debug.Log($"[런 화면 시험] 체인 목록: {string.Join(" / ", chainPanel.Query<Label>().ToList().Select(l => l.text))}");
                    yield return Shot(DuelUi, "2k-chain");
                }
                yield return CheckChainPanel();
                // 선택 창이 뜨면 (뜰 때만): Esc → 창을 닫고 "필드 확인 중", Esc → 창이 다시 열림, "필드 보기" → "필드 확인 중"
                if (!_fieldViewSeen && Find<Button>(DuelUi, "field-view") != null)
                {
                    yield return Tap(UnityEngine.InputSystem.Key.Escape);
                    StringAssert.StartsWith("필드 확인 중", BadgeText(), "선택 창에서 Esc를 눌렀는데 모서리 표시가 다르다");
                    Assert.IsNull(Find<Button>(DuelUi, "field-view"), "Esc를 눌렀는데 선택 창이 닫히지 않았다");
                    yield return Shot(DuelUi, "2h-field-view");
                    yield return Tap(UnityEngine.InputSystem.Key.Escape);
                    var fieldView = Find<Button>(DuelUi, "field-view");
                    Assert.IsNotNull(fieldView, "필드 확인 중 Esc를 눌렀는데 선택 창이 다시 열리지 않았다");
                    Click(fieldView);
                    yield return null;
                    StringAssert.StartsWith("필드 확인 중", BadgeText(), "필드 보기를 눌렀는데 모서리 표시가 다르다");
                    _fieldViewSeen = true;
                    Debug.Log("[런 화면 시험] 선택 창: Esc → 필드 확인 중, Esc → 창 다시 열림, 필드 보기 → 필드 확인 중");
                }
                // 상대 공격을 본 뒤: 건너뛰기 → 모서리에 "건너뛰는 중"
                var skip = Find<Button>(DuelUi, "skip-playback");
                if (battle && !_skipSeen && skip != null)
                {
                    Click(skip);
                    var badge = Find<Label>(DuelUi, "status-badge");
                    StringAssert.StartsWith("건너뜀", badge?.text ?? "", "건너뛰기를 눌렀는데 모서리 표시가 다르다");
                    _skipSeen = true;
                    yield return Shot(DuelUi, "2i-skip");
                    // 건너뛰기가 끝나면 표시가 흐려지며 사라진다 (바로 사라지지 않는다).
                    bool faded = false;
                    float fadeUntil = Time.realtimeSinceStartup + 5;
                    while (badge.style.display.value == DisplayStyle.Flex && Time.realtimeSinceStartup < fadeUntil)
                    {
                        float alpha = badge.style.opacity.value;
                        if (!faded && alpha > 0.1f && alpha < 0.9f)
                        {
                            faded = true;
                            yield return Shot(DuelUi, "2j-skip-fade");
                        }
                        yield return null;
                    }
                    Assert.IsTrue(faded, "건너뜀 표시가 흐려지지 않고 사라졌다");
                    Assert.AreEqual(DisplayStyle.None, badge.style.display.value, "건너뜀 표시가 사라지지 않는다");
                }
                // 건너뛰기를 본 뒤: 보여 주는 중 스페이스를 누르고 있으면 모서리에 "가속 중"
                if (_skipSeen && !_speedSeen && Find<Button>(DuelUi, "skip-playback") != null)
                    yield return CheckSpeedUp();
                // 가속 중을 본 뒤: 보여 주는 중 엔터 → 모서리에 "건너뜀"
                if (_speedSeen && !_enterSeen && Find<Button>(DuelUi, "skip-playback") != null)
                {
                    yield return Tap(UnityEngine.InputSystem.Key.Enter);
                    if (BadgeText().StartsWith("건너뜀"))
                    {
                        _enterSeen = true;
                        Debug.Log("[런 화면 시험] 보여 주는 중 엔터 → 건너뜀");
                    }
                    else // 그 사이 보여 주기가 끝났으면 다음에 다시 본다
                        Assert.IsNull(Find<Button>(DuelUi, "skip-playback"), "보여 주는 중 엔터를 눌렀는데 모서리에 건너뜀이 안 뜬다");
                }
                var once = Find<Button>(DuelUi, "auto-once");
                if (once != null)
                    Click(once);
                yield return null;
            }
            _playbackDone = battle && PlaybackAllSeen && (!Thorough || RareSeen);
            Assert.Greater(steps, 0, "자동(한 번)으로 답했는데 멈춰 보여 주는 단계가 없다");
            Debug.Log($"[런 화면 시험] 한 단계씩 보여 주기 ({_playbackTries}번째): 멈춘 단계 {steps}개, 상대 공격 {(attack ? "봄" : "못 봄")}, 그 뒤 데미지 계산 {(battle ? "봄" : "못 봄")}"
                + $", 필드 확인 중 {(_fieldViewSeen ? "봄" : "못 봄")}, 가속 중 {(_speedSeen ? "봄" : "못 봄")}, LP 변화 {(_lpSeen ? "봄" : "못 봄")}"
                + $", 체인 2 이상 {(_chain2Seen ? "봄" : "못 봄")}, 상대 체인 {(_chainOpponentSeen ? "봄" : "못 봄")}");
        }

        static string BadgeText()
        {
            var badge = Find<Label>(DuelUi, "status-badge");
            return badge != null && badge.style.display.value == DisplayStyle.Flex ? badge.text : "";
        }

        /// <summary>체인 목록에 체인 2 이상, "처리 중", 상대가 발동한 줄이 생기면 찍는다 (생길 때만).</summary>
        IEnumerator CheckChainPanel()
        {
            var panel = Find<VisualElement>(DuelUi, "chain-panel");
            if (panel == null || panel.style.display.value != DisplayStyle.Flex)
                yield break;
            string Labels() => string.Join(" / ", panel.Query<Label>().ToList().Select(l => l.text));
            if (!_chain2Seen && panel.Q("chain-2") != null)
            {
                _chain2Seen = true;
                // 위에서부터 처리: 가장 나중 체인이 맨 위
                var numbers = panel.Query<VisualElement>().ToList().Where(e => e.name != null && Regex.IsMatch(e.name, @"^chain-\d+$"))
                    .Select(e => int.Parse(e.name.Substring(6))).ToList();
                Assert.IsTrue(numbers.Count >= 2 && numbers.SequenceEqual(numbers.OrderByDescending(n => n)),
                    $"체인 목록이 위에서부터 처리 순서(나중 체인이 위)가 아니다: {string.Join(", ", numbers)}");
                Debug.Log($"[런 화면 시험] 체인 2 이상: {Labels()}");
                yield return Shot(DuelUi, "2n-chain-2");
            }
            if (!_chainSolvingSeen && panel.Query<Label>().Where(l => l.text == "처리 중").First() != null)
            {
                _chainSolvingSeen = true;
                Debug.Log($"[런 화면 시험] 체인 처리 중: {Labels()}");
                yield return Shot(DuelUi, "2o-chain-solving");
            }
            if (!_chainOpponentSeen && panel.Query<Label>().Where(l => l.text.StartsWith("상대 ·")).First() != null)
            {
                _chainOpponentSeen = true;
                Debug.Log($"[런 화면 시험] 상대가 발동한 체인: {Labels()}");
                yield return Shot(DuelUi, "2p-chain-opponent");
            }
        }

        /// <summary>시험용 가상 키보드. 배치 모드는 창에 포커스가 없어 키 입력이 버려질 수 있어서, 쓰는 동안만 포커스를 무시하게 한다.</summary>
        sealed class VirtualKeyboard : IDisposable
        {
            readonly UnityEngine.InputSystem.InputSettings.BackgroundBehavior _background;
            readonly UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode _editor;
            public readonly UnityEngine.InputSystem.Keyboard Device;

            public VirtualKeyboard()
            {
                var settings = UnityEngine.InputSystem.InputSystem.settings;
                _background = settings.backgroundBehavior;
                _editor = settings.editorInputBehaviorInPlayMode;
                settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Device = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                Device.MakeCurrent();
            }

            /// <summary>이 키들만 눌린 상태로 둔다 (빈 목록이면 모두 뗀다).</summary>
            public void Hold(params UnityEngine.InputSystem.Key[] keys) =>
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(Device, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));

            public void Dispose()
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(Device);
                var settings = UnityEngine.InputSystem.InputSystem.settings;
                settings.backgroundBehavior = _background;
                settings.editorInputBehaviorInPlayMode = _editor;
            }
        }

        /// <summary>키를 한 번 눌렀다 뗀다 (누른 프레임에 화면이 wasPressedThisFrame으로 받는다).</summary>
        static IEnumerator Tap(UnityEngine.InputSystem.Key key)
        {
            using (var keys = new VirtualKeyboard())
            {
                keys.Hold(key);
                yield return null;
                keys.Hold();
                yield return null;
            }
        }

        /// <summary>
        /// 보여 주는 중 스페이스를 누르고 있으면 모서리에 "가속 중", 떼면 흐려지며 사라진다.
        /// 가상 키보드로 누른다. 그 사이 보여 주기가 끝나 버리면 다음에 다시 본다.
        /// </summary>
        IEnumerator CheckSpeedUp()
        {
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorBehavior = settings.editorInputBehaviorInPlayMode;
            // 배치 모드는 창에 포커스가 없어서 키 입력이 버려질 수 있다
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                keyboard.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space));
                Label badge = null;
                for (int i = 0; i < 30 && badge == null; i++)
                {
                    yield return null;
                    var b = Find<Label>(DuelUi, "status-badge");
                    if (b != null && b.style.display.value == DisplayStyle.Flex && b.text.StartsWith("가속 중"))
                        badge = b;
                }
                Assert.IsTrue(keyboard.spaceKey.isPressed, "가상 키보드의 스페이스 입력이 들어가지 않는다");
                if (badge == null)
                {
                    Assert.IsNull(Find<Button>(DuelUi, "skip-playback"), "보여 주는 중 스페이스를 누르고 있는데 모서리에 가속 중이 안 뜬다");
                    yield break; // 그 사이 보여 주기가 끝났다. 다음에 다시 본다.
                }
                _speedSeen = true;
                yield return Shot(DuelUi, "2m-speed-up");
                // 떼면 바로 사라지지 않고 흐려지며 사라진다
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                bool faded = false;
                float fadeUntil = Time.realtimeSinceStartup + 5;
                while (badge.style.display.value == DisplayStyle.Flex && badge.text.StartsWith("가속 중") && Time.realtimeSinceStartup < fadeUntil)
                {
                    float alpha = badge.style.opacity.value;
                    if (alpha > 0.1f && alpha < 0.9f)
                        faded = true;
                    yield return null;
                }
                Assert.IsTrue(faded, "가속 중 표시가 흐려지지 않고 사라졌다");
                Assert.IsFalse(badge.style.display.value == DisplayStyle.Flex && badge.text.StartsWith("가속 중"), "스페이스를 뗐는데 가속 중 표시가 사라지지 않는다");
                Debug.Log("[런 화면 시험] 스페이스 누르는 동안 가속 중 → 떼면 흐려지며 사라짐");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorBehavior;
            }
        }

        /// <summary>"6턴 · …" → 6. 듀얼 준비 중이거나 없으면 0.</summary>
        /// <summary>칩교환소 창(또는 사는 중인 창)을 닫는다. 사는 중이면 뒤로 한 번.</summary>
        static void CloseShopForStep() => Click(Find<Button>(RunUi, "shop-back") ?? Find<Button>(RunUi, "shop-close"));

        static int TurnOf(Label label)
        {
            if (label == null)
                return 0;
            int end = label.text.IndexOf('턴');
            return end > 0 && int.TryParse(label.text.Substring(0, end), out int turn) ? turn : 0;
        }

        /// <summary>마우스를 올린다 (PointerEnter를 직접 보낸다).</summary>
        static void Hover(VisualElement target)
        {
            var system = new Event { type = EventType.MouseMove, mousePosition = target.worldBound.center };
            using (var e = PointerEnterEvent.GetPooled(system))
            {
                e.target = target;
                target.SendEvent(e);
            }
        }

        static VisualElement InDeck(string code) =>
            Find<VisualElement>(RunUi, "card-main-" + code) ?? Find<VisualElement>(RunUi, "card-extra-" + code);

        /// <summary>화면에서 이 이름인 카드 장수 (같은 카드도 한 장씩 따로 놓인다).</summary>
        static int CountNamed(string name) => Named(name).Count(e => e.name == name);

        /// <summary>사이드 덱에 든 이 카드의 장수 (저장 파일 기준. 덱 창은 고른 탭의 카드만 보여 준다).</summary>
        int SideCount(string code) => SavedCards("side").Count(c => c == code);

        /// <summary>메인·엑스트라 덱에 든 이 카드의 장수 (저장 파일 기준).</summary>
        int DeckCount(string code) => SavedCards("main").Count(c => c == code) + SavedCards("extra").Count(c => c == code);

        /// <summary>카드를 두 번 누른다 (포인터 이벤트를 직접 보낸다).</summary>
        static void DoubleClick(VisualElement target)
        {
            Assert.IsNotNull(target, "두 번 누를 카드가 없다");
            Vector2 at = target.worldBound.center;
            for (int clicks = 1; clicks <= 2; clicks++)
            {
                SendPointer(target, EventType.MouseDown, at, clicks);
                SendPointer(target, EventType.MouseUp, at, clicks);
            }
        }

        /// <summary>카드를 끌어 to 위에 놓는다.</summary>
        static void Drag(VisualElement card, VisualElement to)
        {
            Assert.IsNotNull(card, "끌 카드가 없다");
            Assert.IsNotNull(to, "놓을 곳이 없다");
            Vector2 from = card.worldBound.center, end = to.worldBound.center;
            SendPointer(card, EventType.MouseDown, from, 1);
            SendPointer(card, EventType.MouseDrag, Vector2.Lerp(from, end, 0.5f), 1);
            SendPointer(card, EventType.MouseDrag, end, 1);
            SendPointer(card, EventType.MouseUp, end, 1);
        }

        /// <summary>
        /// 포인터를 화면 왼쪽 위 빈 곳(바깥 여백)으로 옮긴다. 화면을 다시 그리면 유니티가 마지막 포인터 위치의 요소에 마우스 올리기를 보내므로,
        /// 앞서 두 번 누른 자리에 새 화면의 카드가 오면 고른 카드 정보 대신 그 카드 미리 보기가 뜬다 (덱 편집 화면 뒤 층 사이 화면).
        /// </summary>
        static void ParkPointer()
        {
            var root = Root(RunUi);
            if (root != null)
                SendPointer(root, EventType.MouseMove, new Vector2(4, 4), 0);
        }

        static void SendPointer(VisualElement target, EventType type, Vector2 position, int clicks)
        {
            var system = new Event { type = type, mousePosition = position, clickCount = clicks, button = 0 };
            EventBase e = type == EventType.MouseDown ? PointerDownEvent.GetPooled(system)
                : type == EventType.MouseUp ? (EventBase)PointerUpEvent.GetPooled(system)
                : PointerMoveEvent.GetPooled(system);
            using (e)
            {
                e.target = target;
                target.SendEvent(e);
            }
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

        /// <summary>런 화면에서 이름이 prefix로 시작하는 요소들 (화면 순서). 화면이 숨겨져 있으면 빈 목록.</summary>
        static List<VisualElement> Named(string prefix) =>
            Root(RunUi)?.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(prefix)).ToList() ?? new List<VisualElement>();

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

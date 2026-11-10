// 플레이 모드 시험: Run 씬을 열고 화면의 버튼만 눌러 로그라이크 런을 처음부터 끝까지 간다.
// 덱 편집(레시피 고치기·저장·지우기) → 런 시작(레시피·시드) → [층 사이 화면 → 다음 듀얼 시작 → 듀얼 화면 "자동으로 끝까지" → "결과 보기" → (승리 보상 고르기·보충 고르기)] 반복
// → 런 종료 → 새 런. 듀얼은 사람 대신 임시 자동 응답(개발용 버튼)이 답하고, 상대는 WindBot이다.
// 저장도 본다 (저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다):
// - "처음 화면으로" → "이어하기", 씬을 다시 불러(게임을 다시 켠 것처럼) "이어하기"를 해도 층 사이 화면과 저장 파일이 그대로인지
// - 듀얼을 시작하면 듀얼 중 상태로 저장되고, 듀얼 도중에 씬을 다시 부르면 그 듀얼이 패배로 처리되며 알림이 한 번 나오는지
// - 런이 끝나면 진행 중인 런이 없어지고, 마지막으로 시작한 레시피(시작 사이드 덱 포함)가 다음 런에 그대로 골라져 있는지
// - 레시피를 저장·지우면 저장 파일에 바로 쓰이는지, 층 사이 화면의 "지금 덱을 레시피로"가 새 레시피를 만드는지
// 덱 편집: 시작 사이드 덱 3장(꽉 참)까지 넣기·빼기(버튼·두 번 누르기·끌어다 놓기), 덱 ↔ 사이드 덱 옮기기, 같은 카드명 4장째 막기,
// 컬렉션보다 많이 넣으면 저장은 되고 런 시작은 막히는지, 메인 덱 39장은 저장할 수 없는지, 저장하지 않고 나가기 확인, 복사·지우기·빈 새 레시피.
// - 진행 중인 런이 있을 때 새 런: 한 번 더 눌러야 포기하고, 취소할 수 있는지
// 층 사이 화면의 교환(사이드 덱 → 덱, 덱 → 사이드 덱)도 버튼·두 번 누르기·끌어다 놓기로 한 번씩 해 본다 (포인터 이벤트를 직접 보낸다).
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

            // ---- 덱 레시피: 새 게임에는 스타터 덱 4개 ----
            foreach (string id in new[] { "Yugi", "Kaiba", "Joey", "Pegasus" })
                Assert.IsNotNull(Find<Button>(RunUi, "recipe-" + id), $"새 게임에 스타터 덱 레시피 {id}가 없다");
            Click(Find<Button>(RunUi, "recipe-Yugi"));
            yield return null;
            StringAssert.Contains("시작할 수 있습니다", Find<Label>(RunUi, "start-recipe-status").text, "스타터 덱 레시피로 런을 시작할 수 없다고 나온다");

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
            StringAssert.Contains("시작 불가", Find<Button>(RunUi, "recipe-Yugi").text, "컬렉션보다 많은 레시피에 시작 불가 표시가 없다");
            var shortSlot = Find<VisualElement>(RunUi, "deck-main-" + once) ?? Find<VisualElement>(RunUi, "deck-extra-" + once);
            Assert.IsNotNull(shortSlot, "런 시작 화면의 레시피 보기에 컬렉션보다 많이 넣은 카드가 없다");
            Assert.IsTrue(shortSlot.Query<Label>().ToList().Any(l => l.text == "부족"), "런 시작 화면의 레시피 보기에 부족 표시가 없다");
            var startSide = Find<VisualElement>(RunUi, "deck-side-" + picked[0]);
            var startScroll = Find<VisualElement>(RunUi, "start-recipe").Q<ScrollView>();
            Assert.IsNotNull(startSide, "런 시작 화면의 레시피 보기에 시작 사이드 덱 카드가 없다");
            Assert.IsTrue(startSide.worldBound.yMin >= startScroll.worldBound.yMax - 1, "시작 사이드 덱이 스크롤 아래에 고정돼 있지 않다");
            Click(Find<Button>(RunUi, "start-run"));
            yield return null;
            Assert.IsFalse(IsHub(), "컬렉션보다 많은 레시피로 런이 시작됐다");
            StringAssert.Contains("런을 시작할 수 없습니다", Find<Label>(RunUi, "start-error").text, "런 시작을 막은 까닭 알림이 없다");
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
            StringAssert.Contains("시작할 수 있습니다", Find<Label>(RunUi, "start-recipe-status").text, "저장하지 않고 나갔는데 레시피가 바뀌었다");

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
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + code), $"런 시작 화면의 레시피 보기에 시작 사이드 덱 카드 {code}가 없다");
            Click(Named("deck-main-").First()); // 화면 확인용: 카드 하나를 고른 상태
            yield return null;
            yield return Shot(RunUi, "1-start");

            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "card-side-" + code), $"시작 사이드 덱 카드 {code}가 층 사이 화면 사이드 덱에 없다");
            Assert.IsTrue(File.Exists(SaveFile), "런을 시작했는데 저장 파일이 없다");

            // 런 로그 창
            Click(Find<Button>(RunUi, "open-log"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "run-events"), "런 로그 창이 열리지 않았다");
            yield return Shot(RunUi, "9-log");
            Click(Find<Button>(RunUi, "close-log"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "run-events"), "런 로그 창이 닫히지 않았다");

            yield return Exchange();

            // ---- 지금 덱을 레시피로 저장 ----
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
            StringAssert.Contains("시험 런 덱", Find<Button>(RunUi, "recipe-deck-1")?.text ?? "", "지금 덱으로 저장한 레시피가 런 시작 화면 목록에 없다");
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
            Click(Find<Button>(RunUi, "next-duel"));
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
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "deck-side-" + code), $"레시피의 시작 사이드 덱 카드 {code}가 남아 있지 않다");

            // ---- 진행 중인 런이 있을 때 새 런: 취소, 그리고 포기 ----
            SetSeed(2);
            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(IsHub, 10, "층 사이 화면");
            yield return Step(2);
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
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
            StringAssert.Contains("칩 0", Find<Label>(RunUi, "run-summary").text, "런 요약 줄에 칩이 없다");

            yield return EditSave(text =>
            {
                text = SetLine(text, "floor", "5");
                text = SetLine(text, "floor-kind", "ChipExchange");
                text = SetLine(text, "chips", "400 400\nshop SwapSameRarity:30:0 SwapSameRarityChosen:50:0 SwapAny:50:0 SwapAnyChosen:80:0 Unseal:150:0");
                text = Regex.Replace(text, @"\nfloor-event [^\n]*", "");
                var main = Regex.Match(text, @"\nmain ([^\n]*)\n").Groups[1].Value.Split(' ');
                text = SetLine(text, "main", string.Join(" ", main.Skip(3)));
                return SetLine(text, "sealed-main", string.Join(" ", main.Take(3)));
            });
            Assert.AreEqual("5층 · 칩교환소", Find<Label>(RunUi, "floor-label")?.text, "5층 칩교환소가 아니다");
            Assert.IsNull(Find<Button>(RunUi, "next-duel"), "칩교환소에 다음 듀얼 버튼이 있다");
            Assert.IsNotNull(Find<Button>(RunUi, "leave-floor"), "칩교환소에 다음 층으로 버튼이 없다");
            StringAssert.Contains("봉인 3장", HubText(), "봉인 3장이 보이지 않는다");
            StringAssert.Contains("칩 400", Find<Label>(RunUi, "tower-chips")?.text ?? "", "탑 아래에 칩이 없다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 층에 왔는데 칩교환소 창이 저절로 뜨지 않았다");
            Click(Find<Button>(RunUi, "shop-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 닫히지 않았다");
            yield return Shot(RunUi, "11-tower");

            Click(Find<Button>(RunUi, "open-shop"));
            yield return null;
            Assert.IsNotNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 열리지 않았다");
            Assert.AreEqual("보유 칩 400", Find<Label>(RunUi, "shop-chips")?.text, "칩교환소의 보유 칩");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "shop-item-card"), "칩교환소에 아이템 카드 자리가 없다");
            foreach (string product in new[] { "SwapSameRarity", "SwapSameRarityChosen", "SwapAny", "SwapAnyChosen", "Unseal" })
                Assert.IsTrue(Find<Button>(RunUi, "shop-buy-" + product)?.enabledSelf == true, $"칩 400인데 {product}를 살 수 없다");
            yield return Shot(RunUi, "12-shop");

            yield return BuyInShop("SwapSameRarity", chooseSeal: false);
            Assert.AreEqual("보유 칩 370", Find<Label>(RunUi, "shop-chips")?.text, "같은 레어도 교체(30) 뒤 칩");
            var sold = Find<Button>(RunUi, "shop-buy-SwapSameRarity");
            Assert.IsTrue(sold != null && !sold.enabledSelf && sold.text == "샀음", "산 상품을 또 살 수 있다");
            StringAssert.Contains("봉인 3장", HubText(), "교체 뒤 봉인 장수가 바뀌었다");
            yield return BuyInShop("SwapAnyChosen", chooseSeal: true);
            Assert.AreEqual("보유 칩 290", Find<Label>(RunUi, "shop-chips")?.text, "직접 고르는 완전 교체(80) 뒤 칩");
            yield return BuyInShop("Unseal", chooseSeal: false);
            Assert.AreEqual("보유 칩 140", Find<Label>(RunUi, "shop-chips")?.text, "봉인 해제(150) 뒤 칩");
            StringAssert.Contains("봉인 2장", HubText(), "봉인 해제 뒤 봉인 장수");
            string saved = File.ReadAllText(SaveFile).Replace("\r\n", "\n");
            StringAssert.Contains("SwapSameRarity:30:1", saved, "산 상품이 저장되지 않았다");
            StringAssert.Contains("\nchips 140 400\n", saved, "칩이 저장되지 않았다");
            Click(Find<Button>(RunUi, "shop-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "chip-exchange"), "칩교환소 창이 닫히지 않았다");

            // 6층 이벤트: 들어가면 창이 한 번 뜬다. 닫고 다시 열 수 있다.
            Click(Find<Button>(RunUi, "leave-floor"));
            yield return null;
            Assert.AreEqual("6층 · 이벤트", Find<Label>(RunUi, "floor-label")?.text, "칩교환소 다음이 6층 이벤트가 아니다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-event"), "이벤트 층에 들어갔는데 이벤트 창이 뜨지 않았다");
            Assert.AreEqual("고요한 층", Find<Label>(RunUi, "event-title")?.text, "이벤트 제목");
            yield return Shot(RunUi, "14-event");
            Click(Find<Button>(RunUi, "event-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-event"), "이벤트 창이 닫히지 않았다");
            Click(Find<Button>(RunUi, "open-event"));
            yield return null;
            Click(Find<Button>(RunUi, "event-choice-0"));
            yield return null;
            StringAssert.StartsWith("7층", Find<Label>(RunUi, "floor-label")?.text ?? "", "이벤트 선택지를 고른 뒤 7층이 아니다");
            StringAssert.Contains("event-id=quiet-floor", File.ReadAllText(SaveFile), "이벤트 결과가 저장되지 않았다");

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
            Assert.AreEqual("9층 · 휴식", Find<Label>(RunUi, "floor-label")?.text, "8층 다음이 9층 휴식이 아니다");
            StringAssert.Contains("휴식", Find<Label>(RunUi, "floor-note")?.text ?? "", "휴식 층 안내가 없다");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-rest"), "휴식 층에 들어갔는데 휴식 창이 뜨지 않았다");
            Assert.AreEqual("LP 3000 → 8000", Find<Label>(RunUi, "rest-lp")?.text, "휴식 창의 LP");
            StringAssert.Contains("LP 8000", Find<Label>(RunUi, "run-summary").text, "휴식 뒤 LP");
            yield return Shot(RunUi, "15-rest");
            Click(Find<Button>(RunUi, "rest-close"));
            yield return null;
            Assert.IsNull(Find<VisualElement>(RunUi, "tower-rest"), "휴식 창이 닫히지 않았다");
            Click(Find<Button>(RunUi, "open-rest"));
            yield return null;
            Click(Find<Button>(RunUi, "rest-leave"));
            yield return null;
            Assert.AreEqual("10층 · 보스", Find<Label>(RunUi, "floor-label")?.text, "휴식 다음이 10층 보스가 아니다");
            Assert.AreEqual("보스 듀얼 시작", Find<Button>(RunUi, "next-duel")?.text, "보스 층의 듀얼 시작 버튼");
            var bossFloor = Find<VisualElement>(RunUi, "tower-floor-10");
            Assert.IsTrue(bossFloor != null && bossFloor.Query<Label>().ToList().Any(l => l.text == "지금"), "탑에 지금 층(10층) 표시가 없다");
            yield return Shot(RunUi, "15b-boss");

            // 보스를 이기고 봉인된 카드가 있으면: 보스 보상 봉인 해제 창
            yield return EditSave(text => SetLine(text, "phase", "ChoosingUnseal"));
            Assert.IsNotNull(Find<VisualElement>(RunUi, "unseal"), "보스 보상(봉인 해제) 창이 뜨지 않았다");
            Assert.IsFalse(Find<Button>(RunUi, "next-duel").enabledSelf, "보스 보상을 고르기 전인데 듀얼 시작 버튼이 켜져 있다");
            yield return ChooseUnseal();
            StringAssert.StartsWith("11층", Find<Label>(RunUi, "floor-label")?.text ?? "", "보스 보상 뒤 11층이 아니다");
            StringAssert.Contains("봉인 1장", HubText(), "보스 보상 봉인 해제 뒤 봉인 장수");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "tower-floor-20"), "11층인데 탑이 11~20층 묶음이 아니다");
            Debug.Log("[런 화면 시험] 탑·칩교환소: 교체 2가지와 봉인 해제 구매, 이벤트 창, 휴식 → 보스, 보스 보상 봉인 해제");
        }

        /// <summary>칩교환소에서 상품 하나를 산다: 사기 → 풀 카드 → (직접 고르는 교체면 대신 봉인할 카드) → 사기. 산 뒤 칩교환소 창으로 돌아온다.</summary>
        IEnumerator BuyInShop(string product, bool chooseSeal)
        {
            int sideBefore = Named("card-side-").Count;
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
            Assert.AreEqual(sideBefore + 1, Named("card-side-").Count, $"{product}: 풀린 카드가 사이드 덱에 더해지지 않았다");
        }

        /// <summary>처음 화면으로 가서 저장 파일을 고치고, 씬을 다시 불러 이어하기.</summary>
        IEnumerator EditSave(Func<string, string> edit)
        {
            Click(Find<Button>(RunUi, "to-title"));
            yield return null;
            File.WriteAllText(SaveFile, edit(File.ReadAllText(SaveFile).Replace("\r\n", "\n")));
            yield return LoadRunScene();
            Click(Find<Button>(RunUi, "continue-run"));
            yield return null;
            Assert.IsTrue(IsHub(), "고친 저장 파일로 이어하기를 했는데 층 사이 화면이 아니다");
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
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, LoadTimeout, "런 시작 화면");
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
                if (NoModal() && Find<Button>(RunUi, "next-duel") != null)
                    duels++;
                yield return Step(seed);
            }
            Debug.Log($"[런 화면 시험] 시드 {seed}: 런 종료, 보상 누적 {_rewards}번, 보충 누적 {_refills}번, 지나간 비전투 층 {_floorsPassed}개");
            StringAssert.Contains("골드 정산", Find<Label>(RunUi, "run-settlement")?.text ?? "", "런 종료 화면에 골드 정산이 없다");
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
            new[] { "reward", "refill", "unseal", "chip-exchange", "shop-pick", "tower-event", "tower-rest", "run-log" }.All(n => Find<VisualElement>(RunUi, n) == null);

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
        /// 승리 보상 창이 떠 있으면 하나 고르고, 보충 창이 떠 있으면 한 장 넣고, 보스 보상 창이면 봉인을 풀고, 이벤트 창이면 첫 선택지,
        /// 칩교환소 창이면 닫는다. 비전투 층(칩교환소·휴식·이벤트)이면 다음 층으로 가고, 전투 층이면 듀얼 하나를 끝까지 한다.
        /// </summary>
        IEnumerator Step(int seed)
        {
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
                Click(Find<Button>(RunUi, "event-choice-0"));
                _floorsPassed++;
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

            Click(Find<Button>(RunUi, "next-duel"));
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
            var next = Find<Button>(RunUi, "next-duel");
            Assert.IsTrue(next != null && !next.enabledSelf, "보상을 고르기 전인데 다음 듀얼 시작 버튼이 켜져 있다");
            int sideBefore = Named("card-side-").Count;
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
            else if (_rewards % 2 == 0)
            {
                Click(Find<VisualElement>(RunUi, cards[0]));
                yield return null;
                confirm = Find<Button>(RunUi, "reward-confirm");
                Assert.IsTrue(confirm.enabledSelf, "후보를 골랐는데 받기 버튼이 꺼져 있다");
                StringAssert.EndsWith("받기", confirm.text, "후보를 골랐는데 받기 버튼 글이 이상하다");
                yield return Shot(RunUi, "10-reward");
                Click(confirm);
                how = "카드 누르고 받기";
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
            Assert.AreEqual(sideBefore + (gold ? 0 : 1), Named("card-side-").Count, $"보상({how}) 뒤 사이드 덱 장수");
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
            Assert.IsTrue(Find<VisualElement>(RunUi, "editor-collection").Query<Label>().ToList().Any(l => l.text == "찾는 카드가 없습니다."), "검색 결과가 없다는 표시가 없다");

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
            if (_exchanged || !IsHub() || !NoModal())
                yield break;
            var side = Named("card-side-").FirstOrDefault();
            if (side == null)
                yield break;
            string code = side.name.Substring("card-side-".Length);
            int sideStart = SideCount(code), deckStart = DeckCount(code);
            ParkPointer();
            yield return null;
            void AssertMoved(bool inDeck, string how)
            {
                Assert.AreEqual(inDeck ? sideStart - 1 : sideStart, SideCount(code), $"{how}: 사이드 덱 장수");
                Assert.AreEqual(inDeck ? deckStart + 1 : deckStart, DeckCount(code), $"{how}: 메인·엑스트라 덱 장수");
            }
            Click(side);
            yield return null;
            var toDeck = Find<Button>(RunUi, "move-to-deck");
            if (toDeck == null || !toDeck.enabledSelf)
                yield break;
            Click(toDeck);
            yield return null;
            AssertMoved(true, "버튼으로 덱에 넣기");
            string deckZone = Find<VisualElement>(RunUi, "card-main-" + code) != null ? "zone-main" : "zone-extra";
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
            DoubleClick(InDeck(code));
            yield return null;
            AssertMoved(false, "두 번 눌러 사이드 덱으로 빼기");

            // 끌어다 놓기
            Drag(Find<VisualElement>(RunUi, "card-side-" + code), Find<VisualElement>(RunUi, deckZone));
            yield return null;
            AssertMoved(true, "덱 칸으로 끌어다 놓기");
            Drag(InDeck(code), Find<VisualElement>(RunUi, "zone-side"));
            yield return null;
            AssertMoved(false, "사이드 덱 칸으로 끌어다 놓기");

            // 메인 덱 카드 두 번 누르기: 옮길 수 없으면 알림, 옮겼으면 되돌린다
            var main = Named("card-main-").FirstOrDefault();
            string mainCode = main.name.Substring("card-main-".Length);
            int mainSideBefore = SideCount(mainCode), mainDeckBefore = DeckCount(mainCode);
            DoubleClick(main);
            yield return null;
            var notice = Find<Label>(RunUi, "move-notice");
            Assert.AreEqual(DisplayStyle.Flex, notice.style.display.value, "옮기기 알림이 보이지 않는다");
            if (SideCount(mainCode) > mainSideBefore)
            {
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
            var face = root.Q("my-field")?.Q("card-face") ?? root.Q("my-hand")?.Q("card-face");
            Assert.IsNotNull(face, "6턴인데 내 필드와 손패에 앞면 카드가 없다");
            var cell = face.parent;
            while (cell != null && (cell.name == null || !(cell.name.StartsWith("zone-0-") || cell.name.StartsWith("hand-0-"))))
                cell = cell.parent;
            Assert.IsNotNull(cell, "카드 앞면이 필드 칸·손패 칸 안에 있지 않다");
            Hover(cell);
            yield return null;
            Assert.IsNotNull(root.Q("card-detail")?.Q("card-face"), $"{cell.name}에 마우스를 올렸는데 카드 정보에 카드가 없다");
            yield return Shot(DuelUi, "2b-duel-mid");

            Click(Find<VisualElement>(DuelUi, "grave-0"));
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
                        Debug.Log($"[런 화면 시험] LP 변화: {delta.name} {delta.text}");
                        yield return Shot(DuelUi, "2l-lp-delta");
                    }
                }
                // 체인이 쌓이면 로그 위 체인 목록 (체인이 생길 때만)
                var chainPanel = Find<VisualElement>(DuelUi, "chain-panel");
                if (!_chainSeen && chainPanel != null && chainPanel.style.display.value == DisplayStyle.Flex && chainPanel.Q("chain-1") != null)
                {
                    _chainSeen = true;
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

        /// <summary>층 사이 화면에서 이 이름인 카드 장수 (같은 카드도 한 장씩 따로 놓인다).</summary>
        static int CountNamed(string name) => Named(name).Count(e => e.name == name);

        static int SideCount(string code) => CountNamed("card-side-" + code);

        static int DeckCount(string code) => CountNamed("card-main-" + code) + CountNamed("card-extra-" + code);

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

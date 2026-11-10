// 플레이 모드 시험: Run 씬을 열고 화면의 버튼만 눌러 로그라이크 런을 처음부터 끝까지 간다.
// 런 시작(시작 덱·시드·시작 사이드 덱) → [층 사이 화면 → 다음 듀얼 시작 → 듀얼 화면 "자동으로 끝까지" → "결과 보기" → (승리 보상 고르기·보충 고르기)] 반복
// → 런 종료 → 새 런. 듀얼은 사람 대신 임시 자동 응답(개발용 버튼)이 답하고, 상대는 WindBot이다.
// 저장도 본다 (저장 폴더는 시험마다 새 임시 폴더라 실제 저장 파일은 건드리지 않는다):
// - "처음 화면으로" → "이어하기", 씬을 다시 불러(게임을 다시 켠 것처럼) "이어하기"를 해도 층 사이 화면과 저장 파일이 그대로인지
// - 듀얼을 시작하면 듀얼 중 상태로 저장되고, 듀얼 도중에 씬을 다시 부르면 그 듀얼이 패배로 처리되며 알림이 한 번 나오는지
// - 런이 끝나면 진행 중인 런이 없어지고, 시작 사이드 덱은 다음 런에 그대로 채워지는지
// - 진행 중인 런이 있을 때 새 런: 한 번 더 눌러야 포기하고, 취소할 수 있는지
// 층 사이 화면의 교환(사이드 덱 → 덱, 덱 → 사이드 덱)도 버튼·두 번 누르기·끌어다 놓기로 한 번씩 해 본다 (포인터 이벤트를 직접 보낸다).
// 런 로그 창 열고 닫기, 런 시작 화면의 거르기·끌어다 놓기·두 번 누르기, 꽉 찬 시작 사이드 덱(3장)에 더 넣을 때의 알림도 본다.
// 승리 보상 창은 자리 3개를 확인하고, 누르고 받기·두 번 누르기를 번갈아 하며, 빈 자리(골드)가 나오면 한 번 골드를 고른다.
// 보충 고르기를 한 번 이상 거칠 때까지 런을 더 한다.
// 듀얼 화면은 한 번, 6턴쯤에 자동 진행을 멈추고 카드에 마우스 올리기(왼쪽 카드 정보)와 묘지 누르기(카드 목록 창)를 해 본다.
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
        bool _duelViewChecked;
        int _playbackTries;
        bool _playbackDone;
        bool _previewSeen;
        bool _fieldViewSeen;
        bool _skipSeen;
        const int MaxPlaybackTries = 3;

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

            // ---- 첫 런: 시작 덱, 시드, 시작 사이드 덱 3장 (꽉 참) ----
            Click(Find<Button>(RunUi, "deck-Yugi"));
            yield return null;
            SetSeed(1);
            var picked = new List<string>();
            for (int i = 0; i < SidePicks; i++)
                yield return PickToStartSide(picked);
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "side-count").text);
            // 빼고 다시 넣기
            Click(Find<VisualElement>(RunUi, "side-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "side-remove"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks - 1), Find<Label>(RunUi, "side-count").text, "사이드 덱에서 빼기");
            Assert.IsNull(Find<VisualElement>(RunUi, "side-" + picked[0]), "뺀 카드가 사이드 덱 목록에 남아 있다");
            Click(Find<VisualElement>(RunUi, "pick-" + picked[0]));
            yield return null;
            Click(Find<Button>(RunUi, "side-add"));
            yield return null;
            foreach (string code in picked)
                Assert.IsNotNull(Find<VisualElement>(RunUi, "side-" + code), $"고른 카드 {code}가 사이드 덱 목록에 없다");
            // 두 번 눌러 빼고, 끌어다 놓아 다시 넣기
            DoubleClick(Find<VisualElement>(RunUi, "side-" + picked[0]));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks - 1), Find<Label>(RunUi, "side-count").text, "시작 사이드 덱 카드를 두 번 눌러 빼기");
            Assert.IsNull(Find<VisualElement>(RunUi, "side-" + picked[0]), "두 번 눌러 뺀 카드가 사이드 덱 목록에 남아 있다");
            Drag(Find<VisualElement>(RunUi, "pick-" + picked[0]), Find<VisualElement>(RunUi, "start-side"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "side-count").text, "컬렉션 카드를 시작 사이드 덱으로 끌어다 놓기");
            Assert.IsNotNull(Find<VisualElement>(RunUi, "side-" + picked[0]), "끌어다 놓은 카드가 사이드 덱 목록에 없다");
            // 꽉 찬 시작 사이드 덱에 하나 더 끌어다 놓기: 들어가지 않고 까닭 알림
            string extra = Named("pick-").Select(e => e.name.Substring("pick-".Length)).FirstOrDefault(c => !picked.Contains(c));
            Assert.IsNotNull(extra, "컬렉션 목록에 끌어다 놓을 카드가 없다");
            Drag(Find<VisualElement>(RunUi, "pick-" + extra), Find<VisualElement>(RunUi, "start-side"));
            yield return null;
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "side-count").text, "꽉 찬 시작 사이드 덱에 더 들어갔다");
            Assert.IsNull(Find<VisualElement>(RunUi, "side-" + extra), "꽉 찬 시작 사이드 덱에 끌어다 놓은 카드가 들어갔다");
            var full = Find<Label>(RunUi, "move-notice");
            Assert.AreEqual(DisplayStyle.Flex, full.style.display.value, "꽉 찬 시작 사이드 덱에 넣을 때 알림이 보이지 않는다");
            StringAssert.StartsWith("넣을 수 없습니다", full.text, "꽉 찬 시작 사이드 덱에 넣을 때 까닭 알림이 없다");
            StringAssert.Contains($"{SidePicks}장까지", full.text, "꽉 찬 시작 사이드 덱 알림에 장수 제한이 없다");
            Debug.Log($"[런 화면 시험] 꽉 찬 시작 사이드 덱: {full.text}");
            Click(Find<Button>(RunUi, "filter-spell"));
            yield return null;
            Assert.IsTrue(Named("pick-").Count > 0, "마법 거르기에 카드가 없다");
            Click(Find<Button>(RunUi, "filter-all"));
            yield return null;
            Click(Named("pick-").First()); // 화면 확인용: 카드 하나를 고른 상태
            yield return null;
            yield return Shot(RunUi, "1-start");

            Click(Find<Button>(RunUi, "start-run"));
            yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "층 사이 화면");
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
            for (int target = _duels + 2; _duels < target && Find<Button>(RunUi, "new-run") == null;)
                yield return Step(1);
            yield return Settle(1);
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

            // ---- 듀얼 도중에 끄기 → 그 듀얼은 패배 ----
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
            Assert.AreEqual(SideCountText(SidePicks), Find<Label>(RunUi, "side-count").text, "지난번 시작 사이드 덱을 기억하지 못한다");

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

            Debug.Log($"[런 화면 시험] 듀얼 {_duels}판, 보상 {_rewards}번 (빈 자리 골드 {(_goldChosen ? "고름" : "나오지 않음")}), 보충 {_refills}번, 교환 {(_exchanged ? "함" : "못 함")}");
            Assert.Greater(_rewards, 0, "승리 보상을 한 번도 고르지 않았다");
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

        static string SideCountText(int count) => $"시작 사이드 덱 {count}/{SidePicks}장";

        /// <summary>컬렉션 목록에서 아직 고르지 않았고 넣을 수 있는 카드를 하나 골라 "사이드 덱에 넣기"를 누른다.</summary>
        IEnumerator PickToStartSide(List<string> picked)
        {
            // 누를 때마다 화면을 다시 그리므로 이름만 먼저 모아 두고 매번 새로 찾는다.
            var codes = Named("pick-").Select(e => e.name.Substring("pick-".Length)).Where(c => !picked.Contains(c)).ToList();
            foreach (string code in codes)
            {
                Click(Find<VisualElement>(RunUi, "pick-" + code));
                yield return null;
                var add = Find<Button>(RunUi, "side-add");
                if (add == null || !add.enabledSelf)
                    continue; // 같은 카드명이 시작 덱에 이미 3장 있는 등
                Click(add);
                yield return null;
                picked.Add(code);
                yield break;
            }
            Assert.Fail("컬렉션 목록에 시작 사이드 덱에 넣을 수 있는 카드가 없다");
        }

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
                if (Find<VisualElement>(RunUi, "refill") == null && Find<VisualElement>(RunUi, "reward") == null)
                    duels++;
                yield return Step(seed);
            }
            Debug.Log($"[런 화면 시험] 시드 {seed}: 런 종료, 보상 누적 {_rewards}번, 보충 누적 {_refills}번");
            yield return Shot(RunUi, "6-end");
        }

        /// <summary>승리 보상 창·보충 창이 떠 있으면 처리해 층 사이 화면(또는 런 종료)으로 만든다.</summary>
        IEnumerator Settle(int seed)
        {
            for (int i = 0; i < 10 && (Find<VisualElement>(RunUi, "reward") != null || Find<VisualElement>(RunUi, "refill") != null); i++)
                yield return Step(seed);
            Assert.IsNull(Find<VisualElement>(RunUi, "reward"), "승리 보상 창이 닫히지 않는다");
            Assert.IsNull(Find<VisualElement>(RunUi, "refill"), "보충 창이 닫히지 않는다");
        }

        /// <summary>승리 보상 창이 떠 있으면 하나 고르고, 보충 창이 떠 있으면 한 장 넣고, 아니면 듀얼 하나를 끝까지 한다.</summary>
        IEnumerator Step(int seed)
        {
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
            Assert.IsTrue(Find<Button>(RunUi, "next-duel") != null || Find<Button>(RunUi, "new-run") != null,
                "결과 보기 뒤에 층 사이 화면이나 런 종료 화면이 아니다");
            if (Find<Button>(RunUi, "next-duel") != null && Find<VisualElement>(RunUi, "reward") == null && Find<VisualElement>(RunUi, "refill") == null)
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
            Assert.IsNull(Find<VisualElement>(RunUi, "reward"), $"보상을 골랐는데({how}) 보상 창이 닫히지 않았다");
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
        /// 교환 (한 번만): 사이드 덱 카드를 덱에 넣고 다시 뺀다. 버튼으로 한 번, 두 번 누르기로 한 번, 끌어다 놓기로 한 번.
        /// 그리고 메인 덱 카드 하나를 두 번 눌러 본다 (메인 덱이 최소 장수면 옮길 수 없다는 알림, 아니면 옮겼다가 되돌림).
        /// </summary>
        IEnumerator Exchange()
        {
            if (_exchanged || Find<Button>(RunUi, "next-duel") == null)
                yield break;
            var side = Named("card-side-").FirstOrDefault();
            if (side == null)
                yield break;
            string code = side.name.Substring("card-side-".Length);
            int sideStart = SideCount(code), deckStart = DeckCount(code);
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
            _duelViewChecked = true;
            var stop = Find<Button>(DuelUi, "auto-stop");
            if (stop != null)
            {
                Click(stop);
                yield return null;
            }
            if (Find<Button>(DuelUi, "leave-duel") != null)
                yield break; // 그 사이에 끝났다
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
            while (!(battle && _previewSeen && _skipSeen) && Time.realtimeSinceStartup < until && Find<Button>(DuelUi, "leave-duel") == null)
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
                // 선택 창의 "필드 보기" → 모서리에 "필드 확인 중" (선택 창이 뜰 때만)
                var fieldView = Find<Button>(DuelUi, "field-view");
                if (!_fieldViewSeen && fieldView != null)
                {
                    Click(fieldView);
                    yield return null;
                    string badge = Find<Label>(DuelUi, "status-badge")?.text ?? "";
                    StringAssert.StartsWith("필드 확인 중", badge, "필드 보기를 눌렀는데 모서리 표시가 다르다");
                    _fieldViewSeen = true;
                    yield return Shot(DuelUi, "2h-field-view");
                }
                // 상대 공격을 본 뒤: 건너뛰기 → 모서리에 "건너뛰는 중"
                var skip = Find<Button>(DuelUi, "skip-playback");
                if (battle && !_skipSeen && skip != null)
                {
                    Click(skip);
                    string badge = Find<Label>(DuelUi, "status-badge")?.text ?? "";
                    StringAssert.StartsWith("건너뛰는 중", badge, "건너뛰기를 눌렀는데 모서리 표시가 다르다");
                    _skipSeen = true;
                    yield return Shot(DuelUi, "2i-skip");
                }
                var once = Find<Button>(DuelUi, "auto-once");
                if (once != null)
                    Click(once);
                yield return null;
            }
            _playbackDone = battle && _previewSeen && _skipSeen;
            Assert.Greater(steps, 0, "자동(한 번)으로 답했는데 멈춰 보여 주는 단계가 없다");
            Debug.Log($"[런 화면 시험] 한 단계씩 보여 주기 ({_playbackTries}번째): 멈춘 단계 {steps}개, 상대 공격 {(attack ? "봄" : "못 봄")}, 그 뒤 데미지 계산 {(battle ? "봄" : "못 봄")}");
        }

        /// <summary>"6턴 · …" → 6. 듀얼 준비 중이거나 없으면 0.</summary>
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

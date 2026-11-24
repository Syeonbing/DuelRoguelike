// 플레이 모드 시험 (짧은 전용 시험): 카드에 놓인 카운터가 필드에 보인다.
// Duel 씬을 열고, 내 덱을 앤틱 기어 캐슬 20장 + 엘프 검사 20장으로 한 듀얼을 다시 시작한다 (상대는 스타터 덱 —유희 편—, WindBot).
// 개발용 "자동"(한 번)으로 답하다가, 캐슬이 있을 때 몬스터가 일반 소환되어 카운터가 놓이면:
// - 필드 카드 아래 표시에 "카운터 N"이 뜬다 (엑시즈 소재의 "소재 N"과 같은 자리).
// - 로그에 "「앤틱 기어 캐슬」에 카운터 1개를 놓았다"가 남는다.
// 소리는 끄고 (AudioListener.volume = 0), 연출 속도는 "빠름"으로 두고 끝나면 되돌린다.
// 환경 변수 DUELROGUELIKE_SCREENSHOT_DIR에 폴더를 주면 그 화면을 1920×1080 PNG로 저장한다.
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (DuelSpeedUpPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1분 안팎)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.CounterMarkPlayTest -testResults C:\Dev\counter-results.xml -logFile C:\Dev\counter.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Duel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class CounterMarkPlayTest
    {
        const string DuelUi = "DuelUI";
        const string SpeedPrefKey = "duel.playback-speed";
        const int FastSpeed = 0; // DuelScreen.PlaybackSpeed.Fast
        const uint AncientGearCastle = 92001300; // 앤틱 기어 캐슬
        const uint CelticGuardian = 91152256;    // 엘프 검사 (레벨 4 일반 몬스터)
        const float LoadTimeout = 120, PlayTimeout = 240;

        static readonly Regex CounterMark = new Regex(@"^(소재 \d+ · )?카운터 \d+$");

        int? _speedPref;
        float _listenerVolume = 1f;

        [SetUp]
        public void SetUp()
        {
            _speedPref = PlayerPrefs.HasKey(SpeedPrefKey) ? PlayerPrefs.GetInt(SpeedPrefKey) : (int?)null;
            PlayerPrefs.SetInt(SpeedPrefKey, FastSpeed);
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            AudioListener.volume = _listenerVolume;
            if (_speedPref.HasValue)
                PlayerPrefs.SetInt(SpeedPrefKey, _speedPref.Value);
            else
                PlayerPrefs.DeleteKey(SpeedPrefKey);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator CounterShowsOnFieldAndInLog()
        {
            SceneManager.LoadScene("Duel");
            yield return null;
            yield return WaitFor(() => Find<Button>("auto-once") != null, LoadTimeout, "듀얼 화면");

            // 캐슬과 일반 몬스터만 든 덱으로 다시 시작한다 (덱 장수 규칙은 엔진이 따지지 않는다)
            var myDeck = Enumerable.Repeat(AncientGearCastle, 20).Concat(Enumerable.Repeat(CelticGuardian, 20)).ToList();
            var options = new DuelSessionOptions
            {
                Seed = new ulong[] { 1, 2, 3, 4 },
                Decks = new IReadOnlyList<uint>[] { myDeck, StarterDecks.Get(StarterDeckId.Yugi).ToCardList() },
                DeckShuffleSeed = 100,
            };
            var screen = Screen();
            var begin = screen.GetType().GetMethod("BeginExternalDuel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(begin, "DuelScreen.BeginExternalDuel이 없다");
            begin.Invoke(screen, new object[] { options, 0, 1, 1, "나", "상대", "카운터 시험", null });
            yield return null;

            float until = Time.realtimeSinceStartup + PlayTimeout;
            Label mark = null;
            int answers = 0;
            while (mark == null)
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"{PlayTimeout}초 안에 카운터 표시가 나오지 않았다 ({answers}번 답함)");
                Assert.IsFalse(Finished(), $"카운터 표시를 보기 전에 듀얼이 끝났다 ({answers}번 답함)");
                mark = Root()?.Query<Label>().ToList().FirstOrDefault(l => CounterMark.IsMatch(l.text ?? ""));
                if (mark != null)
                    break;
                var reveal = Find<Button>("reveal-confirm");
                if (reveal != null)
                    Click(reveal);
                else if (Find<Button>("auto-once") is Button once)
                {
                    Click(once);
                    answers++;
                }
                yield return null;
            }
            Debug.Log($"[카운터 시험] {answers}번 답한 뒤 표시: {mark.text}");
            for (VisualElement e = mark; e != null; e = e.parent)
                Assert.AreNotEqual(DisplayStyle.None, e.resolvedStyle.display, "카운터 표시가 숨겨져 있다");

            // 다음에 답할 차례까지 기다려 화면이 멈춘 뒤 찍는다
            yield return WaitFor(() => Find<Button>("auto-once") != null || Finished(), PlayTimeout, "다음 답할 차례");
            yield return Shot("counter-mark");

            string snapshot = Snapshot();
            StringAssert.Contains("「앤틱 기어 캐슬」에 카운터 1개를 놓았다", snapshot, "로그에 카운터를 놓은 줄이 없다");
        }

        // ================= 화면 읽기 =================

        static Component Screen()
        {
            var screen = GameObject.Find(DuelUi)?.transform.parent.GetComponent("DuelScreen");
            Assert.IsNotNull(screen, "DuelScreen을 찾지 못했다");
            return screen;
        }

        static VisualElement Root()
        {
            var go = GameObject.Find(DuelUi);
            var root = go != null ? go.GetComponent<UIDocument>()?.rootVisualElement : null;
            return root == null || root.style.display.value == DisplayStyle.None ? null : root;
        }

        static T Find<T>(string name) where T : VisualElement => Root()?.Q<T>(name);

        static bool Finished() => (Find<Label>("prompt-label")?.text ?? "").StartsWith("듀얼 종료");

        /// <summary>듀얼 화면(DuelScreen.DebugSnapshot): 턴·LP·필드·패·묘지·제외와 전체 로그.</summary>
        static string Snapshot()
        {
            var screen = Screen();
            var method = screen.GetType().GetMethod("DebugSnapshot");
            Assert.IsNotNull(method, "DuelScreen.DebugSnapshot이 없다");
            string text = (string)method.Invoke(screen, null);
            Assert.IsFalse(string.IsNullOrEmpty(text), "듀얼 상태를 읽지 못했다");
            return text;
        }

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

        /// <summary>DUELROGUELIKE_SCREENSHOT_DIR이 있으면 듀얼 화면을 텍스처에 그려 PNG로 저장한다.</summary>
        static IEnumerator Shot(string name)
        {
            string dir = Environment.GetEnvironmentVariable("DUELROGUELIKE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                yield break;
            var settings = GameObject.Find(DuelUi).GetComponent<UIDocument>().panelSettings;
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

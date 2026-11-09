// 플레이 모드 시험: Run 씬을 열고 화면의 버튼만 눌러 로그라이크 런을 처음부터 끝까지 간다.
// 런 시작(시작 덱·시드) → [층 사이 화면 → 다음 듀얼 시작 → 듀얼 화면 "자동으로 끝까지" → "결과 보기" → (보충 고르기)] 반복
// → 런 종료 → 새 런. 듀얼은 사람 대신 임시 자동 응답(개발용 버튼)이 답하고, 상대는 WindBot이다.
// 층 사이 화면의 교환(사이드 덱 → 덱, 덱 → 사이드 덱)도 한 번 해 본다.
// 보충 고르기를 한 번 이상 거칠 때까지 시드 1부터 런을 이어서 한다 (최대 MaxRuns개).
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
        const int MaxRuns = 4;
        const int MaxDuelsPerRun = 300;
        const float LoadTimeout = 120;
        const float DuelTimeout = 300;

        readonly HashSet<string> _shots = new HashSet<string>();

        [UnityTest, Timeout(3600000)]
        public IEnumerator RunFromStartToEnd()
        {
            SceneManager.LoadScene("Run");
            yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, LoadTimeout, "런 시작 화면");
            yield return Shot(RunUi, "1-start");

            int refills = 0, totalDuels = 0;
            bool exchanged = false;
            for (int seed = 1; seed <= MaxRuns; seed++)
            {
                // ---- 런 시작 화면 ----
                Click(Find<Button>(RunUi, "deck-Yugi"));
                yield return null;
                var seedField = Find<TextField>(RunUi, "seed");
                Assert.IsNotNull(seedField, "시드 칸이 없다");
                seedField.value = seed.ToString();
                yield return null;
                Click(Find<Button>(RunUi, "start-run"));
                yield return WaitFor(() => Find<Button>(RunUi, "next-duel") != null, 10, "층 사이 화면");

                int duels = 0;
                while (Find<Button>(RunUi, "new-run") == null)
                {
                    Assert.Less(duels, MaxDuelsPerRun, $"시드 {seed}: 듀얼 {MaxDuelsPerRun}판 안에 런이 끝나지 않았다");

                    // ---- 보충 고르기 ----
                    if (Find<VisualElement>(RunUi, "refill") != null)
                    {
                        var candidate = Root(RunUi).Query<Button>().Where(b => b.name.StartsWith("refill-") && b.name != "refill-confirm").First();
                        Assert.IsNotNull(candidate, "보충 창에 고를 카드가 없다");
                        Click(candidate);
                        yield return null;
                        yield return Shot(RunUi, "5-refill");
                        Click(Find<Button>(RunUi, "refill-confirm"));
                        refills++;
                        yield return null;
                        continue;
                    }

                    // ---- 교환 (한 번만): 사이드 덱 카드를 덱에 넣고 다시 뺀다 ----
                    if (!exchanged)
                    {
                        var side = Root(RunUi).Query<Button>().Where(b => b.name.StartsWith("card-side-")).First();
                        if (side != null)
                        {
                            string code = side.name.Substring("card-side-".Length);
                            Click(side);
                            yield return null;
                            var toDeck = Find<Button>(RunUi, "move-to-deck");
                            if (toDeck != null && toDeck.enabledSelf)
                            {
                                Click(toDeck);
                                yield return null;
                                Assert.IsTrue(Find<Button>(RunUi, "card-main-" + code) != null || Find<Button>(RunUi, "card-extra-" + code) != null,
                                    "덱에 넣은 카드가 메인·엑스트라 덱 목록에 없다");
                                var toSide = Find<Button>(RunUi, "move-to-side");
                                Assert.IsNotNull(toSide, "덱에 넣은 카드에 사이드 덱으로 빼기 버튼이 없다");
                                Click(toSide);
                                yield return null;
                                Assert.IsNotNull(Find<Button>(RunUi, "card-side-" + code), "사이드 덱으로 뺀 카드가 사이드 덱 목록에 없다");
                                yield return Shot(RunUi, "4-hub");
                                exchanged = true;
                                Debug.Log($"[런 화면 시험] 교환: {code} 사이드 덱 → 덱 → 사이드 덱");
                            }
                        }
                    }

                    // ---- 듀얼 ----
                    Click(Find<Button>(RunUi, "next-duel"));
                    yield return WaitFor(() => Find<Button>(DuelUi, "auto-to-end") != null || Find<Button>(DuelUi, "leave-duel") != null,
                        LoadTimeout, "듀얼 화면");
                    Assert.IsNull(Find<Button>(RunUi, "next-duel"), "듀얼 중에 런 화면이 보인다");
                    yield return Shot(DuelUi, "2-duel");
                    float until = Time.realtimeSinceStartup + DuelTimeout;
                    while (Find<Button>(DuelUi, "leave-duel") == null)
                    {
                        Assert.Less(Time.realtimeSinceStartup, until, $"시드 {seed} 듀얼 {duels + 1}: {DuelTimeout}초 안에 끝나지 않았다");
                        var auto = Find<Button>(DuelUi, "auto-to-end");
                        if (auto != null)
                            Click(auto); // 처음, 또는 자동 응답이 멈췄으면 다시 켠다
                        yield return null;
                    }
                    yield return Shot(DuelUi, "3-duel-end");
                    Click(Find<Button>(DuelUi, "leave-duel"));
                    duels++;
                    yield return null;
                    Assert.IsNull(Find<Button>(DuelUi, "leave-duel"), "결과 보기 뒤에도 듀얼 화면이 보인다");
                    Assert.IsTrue(Find<Button>(RunUi, "next-duel") != null || Find<Button>(RunUi, "new-run") != null,
                        "결과 보기 뒤에 층 사이 화면이나 런 종료 화면이 아니다");
                }

                totalDuels += duels;
                Debug.Log($"[런 화면 시험] 시드 {seed}: 듀얼 {duels}판 뒤 런 종료, 보충 누적 {refills}번");
                yield return Shot(RunUi, "6-end");
                Click(Find<Button>(RunUi, "new-run"));
                yield return WaitFor(() => Find<Button>(RunUi, "start-run") != null, 10, "새 런의 런 시작 화면");
                if (refills > 0 && exchanged)
                    break;
            }

            Debug.Log($"[런 화면 시험] 듀얼 {totalDuels}판, 보충 {refills}번, 교환 {(exchanged ? "함" : "못 함")}");
            Assert.Greater(refills, 0, $"런 {MaxRuns}개 안에 보충 고르기를 한 번도 거치지 않았다");
            Assert.IsTrue(exchanged, "사이드 덱 카드를 한 번도 교환하지 못했다");
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

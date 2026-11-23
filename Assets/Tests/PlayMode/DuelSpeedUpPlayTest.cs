// 플레이 모드 시험 (짧은 전용 시험): 듀얼의 가속. docs/UI.md "듀얼의 보조 키"·"연출".
// Duel 씬(혼자 듀얼, 인스펙터의 고정 덱과 시드)을 열어 같은 듀얼의 앞부분을 두 번 본다: 한 번은 보통 속도로, 한 번은 스페이스를 누른 채(가속)로.
// 내 답은 개발용 "자동"(한 번) 버튼이 대신 하고, 답한 횟수가 같아질 때 멈춰 견준다.
// - 두 번의 결과(턴·LP·필드·패·묘지·제외, 전체 로그)가 같다. 가속한 쪽이 더 빨리 끝난다.
// - 가속 중에는 듀얼 효과음 배속(Sound.Rate)이 오르고 내는 소리의 재생 속도도 오른다. 연출은 연출 층에 그대로 나온다 (지워지지 않는다).
//   효과음을 내는 횟수도 거의 같다 (같은 소리를 금방 다시 내지 않는 간격이 프레임에 따라 한두 번 달라질 수 있어 조금의 차이는 둔다).
// - 내가 답할 차례가 되면, 스페이스를 떼면 배속이 1로 돌아온다.
// - 필드를 누르고 있는 동안 가속하고 모서리에 "가속 중"이 뜬다. 떼면 배속이 1로 돌아오고 표시가 흐려지며 사라진다.
// - 필드를 한 번 눌렀다 떼거나 엔터를 눌러도 연출이 지워지지 않고, 건너뛰기 버튼·건너뜀 표시가 없다.
// 소리는 끈다 (AudioListener.volume = 0. 재생 횟수와 재생 속도는 그대로 볼 수 있다). 속도 설정은 "보통"으로 두고 끝나면 되돌린다.
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (2분 안팎)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.DuelSpeedUpPlayTest -testResults C:\Dev\speedup-results.xml -logFile C:\Dev\speedup.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DuelRoguelike.Tests
{
    public sealed class DuelSpeedUpPlayTest
    {
        const string DuelUi = "DuelUI";
        const string SpeedPrefKey = "duel.playback-speed";
        /// <summary>진행을 보여 주는 중 가운데 띠 아래의 작은 설명 (DuelScreen.PlayingHint).</summary>
        const string PlayingHint = "누르고 있으면 빨라진다";
        /// <summary>가속할 때의 배속 (DuelScreen.AccelRate).</summary>
        const float AccelRate = 3f;
        /// <summary>견줄 때까지 "자동"으로 답하는 횟수.</summary>
        const int Answers = 12;
        const float LoadTimeout = 120, PartTimeout = 150, PointerTimeout = 120;

        static readonly string[] DuelSounds =
        {
            "Draw", "Set", "Flip", "Shuffle", "Summon", "SpecialSummon", "Activate", "Target", "ToGrave", "ToHand", "Destroy", "Banish", "Equip",
            "Attack", "DirectHit", "Damage", "Recover", "NewTurn", "Phase", "Coin", "Dice",
        };

        int? _speedPref;
        float _listenerVolume = 1f;

        [SetUp]
        public void SetUp()
        {
            _speedPref = PlayerPrefs.HasKey(SpeedPrefKey) ? PlayerPrefs.GetInt(SpeedPrefKey) : (int?)null;
            PlayerPrefs.SetInt(SpeedPrefKey, 1); // 보통
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

        /// <summary>듀얼 앞부분을 한 번 본 결과.</summary>
        sealed class Part
        {
            public string Snapshot;
            public float Seconds;
            public int Steps, PlayingFrames, FastFrames, FxMax;
            /// <summary>보여 주는 동안 내고 있던 효과음의 가장 빠른 재생 속도 (소리를 내지 못하는 환경이면 0).</summary>
            public float MaxPitch;
            public Dictionary<string, int> Sounds;
            public int SoundTotal => Sounds.Values.Sum();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SpeedUpIsFasterKeepsResultAndNeverSkips()
        {
            var normal = new Part();
            yield return PlayPart(false, normal);
            var fast = new Part();
            yield return PlayPart(true, fast);
            Debug.Log($"[가속 시험] 보통: {normal.Seconds:0.0}초, 멈춘 단계 {normal.Steps}개, 연출 층 최대 {normal.FxMax}개, 효과음 {normal.SoundTotal}번, 가장 빠른 재생 속도 {normal.MaxPitch:0.00}");
            Debug.Log($"[가속 시험] 가속: {fast.Seconds:0.0}초, 멈춘 단계 {fast.Steps}개, 연출 층 최대 {fast.FxMax}개, 효과음 {fast.SoundTotal}번, 가장 빠른 재생 속도 {fast.MaxPitch:0.00}"
                + $", 보여 주는 프레임 {fast.PlayingFrames} 중 가속 {fast.FastFrames}");
            Debug.Log("[가속 시험] 효과음 (보통/가속): " + string.Join(", ", DuelSounds.Where(n => normal.Sounds[n] + fast.Sounds[n] > 0).Select(n => $"{n} {normal.Sounds[n]}/{fast.Sounds[n]}")));

            // 결과가 같다: 턴·LP·필드·패·묘지·제외와 전체 로그
            Assert.Greater(normal.Steps, 5, "보통 속도로 본 단계가 너무 적어 견줄 것이 없다");
            Assert.AreEqual(normal.Snapshot, fast.Snapshot, "가속한 듀얼과 하지 않은 듀얼의 결과(필드·LP·로그)가 다르다");
            // 더 빨리 끝난다
            Assert.Less(fast.Seconds, normal.Seconds * 0.7f, $"가속했는데 빨라지지 않았다 (보통 {normal.Seconds:0.0}초, 가속 {fast.Seconds:0.0}초)");
            // 보여 주는 동안 줄곧 가속했다 (내가 답한 바로 다음 프레임 같은 경계만 빼고)
            Assert.Greater(fast.PlayingFrames, 0, "가속한 듀얼에서 보여 주는 단계를 보지 못했다");
            Assert.GreaterOrEqual(fast.FastFrames, fast.PlayingFrames * 0.9f, "스페이스를 누르고 있는데 가속하지 않은 프레임이 많다");
            Assert.AreEqual(0, normal.FastFrames, "누르지 않았는데 가속했다");
            // 연출은 그대로 나온다
            Assert.Greater(normal.FxMax, 0, "보통 속도에서 연출이 나오지 않았다");
            Assert.Greater(fast.FxMax, 0, "가속 중에 연출이 나오지 않았다 (연출이 통째로 건너뛰어졌다)");
            // 소리: 내는 횟수는 거의 같고, 내는 소리의 재생 속도가 오른다
            Assert.Greater(fast.SoundTotal, 0, "가속 중에 듀얼 효과음이 하나도 나지 않았다");
            Assert.LessOrEqual(Mathf.Abs(fast.SoundTotal - normal.SoundTotal), Mathf.Max(3, normal.SoundTotal / 5),
                $"가속한 듀얼의 효과음 횟수가 많이 다르다 (보통 {normal.SoundTotal}, 가속 {fast.SoundTotal})");
            if (normal.MaxPitch > 0f)
            {
                Assert.LessOrEqual(normal.MaxPitch, 1.01f, "누르지 않았는데 효과음이 빨라졌다");
                Assert.Greater(fast.MaxPitch, 1.5f, "가속 중에 내는 효과음의 재생 속도가 오르지 않았다");
            }
            else
                Debug.Log("[가속 시험] 이 환경에서는 소리를 내지 못해 재생 속도는 보지 못했다 (배속 값만 보았다)");

            // 두 번째 듀얼을 이어서: 필드 누르고 있기, 한 번 누르기·엔터
            yield return PointerChecks();
        }

        /// <summary>Duel 씬을 새로 열어 "자동"으로 Answers번 답할 때까지 본다. accelerate: 그동안 스페이스를 누르고 있는다.</summary>
        IEnumerator PlayPart(bool accelerate, Part part)
        {
            SceneManager.LoadScene("Duel");
            yield return null;
            yield return WaitFor(() => Playing() || Find<Button>("auto-once") != null, LoadTimeout, "듀얼 화면");
            Assert.AreEqual(1f, SoundRate(), 0.001f, "듀얼을 시작할 때 듀얼 효과음 배속이 1이 아니다");
            var before = DuelSounds.ToDictionary(n => n, n => SoundCount(n));
            var keys = accelerate ? new VirtualKeyboard() : null;
            try
            {
                keys?.Hold(UnityEngine.InputSystem.Key.Space);
                yield return null; // 누른 것이 다음 프레임에 들어간다
                float started = Time.realtimeSinceStartup;
                int answers = 0;
                string last = null;
                while (true)
                {
                    Assert.Less(Time.realtimeSinceStartup, started + PartTimeout, $"{PartTimeout}초 안에 {Answers}번 답하지 못했다 (지금 {answers}번)");
                    Assert.IsFalse(Finished(), $"견주기 전에 듀얼이 끝났다 ({answers}번 답함)");
                    Assert.IsNull(Find<Button>("skip-playback"), "건너뛰기 버튼이 남아 있다");
                    var reveal = Find<Button>("reveal-confirm");
                    if (reveal != null)
                    {
                        Click(reveal);
                        yield return null;
                        continue;
                    }
                    if (Playing())
                    {
                        part.PlayingFrames++;
                        if (SoundRate() > 1.5f)
                            part.FastFrames++;
                        string line = Find<Label>("prompt-label")?.text ?? "";
                        if (line != "진행 중..." && line != last)
                        {
                            last = line;
                            part.Steps++;
                        }
                        part.FxMax = Mathf.Max(part.FxMax, Find<VisualElement>("fx-layer")?.childCount ?? 0);
                        part.MaxPitch = Mathf.Max(part.MaxPitch, FastestSfxPitch());
                    }
                    var once = Find<Button>("auto-once");
                    if (once != null)
                    {
                        if (answers == Answers)
                            break;
                        Click(once);
                        answers++;
                    }
                    yield return null;
                }
                part.Seconds = Time.realtimeSinceStartup - started;
            }
            finally
            {
                keys?.Dispose();
            }
            part.Snapshot = Snapshot();
            part.Sounds = DuelSounds.ToDictionary(n => n, n => SoundCount(n) - before[n]);
            // 내가 답할 차례다: 누르고 있었어도 배속은 1로 돌아와 있어야 한다
            yield return null;
            yield return null;
            Assert.AreEqual(1f, SoundRate(), 0.001f, "내가 답할 차례인데 듀얼 효과음 배속이 1로 돌아오지 않았다");
        }

        /// <summary>
        /// 이어서 "자동"으로 답하며: (1) 보여 주는 중에 필드를 누르고 있으면 가속하고, 떼면 배속이 1로 돌아오며 표시가 흐려지며 사라진다.
        /// (2) 연출이 나오는 단계에서 필드를 한 번 눌렀다 떼고 엔터를 눌러도 연출이 지워지지 않고 건너뜀 표시가 없다.
        /// </summary>
        IEnumerator PointerChecks()
        {
            bool held = false, tapped = false;
            float until = Time.realtimeSinceStartup + PointerTimeout;
            string last = null;
            var at = new Vector2(960, 460);
            while (!(held && tapped))
            {
                Assert.Less(Time.realtimeSinceStartup, until, $"{PointerTimeout}초 안에 필드 누르기를 다 보지 못했다 (누르고 있기 {held}, 한 번 누르기 {tapped})");
                Assert.IsFalse(Finished(), "필드 누르기를 보기 전에 듀얼이 끝났다");
                var reveal = Find<Button>("reveal-confirm");
                if (reveal != null)
                {
                    Click(reveal);
                    yield return null;
                    continue;
                }
                var mat = Find<VisualElement>("field-mat");
                var fx = Find<VisualElement>("fx-layer");
                string line = Find<Label>("prompt-label")?.text ?? "";
                bool newStep = Playing() && line != "진행 중..." && line != last;
                if (newStep)
                    last = line;
                if (newStep && !held)
                {
                    // (1) 누르고 있는 동안 가속
                    SendPointer(mat, EventType.MouseDown, at);
                    yield return null;
                    bool sped = Playing() && SoundRate() > 1.5f;
                    if (Playing())
                    {
                        Assert.AreEqual(AccelRate, SoundRate(), 0.001f, "필드를 누르고 있는데 듀얼 효과음 배속이 오르지 않았다");
                        StringAssert.StartsWith("가속 중", BadgeText(), "필드를 누르고 있는데 모서리에 가속 중이 안 뜬다");
                    }
                    SendPointer(mat, EventType.MouseUp, at);
                    yield return null;
                    Assert.AreEqual(1f, SoundRate(), 0.001f, "필드에서 뗐는데 듀얼 효과음 배속이 1로 돌아오지 않았다");
                    if (!sped)
                        continue; // 그 사이 보여 주기가 끝났다. 다음에 다시 본다.
                    // 표시는 바로 사라지지 않고 흐려지며 사라진다
                    var badge = Find<Label>("status-badge");
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
                    Assert.IsFalse(badge.style.display.value == DisplayStyle.Flex && badge.text.StartsWith("가속 중"), "필드에서 뗐는데 가속 중 표시가 사라지지 않는다");
                    held = true;
                    Debug.Log($"[가속 시험] 필드 누르고 있기 → 가속 중 ×{AccelRate}, 떼면 배속 1·표시가 흐려지며 사라짐 ({line})");
                }
                else if (newStep && held && !tapped && fx != null && fx.childCount > 0)
                {
                    // (2) 한 번 눌렀다 떼기, 엔터: 아무것도 건너뛰지 않는다
                    SendPointer(mat, EventType.MouseDown, at);
                    SendPointer(mat, EventType.MouseUp, at);
                    yield return Tap(UnityEngine.InputSystem.Key.Enter);
                    string badge = BadgeText();
                    Assert.IsFalse(badge.StartsWith("건너뜀") || badge.StartsWith("빨리 넘김"), $"필드를 누르거나 엔터를 눌렀는데 건너뛰기 표시가 떴다: {badge}");
                    Assert.IsNull(Find<Button>("skip-playback"), "건너뛰기 버튼이 남아 있다");
                    Assert.AreEqual(1f, SoundRate(), 0.001f, "누르고 있지 않은데 배속이 1이 아니다");
                    string now = Find<Label>("prompt-label")?.text ?? "";
                    if (!Playing() || now != line)
                        continue; // 그 사이 단계가 넘어갔다. 다음 단계에서 다시 본다.
                    Assert.Greater(fx.childCount, 0, $"필드를 한 번 누르고 엔터를 눌렀더니 연출이 지워졌다 ({line})");
                    tapped = true;
                    Debug.Log($"[가속 시험] 필드 한 번 누르기·엔터 → 연출 그대로 ({line}, 연출 층 {fx.childCount}개)");
                }
                var once = Find<Button>("auto-once");
                if (once != null)
                    Click(once);
                yield return null;
            }
        }

        // ================= 화면 읽기 =================

        static VisualElement Root()
        {
            var go = GameObject.Find(DuelUi);
            var root = go != null ? go.GetComponent<UIDocument>()?.rootVisualElement : null;
            return root == null || root.style.display.value == DisplayStyle.None ? null : root;
        }

        static T Find<T>(string name) where T : VisualElement => Root()?.Q<T>(name);

        /// <summary>진행을 보여 주는 중인지 (가운데 띠 아래에 가속 안내가 있다).</summary>
        static bool Playing() => (Find<Label>("prompt-detail")?.text ?? "").Contains(PlayingHint);

        static bool Finished() => (Find<Label>("prompt-label")?.text ?? "").StartsWith("듀얼 종료");

        static string BadgeText()
        {
            var badge = Find<Label>("status-badge");
            return badge != null && badge.style.display.value == DisplayStyle.Flex ? badge.text : "";
        }

        /// <summary>듀얼 화면(DuelScreen.DebugSnapshot): 턴·LP·필드·패·묘지·제외와 전체 로그.</summary>
        static string Snapshot()
        {
            var screen = GameObject.Find(DuelUi).transform.parent.GetComponent("DuelScreen");
            Assert.IsNotNull(screen, "DuelScreen을 찾지 못했다");
            var method = screen.GetType().GetMethod("DebugSnapshot");
            Assert.IsNotNull(method, "DuelScreen.DebugSnapshot이 없다");
            string text = (string)method.Invoke(screen, null);
            Assert.IsFalse(string.IsNullOrEmpty(text), "듀얼 상태를 읽지 못했다");
            return text;
        }

        // ================= 소리 =================

        static Type SoundType => Type.GetType("DuelRoguelike.UI.Sound, DuelRoguelike.UI", true);

        /// <summary>듀얼 효과음의 배속 (Sound.Rate).</summary>
        static float SoundRate() => (float)SoundType.GetProperty("Rate").GetValue(null);

        static int SoundCount(string name)
        {
            var sfx = Type.GetType("DuelRoguelike.UI.Sfx, DuelRoguelike.UI", true);
            return (int)SoundType.GetMethod("Count").Invoke(null, new[] { Enum.Parse(sfx, name) });
        }

        AudioSource[] _sources;
        int _sourcesAge;

        /// <summary>지금 내고 있는 효과음(되풀이하는 배경음은 빼고)의 가장 빠른 재생 속도. 내는 것이 없으면 0.</summary>
        float FastestSfxPitch()
        {
            // 소리통은 처음 소리를 낼 때 생기므로 가끔 다시 찾는다
            if (_sources == null || _sourcesAge++ % 30 == 0 || _sources.Any(s => s == null))
                _sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            float fastest = 0f;
            foreach (var source in _sources)
                if (source != null && !source.loop && source.isPlaying)
                    fastest = Mathf.Max(fastest, source.pitch);
            return fastest;
        }

        // ================= 조작 =================

        /// <summary>버튼을 누른다 (제출 이벤트 = 클릭과 같은 동작).</summary>
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

        /// <summary>포인터 이벤트를 직접 보낸다 (왼쪽 버튼).</summary>
        static void SendPointer(VisualElement target, EventType type, Vector2 position)
        {
            Assert.IsNotNull(target, "누를 곳이 없다");
            var system = new Event { type = type, mousePosition = position, clickCount = 1, button = 0 };
            EventBase e = type == EventType.MouseDown ? PointerDownEvent.GetPooled(system) : (EventBase)PointerUpEvent.GetPooled(system);
            using (e)
            {
                e.target = target;
                target.SendEvent(e);
            }
        }

        sealed class VirtualKeyboard : IDisposable
        {
            readonly UnityEngine.InputSystem.InputSettings.BackgroundBehavior _background;
            readonly UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode _editor;
            public readonly UnityEngine.InputSystem.Keyboard Device;

            public VirtualKeyboard()
            {
                // 배치 모드는 창에 포커스가 없어서 키 입력이 버려질 수 있다
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

        /// <summary>키를 한 번 눌렀다 뗀다.</summary>
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

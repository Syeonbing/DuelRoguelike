// 플레이 모드 시험 (짧은 전용 시험): 효과음 크기 맞춤. Assets/Scripts/UI/Sound.cs, SoundLevel.cs.
// 표(Sound.Cues)의 모든 효과음 파일을 준비시켜 (Sound.LevelReport) 소리마다의 결과를 로그에 표로 남기고 견준다:
// - 표의 파일이 모두 있고 샘플을 읽을 수 있다.
// - 귀에 들리는 세기가 목표(표의 크기)를 넘지 않고, 끝까지 키워도 모자란 소리도 목표보다 MaxShort dB 넘게 작지 않다.
// - 크기가 같은 소리끼리의 차이가 MaxShort dB 안이다.
// 소리는 내지 않는다 (파일을 읽어 재기만 한다). 그래도 다른 시험과 같이 AudioListener.volume = 0으로 둔다.
// 시험 어셈블리는 화면 어셈블리를 참조하지 않으므로 화면의 것은 이름으로 부른다 (RunScreenPlayTest와 같다).
//
// 실행: 에디터의 Test Runner 창 PlayMode 탭, 또는 에디터를 닫고 배치 모드로 (1분 안팎)
//   & "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -projectPath C:\Dev\DuelRoguelike -runTests -testPlatform PlayMode -testFilter DuelRoguelike.Tests.SoundLevelPlayTest -testResults C:\Dev\soundlevel-results.xml -logFile C:\Dev\soundlevel.log
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DuelRoguelike.Tests
{
    public sealed class SoundLevelPlayTest
    {
        /// <summary>끝까지 키워도 모자란 소리가 목표보다 작아도 되는 한계 (dB).</summary>
        const float MaxShort = 2.5f;
        /// <summary>목표를 넘어도 되는 한계 (dB): 계산의 반올림만큼.</summary>
        const float MaxOver = 0.2f;

        float _listenerVolume = 1f;

        [SetUp]
        public void SetUp()
        {
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            AudioListener.volume = _listenerVolume;
        }

        /// <summary>Sound.LevelReport의 한 줄.</summary>
        sealed class Row
        {
            public string Sfx, File;
            public float Seconds, Skipped, SideDb, Gain, TargetDb, ActualDb;
            public bool Centered;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EverySoundIsLeveledToItsTarget()
        {
            yield return null;
            var sound = Type.GetType("DuelRoguelike.UI.Sound, DuelRoguelike.UI", true);
            var missing = (List<string>)sound.GetMethod("MissingFiles").Invoke(null, null);
            Assert.IsEmpty(missing, "표에 있는데 없는 효과음 파일: " + string.Join(", ", missing));

            var lines = (List<string>)sound.GetMethod("LevelReport").Invoke(null, null);
            Assert.Greater(lines.Count, 30, "크기 맞춤 결과가 너무 적다");
            var rows = new List<Row>();
            foreach (string line in lines)
            {
                var cells = line.Split('\t');
                Assert.AreEqual(9, cells.Length, $"샘플을 읽지 못해 크기를 맞추지 못한 소리: {line}");
                rows.Add(new Row
                {
                    Sfx = cells[0], File = cells[1],
                    Seconds = Number(cells[2]), Skipped = Number(cells[3]), SideDb = Number(cells[4]), Centered = cells[5] == "Y",
                    Gain = Number(cells[6]), TargetDb = Number(cells[7]), ActualDb = Number(cells[8]),
                });
            }

            // 표로 남긴다 (소리 크기를 맞출 때 본다)
            var table = new List<string> { "[소리 크기] 소리\t파일\t길이\t자른 무음\t좌우 차이\t가운데로\t원본의 몇 배\t목표 dB\t실제 dB\t모자람" };
            foreach (var r in rows)
                table.Add($"[소리 크기] {r.Sfx}\t{r.File}\t{r.Seconds:0.00}\t{r.Skipped:0.00}\t{r.SideDb:0.0}\t{(r.Centered ? "예" : "")}\t{r.Gain:0.00}\t{r.TargetDb:0.0}\t{r.ActualDb:0.0}\t{(r.ActualDb - r.TargetDb < -0.05f ? (r.ActualDb - r.TargetDb).ToString("0.0") : "")}");
            Debug.Log(string.Join("\n", table));

            foreach (var r in rows)
            {
                Assert.LessOrEqual(r.ActualDb, r.TargetDb + MaxOver, $"{r.Sfx}({r.File})가 목표보다 크다: 목표 {r.TargetDb:0.0}dB, 실제 {r.ActualDb:0.0}dB");
                Assert.GreaterOrEqual(r.ActualDb, r.TargetDb - MaxShort, $"{r.Sfx}({r.File})가 목표보다 너무 작다 (끝까지 키워도 모자란다): 목표 {r.TargetDb:0.0}dB, 실제 {r.ActualDb:0.0}dB");
                Assert.Greater(r.Seconds, 0f, $"{r.Sfx}({r.File})의 길이가 0이다");
                Assert.IsTrue(r.Centered || Mathf.Abs(r.SideDb) <= 2.05f, $"{r.Sfx}({r.File})가 한쪽으로 치우쳤는데 가운데로 모으지 않았다: 좌우 차이 {r.SideDb:0.0}dB");
            }
            // 크기가 같은 소리끼리
            foreach (var group in rows.GroupBy(r => r.TargetDb))
            {
                float spread = group.Max(r => r.ActualDb) - group.Min(r => r.ActualDb);
                Debug.Log($"[소리 크기] 목표 {group.Key:0.0}dB인 소리 {group.Count()}개: 실제 {group.Min(r => r.ActualDb):0.0} ~ {group.Max(r => r.ActualDb):0.0}dB");
                Assert.LessOrEqual(spread, MaxShort + MaxOver, $"목표가 {group.Key:0.0}dB인 소리끼리 {spread:0.0}dB 차이 난다");
            }
        }

        static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    }
}

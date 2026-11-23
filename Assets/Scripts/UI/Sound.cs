// 화면의 소리 (효과음·배경음). 화면 코드만 부르고 룰 엔진은 모른다.
// - 파일: Assets/UI/Resources/Audio/Sfx/*.ogg (효과음), Assets/UI/Resources/Audio/Music/tower.ogg·duel.mp3 (배경음, 없으면 조용하다.
//   길어서 Streaming으로 불러온다 — .meta의 loadType: 2).
//   출처와 라이선스는 THIRD_PARTY_NOTICES.md "소리".
// - 어느 소리에 어느 파일을 쓰고 얼마나 크게 낼지는 아래 Cues 표 하나에 모았다. 파일을 바꾸거나 크기를 고치려면 표만 고친다.
// - 음량 설정(효과음·배경음 0~10, 0이면 끔)은 PlayerPrefs에 둔다. 처음 화면·탑 메뉴의 "설정" 창에서 고친다.
// - 겹쳐서 시끄럽지 않게: 같은 소리는 MinGap 안에 다시 내지 않는다. 동시에 내는 소리는 Voices개까지이고, 넘으면 가장 먼저 낸 것을 끊는다.
// - 듀얼의 가속: 듀얼 화면이 가속하는 동안 듀얼 효과음을 그 배속으로 빠르게 낸다 (Rate). 내던 소리도 그 순간부터 빨라지고 음이 높아진다.
//   늦게 내기·같은 소리 간격·앞부분만 쓰기도 같은 배속의 시계(DuelClock)로 잰다. 조작음·탑의 소리와 배경음은 그대로다.
// - 크기 맞춤: 파일마다 크기·시작하는 때·좌우가 제각각이라, 처음 불러올 때 낼 수 있게 준비한 사본을 만든다 (SoundLevel.cs):
//   앞의 무음을 잘라 내고, 한쪽으로 치우친 소리는 가운데로 모으고, 깨지지 않는 데까지 키운다. 원본 파일은 고치지 않는다.
//   낼 때는 사본의 귀에 들리는 세기(가장 큰 0.2초)가 "기준 × 표의 크기"가 되게 줄인다. 그래서 표의 크기가 같은 소리는 같은 크기로 들린다.
//   사본을 1배보다 크게 낼 수는 없으므로, 끝까지 키워도 모자란 소리(짧고 뾰족한 딸깍 소리)는 목표보다 조금 작게 난다.
//   소리마다의 결과는 LevelReport()로 본다 (SoundLevelPlayTest가 표로 남긴다).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuelRoguelike.UI
{
    /// <summary>효과음. 무엇에 쓰는지는 Sound.Cues 표를 본다.</summary>
    public enum Sfx
    {
        // 듀얼
        Draw, Set, Flip, Shuffle, Summon, SpecialSummon, Activate, ActivateSwoosh, Target, ToGrave, ToHand, Destroy, Banish, Equip,
        Attack, DirectHit, DirectHitBoom, Damage, Recover, NewTurn, Phase, Coin, Dice, Win, Lose, GameDraw,
        // 조작
        Confirm, Move, Cancel, WindowOpen,
        // 탑
        Step, Arrive, Encounter, BossEncounter,
    }

    public enum MusicTrack { None, Tower, Duel }

    public static class Sound
    {
        sealed class Cue
        {
            public string[] Files;
            /// <summary>
            /// 크기: 귀에 들리는 세기를 "보통"(1)에 견준 배율. 파일마다의 크기는 따로 맞추므로 (SoundLevel) 같은 값이면 같은 크기로 들린다.
            /// 아래 등급(Loud·Normal·Light·Soft)을 쓰고, 따로 맞출 소리만 숫자를 곱한다.
            /// </summary>
            public float Level = 1f;
            public float Pitch = 1f;
            /// <summary>0보다 크면 이만큼(초) 낸 뒤 줄여서 끊는다 (긴 파일의 앞부분만 쓴다). 앞의 무음을 잘라 낸 곳부터 센다.</summary>
            public float MaxSeconds;
            /// <summary>같은 소리를 다시 내지 않는 시간 (초).</summary>
            public float MinGap = 0.06f;
            /// <summary>조작음·탑의 소리 (듀얼의 가속에 빨라지지 않는다).</summary>
            public bool Ui;

            public Cue(float level, params string[] files)
            {
                Level = level;
                Files = files;
            }
        }

        // 크기 등급. 귀에 들리는 세기를 "보통"에 견준 배율이다 (1.6 = +4dB, 0.63 = -4dB, 0.45 = -7dB).
        /// <summary>강조: 묵직하게 들려야 하는 소리.</summary>
        const float Loud = 1.6f;
        /// <summary>보통: 소환·공격·파괴 같은 사건.</summary>
        const float Normal = 1f;
        /// <summary>가볍게: 카드가 움직이는 소리.</summary>
        const float Light = 0.63f;
        /// <summary>조작: 조작음과 발걸음처럼 자주 나는 작은 소리.</summary>
        const float Soft = 0.45f;

        // 듀얼의 기본음은 EDOPro(유희왕 팬 시뮬레이터)가 쓰는 Freesound 소리를 따랐다 (드로우·세트·반전·셔플·일반 소환·공격·데미지·회복·턴·페이즈·코인·주사위).
        static readonly Dictionary<Sfx, Cue> Cues = new Dictionary<Sfx, Cue>
        {
            { Sfx.Draw, new Cue(Light, "draw") { MinGap = 0.05f } },
            { Sfx.Set, new Cue(Light, "set") },
            { Sfx.Flip, new Cue(Light, "flip") },
            { Sfx.Shuffle, new Cue(Light, "shuffle") { MaxSeconds = 1.4f, MinGap = 0.5f } },
            { Sfx.Summon, new Cue(Normal, "summon") { MaxSeconds = 2.2f } },
            { Sfx.SpecialSummon, new Cue(Normal, "special-summon") { MaxSeconds = 1.8f } },
            { Sfx.Activate, new Cue(Light, "activate") },
            { Sfx.ActivateSwoosh, new Cue(Soft, "phase") },
            { Sfx.Target, new Cue(Light, "target") },
            { Sfx.ToGrave, new Cue(Light, "grave-1", "grave-2") { MinGap = 0.05f } },
            { Sfx.ToHand, new Cue(Light, "slide-1", "slide-2") { MinGap = 0.05f } },
            { Sfx.Destroy, new Cue(Normal, "destroy") { MinGap = 0.08f } },
            { Sfx.Banish, new Cue(Normal, "banish") { MinGap = 0.08f } },
            { Sfx.Equip, new Cue(Normal, "equip") },
            { Sfx.Attack, new Cue(Normal, "attack") },
            // 직접 공격의 데미지: 펀치와 낮은 쿵을 같이 낸다 (DuelScreen.Sound.cs)
            { Sfx.DirectHit, new Cue(Loud, "hit-punch") },
            { Sfx.DirectHitBoom, new Cue(Loud, "hit-boom") { MaxSeconds = 2f } },
            { Sfx.Damage, new Cue(Normal, "damage") { MaxSeconds = 1.6f } },
            { Sfx.Recover, new Cue(Normal, "recover") },
            { Sfx.NewTurn, new Cue(Light, "turn") { MaxSeconds = 2.0f, MinGap = 0.3f } },
            { Sfx.Phase, new Cue(Soft, "phase") { MinGap = 0.2f } },
            { Sfx.Coin, new Cue(Normal, "coin") { MinGap = 0.1f } },
            { Sfx.Dice, new Cue(Normal, "dice") { MinGap = 0.1f } },
            { Sfx.Win, new Cue(Normal, "win") { MaxSeconds = 5.5f, MinGap = 1f } },
            { Sfx.Lose, new Cue(Normal, "gong") { Pitch = 0.75f, MaxSeconds = 5f, MinGap = 1f } },
            { Sfx.GameDraw, new Cue(Normal, "gong") { MaxSeconds = 4f, MinGap = 1f } },

            { Sfx.Confirm, new Cue(Soft, "ui-confirm") { Ui = true, MinGap = 0.05f } },
            // 커서를 옮길 때마다 나므로 다른 조작음보다 작게 (-10dB)
            { Sfx.Move, new Cue(Soft * 0.7f, "ui-move") { Ui = true, MinGap = 0.03f } },
            { Sfx.Cancel, new Cue(Soft, "ui-cancel") { Ui = true, MinGap = 0.05f } },
            { Sfx.WindowOpen, new Cue(Soft, "ui-open") { Ui = true, MinGap = 0.15f } },

            { Sfx.Step, new Cue(Soft, "step-1", "step-2", "step-3", "step-4") { Ui = true, MinGap = 0.1f } },
            { Sfx.Arrive, new Cue(Light, "arrive") { Ui = true } },
            { Sfx.Encounter, new Cue(Light, "heartbeat") { Ui = true, MinGap = 0.2f } },
            { Sfx.BossEncounter, new Cue(Normal, "heartbeat") { Ui = true, Pitch = 0.8f, MinGap = 0.2f } },
        };

        static readonly Dictionary<MusicTrack, string> MusicFiles = new Dictionary<MusicTrack, string>
        {
            { MusicTrack.Tower, "tower" },
            { MusicTrack.Duel, "duel" },
        };

        const string SfxPrefKey = "audio.sfx-level", MusicPrefKey = "audio.music-level";
        public const int MaxLevel = 10;
        const int DefaultSfxLevel = 8, DefaultMusicLevel = 6;
        /// <summary>배경음은 효과음보다 작게 낸다 (설정 10일 때의 크기).</summary>
        const float MusicBase = 0.5f;
        /// <summary>
        /// 크기 맞춤의 기준: 크기가 "보통"(1)인 소리의 귀에 들리는 세기 (가장 큰 0.2초의 RMS, SoundLevel.Loudest).
        /// 올리면 효과음이 모두 커지지만, 끝까지 키워도 모자라는 소리가 늘어 크기가 덜 고르게 된다.
        /// </summary>
        const float Reference = 0.12f;

        /// <summary>크기를 맞춘 효과음 파일.</summary>
        sealed class Leveled
        {
            /// <summary>낼 소리: 준비한 사본. 파일을 읽지 못했으면 원본.</summary>
            public AudioClip Clip;
            /// <summary>준비한 결과 (10ms마다의 세기, 잘라 낸 무음 등). 파일을 읽지 못했으면 없다.</summary>
            public SoundLevel.Prepared Info;
        }

        static int? _sfxLevel, _musicLevel;
        static SoundPlayer _player;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, Leveled> LeveledFiles = new Dictionary<string, Leveled>();
        static readonly Dictionary<Sfx, float> LastPlayed = new Dictionary<Sfx, float>();
        static readonly Dictionary<Sfx, int> Counts = new Dictionary<Sfx, int>();
        static readonly Dictionary<Sfx, int> NextVariant = new Dictionary<Sfx, int>();

        /// <summary>효과음 음량 (0~10, 0이면 끔).</summary>
        public static int SfxLevel
        {
            get => _sfxLevel ??= Mathf.Clamp(PlayerPrefs.GetInt(SfxPrefKey, DefaultSfxLevel), 0, MaxLevel);
            set
            {
                _sfxLevel = Mathf.Clamp(value, 0, MaxLevel);
                PlayerPrefs.SetInt(SfxPrefKey, _sfxLevel.Value);
                if (_player != null)
                    _player.ApplyLevels();
            }
        }

        /// <summary>배경음 음량 (0~10, 0이면 끔).</summary>
        public static int MusicLevel
        {
            get => _musicLevel ??= Mathf.Clamp(PlayerPrefs.GetInt(MusicPrefKey, DefaultMusicLevel), 0, MaxLevel);
            set
            {
                _musicLevel = Mathf.Clamp(value, 0, MaxLevel);
                PlayerPrefs.SetInt(MusicPrefKey, _musicLevel.Value);
                if (_player != null)
                    _player.ApplyLevels();
            }
        }

        internal static float SfxScale => SfxLevel / (float)MaxLevel;
        internal static float MusicScale => MusicLevel / (float)MaxLevel * MusicBase;

        /// <summary>듀얼 효과음의 시계: 배속(Rate)만큼 빨리 흐른다. 조작음·탑의 소리는 실제 시간으로 잰다.</summary>
        internal static readonly ScaledClock DuelClock = new ScaledClock();

        /// <summary>
        /// 듀얼 효과음의 배속 (1이면 보통). 듀얼 화면이 가속하는 동안 올린다. 내던 소리도 그 순간부터 이 배속으로 빨라지고
        /// (음이 높아진다), 1로 되돌리면 원래 속도로 돌아온다. 조작음·탑의 소리와 배경음은 바뀌지 않는다.
        /// </summary>
        public static float Rate
        {
            get => DuelClock.Rate;
            set
            {
                if (Mathf.Approximately(value, DuelClock.Rate))
                    return;
                DuelClock.Rate = value;
                if (_player != null)
                    _player.ApplyRate();
            }
        }

        /// <summary>이 소리를 재는 시계의 지금: 듀얼 효과음은 배속 시계, 조작음·탑의 소리는 실제 시간.</summary>
        internal static float Now(bool ui) => ui ? Time.realtimeSinceStartup : DuelClock.Now;

        /// <summary>이 소리를 낸 횟수 (MinGap에 걸러진 것은 세지 않는다). 화면 시험이 본다.</summary>
        public static int Count(Sfx sfx) => Counts.TryGetValue(sfx, out int n) ? n : 0;

        /// <summary>표에 있는데 불러올 수 없는 효과음 파일 (화면 시험이 본다). 배경음은 없어도 되므로 보지 않는다.</summary>
        public static List<string> MissingFiles()
        {
            var missing = new List<string>();
            foreach (var cue in Cues.Values)
                foreach (string file in cue.Files)
                    if (!missing.Contains(file) && Resources.Load<AudioClip>("Audio/Sfx/" + file) == null)
                        missing.Add(file);
            return missing;
        }

        /// <summary>켜면 파일을 처음 불러올 때 크기 맞춤의 결과(길이, 잘라 낸 무음, 키운 배율)를 로그에 남긴다 (소리 크기를 맞출 때).</summary>
        public static bool LogLevels;

        /// <summary>
        /// 표의 모든 소리의 크기 맞춤 결과. 한 줄에 소리 하나, 칸은 탭으로 나눈다 (소리 크기를 맞출 때와 화면 시험이 본다):
        /// Sfx, 파일, 낼 길이(초), 잘라 낸 앞의 무음(초), 좌우 차이(dB, 왼쪽 - 오른쪽), 가운데로 모았는지(Y/N), 원본에 견준 배율, 목표(dB), 실제(dB).
        /// dB는 귀에 들리는 세기를 "보통"에 견준 값이다. 실제가 목표보다 작으면 끝까지 키워도 모자란 소리다.
        /// 파일을 읽지 못한 소리는 뒤의 칸이 비어 있다.
        /// </summary>
        public static List<string> LevelReport()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<string>();
            foreach (var pair in Cues)
                foreach (string file in pair.Value.Files)
                {
                    var cue = pair.Value;
                    var sound = LoadLeveled(file);
                    var info = sound?.Info;
                    if (info == null)
                    {
                        lines.Add($"{pair.Key}\t{file}");
                        continue;
                    }
                    float length = sound.Clip.length / cue.Pitch;
                    if (cue.MaxSeconds > 0f)
                        length = Mathf.Min(length, cue.MaxSeconds);
                    float loudness = SoundLevel.Loudest(info.HopPower, cue.MaxSeconds * cue.Pitch);
                    float gain = GainOf(sound, cue);
                    lines.Add(string.Join("\t", pair.Key.ToString(), file,
                        length.ToString("0.00", inv), info.SkippedSeconds.ToString("0.00", inv),
                        info.SideDb.ToString("0.0", inv), info.Centered ? "Y" : "N", (info.Boost * gain).ToString("0.00", inv),
                        SoundLevel.Db(cue.Level).ToString("0.0", inv), SoundLevel.Db(loudness * gain / Reference).ToString("0.0", inv)));
                }
            return lines;
        }

        /// <summary>지금 고른 배경음 (파일이 없어도 고른 것은 남는다). 화면 시험이 본다.</summary>
        public static MusicTrack CurrentMusic { get; private set; }

        /// <summary>
        /// 효과음을 낸다. delay초 뒤에 낸다 (여러 장 드로우처럼 조금씩 늦게 낼 때). pitch·volume은 표의 값에 곱한다.
        /// 듀얼 효과음의 delay와 MinGap은 배속 시계로 잰다 (가속 중에는 그만큼 짧아진다).
        /// </summary>
        public static void Play(Sfx sfx, float delay = 0f, float pitch = 1f, float volume = 1f)
        {
            if (!Application.isPlaying || !Cues.TryGetValue(sfx, out var cue))
                return;
            float now = Now(cue.Ui);
            float at = now + Mathf.Max(0f, delay);
            if (LastPlayed.TryGetValue(sfx, out float last) && Mathf.Abs(at - last) < cue.MinGap)
                return;
            LastPlayed[sfx] = at;
            // 취소 키가 닫기 버튼을 누르면 버튼의 결정 소리는 내지 않는다
            if (sfx == Sfx.Cancel)
                LastPlayed[Sfx.Confirm] = at;
            Counts[sfx] = Count(sfx) + 1;
            if (SfxLevel == 0)
                return;
            var sound = NextSound(sfx, cue);
            if (sound == null)
                return;
            Player().Play(sound.Clip, GainOf(sound, cue) * volume, cue.Pitch * pitch, Mathf.Max(0f, delay), cue.MaxSeconds, cue.Ui);
        }

        /// <summary>배경음을 바꾼다 (같은 곡이면 그대로 둔다). 천천히 넘어간다. None이면 멈춘다.</summary>
        public static void Music(MusicTrack track)
        {
            if (!Application.isPlaying || (track == CurrentMusic && _player != null))
                return;
            CurrentMusic = track;
            var clip = track != MusicTrack.None && MusicFiles.TryGetValue(track, out string file) ? Load("Audio/Music/" + file) : null;
            Player().CrossFade(clip);
        }

        static Leveled NextSound(Sfx sfx, Cue cue)
        {
            if (cue.Files.Length == 0)
                return null;
            // 여러 파일은 돌아가며 쓴다 (같은 소리가 연달아 똑같이 들리지 않게)
            int i = NextVariant.TryGetValue(sfx, out int n) ? n : 0;
            NextVariant[sfx] = (i + 1) % cue.Files.Length;
            return LoadLeveled(cue.Files[i]);
        }

        /// <summary>표에 있는 효과음 파일 (겹치지 않게).</summary>
        internal static List<string> SfxFiles()
        {
            var files = new List<string>();
            foreach (var cue in Cues.Values)
                foreach (string file in cue.Files)
                    if (!files.Contains(file))
                        files.Add(file);
            return files;
        }

        /// <summary>효과음 파일 하나를 미리 준비해 둔다 (처음 낼 때 끊기지 않게, SoundPlayer가 한 프레임에 하나씩 부른다).</summary>
        internal static void Warm(string file) => LoadLeveled(file);

        /// <summary>크기를 맞춘 효과음 파일. 처음 부를 때 준비한다. 파일이 없으면 null.</summary>
        static Leveled LoadLeveled(string file)
        {
            if (LeveledFiles.TryGetValue(file, out var sound) && sound.Clip != null)
                return sound;
            var clip = Load("Audio/Sfx/" + file);
            if (clip == null)
                return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Loading)
                return new Leveled { Clip = clip }; // 아직 읽는 중: 이번에는 원본을 내고 다음에 준비한다
            sound = Level(clip);
            LeveledFiles[file] = sound;
            return sound;
        }

        /// <summary>파일을 낼 수 있게 준비한 사본을 만든다 (SoundLevel.Prepare). 샘플을 읽지 못하면 원본을 그대로 쓴다.</summary>
        static Leveled Level(AudioClip clip)
        {
            int channels = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * channels];
            if (data.Length == 0 || clip.loadState != AudioDataLoadState.Loaded || !clip.GetData(data, 0))
            {
                // 가져오기 설정의 Load Type이 Decompress On Load가 아니면 샘플을 읽을 수 없다
                Debug.LogWarning($"[소리] 크기를 맞출 수 없어 그대로 냅니다: {clip.name}");
                return new Leveled { Clip = clip };
            }
            var info = SoundLevel.Prepare(data, channels, clip.frequency);
            if (info.Frames == 0)
                return new Leveled { Clip = clip };
            var copy = AudioClip.Create(clip.name, info.Frames, 2, clip.frequency, false);
            copy.SetData(info.Samples, 0);
            info.Samples = null; // 사본에 담았으니 놓아준다
            clip.UnloadAudioData(); // 원본의 샘플은 더 쓰지 않는다
            if (LogLevels)
                Debug.Log($"[소리] {clip.name}: {copy.length:0.00}초 (앞 {info.SkippedSeconds:0.00}초를 잘라 냄{(info.Centered ? ", 가운데로 모음" : "")}), {info.Boost:0.00}배로 키움, "
                    + $"가장 큰 세기 {SoundLevel.Loudest(info.HopPower, 0f):0.000} (기준 {Reference})");
            return new Leveled { Clip = copy, Info = info };
        }

        /// <summary>
        /// 이 소리를 낼 음량 (0~1): 사본의 귀에 들리는 세기(쓰는 앞부분에서 가장 큰 0.2초)가 "기준 × 표의 크기"가 되게 한다.
        /// 사본은 이미 깨지지 않는 데까지 키워 두었으므로 1을 넘길 수 없다.
        /// </summary>
        static float GainOf(Leveled sound, Cue cue)
        {
            if (sound.Info == null)
                return Mathf.Min(1f, cue.Level) * 0.5f; // 세기를 모른다: 크게 나지 않게 반만 낸다
            float loudness = SoundLevel.Loudest(sound.Info.HopPower, cue.MaxSeconds * cue.Pitch);
            return loudness > 0f ? Mathf.Min(1f, cue.Level * Reference / loudness) : 0f;
        }

        static AudioClip Load(string path)
        {
            if (Clips.TryGetValue(path, out var clip) && clip != null)
                return clip;
            clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                if (!path.StartsWith("Audio/Music/"))
                    Debug.LogWarning($"[소리] 파일이 없습니다: Resources/{path}");
            }
            Clips[path] = clip;
            return clip;
        }

        static SoundPlayer Player()
        {
            if (_player != null)
                return _player;
            var go = new GameObject("Sound") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _player = go.AddComponent<SoundPlayer>();
            return _player;
        }
    }

    /// <summary>소리를 실제로 내는 쪽: 효과음 소리통 여러 개와 배경음 두 개(넘어갈 때 엇갈려 키우고 줄인다).</summary>
    sealed class SoundPlayer : MonoBehaviour
    {
        const int Voices = 10;
        const float StopFade = 0.25f, MusicFade = 1.2f;
        /// <summary>소리통의 재생 속도(AudioSource.pitch)는 3배까지만 올린다. 배속이 그보다 크면 남는 길이는 줄여서 끊긴다.</summary>
        const float MaxPitch = 3f;

        sealed class Voice
        {
            public AudioSource Source;
            /// <summary>끝나는 때·줄여서 끊는 때. 이 소리의 시계(Sound.Now(Ui))로 잰다: 듀얼 효과음은 배속 시계, 조작음은 실제 시간.</summary>
            public float BusyUntil, StopAt;
            /// <summary>낸 때 (실제 시간). 소리통이 모자랄 때 가장 먼저 낸 것을 고른다.</summary>
            public float StartedReal;
            public float BaseVolume, BasePitch = 1f;
            public bool Ui;
        }

        readonly List<Voice> _voices = new List<Voice>();
        readonly AudioSource[] _music = new AudioSource[2];
        // 배경음 두 개의 지금 크기·넘어가기 전 크기·목표 크기 (0~1, 설정 음량은 곱하기 전)
        readonly float[] _mix = new float[2], _mixFrom = new float[2], _mixTo = new float[2];
        float _fadeStart = -1f;
        int _current;

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _voices.Add(new Voice { Source = source });
            }
            for (int i = 0; i < 2; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                _music[i] = source;
            }
        }

        /// <summary>효과음 파일을 한 프레임에 하나씩 미리 준비한다 (처음 낼 때 사본을 만드느라 끊기지 않게).</summary>
        IEnumerator Start()
        {
            foreach (string file in Sound.SfxFiles())
            {
                Sound.Warm(file);
                yield return null;
            }
        }

        /// <summary>delay·maxSeconds와 소리의 길이는 이 소리의 시계로 잰다 (듀얼 효과음은 배속 시계: 가속 중에는 그만큼 빨리 지나간다).</summary>
        public void Play(AudioClip clip, float volume, float pitch, float delay, float maxSeconds, bool ui)
        {
            float now = Sound.Now(ui), real = Time.realtimeSinceStartup;
            float rate = ui ? 1f : Sound.Rate;
            // 비어 있는 소리통, 없으면 가장 먼저 낸 것을 끊는다
            Voice voice = null;
            foreach (var v in _voices)
                if (v.BusyUntil <= Sound.Now(v.Ui))
                {
                    voice = v;
                    break;
                }
            if (voice == null)
            {
                voice = _voices[0];
                foreach (var v in _voices)
                    if (v.StartedReal < voice.StartedReal)
                        voice = v;
            }
            float length = clip.length / Mathf.Max(0.05f, pitch);
            if (maxSeconds > 0f)
                length = Mathf.Min(length, maxSeconds);
            voice.StartedReal = real + delay / rate;
            voice.BusyUntil = now + delay + length;
            voice.StopAt = maxSeconds > 0f && maxSeconds < clip.length / Mathf.Max(0.05f, pitch) ? voice.BusyUntil : 0f;
            voice.BaseVolume = Mathf.Clamp01(volume);
            voice.BasePitch = pitch;
            voice.Ui = ui;
            var source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = RatedPitch(voice, rate);
            source.volume = voice.BaseVolume * Sound.SfxScale;
            if (delay > 0f)
                source.PlayDelayed(delay / rate); // 실제 시간으로는 배속만큼 일찍 낸다
            else
                source.Play();
        }

        static float RatedPitch(Voice voice, float rate) => Mathf.Min(Mathf.Max(MaxPitch, voice.BasePitch), voice.BasePitch * rate);

        /// <summary>배속이 바뀌었다: 내고 있거나 곧 낼 듀얼 효과음의 재생 속도를 새 배속에 맞춘다.</summary>
        public void ApplyRate()
        {
            float now = Sound.DuelClock.Now, rate = Sound.Rate;
            foreach (var v in _voices)
                if (!v.Ui && v.BusyUntil > now)
                    v.Source.pitch = RatedPitch(v, rate);
        }

        public void CrossFade(AudioClip clip)
        {
            var current = _music[_current];
            if (clip != null && current.clip == clip && current.isPlaying)
            {
                // 같은 곡으로 돌아왔다: 줄이던 것을 다시 키운다
                FadeTo(_current, 1f);
                return;
            }
            _current = 1 - _current;
            var source = _music[_current];
            source.Stop();
            source.clip = clip;
            _mix[_current] = 0f;
            if (clip != null)
                source.Play();
            FadeTo(_current, clip != null ? 1f : 0f);
        }

        /// <summary>main은 mainTo로, 다른 하나는 0으로 MusicFade초 동안 옮긴다.</summary>
        void FadeTo(int main, float mainTo)
        {
            for (int i = 0; i < 2; i++)
                _mixFrom[i] = _mix[i];
            _mixTo[main] = mainTo;
            _mixTo[1 - main] = 0f;
            _fadeStart = Time.realtimeSinceStartup;
        }

        public void ApplyLevels()
        {
            foreach (var v in _voices)
                if (v.BusyUntil > Sound.Now(v.Ui) && v.StopAt <= 0f)
                    v.Source.volume = v.BaseVolume * Sound.SfxScale;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var v in _voices)
            {
                if (v.StopAt <= 0f)
                    continue;
                float left = v.StopAt - Sound.Now(v.Ui);
                if (left <= 0f)
                {
                    v.Source.Stop();
                    v.StopAt = 0f;
                }
                else if (left < StopFade)
                    v.Source.volume = Mathf.Min(v.Source.volume, v.BaseVolume * Sound.SfxScale * (left / StopFade));
            }
            if (_fadeStart >= 0f)
            {
                float t = Mathf.Clamp01((now - _fadeStart) / MusicFade);
                for (int i = 0; i < 2; i++)
                {
                    _mix[i] = Mathf.Lerp(_mixFrom[i], _mixTo[i], t);
                    if (t >= 1f && _mixTo[i] <= 0f)
                        _music[i].Stop();
                }
                if (t >= 1f)
                    _fadeStart = -1f;
            }
            for (int i = 0; i < 2; i++)
                _music[i].volume = _mix[i] * Sound.MusicScale;
        }
    }
}

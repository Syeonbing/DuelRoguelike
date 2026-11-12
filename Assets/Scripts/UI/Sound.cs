// 화면의 소리 (효과음·배경음). 화면 코드만 부르고 룰 엔진은 모른다.
// - 파일: Assets/UI/Resources/Audio/Sfx/*.ogg (효과음), Assets/UI/Resources/Audio/Music/tower.ogg·duel.mp3 (배경음, 없으면 조용하다.
//   길어서 Streaming으로 불러온다 — .meta의 loadType: 2).
//   출처와 라이선스는 THIRD_PARTY_NOTICES.md "소리".
// - 어느 소리에 어느 파일을 쓰고 얼마나 크게 낼지는 아래 Cues 표 하나에 모았다. 파일을 바꾸거나 음량을 고치려면 표만 고친다.
// - 음량 설정(효과음·배경음 0~10, 0이면 끔)은 PlayerPrefs에 둔다. 처음 화면·탑 메뉴의 "설정" 창에서 고친다.
// - 겹쳐서 시끄럽지 않게: 같은 소리는 MinGap 안에 다시 내지 않는다. 동시에 내는 소리는 Voices개까지이고, 넘으면 가장 먼저 낸 것을 끊는다.
//   듀얼의 건너뛰기 중에는 화면이 소리를 부르지 않고(연출과 같다), 건너뛰기를 시작하면 내던 듀얼 소리를 빨리 줄인다 (Hush).
// - 파일마다 크기가 달라서, 처음 불러올 때 가장 큰 순간의 세기(50ms 창의 RMS)를 재고 기준보다 크면 줄인다 (키우지는 않는다).
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
            public float Volume = 1f;
            public float Pitch = 1f;
            /// <summary>0보다 크면 이만큼(초) 낸 뒤 줄여서 끊는다 (긴 파일의 앞부분만 쓴다).</summary>
            public float MaxSeconds;
            /// <summary>같은 소리를 다시 내지 않는 시간 (초).</summary>
            public float MinGap = 0.06f;
            /// <summary>조작음 (건너뛰기의 Hush에 줄이지 않는다).</summary>
            public bool Ui;
            /// <summary>크기 맞춤의 기준 세기 (RMS). 묵직하게 들려야 하는 소리는 더 크게 둔다.</summary>
            public float Rms = TargetRms;

            public Cue(float volume, params string[] files)
            {
                Volume = volume;
                Files = files;
            }
        }

        // 듀얼의 기본음은 EDOPro(유희왕 팬 시뮬레이터)가 쓰는 Freesound 소리를 따랐다 (드로우·세트·반전·셔플·일반 소환·공격·데미지·회복·턴·페이즈·코인·주사위).
        static readonly Dictionary<Sfx, Cue> Cues = new Dictionary<Sfx, Cue>
        {
            { Sfx.Draw, new Cue(0.8f, "draw") { MinGap = 0.05f } },
            { Sfx.Set, new Cue(0.9f, "set") },
            { Sfx.Flip, new Cue(0.9f, "flip") },
            { Sfx.Shuffle, new Cue(0.7f, "shuffle") { MaxSeconds = 1.4f, MinGap = 0.5f } },
            { Sfx.Summon, new Cue(0.85f, "summon") { MaxSeconds = 2.2f } },
            { Sfx.SpecialSummon, new Cue(0.8f, "special-summon") { MaxSeconds = 1.8f } },
            { Sfx.Activate, new Cue(1f, "activate") },
            { Sfx.ActivateSwoosh, new Cue(0.5f, "phase") },
            { Sfx.Target, new Cue(0.8f, "target") },
            { Sfx.ToGrave, new Cue(0.8f, "grave-1", "grave-2") { MinGap = 0.05f } },
            { Sfx.ToHand, new Cue(0.8f, "slide-1", "slide-2") { MinGap = 0.05f } },
            { Sfx.Destroy, new Cue(0.85f, "destroy") { MinGap = 0.08f } },
            { Sfx.Banish, new Cue(0.9f, "banish") { MinGap = 0.08f } },
            { Sfx.Equip, new Cue(0.9f, "equip") },
            { Sfx.Attack, new Cue(0.85f, "attack") },
            // 직접 공격의 데미지: 펀치와 낮은 쿵을 같이 낸다 (DuelScreen.Sound.cs)
            { Sfx.DirectHit, new Cue(1f, "hit-punch") { Rms = 0.3f } },
            { Sfx.DirectHitBoom, new Cue(1f, "hit-boom") { Rms = 0.3f, MaxSeconds = 2f } },
            { Sfx.Damage, new Cue(0.85f, "damage") { MaxSeconds = 1.6f } },
            { Sfx.Recover, new Cue(0.75f, "recover") },
            { Sfx.NewTurn, new Cue(0.6f, "turn") { MaxSeconds = 2.0f, MinGap = 0.3f } },
            { Sfx.Phase, new Cue(0.4f, "phase") { MinGap = 0.2f } },
            { Sfx.Coin, new Cue(0.85f, "coin") { MinGap = 0.1f } },
            { Sfx.Dice, new Cue(0.85f, "dice") { MinGap = 0.1f } },
            { Sfx.Win, new Cue(0.9f, "win") { MaxSeconds = 5.5f, MinGap = 1f } },
            { Sfx.Lose, new Cue(0.9f, "gong") { Pitch = 0.75f, MaxSeconds = 5f, MinGap = 1f } },
            { Sfx.GameDraw, new Cue(0.8f, "gong") { MaxSeconds = 4f, MinGap = 1f } },

            { Sfx.Confirm, new Cue(0.55f, "ui-confirm") { Ui = true, MinGap = 0.05f } },
            { Sfx.Move, new Cue(0.35f, "ui-move") { Ui = true, MinGap = 0.03f } },
            { Sfx.Cancel, new Cue(0.45f, "ui-cancel") { Ui = true, MinGap = 0.05f } },
            { Sfx.WindowOpen, new Cue(0.5f, "ui-open") { Ui = true, MinGap = 0.15f } },

            { Sfx.Step, new Cue(0.55f, "step-1", "step-2", "step-3", "step-4") { Ui = true, MinGap = 0.1f } },
            { Sfx.Arrive, new Cue(0.8f, "arrive") { Ui = true } },
            { Sfx.Encounter, new Cue(0.9f, "heartbeat") { Ui = true, MinGap = 0.2f } },
            { Sfx.BossEncounter, new Cue(1f, "heartbeat") { Ui = true, Pitch = 0.8f, MinGap = 0.2f } },
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
        /// <summary>가장 큰 순간의 세기가 이보다 크면 이만큼으로 줄인다 (RMS).</summary>
        const float TargetRms = 0.16f;

        static int? _sfxLevel, _musicLevel;
        static SoundPlayer _player;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<AudioClip, float> Loudness = new Dictionary<AudioClip, float>();
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

        /// <summary>켜면 파일을 처음 불러올 때 길이와 크기 맞춤 배율을 로그에 남긴다 (소리 크기를 맞출 때, 화면 시험).</summary>
        public static bool LogLevels;

        /// <summary>지금 고른 배경음 (파일이 없어도 고른 것은 남는다). 화면 시험이 본다.</summary>
        public static MusicTrack CurrentMusic { get; private set; }

        /// <summary>
        /// 효과음을 낸다. delay초 뒤에 낸다 (여러 장 드로우처럼 조금씩 늦게 낼 때). pitch·volume은 표의 값에 곱한다.
        /// </summary>
        public static void Play(Sfx sfx, float delay = 0f, float pitch = 1f, float volume = 1f)
        {
            if (!Application.isPlaying || !Cues.TryGetValue(sfx, out var cue))
                return;
            float now = Time.realtimeSinceStartup;
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
            var clip = NextClip(sfx, cue);
            if (clip == null)
                return;
            Player().Play(clip, cue.Volume * volume * GainOf(clip, cue.Rms), cue.Pitch * pitch, Mathf.Max(0f, delay), cue.MaxSeconds, cue.Ui);
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

        /// <summary>내던 듀얼 효과음을 빨리 줄인다 (건너뛰기를 시작할 때). 조작음과 탑의 소리는 두고.</summary>
        public static void Hush()
        {
            if (_player != null)
                _player.Hush();
        }

        static AudioClip NextClip(Sfx sfx, Cue cue)
        {
            if (cue.Files.Length == 0)
                return null;
            // 여러 파일은 돌아가며 쓴다 (같은 소리가 연달아 똑같이 들리지 않게)
            int i = NextVariant.TryGetValue(sfx, out int n) ? n : 0;
            NextVariant[sfx] = (i + 1) % cue.Files.Length;
            return Load("Audio/Sfx/" + cue.Files[i]);
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

        /// <summary>파일의 크기 맞춤: 가장 큰 순간(50ms 창)의 RMS가 기준(target)보다 크면 그만큼 줄이는 배율. 세기는 처음 한 번 잰다.</summary>
        static float GainOf(AudioClip clip, float target) => GainFor(LoudnessOf(clip), target);

        /// <summary>가장 큰 순간(50ms 창)의 RMS. 아직 읽는 중이면 0 (다음에 다시 잰다).</summary>
        static float LoudnessOf(AudioClip clip)
        {
            if (Loudness.TryGetValue(clip, out float loudest))
                return loudest;
            loudest = 0f;
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                return loudest; // 아직 읽는 중: 다음에 잰다
            int channels = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * channels];
            if (data.Length > 0 && clip.GetData(data, 0))
            {
                int window = Mathf.Max(channels, (int)(clip.frequency * 0.05f) * channels);
                for (int start = 0; start < data.Length; start += window)
                {
                    int end = Mathf.Min(data.Length, start + window);
                    double sum = 0;
                    for (int i = start; i < end; i++)
                        sum += data[i] * data[i];
                    loudest = Mathf.Max(loudest, Mathf.Sqrt((float)(sum / Mathf.Max(1, end - start))));
                }
                if (LogLevels)
                    Debug.Log($"[소리] {clip.name}: {clip.length:0.00}초, 가장 큰 세기 {loudest:0.000} (기준 {TargetRms} → {GainFor(loudest, TargetRms):0.00}배)");
            }
            Loudness[clip] = loudest;
            return loudest;
        }

        static float GainFor(float loudest, float target) => loudest > target ? target / loudest : 1f;

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
        const float StopFade = 0.25f, HushFade = 0.12f, MusicFade = 1.2f;

        sealed class Voice
        {
            public AudioSource Source;
            public float Started, BusyUntil, StopAt, BaseVolume;
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

        public void Play(AudioClip clip, float volume, float pitch, float delay, float maxSeconds, bool ui)
        {
            float now = Time.realtimeSinceStartup;
            // 비어 있는 소리통, 없으면 가장 먼저 낸 것을 끊는다
            Voice voice = null;
            foreach (var v in _voices)
                if (v.BusyUntil <= now)
                {
                    voice = v;
                    break;
                }
            if (voice == null)
            {
                voice = _voices[0];
                foreach (var v in _voices)
                    if (v.Started < voice.Started)
                        voice = v;
            }
            float length = clip.length / Mathf.Max(0.05f, pitch);
            if (maxSeconds > 0f)
                length = Mathf.Min(length, maxSeconds);
            voice.Started = now + delay;
            voice.BusyUntil = now + delay + length;
            voice.StopAt = maxSeconds > 0f && maxSeconds < clip.length / Mathf.Max(0.05f, pitch) ? voice.BusyUntil : 0f;
            voice.BaseVolume = Mathf.Clamp01(volume);
            voice.Ui = ui;
            var source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = voice.BaseVolume * Sound.SfxScale;
            if (delay > 0f)
                source.PlayDelayed(delay);
            else
                source.Play();
        }

        public void Hush()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var v in _voices)
                if (!v.Ui && v.BusyUntil > now)
                {
                    v.StopAt = now + HushFade;
                    v.BusyUntil = v.StopAt;
                }
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
            float now = Time.realtimeSinceStartup;
            foreach (var v in _voices)
                if (v.BusyUntil > now && v.StopAt <= 0f)
                    v.Source.volume = v.BaseVolume * Sound.SfxScale;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var v in _voices)
            {
                if (v.StopAt <= 0f)
                    continue;
                float left = v.StopAt - now;
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

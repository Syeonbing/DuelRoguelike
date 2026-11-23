// 효과음 크기 맞춤의 계산. 샘플 배열만 다루고 유니티의 소리 기능에는 기대지 않는다 (쓰는 곳: Sound.cs).
// 파일 하나를 낼 수 있게 준비한다 (Prepare):
// 1. 20Hz 아래를 걸러 낸다. 귀에 들리지 않는 떨림인데, 키우면 다른 소리와 섞일 때 깨지기 쉽다.
// 2. 앞의 무음을 잘라 낸다. 소리가 사건과 같은 때에 시작하게 한다.
// 3. 좌우 한쪽으로 치우친 소리는 가운데로 모은다 (한쪽 채널이 비어 있는 파일도 있다).
// 4. 깨지지 않는 데까지(최대 진폭 Headroom) 키운다. 누르거나 깎아서 더 키우지는 않는다.
//    원본의 MaxBoost배까지만 키운다 (거의 무음인 파일의 잡음까지 키우지 않게).
// 5. 10ms마다의 귀 기준 세기를 잰다.
// 귀 기준 세기: ITU-R BS.1770의 K 가중(높은 쪽을 4dB 올리고 38Hz 아래를 깎는다)을 건 뒤의 세기. 낮은 소리는 같은 진폭이라도 작게 들린다.
// 소리의 크기: 가장 큰 0.2초의 귀 기준 세기 (Loudest). 0.2초보다 짧은 소리는 0.2초로 나눈다 (짧을수록 작게 들린다).
using System;

namespace DuelRoguelike.UI
{
    static class SoundLevel
    {
        /// <summary>준비한 소리의 최대 진폭 (1을 넘으면 깨진다).</summary>
        public const float Headroom = 0.98f;
        /// <summary>원본을 키우는 배율의 한계.</summary>
        public const float MaxBoost = 8f;
        /// <summary>좌우의 귀 기준 세기가 이보다 크게 다르면 가운데로 모은다 (dB).</summary>
        public const float CenterDb = 2f;
        /// <summary>귀 기준 세기를 재는 간격 (초).</summary>
        public const float HopSeconds = 0.01f;
        /// <summary>가장 큰 순간을 재는 창의 길이 (간격의 개수: 0.2초).</summary>
        const int WindowHops = 20;
        /// <summary>이 아래는 귀에 들리지 않는다 (Hz).</summary>
        const double RumbleHz = 20;
        /// <summary>소리의 시작을 찾는 창(초), 시작보다 앞에 남기는 길이(초), 잘라 낸 곳을 키워 들이는 길이(초).</summary>
        const float OnsetWindow = 0.005f, OnsetLead = 0.01f, FadeIn = 0.003f;
        /// <summary>창의 세기가 가장 큰 창의 이 비율(-30dB)을 처음 넘는 곳을 소리의 시작으로 본다.</summary>
        const double OnsetPower = 0.001;

        /// <summary>낼 수 있게 준비한 소리.</summary>
        public sealed class Prepared
        {
            /// <summary>낼 샘플: 스테레오, 왼쪽·오른쪽을 번갈아 담았다.</summary>
            public float[] Samples;
            public int Frames;
            /// <summary>10ms마다의 귀 기준 세기 (제곱 평균, 두 채널의 평균).</summary>
            public float[] HopPower;
            /// <summary>잘라 낸 앞의 무음 (초).</summary>
            public float SkippedSeconds;
            /// <summary>가운데로 모았다.</summary>
            public bool Centered;
            /// <summary>좌우의 귀 기준 세기 차이 (dB, 왼쪽 - 오른쪽). 모노면 0.</summary>
            public float SideDb;
            /// <summary>원본을 키운 배율.</summary>
            public float Boost;
        }

        /// <summary>파일의 샘플(채널을 번갈아 담은 것)을 낼 수 있게 준비한다. 채널이 셋 이상이면 앞의 둘만 쓴다.</summary>
        public static Prepared Prepare(float[] interleaved, int channels, int rate)
        {
            channels = Math.Max(1, channels);
            int frames = interleaved.Length / channels;
            var left = new float[frames];
            var right = channels > 1 ? new float[frames] : null;
            for (int i = 0; i < frames; i++)
            {
                left[i] = interleaved[i * channels];
                if (right != null)
                    right[i] = interleaved[i * channels + 1];
            }
            HighPass(left, rate, RumbleHz);
            if (right != null)
                HighPass(right, rate, RumbleHz);

            int start = Onset(left, right, rate);
            int count = frames - start;
            var result = new Prepared { Frames = count, SkippedSeconds = start / (float)rate };

            // 귀 기준으로 거른 것 (세기를 잴 때만 쓴다)
            var heardLeft = KWeighted(left, start, count, rate);
            var heardRight = right != null ? KWeighted(right, start, count, rate) : null;
            if (right != null)
            {
                result.SideDb = (float)(10 * Math.Log10(Math.Max(MeanSquare(heardLeft), 1e-12) / Math.Max(MeanSquare(heardRight), 1e-12)));
                if (Math.Abs(result.SideDb) > CenterDb)
                {
                    for (int i = 0; i < count; i++)
                    {
                        left[start + i] = (left[start + i] + right[start + i]) * 0.5f;
                        heardLeft[i] = (heardLeft[i] + heardRight[i]) * 0.5f;
                    }
                    right = null;
                    heardRight = null;
                    result.Centered = true;
                }
            }

            float peak = 0f;
            for (int i = start; i < frames; i++)
            {
                peak = Math.Max(peak, Math.Abs(left[i]));
                if (right != null)
                    peak = Math.Max(peak, Math.Abs(right[i]));
            }
            float boost = peak > 1e-6f ? Math.Min(MaxBoost, Headroom / peak) : 1f;
            result.Boost = boost;

            // 모노와 가운데로 모은 소리는 양쪽에 같은 소리를 담는다
            int fade = start > 0 ? Math.Min(count, (int)(rate * FadeIn)) : 0;
            var samples = new float[count * 2];
            for (int i = 0; i < count; i++)
            {
                float gain = i < fade ? boost * i / fade : boost;
                samples[i * 2] = left[start + i] * gain;
                samples[i * 2 + 1] = (right ?? left)[start + i] * gain;
            }
            result.Samples = samples;

            int hop = Math.Max(1, (int)(rate * HopSeconds));
            var power = new float[(count + hop - 1) / hop];
            for (int h = 0; h < power.Length; h++)
            {
                int end = Math.Min(count, (h + 1) * hop);
                double sum = 0;
                for (int i = h * hop; i < end; i++)
                {
                    double a = heardLeft[i], b = heardRight != null ? heardRight[i] : a;
                    sum += (a * a + b * b) * 0.5;
                }
                // 끝의 모자란 간격도 간격 길이로 나눈다 (빈 곳은 무음으로 센다)
                power[h] = (float)(sum / hop * boost * boost);
            }
            result.HopPower = power;
            return result;
        }

        /// <summary>
        /// 앞의 seconds초(0 이하면 전부) 안에서 가장 큰 0.2초의 귀 기준 세기 (RMS).
        /// 0.2초보다 짧으면 모자란 곳을 무음으로 센다.
        /// </summary>
        public static float Loudest(float[] hopPower, float seconds)
        {
            int used = hopPower.Length;
            if (seconds > 0f)
                used = Math.Min(used, Math.Max(1, (int)Math.Round(seconds / HopSeconds)));
            double sum = 0, best = 0;
            for (int i = 0; i < used; i++)
            {
                sum += hopPower[i];
                if (i >= WindowHops)
                    sum -= hopPower[i - WindowHops];
                if (sum > best)
                    best = sum;
            }
            return (float)Math.Sqrt(best / WindowHops);
        }

        /// <summary>세기의 비율을 dB로.</summary>
        public static float Db(float ratio) => 20f * (float)Math.Log10(Math.Max(ratio, 1e-9f));

        /// <summary>소리가 시작하는 샘플: 짧은 창의 세기가 가장 큰 창의 -30dB를 처음 넘는 곳에서 조금 앞.</summary>
        static int Onset(float[] left, float[] right, int rate)
        {
            int window = Math.Max(1, (int)(rate * OnsetWindow));
            int count = left.Length / window;
            if (count == 0)
                return 0;
            var power = new double[count];
            double top = 0;
            for (int w = 0; w < count; w++)
            {
                double sum = 0;
                for (int i = w * window; i < (w + 1) * window; i++)
                {
                    sum += (double)left[i] * left[i];
                    if (right != null)
                        sum += (double)right[i] * right[i];
                }
                power[w] = sum;
                top = Math.Max(top, sum);
            }
            if (top <= 0)
                return 0;
            int first = 0;
            while (first < count && power[first] < top * OnsetPower)
                first++;
            return Math.Max(0, first * window - (int)(rate * OnsetLead));
        }

        static double MeanSquare(float[] x)
        {
            double sum = 0;
            foreach (float v in x)
                sum += (double)v * v;
            return sum / Math.Max(1, x.Length);
        }

        /// <summary>x의 [from, from+count)에 K 가중을 건 것을 새 배열로 돌려준다.</summary>
        static float[] KWeighted(float[] x, int from, int count, int rate)
        {
            var y = new float[count];
            Array.Copy(x, from, y, 0, count);
            // 높은 쪽 선반 (+4dB, 1.68kHz)
            {
                const double gainDb = 3.999843853973347, q = 0.7071752369554196, hz = 1681.974450955533;
                double k = Math.Tan(Math.PI * hz / rate), vh = Math.Pow(10, gainDb / 20), vb = Math.Pow(vh, 0.4996667741545416);
                double a0 = 1 + k / q + k * k;
                Biquad(y, (vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
            }
            // 낮은 쪽 깎기 (38Hz)
            {
                const double q = 0.5003270373238773, hz = 38.13547087602444;
                double k = Math.Tan(Math.PI * hz / rate);
                double a0 = 1 + k / q + k * k;
                Biquad(y, 1, -2, 1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
            }
            return y;
        }

        /// <summary>hz 아래를 깎는다 (2차 버터워스). 제자리에서 고친다.</summary>
        static void HighPass(float[] x, int rate, double hz)
        {
            double q = Math.Sqrt(0.5), k = Math.Tan(Math.PI * hz / rate);
            double a0 = 1 + k / q + k * k;
            Biquad(x, 1 / a0, -2 / a0, 1 / a0, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
        }

        /// <summary>2차 필터 하나를 제자리에서 건다.</summary>
        static void Biquad(float[] x, double b0, double b1, double b2, double a1, double a2)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double input = x[i];
                double output = b0 * input + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = input;
                y2 = y1;
                y1 = output;
                x[i] = (float)output;
            }
        }
    }
}

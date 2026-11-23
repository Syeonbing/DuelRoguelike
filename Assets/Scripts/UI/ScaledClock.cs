// 배속을 바꿀 수 있는 시계. 실제 시간이 흐른 만큼에 배속을 곱해 쌓는다 (배속을 바꾸면 그 순간부터 새 배속으로 흐른다).
// 듀얼 화면의 연출 시계(DuelScreen: 연출·멈추는 시간)와 듀얼 효과음(Sound)이 쓴다. 가속하면 둘이 같은 비율로 빨라진다.
using UnityEngine;

namespace DuelRoguelike.UI
{
    sealed class ScaledClock
    {
        double _now, _lastReal = -1;
        float _rate = 1f;

        /// <summary>배속 (1이면 실제 시간과 같이 흐른다).</summary>
        public float Rate
        {
            get => _rate;
            set
            {
                value = Mathf.Max(0.01f, value);
                if (value == _rate)
                    return;
                Sync(); // 지금까지는 옛 배속으로 흐른 것으로 친다
                _rate = value;
            }
        }

        /// <summary>이 시계의 지금 (초). 0에서 시작한다.</summary>
        public float Now
        {
            get
            {
                Sync();
                return (float)_now;
            }
        }

        void Sync()
        {
            double real = Time.realtimeSinceStartupAsDouble;
            if (_lastReal >= 0)
                _now += (real - _lastReal) * _rate;
            _lastReal = real;
        }
    }
}

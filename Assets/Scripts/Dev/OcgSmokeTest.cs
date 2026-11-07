// 개발용 확인 스크립트: 플레이 모드에서 ocgcore를 불러와 카드 없는 빈 듀얼을 만들고,
// 엔진 API 버전과 엔진이 보낸 메시지를 콘솔에 출력한 뒤 듀얼을 해제한다.
// 씬: Assets/Scenes/Dev/OcgSmokeTest.unity
using System;
using DuelRoguelike.Engine.Ocg;
using UnityEngine;

namespace DuelRoguelike.Dev
{
    public sealed class OcgSmokeTest : MonoBehaviour
    {
        [SerializeField, Tooltip("응답 없이 DuelProcess를 부를 최대 횟수")]
        int maxProcessCalls = 20;

        const string Tag = "[ocgcore]";

        void Start()
        {
            (int Major, int Minor) version;
            try
            {
                version = OcgDuel.GetVersion();
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"{Tag} ocgcore.dll을 찾지 못했습니다. tools/build-ocgcore.ps1로 빌드하세요. ({e.Message})");
                return;
            }
            Debug.Log($"{Tag} 엔진 API 버전 {version.Major}.{version.Minor}");

            var options = new OcgDuelOptions
            {
                Seed = new ulong[] { 1, 2, 3, 4 },
                // 임시 값: 마스터 룰에서 엑스트라 몬스터 존만 뺐다. 정식 룰 플래그는 최소 듀얼 단계에서 정한다.
                Flags = OcgDuelFlags.ModeMR5 & ~OcgDuelFlags.EMZone,
                CardSource = code =>
                {
                    Debug.LogWarning($"{Tag} 카드 데이터 요청: {code} (아직 카드 DB 없음)");
                    return null;
                },
                ScriptSource = name =>
                {
                    Debug.Log($"{Tag} 스크립트 요청: {name} (아직 스크립트 없음)");
                    return null;
                },
                Log = (type, text) =>
                {
                    if (type == OcgLogType.Error)
                        Debug.LogWarning($"{Tag} 엔진 로그({type}): {text}");
                    else
                        Debug.Log($"{Tag} 엔진 로그({type}): {text}");
                },
            };

            using (var duel = OcgDuel.Create(options))
            {
                Debug.Log($"{Tag} 빈 듀얼 생성 성공 (카드 0장, 플래그 0x{(ulong)options.Flags:X})");
                duel.Start();

                for (int call = 1; call <= maxProcessCalls; call++)
                {
                    var status = duel.Process();
                    foreach (var message in duel.ReadMessages())
                        Debug.Log($"{Tag} 처리 {call}회: 메시지 {message.Type} ({(int)message.Type}), 본문 {message.Body.Length}바이트");

                    if (status != OcgDuelStatus.Continue)
                    {
                        Debug.Log($"{Tag} 처리 {call}회: 상태 {status}. 여기서 멈춥니다.");
                        break;
                    }
                }
            }
            Debug.Log($"{Tag} 듀얼 해제 완료");
        }
    }
}

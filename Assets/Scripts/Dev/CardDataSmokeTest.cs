// 개발용 확인 스크립트: 플레이 모드에서 카드 DB(BabelCDB)와 카드 스크립트(CardScripts)를 엔진에 연결해
// 테스트 덱으로 듀얼을 시작하고, 메인 페이즈 1 입력 대기까지 진행한 결과를 콘솔에 출력한다.
// 씬: Assets/Scenes/Dev/CardDataSmokeTest.unity
using System;
using System.IO;
using UnityEngine;

namespace DuelRoguelike.Dev
{
    public sealed class CardDataSmokeTest : MonoBehaviour
    {
        [SerializeField, Tooltip("응답 없이 DuelProcess를 부를 최대 횟수")]
        int maxProcessCalls = 50;

        const string Tag = "[카드 데이터]";

        void Start()
        {
            // 에디터에서는 저장소의 External 폴더를 바로 읽는다. 실행 파일 빌드용 경로는 나중에 정한다.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cdbPath = Path.Combine(projectRoot, "External", "BabelCDB", "cards.cdb");
            string scriptsRoot = Path.Combine(projectRoot, "External", "CardScripts");

            var run = new CardDataSmokeRun(
                message => Debug.Log($"{Tag} {message}"),
                message => Debug.LogWarning($"{Tag} {message}"));
            try
            {
                if (run.Run(cdbPath, scriptsRoot, maxProcessCalls))
                    Debug.Log($"{Tag} 성공");
                else
                    Debug.LogError($"{Tag} 실패: 위 경고를 확인하세요.");
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"{Tag} 네이티브 DLL을 찾지 못했습니다. tools/build-ocgcore.ps1과 tools/fetch-sqlite.ps1을 실행하세요. ({e.Message})");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

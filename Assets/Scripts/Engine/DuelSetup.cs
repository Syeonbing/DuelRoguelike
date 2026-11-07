// 카드 DB와 카드 스크립트를 엔진에 연결해 듀얼을 만든다.
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.Engine
{
    public static class DuelSetup
    {
        /// <summary>
        /// options의 CardSource와 ScriptSource를 database와 scripts로 채워 듀얼을 만들고,
        /// 기본 스크립트(constant.lua, utility.lua)를 불러온다.
        /// options에 이미 넣은 CardSource, ScriptSource는 덮어쓴다.
        /// onScriptRequest: 엔진이 스크립트를 요청할 때마다 (이름, 파일을 찾았는지)를 받는다. 기록·진단용.
        /// </summary>
        public static OcgDuel CreateDuel(OcgDuelOptions options, CardDatabase database, CardScriptSource scripts,
            System.Action<string, bool> onScriptRequest = null)
        {
            options.CardSource = database.GetCardData;
            options.ScriptSource = name =>
            {
                byte[] source = scripts.Read(name);
                onScriptRequest?.Invoke(name, source != null);
                return source;
            };

            var duel = OcgDuel.Create(options);
            try
            {
                foreach (string name in CardScriptSource.StartupScripts)
                {
                    byte[] source = scripts.Read(name);
                    if (source == null)
                        throw new OcgException($"기본 스크립트가 없습니다: {name} ({scripts.Root})");
                    if (!duel.LoadScript(name, source))
                        throw new OcgException($"기본 스크립트를 불러오지 못했습니다: {name}");
                }
            }
            catch
            {
                duel.Dispose();
                throw;
            }
            return duel;
        }
    }
}

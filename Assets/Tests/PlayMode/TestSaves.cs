// 플레이 모드 시험이 쓰는 저장 파일. 게임이 새로 만드는 저장은 시작 덱을 고르기 전(빈 컬렉션)이라,
// 스타터 덱 4개를 가진 예전 새 게임으로 시작하는 시험(RunScreenPlayTest, GoldShopPlayTest, HardResetPlayTest)은
// 씬을 부르기 전에 이 저장을 시험 폴더에 써 둔다. 시작 덱 고르기는 StartDeckPlayTest가 빈 폴더에서 본다.
using System.IO;
using System.Text;
using DuelRoguelike.Run;

namespace DuelRoguelike.Tests
{
    static class TestSaves
    {
        /// <summary>스타터 덱 4개(컬렉션 200장, 레시피 4개)를 가진 저장을 directory/save.txt에 쓰고 그 내용을 돌려준다.</summary>
        public static string WriteStarterSave(string directory)
        {
            Directory.CreateDirectory(directory);
            string text = GameSave.NewGame().Write();
            File.WriteAllText(GameSaveFile.FilePath(directory), text, new UTF8Encoding(false));
            return text;
        }
    }
}

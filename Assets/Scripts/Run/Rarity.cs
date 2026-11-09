// 카드 희귀도(등급)와 카드별 등급 표.
// 등급은 패치로 자주 바뀔 수 있으므로 코드에 두지 않고 데이터 파일(StreamingAssets/Data/rarity.txt)에서 읽는다.
// 런 상태와 컬렉션은 카드 코드만 들고 있고, 등급은 보상을 뽑을 때마다 지금 표에서 찾는다.
// 그래서 표를 고치면 다음 보상부터 바로 반영되고, 이미 얻은 카드나 진행 중인 런을 고칠 필요가 없다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DuelRoguelike.Engine.Data;

namespace DuelRoguelike.Run
{
    /// <summary>희귀도 5등급, 낮은 쪽부터 (설계 문서 "카드풀과 시작 덱"). 값 순서가 보상 확률표의 열 순서다.</summary>
    public enum Rarity
    {
        N,
        R,
        SR,
        UR,
        FR,
    }

    /// <summary>카드 코드 → 등급. 표에 없는 카드는 등급이 없는 것으로 보고 보상에서 빠진다 (Check로 찾는다).</summary>
    public sealed class RarityTable
    {
        public const int RarityCount = 5;

        /// <summary>게임이 읽는 기본 파일 위치 (StreamingAssets 기준).</summary>
        public const string DefaultRelativePath = "Data/rarity.txt";

        readonly Dictionary<uint, Rarity> _rarities = new Dictionary<uint, Rarity>();

        public int Count => _rarities.Count;

        public IReadOnlyDictionary<uint, Rarity> Entries => _rarities;

        public bool TryGet(uint code, out Rarity rarity) => _rarities.TryGetValue(code, out rarity);

        /// <summary>등급을 정하거나 바꾼다 (시험·도구용).</summary>
        public void Set(uint code, Rarity rarity) => _rarities[code] = rarity;

        public static RarityTable Load(string path) => Parse(File.ReadAllText(path), Path.GetFileName(path));

        /// <summary>
        /// 한 줄에 카드 하나: "카드코드 등급". # 뒤는 주석이고 빈 줄은 건너뛴다. 예: <c>53129443 UR  # 블랙홀</c>
        /// 형식이 틀린 줄, 모르는 등급, 같은 카드가 두 번 나오면 FormatException (줄 번호 포함).
        /// </summary>
        public static RarityTable Parse(string text, string sourceName = "rarity.txt")
        {
            var table = new RarityTable();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf('#');
                if (comment >= 0)
                    line = line.Substring(0, comment);
                line = line.Trim();
                if (line.Length == 0)
                    continue;
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2
                    || !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint code)
                    || !TryParseRarity(parts[1], out var rarity))
                    throw new FormatException($"{sourceName} {i + 1}번째 줄: \"카드코드 등급(N, R, SR, UR, FR)\" 형식이 아닙니다: {lines[i].Trim()}");
                if (table._rarities.ContainsKey(code))
                    throw new FormatException($"{sourceName} {i + 1}번째 줄: 카드 {code}의 등급이 이미 앞에 있습니다.");
                table._rarities[code] = rarity;
            }
            return table;
        }

        /// <summary>N, R, SR, UR, FR (대소문자 무시). 숫자는 받지 않는다.</summary>
        public static bool TryParseRarity(string text, out Rarity rarity)
        {
            rarity = default;
            if (string.IsNullOrEmpty(text) || !char.IsLetter(text[0]))
                return false;
            return Enum.TryParse(text, true, out rarity) && Enum.IsDefined(typeof(Rarity), rarity);
        }

        /// <summary>덱에 든 카드를 등급별로 센다 (장수 포함). 등급이 없는 카드는 세지 않는다.</summary>
        public int[] CountByRarity(DeckRecipe deck)
        {
            var counts = new int[RarityCount];
            foreach (var (code, count) in deck.Entries)
                if (TryGet(code, out var rarity))
                    counts[(int)rarity] += count;
            return counts;
        }

        /// <summary>
        /// 표를 고친 뒤 확인할 것들. 문제가 없으면 빈 목록.
        /// - 상대 덱의 카드 중 등급이 없는 카드 (보상에서 빠진다)
        /// - SR이나 UR이 한 장도 없는 상대 덱 (설계: 모든 상대 덱에 SR·UR 1장 이상, 골드 대체는 N·R에서만)
        /// - 표에는 있지만 카드 DB에 없거나 일러스트만 다른 ID인 코드 (에라타 전 버전은 따로 등급을 매기므로 받는다)
        /// </summary>
        public List<string> Check(IEnumerable<DeckRecipe> opponentDecks, CardDatabase database)
        {
            var problems = new List<string>();
            foreach (var deck in opponentDecks)
            {
                foreach (var (code, _) in deck.Entries)
                    if (!TryGet(code, out _))
                        problems.Add($"{deck.Name}: {code} {database.GetName(code)}의 등급이 없음");
                int[] counts = CountByRarity(deck);
                foreach (var needed in new[] { Rarity.SR, Rarity.UR })
                    if (counts[(int)needed] == 0)
                        problems.Add($"{deck.Name}: {needed} 카드가 없음 (모든 상대 덱에 SR·UR이 1장 이상 있어야 함)");
            }
            foreach (uint code in _rarities.Keys.OrderBy(c => c))
            {
                if (!database.TryGet(code, out var record))
                    problems.Add($"{code}: 카드 DB에 없는 코드");
                else if (database.IsAlternativeArt(code))
                    problems.Add($"{code} {record.Name}: 일러스트만 다른 ID (기본 ID {record.Data.Alias}를 쓴다)");
            }
            return problems;
        }
    }
}

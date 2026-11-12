// 카드 견본 화면: 카드 틀을 종류별로 한 화면에 늘어놓는다 (작은 카드, 긴 이름, 다섯 자리 수치, 뒷면, 상세 카드).
// 게임 메뉴에는 없다. 화면 시험(Assets/Tests/PlayMode)이 카드 모양을 스크린샷으로 남길 때 부른다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        const string GalleryName = "CardGalleryUI";

        /// <summary>카드 견본 화면을 띄운다. 이미 떠 있으면 닫는다.</summary>
        public void ToggleCardGallery()
        {
            var old = transform.Find(GalleryName);
            if (old != null)
            {
                Destroy(old.gameObject);
                return;
            }
            if (_faces == null || _data == null)
                return;

            var root = UiStyle.CreateDocument(transform, GalleryName);
            root.style.backgroundColor = UiStyle.Background;
            UiStyle.Padding(root, 18);

            // 종류별 작은 카드 (패·컬렉션 크기 108×158)
            root.Add(GalleryTitle("작은 카드: 일반 · 효과 · 융합 · 의식 · 싱크로 · 엑시즈 · 마법(일반·속공·지속·장착·필드·의식) · 함정(일반·지속·카운터) · 뒷면"));
            var kinds = GalleryRow();
            foreach (string name in new[]
                     {
                         "블랙 매지션", "환상의 벽", "용기사 가이아", "카오스 솔저", "스타더스트 드래곤", "No.39 유토피아",
                         "블랙홀", "싸이크론", "빛의 봉인검", "어둠의 파신검", "어둠", "카오스의 의식",
                         "성벽", "드래곤족 봉인항아리", "매직 재머",
                     })
            {
                uint code = GalleryCode(name);
                if (code != 0)
                    kinds.Add(GalleryCell(_faces.Build(code, CardFaceSize.Medium)));
            }
            kinds.Add(GalleryCell(_faces.BuildBack(CardFaceSize.Medium)));
            root.Add(kinds);

            // 긴 이름, 다섯 자리 수치, 필드·덱 격자 크기
            root.Add(GalleryTitle("긴 카드명(폭 누르기 → 글자 줄이기 → 두 줄) · 다섯 자리 수치 · 장수와 표시 · 필드 크기(84×122) · 덱 격자 크기(82×120)"));
            var special = GalleryRow();
            foreach (string name in new[] { "커스 오브 드래곤", "드래곤족 봉인항아리", "궁극완전체 그레이트 모스", "유벨-다스 엑스트레머 트라우리히 드라헨" })
            {
                uint code = GalleryCode(name);
                if (code != 0)
                    special.Add(GalleryCell(_faces.Build(code, CardFaceSize.Medium)));
            }
            uint dragon = GalleryCode("푸른 눈의 백룡");
            if (dragon != 0)
            {
                special.Add(GalleryCell(_faces.Build(dragon, CardFaceSize.Medium,
                    stats: new CardStats { Attack = 13000, Defense = 12500, BaseAttack = 3000, BaseDefense = 2500 })));
                special.Add(GalleryCell(_faces.Build(dragon, CardFaceSize.Medium,
                    stats: new CardStats { Attack = 125000, Defense = 0, BaseAttack = 3000, BaseDefense = 2500 })));
                special.Add(GalleryCell(_faces.Build(dragon, CardFaceSize.Medium, count: 3, tag: "새 카드")));
            }
            foreach (string name in new[] { "블랙 매지션", "유벨-다스 엑스트레머 트라우리히 드라헨", "블랙홀" })
            {
                uint code = GalleryCode(name);
                if (code == 0)
                    continue;
                special.Add(GalleryCell(_faces.Build(code, CardFaceSize.Field)));
                special.Add(GalleryCell(_faces.Build(code, CardFaceSize.Small)));
            }
            special.Add(GalleryCell(_faces.BuildBack(CardFaceSize.Field)));
            // 동전(앞·뒤)과 주사위 여섯 면 (코인 토스·주사위 연출에 쓰는 그림)
            var front = new CoinView(96);
            special.Add(GalleryCell(front));
            var back = new CoinView(96);
            back.SetFace(false);
            special.Add(GalleryCell(back));
            for (int pips = 1; pips <= 6; pips++)
                special.Add(GalleryCell(new DieView(60) { Value = pips }));
            root.Add(special);

            // 상세 카드와 지금 화면의 "크게 보기" 크기(250×365)
            root.Add(GalleryTitle("상세 카드 (324×620): 효과문까지 넣은 세로로 긴 틀 · 오른쪽 끝은 지금 화면이 쓰는 크게 보기 크기 (250×365)"));
            var details = GalleryRow();
            foreach (string name in new[] { "블랙 매지션", "환상의 벽", "유벨-다스 엑스트레머 트라우리히 드라헨", "죽은 자의 소생", "매직 재머" })
            {
                uint code = GalleryCode(name);
                if (code != 0)
                    details.Add(GalleryCell(_faces.BuildDetail(code, 324, 620)));
            }
            uint magician = GalleryCode("블랙 매지션");
            if (magician != 0)
                details.Add(GalleryCell(_faces.Build(magician, CardFaceSize.Large)));
            root.Add(details);
        }

        static Label GalleryTitle(string text)
        {
            var label = UiStyle.Label(text, 17, UiStyle.Accent, bold: true);
            label.style.marginTop = 4;
            label.style.marginBottom = 6;
            return label;
        }

        static VisualElement GalleryRow()
        {
            var row = UiStyle.Box(null, FlexDirection.Row);
            row.style.flexShrink = 0;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 8;
            return row;
        }

        static VisualElement GalleryCell(VisualElement face)
        {
            face.style.marginRight = 10;
            return face;
        }

        /// <summary>한글 카드명으로 카드 코드를 찾는다. 없으면 0. 같은 이름이 여럿이면 코드가 가장 작은 것 (일러스트만 다른 카드를 피한다).</summary>
        uint GalleryCode(string name)
        {
            uint found = 0;
            foreach (var record in _data.Database.Records)
                if (record.Name == name && (found == 0 || record.Data.Code < found))
                    found = record.Data.Code;
            return found;
        }
    }
}

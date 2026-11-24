// 런 화면: 시작 덱 고르기. 새 저장(처음 켰을 때, 게임 데이터 초기화 뒤)은 시작 덱이 없어서 처음 화면 위에 이 창이 바로 뜬다.
// 공통 시작 덱 셋(StructureDecks.StartVersions) 중 하나를 고르면 그 덱의 카드 40장이 첫 컬렉션, 그 덱이 첫 레시피가 된다
// (GameSave.ChooseStartDeck, Run 어셈블리). 다른 두 덱의 카드는 탑의 상대(스트럭처 덱 원래 구성)에게 이겨 보상으로 모은다.
// - 고르기 창(start-deck): 탑 장면을 어둡게 덮고 세 덱의 돌판(start-deck-<덱 Id>)을 나란히 놓는다. 돌판마다 덱 이름, 많은 속성·종족,
//   대표 카드 3장, 한 줄 설명, 몬스터·마법·함정 수와 등급 수. 고른 덱은 금색 테로 떠오르고 "카드 40장"(start-deck-cards)과
//   "이 덱으로 시작"(start-deck-pick) 버튼이 생긴다. 처음에는 가운데 덱이 골라져 있다.
//   키: ←→ 고르기, E 카드 40장, Z 이 덱으로 시작, X 닫기(start-deck-close). 마우스: 돌판을 누르면 고르고, 고른 돌판을 한 번 더 누르면 시작.
// - 카드 40장 창(start-deck-list): 그 덱의 카드 전부(같은 카드는 ×n)와 오른쪽 상세 카드. Q·E로 다른 덱. X·"뒤로"(start-deck-back)로 고르기 창.
// - 확인 창(start-deck-confirm): 바꿀 수 없는 선택이라 한 번 묻는다. "이 덱으로 한다"(start-deck-yes)가 금색이고 처음에 골라져 있다.
//   "다시 고른다"(start-deck-no)·취소 키는 확인 창을 연 창으로 돌아간다.
// - 고르면 저장하고 처음 화면(새 런이 맨 위)으로 돌아와 띠 알림(start-deck-banner) "「…」 카드 40장을 받았다."
// - 고르기 전에 닫으면 처음 화면의 메뉴는 "시작 덱 고르기"(title-start-deck), 설정, 끝내기뿐이다 (RunScreen.Start.cs).
// 덱마다의 대표 카드와 한 줄 설명은 화면에 보이는 글이라 여기 둔다 (StartDeckNotes).
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Data;
using DuelRoguelike.Engine.Ocg;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        enum StartDeckView
        {
            Closed,
            /// <summary>세 덱을 나란히 놓은 고르기 창</summary>
            Choose,
            /// <summary>고른 덱의 카드 40장 창</summary>
            Cards,
        }

        const float StartDeckSlabWidth = 580, StartDeckSlabHeight = 660, StartDeckSlabGap = 30;
        const float StartDeckShowcaseWidth = CardFaces.BaseWidth * 1.5f, StartDeckConfirmCardWidth = CardFaces.BaseWidth * 1.1f;
        /// <summary>카드 40장 창의 격자: 한 줄에 놓는 카드 수 (위아래 키는 이만큼 건너뛴다).</summary>
        const int StartDeckPerRow = 11;
        /// <summary>격자 카드 하나가 차지하는 폭: 카드 108 + 안쪽 여백 2×2 + 테두리 2×2 + 오른쪽 틈 6.</summary>
        const float StartDeckCellWidth = CardFaces.BaseWidth + 14;
        const float StartDeckDetailHeight = 560;

        /// <summary>덱마다 화면에 보여 줄 한 줄 설명과 대표 카드 3장 (덱 Id → 설명, 카드 코드).</summary>
        static readonly Dictionary<string, (string Line, uint[] Showcase)> StartDeckNotes = new Dictionary<string, (string, uint[])>
        {
            {
                "LordOfTheStormStart", ("해피 레이디와 바람의 새 몬스터. 마법·함정 파괴로 상대의 뒷줄을 걷어 낸다.",
                    new uint[] { 14989021, 12206212, 75782277 }) // 신조 시무르그, 해피 레이디 세자매, 해피의 사냥터
            },
            {
                "MachineReVoltStart", ("앤틱 기어를 릴리스 소환해 공격력 3000으로 밀어붙인다. 가제트가 손패를 채운다.",
                    new uint[] { 50933533, 83104731, 23171610 }) // 앤틱 기어 가젤 드래곤, 앤틱 기어 골렘, 리미터 해제
            },
            {
                "CurseOfDarknessStart", ("어둠 몬스터를 바쳐 덱 파괴 바이러스로 상대의 손패와 덱을 무너뜨린다.",
                    new uint[] { 29424328, 97342942, 57728570 }) // 마왕 디아블로스, 엑토플라즈머, 죽음의 덱 파괴 바이러스
            },
        };

        StartDeckView _startDeckView;
        /// <summary>확인 창이 떠 있는지 (고르기 창이나 카드 40장 창 위에).</summary>
        bool _startDeckConfirm;
        /// <summary>고른 덱: StructureDecks.StartVersions의 순번. 처음에는 가운데 덱.</summary>
        int _startDeckIndex = 1;
        /// <summary>ShowStart가 시작 덱 고르기 창을 바로 띄울지 (저장을 읽었을 때와 초기화한 뒤에 한 번).</summary>
        bool _offerStartDeck;
        VisualElement _startDeckLayer;
        ButtonRow _startDeckRow;

        bool StartDeckOpen => _startDeckView != StartDeckView.Closed && _startDeckLayer != null && _startDeckLayer.panel != null;

        DeckRecipe StartDeck => StructureDecks.StartVersions[Mathf.Clamp(_startDeckIndex, 0, StructureDecks.StartVersions.Count - 1)];

        /// <summary>"시작 덱 —열풍의 패왕—" → "열풍의 패왕".</summary>
        static string StartDeckShortName(DeckRecipe deck)
        {
            int from = deck.Name.IndexOf('—'), to = deck.Name.LastIndexOf('—');
            return from >= 0 && to > from ? deck.Name.Substring(from + 1, to - from - 1) : deck.Name;
        }

        static string StartDeckLine(DeckRecipe deck) => StartDeckNotes.TryGetValue(deck.Id, out var note) ? note.Line : "";

        static IEnumerable<uint> StartDeckShowcase(DeckRecipe deck) =>
            StartDeckNotes.TryGetValue(deck.Id, out var note) ? note.Showcase : deck.Entries.Select(e => e.Code).Take(3);

        /// <summary>처음 화면의 "시작 덱 고르기"와 처음 켰을 때: 고르기 창을 연다.</summary>
        void OpenStartDeck()
        {
            if (!_save.NeedsStartDeck)
                return;
            if (_titleMenu != null && _titleMenu.Root.panel != null)
                _titleMenuIndex = _titleMenu.Index;
            _startDeckView = StartDeckView.Choose;
            _startDeckConfirm = false;
            ShowStart(); // 처음 화면의 메뉴·판을 걷고 탑 장면 위에 창을 띄운다
        }

        /// <summary>고르기 창을 닫는다 (고르지 않고): 처음 화면의 메뉴가 다시 보인다.</summary>
        void CloseStartDeck()
        {
            _startDeckView = StartDeckView.Closed;
            _startDeckConfirm = false;
            CloseModal();
            ShowStart();
        }

        /// <summary>지금 상태(_startDeckView, _startDeckConfirm)대로 창을 다시 그린다.</summary>
        void DrawStartDeck()
        {
            _cardViews.Clear();
            _detail = null;
            _renderDetail = null;
            _startDeckRow = null;
            _startDeckLayer = OpenStageLayer("start-deck-layer", 0.72f);
            if (_startDeckView == StartDeckView.Cards)
                _startDeckLayer.Add(StartDeckList());
            else
                StartDeckChooser(_startDeckLayer);
            if (_startDeckConfirm)
                _startDeckLayer.Add(StartDeckConfirmOverlay());
            ApplyKeyCaps();
            if (_startDeckView == StartDeckView.Cards)
                ShowDetail(Pile.StartDeck, _selectedCode, pinned: true);
        }

        // ================= 고르기 창: 세 덱 나란히 =================

        void StartDeckChooser(VisualElement layer)
        {
            var window = new VisualElement { name = "start-deck", pickingMode = PickingMode.Ignore };
            UiStyle.Place(window, 0, 0, StageWidth, StageHeight);
            layer.Add(window);

            var head = UiStyle.Box("start-deck-head");
            head.pickingMode = PickingMode.Ignore;
            UiStyle.Place(head, 60, 60);
            head.Add(WindowTitle("시작 덱", 48));
            var sub = Text("탑을 오를 첫 덱을 하나 고른다. 고른 덱의 카드 40장이 첫 컬렉션이 된다.", 22, UiStyle.TextDim, top: 6);
            sub.style.whiteSpace = WhiteSpace.NoWrap;
            head.Add(sub);
            // 처음 화면의 왼쪽 아래 판은 이 창 밑에 그리지 않으므로, 그 판의 알림(깨진 저장 파일, 초기화)을 여기 쓴다
            string notice = _saveNotice ?? _resetNotice;
            if (notice != null)
            {
                var note = Text(notice, 19, _saveNotice != null ? UiStyle.Opponent : UiStyle.Good, top: 8);
                note.name = "start-deck-notice";
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.maxWidth = StageWidth - 120;
                head.Add(note);
            }
            window.Add(head);

            var decks = StructureDecks.StartVersions;
            for (int i = 0; i < decks.Count; i++)
                window.Add(StartDeckSlab(decks[i], i));

            var foot = Row();
            foot.pickingMode = PickingMode.Ignore;
            foot.style.position = Position.Absolute;
            foot.style.left = 60;
            foot.style.right = 60;
            foot.style.bottom = 40;
            foot.style.alignItems = Align.Center;
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("E", "E", "카드 40장 보기"), ("Z", "J", "이 덱으로 시작"), ("X", "K", "닫기"));
            hints.name = "modal-hints";
            foot.Add(hints);
            var close = KeyButton("start-deck-close", "닫기", CloseStartDeck, "X", "K", 112);
            close.style.marginLeft = StyleKeyword.Auto;
            foot.Add(close);
            window.Add(foot);
        }

        VisualElement StartDeckSlab(DeckRecipe deck, int index)
        {
            bool chosen = index == _startDeckIndex;
            var summary = SummarizeStartDeck(deck);
            var slab = new VisualElement { name = "start-deck-" + deck.Id };
            UiStyle.Slab(slab);
            UiStyle.Place(slab, 60 + index * (StartDeckSlabWidth + StartDeckSlabGap), 236, StartDeckSlabWidth, StartDeckSlabHeight);
            slab.style.paddingLeft = slab.style.paddingRight = 28;
            slab.style.paddingTop = slab.style.paddingBottom = 22;
            if (chosen)
            {
                UiStyle.BorderColor(slab, UiStyle.Gold, 4);
                slab.style.translate = new Translate(0, -14);
                slab.AddToClassList("selected");
            }
            slab.RegisterCallback<ClickEvent>(e =>
            {
                // 돌판 안의 버튼은 제 일을 한다
                if (e.target is VisualElement target && (target is Button || target.GetFirstAncestorOfType<Button>() != null))
                    return;
                if (index == _startDeckIndex)
                    OpenStartDeckConfirm();
                else
                    ChooseStartDeckSlab(index);
            });

            slab.Add(Text(deck.Name, 19, UiStyle.TextDim));
            var big = UiStyle.Label(StartDeckShortName(deck), 52);
            UiStyle.Black(big);
            big.name = "start-deck-name";
            big.style.whiteSpace = WhiteSpace.NoWrap;
            slab.Add(big);

            var type = Row();
            type.style.alignItems = Align.Center;
            type.style.marginTop = 8;
            if (summary.Attribute != null)
            {
                var emblem = AttributeEmblem(summary.Attribute, 28);
                emblem.style.marginRight = 8;
                type.Add(emblem);
            }
            var typeText = Text(summary.Attribute == null ? "" : $"{summary.Attribute} {summary.AttributeCount}장 · {summary.Race} {summary.RaceCount}장", 19, UiStyle.TextDim);
            typeText.name = "start-deck-type";
            type.Add(typeText);
            slab.Add(type);

            var show = Row();
            show.pickingMode = PickingMode.Ignore;
            show.style.justifyContent = Justify.Center;
            show.style.marginTop = 18;
            int n = 0;
            foreach (uint code in StartDeckShowcase(deck))
            {
                var face = _faces.Build(code, StartDeckShowcaseWidth);
                face.pickingMode = PickingMode.Ignore;
                if (n++ > 0)
                    face.style.marginLeft = 14;
                show.Add(face);
            }
            slab.Add(show);

            var line = Text(StartDeckLine(deck), 21, UiStyle.Text, top: 16);
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.minHeight = 61;
            slab.Add(line);

            slab.Add(StartDeckStatRow(summary, top: 0, divider: true));

            var buttons = Row();
            buttons.style.marginTop = 14;
            buttons.style.flexShrink = 0;
            if (!chosen)
                buttons.style.visibility = Visibility.Hidden; // 자리는 남겨 둔다 (돌판의 높이가 같게)
            var cards = KeyButton("start-deck-cards", $"카드 {deck.CardCount}장", OpenStartDeckList, "E", "E", 190);
            cards.style.height = 64; // 옆의 금색 버튼과 같은 높이
            var cardsKey = cards.Q<Label>(); // KeyButton이 단 키 표시
            if (cardsKey != null)
                cardsKey.style.top = 17;
            buttons.Add(cards);
            var pick = UiStyle.StoneButton("이 덱으로 시작", OpenStartDeckConfirm, gold: true);
            pick.name = "start-deck-pick";
            pick.style.flexGrow = 1;
            pick.style.marginLeft = 10;
            buttons.Add(pick);
            slab.Add(buttons);
            if (!chosen)
            {
                // 고르지 않은 돌판의 버튼은 보이지 않는다: 누를 수도 없게 끈다
                cards.SetEnabled(false);
                pick.SetEnabled(false);
            }
            return slab;
        }

        /// <summary>"몬스터 n · 마법 n · 함정 n"과 등급 수 (FR·UR·SR·R·N 중 있는 것).</summary>
        VisualElement StartDeckStatRow((int[] Kinds, int[] Rarities, string Attribute, int AttributeCount, string Race, int RaceCount) summary,
            float top, bool divider)
        {
            var row = Row();
            row.name = "start-deck-stat";
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            if (divider)
            {
                row.style.marginTop = StyleKeyword.Auto; // 돌판의 아래쪽에 붙는다
                row.style.paddingTop = 14;
                row.style.borderTopWidth = 2;
                row.style.borderTopColor = UiStyle.SlabTop;
            }
            else
                row.style.marginTop = top;
            var kinds = Text($"몬스터 {summary.Kinds[0]} · 마법 {summary.Kinds[1]} · 함정 {summary.Kinds[2]}", 20);
            kinds.name = "start-deck-kinds";
            kinds.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(kinds);
            var pills = Row();
            pills.style.marginLeft = StyleKeyword.Auto;
            pills.style.paddingLeft = 10;
            for (int i = summary.Rarities.Length - 1; i >= 0; i--)
            {
                if (summary.Rarities[i] == 0)
                    continue;
                var rarity = (Rarity)i;
                var pill = UiStyle.Label($"{rarity} {summary.Rarities[i]}", 18, CardFaces.RarityColor(rarity));
                UiStyle.Black(pill);
                pill.name = "start-deck-rarity-" + rarity;
                pill.style.whiteSpace = WhiteSpace.NoWrap;
                pill.style.paddingLeft = pill.style.paddingRight = 7;
                pill.style.paddingTop = pill.style.paddingBottom = 1;
                pill.style.marginLeft = 5;
                pill.style.backgroundColor = UiStyle.Stone9;
                UiStyle.Radius(pill, 4);
                UiStyle.BorderColor(pill, UiStyle.SlabTop, 2);
                pills.Add(pill);
            }
            row.Add(pills);
            return row;
        }

        /// <summary>
        /// 덱 요약: 종류별 장수(몬스터·마법·함정), 등급별 장수(Rarity 순번), 몬스터 중 가장 많은 속성과 종족 (같은 카드도 장수대로 센다).
        /// </summary>
        (int[] Kinds, int[] Rarities, string Attribute, int AttributeCount, string Race, int RaceCount) SummarizeStartDeck(DeckRecipe deck)
        {
            var kinds = new int[3];
            var rarities = new int[RarityTable.RarityCount];
            var attributes = new Dictionary<string, int>();
            var races = new Dictionary<string, int>();
            foreach (var (code, count) in deck.Entries)
            {
                int kind = KindOrder(code);
                kinds[kind] += count;
                if (_rarities.TryGet(code, out var rarity))
                    rarities[(int)rarity] += count;
                var record = _text.Record(code);
                if (kind != 0 || record == null)
                    continue;
                string attribute = DuelText.Attribute((OcgAttribute)record.Data.Attribute), race = DuelText.Race(record.Data.Race);
                attributes[attribute] = (attributes.TryGetValue(attribute, out int a) ? a : 0) + count;
                races[race] = (races.TryGetValue(race, out int r) ? r : 0) + count;
            }
            var topAttribute = attributes.OrderByDescending(p => p.Value).FirstOrDefault();
            var topRace = races.OrderByDescending(p => p.Value).FirstOrDefault();
            return (kinds, rarities, topAttribute.Key, topAttribute.Value, topRace.Key, topRace.Value);
        }

        /// <summary>속성 문장: 속성 색 원 위에 어두운 픽토그램 (카드의 속성 문장과 같다).</summary>
        static VisualElement AttributeEmblem(string attribute, float size)
        {
            var emblem = new VisualElement();
            emblem.pickingMode = PickingMode.Ignore;
            emblem.style.width = emblem.style.height = size;
            emblem.style.flexShrink = 0;
            emblem.style.backgroundColor = CardFaces.AttributeColor(attribute);
            UiStyle.Radius(emblem, size / 2);
            UiStyle.Padding(emblem, Mathf.Round(size * 0.17f));
            var data = Pictograms.Get("속성:" + attribute);
            if (data != null)
            {
                var view = new PictogramView(data, UiStyle.Hex(0x1b140b));
                view.style.flexGrow = 1;
                emblem.Add(view);
            }
            return emblem;
        }

        void ChooseStartDeckSlab(int index)
        {
            index = Mathf.Clamp(index, 0, StructureDecks.StartVersions.Count - 1);
            if (index == _startDeckIndex)
                return;
            _startDeckIndex = index;
            _selectedCode = 0;
            DrawStartDeck();
        }

        // ================= 카드 40장 창 =================

        void OpenStartDeckList()
        {
            _startDeckView = StartDeckView.Cards;
            _startDeckConfirm = false;
            _selectedPile = Pile.StartDeck;
            _selectedCode = StartDeckCards(StartDeck).FirstOrDefault();
            DrawStartDeck();
        }

        /// <summary>덱의 카드 (같은 카드는 한 번): 몬스터(레벨 높은 순, 공격력 높은 순) → 마법 → 함정.</summary>
        List<uint> StartDeckCards(DeckRecipe deck) =>
            deck.Entries.Select(e => e.Code).Distinct()
                .OrderBy(c => KindOrder(c))
                .ThenByDescending(c => _text.Record(c)?.Data.Level ?? 0)
                .ThenByDescending(c => _text.Record(c)?.Data.Attack ?? 0)
                .ThenBy(c => _text.CardName(c), StringComparer.Ordinal)
                .ToList();

        static int StartDeckCount(DeckRecipe deck, uint code) => deck.Entries.Where(e => e.Code == code).Sum(e => e.Count);

        VisualElement StartDeckList()
        {
            var deck = StartDeck;
            var cards = StartDeckCards(deck);
            if (!cards.Contains(_selectedCode))
                _selectedCode = cards.FirstOrDefault();
            _selectedPile = Pile.StartDeck;

            var panel = new VisualElement { name = "start-deck-list" };
            UiStyle.Slab(panel);
            UiStyle.Place(panel, 60, 50, 1800, 980);
            panel.style.paddingLeft = panel.style.paddingRight = 26;
            panel.style.paddingTop = 22;
            panel.style.paddingBottom = 18;

            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.flexShrink = 0;
            head.Add(WindowTitle(deck.Name));
            var line = Text(StartDeckLine(deck), 19, UiStyle.TextDim);
            line.style.marginLeft = 18;
            line.style.marginBottom = 7;
            line.style.flexShrink = 1;
            line.style.whiteSpace = WhiteSpace.Normal;
            head.Add(line);
            panel.Add(head);

            var tabs = Row();
            tabs.style.alignItems = Align.Center;
            tabs.style.marginTop = 14;
            tabs.style.flexShrink = 0;
            var decks = StructureDecks.StartVersions;
            for (int i = 0; i < decks.Count; i++)
            {
                int at = i;
                var tab = UiStyle.StoneButton(StartDeckShortName(decks[i]), () =>
                {
                    _startDeckIndex = at;
                    _selectedCode = 0;
                    DrawStartDeck();
                }, gold: i == _startDeckIndex, small: true);
                tab.name = "start-deck-tab-" + decks[i].Id;
                tab.style.width = 230;
                tab.style.marginRight = 6;
                tabs.Add(tab);
            }
            var stat = StartDeckStatRow(SummarizeStartDeck(deck), 0, divider: false);
            stat.style.marginLeft = StyleKeyword.Auto;
            tabs.Add(stat);
            panel.Add(tabs);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.marginTop = 16;
            var grid = Grid();
            grid.name = "start-deck-grid";
            grid.style.width = StartDeckPerRow * StartDeckCellWidth;
            grid.style.flexShrink = 0;
            foreach (uint code in cards)
            {
                int count = StartDeckCount(deck, code);
                var slot = CardSlot(Pile.StartDeck, code, CardFaceSize.Medium, count: count > 1 ? count : 0, draggable: false);
                slot.focusable = false;
                UiStyle.Margin(slot, 0);
                slot.style.marginRight = 6;
                slot.style.marginBottom = 8;
                grid.Add(slot);
            }
            body.Add(grid);

            var side = UiStyle.Box();
            side.style.width = 324;
            side.style.marginLeft = StyleKeyword.Auto;
            side.style.flexShrink = 0;
            var detail = UiStyle.Box("start-deck-detail");
            detail.style.height = StartDeckDetailHeight + 34;
            detail.style.flexShrink = 0;
            side.Add(detail);
            _detail = detail;
            _renderDetail = RenderStartDeckDetail;
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            side.Add(gap);
            var pick = UiStyle.StoneButton("이 덱으로 시작", OpenStartDeckConfirm, gold: true);
            pick.name = "start-deck-list-pick";
            side.Add(pick);
            side.Add(RecipeBackButton("start-deck-back", "뒤로", BackToStartDeckChooser));
            body.Add(side);
            panel.Add(body);

            var hints = Hints(("←", "A", null), ("↑", "W", null), ("↓", "S", null), ("→", "D", "카드"), ("Q", "Q", null), ("E", "E", "덱 바꾸기"),
                ("Z", "J", "이 덱으로 시작"), ("X", "K", "뒤로"));
            hints.name = "modal-hints";
            hints.style.marginTop = 12;
            hints.style.paddingLeft = 10;
            hints.style.flexShrink = 0;
            panel.Add(hints);
            return panel;
        }

        void RenderStartDeckDetail(VisualElement panel, Pile pile, uint code, bool pinned)
        {
            if (code == 0)
                return;
            panel.Add(_faces.BuildDetail(code, 324, StartDeckDetailHeight));
            string rarity = _rarities.TryGet(code, out var r) ? r.ToString() : "없음";
            var info = Text($"이 덱에 {StartDeckCount(StartDeck, code)}장 · 등급 {rarity}", 17, UiStyle.TextDim, top: 8);
            info.name = "start-deck-card-info";
            panel.Add(info);
        }

        void BackToStartDeckChooser()
        {
            _startDeckView = StartDeckView.Choose;
            _startDeckConfirm = false;
            DrawStartDeck();
        }

        /// <summary>카드 40장 창의 이동 키: 좌우 1장, 위아래 한 줄(11장). 끝에서 멈춘다.</summary>
        void MoveStartDeckCard(int delta)
        {
            var cards = StartDeckCards(StartDeck);
            if (cards.Count == 0)
                return;
            int index = Mathf.Max(0, cards.IndexOf(_selectedCode));
            int next = Mathf.Clamp(index + delta, 0, cards.Count - 1);
            if (next != index)
                Select(Pile.StartDeck, cards[next]);
        }

        // ================= 확인 창 =================

        void OpenStartDeckConfirm()
        {
            if (!_save.NeedsStartDeck)
                return;
            _startDeckConfirm = true;
            DrawStartDeck();
        }

        void CloseStartDeckConfirm()
        {
            _startDeckConfirm = false;
            DrawStartDeck();
        }

        VisualElement StartDeckConfirmOverlay()
        {
            var deck = StartDeck;
            var overlay = new VisualElement { name = "start-deck-confirm-layer" };
            UiStyle.Place(overlay, 0, 0, StageWidth, StageHeight);
            overlay.style.backgroundColor = UiStyle.Alpha(UiStyle.Stone9, 0.8f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            var panel = new VisualElement { name = "start-deck-confirm" };
            UiStyle.Slab(panel);
            panel.style.width = 920;
            panel.style.paddingLeft = panel.style.paddingRight = 40;
            panel.style.paddingTop = 30;
            panel.style.paddingBottom = 22;
            panel.style.alignItems = Align.Center;
            panel.Add(WindowTitle("시작 덱"));
            var big = UiStyle.Label(StartDeckShortName(deck), 52);
            UiStyle.Black(big);
            big.name = "start-deck-confirm-name";
            big.style.whiteSpace = WhiteSpace.NoWrap;
            big.style.marginTop = 10;
            panel.Add(big);
            var show = Row();
            show.style.marginTop = 14;
            int n = 0;
            foreach (uint code in StartDeckShowcase(deck))
            {
                var face = _faces.Build(code, StartDeckConfirmCardWidth);
                face.pickingMode = PickingMode.Ignore;
                if (n++ > 0)
                    face.style.marginLeft = 12;
                show.Add(face);
            }
            panel.Add(show);
            panel.Add(Text($"이 덱의 카드 {deck.CardCount}장이 첫 컬렉션이 된다.", 25, bold: true, top: 18));
            panel.Add(Text("다른 두 덱의 카드는 탑의 상대에게 이겨 보상으로 모은다.", 19, UiStyle.TextDim, top: 6));
            panel.Add(Text("한 번 고르면 바꿀 수 없다. 설정의 게임 데이터 초기화로만 다시 고른다.", 19, UiStyle.TextDim, top: 2));

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 22;
            buttons.Add("start-deck-yes", "이 덱으로 한다", ConfirmStartDeck, gold: true, width: 320);
            buttons.Add("start-deck-no", "다시 고른다", CloseStartDeckConfirm, width: 320);
            buttons.Select(0);
            _startDeckRow = buttons;
            panel.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "다시 고른다"));
            hints.style.marginTop = 16;
            panel.Add(hints);
            overlay.Add(panel);
            return overlay;
        }

        /// <summary>"이 덱으로 한다": 그 덱의 카드와 레시피를 받고 저장한 뒤 처음 화면으로 (새 런이 맨 위).</summary>
        void ConfirmStartDeck()
        {
            var deck = StartDeck;
            try
            {
                _save.ChooseStartDeck(deck);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogWarning("[런] 시작 덱을 받지 못함: " + e.Message);
                CloseStartDeck();
                return;
            }
            Persist();
            Sound.Play(Sfx.Shuffle);
            Debug.Log($"[런] 시작 덱: {deck.Name} ({deck.Id}), 컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장, 레시피 {_save.Recipes.Count}개");
            _startRecipeId = deck.Id;
            _titleMenuIndex = 0;
            _startDeckView = StartDeckView.Closed;
            _startDeckConfirm = false;
            CloseModal();
            ShowStart();
            ShowStageBanner("start-deck-banner", $"「{deck.Name}」 카드 {_collection.TotalCount}장을 받았다.");
        }

        // ================= 키보드 =================

        void StartDeckKey(Nav nav)
        {
            if (_startDeckConfirm)
            {
                switch (nav)
                {
                    case Nav.Left:
                        _startDeckRow?.Move(-1);
                        break;
                    case Nav.Right:
                        _startDeckRow?.Move(1);
                        break;
                    case Nav.Confirm:
                        _startDeckRow?.Activate();
                        break;
                    case Nav.Cancel:
                        CloseStartDeckConfirm();
                        break;
                }
                return;
            }
            if (_startDeckView == StartDeckView.Cards)
            {
                switch (nav)
                {
                    case Nav.Left:
                        MoveStartDeckCard(-1);
                        break;
                    case Nav.Right:
                        MoveStartDeckCard(1);
                        break;
                    case Nav.Up:
                        MoveStartDeckCard(-StartDeckPerRow);
                        break;
                    case Nav.Down:
                        MoveStartDeckCard(StartDeckPerRow);
                        break;
                    case Nav.PrevTab:
                    case Nav.NextTab:
                    {
                        int count = StructureDecks.StartVersions.Count;
                        _startDeckIndex = (_startDeckIndex + (nav == Nav.NextTab ? 1 : count - 1)) % count;
                        _selectedCode = 0;
                        DrawStartDeck();
                        break;
                    }
                    case Nav.Confirm:
                        OpenStartDeckConfirm();
                        break;
                    case Nav.Cancel:
                        BackToStartDeckChooser();
                        break;
                }
                return;
            }
            switch (nav)
            {
                case Nav.Left:
                    ChooseStartDeckSlab(_startDeckIndex - 1);
                    break;
                case Nav.Right:
                    ChooseStartDeckSlab(_startDeckIndex + 1);
                    break;
                case Nav.NextTab:
                    OpenStartDeckList();
                    break;
                case Nav.Confirm:
                    OpenStartDeckConfirm();
                    break;
                case Nav.Cancel:
                    CloseStartDeck();
                    break;
            }
        }

        // ================= 띠 알림 (처음 화면 위) =================

        /// <summary>처음 화면의 무대 위에 띠 알림을 띄운다 (골드 상점의 띠 알림과 같은 모양·시간). 누를 필요가 없다.</summary>
        void ShowStageBanner(string name, string text)
        {
            if (_stage == null)
                return;
            var banner = BannerStrip(name, text);
            _stage.Add(banner);
            StartCoroutine(GoldBannerRoutine(banner, banner));
        }
    }
}

// 이벤트 도감 창 (처음 화면에서 연다). 규칙 결정 문서 "이벤트 도감", docs/UI.md "나머지 창 > 런 기록과 이벤트 도감".
// - 왼쪽에 이벤트 칸 격자(한 줄 4칸), 오른쪽에 고른 이벤트의 글과 선택지, 본 결과.
// - 만나지 않은 이벤트는 "???"로만 보인다.
// - 만난 이벤트는 상황 글과 선택지가 보이고, 골라 본 선택지만 결과가 보인다.
// - 도감에 쓰는 선택지("지나간다"·강제 선택지 제외)를 모두 골라 본 이벤트는 등장 조건과 성향(+기대값)이 보인다.
// - 키보드: 이동 키로 이벤트를 고르고, 취소 키로 닫는다.
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        const int CodexColumns = 4;

        readonly List<Button> _codexTiles = new List<Button>();
        VisualElement _codexDetail;
        int _codexIndex;

        bool CodexOpen => _codexDetail != null && _codexDetail.panel != null && _modalLayer.style.display != DisplayStyle.None;

        void OpenCodex()
        {
            var codex = _save.Codex;
            codex.Absorb(_save.Run); // 진행 중인 런에서 만난 것도 (저장할 때 어차피 더해진다)
            var all = TowerEventList.All;
            var panel = OpenSlab("event-codex", 1720);
            panel.style.height = 960;
            panel.style.paddingLeft = panel.style.paddingRight = 24;
            panel.style.paddingTop = 20;
            panel.style.paddingBottom = 18;

            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.flexShrink = 0;
            head.Add(WindowTitle("이벤트 도감"));
            var sub = Text($"만난 이벤트 {all.Count(e => codex.Seen(e.Id))} / {all.Count} · 선택지를 모두 골라 본 이벤트 {all.Count(codex.IsComplete)}", 19, UiStyle.TextDim);
            sub.style.marginLeft = 18;
            sub.style.marginBottom = 7;
            head.Add(sub);
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            head.Add(gap);
            head.Add(KeyButton("event-codex-close", "닫기", CloseModal, "X", "K", 112));
            panel.Add(head);

            var body = Row();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.marginTop = 18;
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "event-codex-list" };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.width = CodexColumns * 270 + 8;
            scroll.style.flexShrink = 0;
            var grid = Grid(scroll.contentContainer);
            _codexTiles.Clear();
            for (int i = 0; i < all.Count; i++)
                grid.Add(CodexTile(all[i], codex, i));
            body.Add(scroll);
            _codexDetail = UiStyle.Box("event-codex-detail");
            _codexDetail.style.width = 520;
            _codexDetail.style.marginLeft = StyleKeyword.Auto;
            _codexDetail.style.flexShrink = 0;
            body.Add(_codexDetail);
            panel.Add(body);
            panel.Add(WindowFooter(Hints(("← ↑ ↓ →", "W A S D", "이벤트 고르기"), ("X", "K", "닫기"))));
            ApplyKeyCaps();

            // 처음에는 만난 첫 이벤트를 가리킨다
            int first = all.ToList().FindIndex(e => codex.Seen(e.Id));
            if (_codexIndex >= all.Count || !codex.Seen(all[_codexIndex].Id))
                _codexIndex = Mathf.Max(0, first);
            SelectCodex(_codexIndex);
        }

        /// <summary>도감의 칸 하나: 아이콘, 이름, 골라 본 선택지 수. 만나지 않은 이벤트는 "???".</summary>
        Button CodexTile(TowerEvent ev, EventCodex codex, int index)
        {
            var tile = new Button(UiStyle.Clicked(() => SelectCodex(index))) { name = $"codex-{ev.Id}", text = "", focusable = false };
            tile.style.width = 256;
            tile.style.height = 150;
            UiStyle.Margin(tile, 7);
            UiStyle.Padding(tile, 0);
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            tile.style.backgroundColor = UiStyle.Stone7;
            UiStyle.Radius(tile, 0);
            UiStyle.BorderColor(tile, UiStyle.Stone9, 3);
            tile.style.borderTopColor = UiStyle.SlabTop;
            if (!codex.Seen(ev.Id))
            {
                tile.style.opacity = 0.5f;
                tile.Add(RowLabel("???", 22, UiStyle.Text, bold: true));
                tile.Add(RowLabel("아직 만나지 못했다", 16, UiStyle.TextDim));
            }
            else
            {
                tile.Add(IconTile(EventPictogram(ev.Id), 56, EventFloorColor));
                var name = RowLabel(ev.Title, 22, UiStyle.Text, bold: true);
                name.style.marginTop = 6;
                tile.Add(name);
                int total = ev.Choices.Count, known = Enumerable.Range(0, total).Count(i => codex.Chosen(ev.Id, i));
                string count = total == 0 ? ev.FormName : $"선택지 {known} / {total}" + (codex.IsComplete(ev) ? " · 다 봄" : "");
                tile.Add(RowLabel(count, 16, UiStyle.TextDim));
            }
            _codexTiles.Add(tile);
            return tile;
        }

        void SelectCodex(int index)
        {
            if (_codexTiles.Count == 0)
                return;
            _codexIndex = Mathf.Clamp(index, 0, _codexTiles.Count - 1);
            for (int i = 0; i < _codexTiles.Count; i++)
            {
                bool on = i == _codexIndex;
                UiStyle.BorderColor(_codexTiles[i], on ? UiStyle.Gold : UiStyle.Stone9, 3);
                if (!on)
                    _codexTiles[i].style.borderTopColor = UiStyle.SlabTop;
                _codexTiles[i].EnableInClassList("selected", on);
            }
            _codexTiles[_codexIndex].GetFirstAncestorOfType<ScrollView>()?.ScrollTo(_codexTiles[_codexIndex]);
            RenderCodexDetail(TowerEventList.All[_codexIndex], _save.Codex);
        }

        /// <summary>오른쪽 칸: 고른 이벤트의 글, 선택지(조건은 흐린 글로 옆에), 골라 본 선택지의 결과.</summary>
        void RenderCodexDetail(TowerEvent ev, EventCodex codex)
        {
            var panel = _codexDetail;
            panel.Clear();
            if (!codex.Seen(ev.Id))
            {
                var unknown = UiStyle.Label("???", 30, UiStyle.TextDim);
                UiStyle.Black(unknown);
                panel.Add(unknown);
                panel.Add(Text("아직 만나지 못한 이벤트다.", 21, UiStyle.TextDim, top: 8));
                return;
            }
            bool full = codex.IsComplete(ev);
            var title = UiStyle.Label(ev.Title, 30, full ? UiStyle.Gold : UiStyle.Text);
            UiStyle.Black(title);
            panel.Add(title);
            var kind = Text(ev.FormName + (full ? $" · {TowerEvent.TendencyName(ev.Tendency)} · 기대값 {ev.Value:+0.#;-0.#;0}장" : ""), 19, UiStyle.TextDim);
            panel.Add(kind);
            var text = Text(ev.Text, 21, top: 8);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginBottom = 12;
            panel.Add(text);
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var choice = ev.Choices[i];
                bool known = codex.Chosen(ev.Id, i);
                var line = Row();
                line.style.flexWrap = Wrap.Wrap;
                line.style.alignItems = Align.FlexEnd;
                line.style.marginTop = i == 0 ? 0 : 10;
                line.Add(UiStyle.Label(ChoiceTitle(choice.Label), 21, UiStyle.Text, bold: true));
                string condition = choice.HasCondition ? RunState.ConditionText(choice, ev.Scope).Replace(" (", " · ").Replace(")", "") : ChoiceCost(choice.Label);
                if (choice.IsForced)
                    condition = "다른 선택지가 모두 잠겼을 때";
                if (!string.IsNullOrEmpty(condition))
                    line.Add(UiStyle.Label(" · " + condition, 19, UiStyle.TextDim));
                panel.Add(line);
                var result = Text("결과: " + (known ? choice.DescribeEffects() : "???"), 19, known ? UiStyle.Good : UiStyle.TextDim, top: 1);
                result.style.whiteSpace = WhiteSpace.Normal;
                result.style.paddingLeft = 20;
                panel.Add(result);
            }
            if (full)
            {
                var tags = Text("등장 조건: " + ev.DescribeTags(), 17, UiStyle.TextDim, top: 14);
                tags.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(tags);
            }
        }

        void CodexKey(Nav nav)
        {
            int n = _codexTiles.Count, i = _codexIndex;
            switch (nav)
            {
                case Nav.Left:
                    if (i % CodexColumns > 0)
                        i--;
                    break;
                case Nav.Right:
                    if (i % CodexColumns < CodexColumns - 1 && i + 1 < n)
                        i++;
                    break;
                case Nav.Up:
                    if (i - CodexColumns >= 0)
                        i -= CodexColumns;
                    break;
                case Nav.Down:
                    if (i + CodexColumns < n)
                        i += CodexColumns;
                    break;
                case Nav.Cancel:
                    CloseModal();
                    return;
                default:
                    return;
            }
            SelectCodex(i);
        }
    }
}

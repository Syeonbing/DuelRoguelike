// 이벤트 도감 창 (시작 화면에서 연다). 규칙 결정 문서 "이벤트 도감".
// - 만나지 않은 이벤트는 "???"로만 보인다.
// - 만난 이벤트는 상황 글과 선택지가 보이고, 골라 본 선택지만 결과가 보인다.
// - 도감에 쓰는 선택지("지나간다"·강제 선택지 제외)를 모두 골라 본 이벤트는 등장 조건과 성향(+기대값)이 보인다.
using System.Linq;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        void OpenCodex()
        {
            var codex = _save.Codex;
            codex.Absorb(_save.Run); // 진행 중인 런에서 만난 것도 (저장할 때 어차피 더해진다)
            var panel = OpenModal(1300);
            panel.name = "event-codex";
            panel.style.height = Length.Percent(90);
            int complete = TowerEventList.All.Count(codex.IsComplete);
            panel.Add(UiStyle.Label("이벤트 도감", 30, UiStyle.Accent, bold: true));
            panel.Add(Text($"만난 이벤트 {TowerEventList.All.Count(e => codex.Seen(e.Id))}/{TowerEventList.All.Count} · " +
                           $"모든 선택지를 골라 본 이벤트 {complete} (등장 조건과 성향이 보입니다)", 15, UiStyle.TextDim, top: 4));

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "event-codex-list" };
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 10;
            foreach (var ev in TowerEventList.All)
                scroll.Add(CodexEntry(ev, codex));
            panel.Add(scroll);

            var close = UiStyle.Button("닫기", CloseModal, 18);
            close.name = "event-codex-close";
            close.style.marginTop = 10;
            close.style.alignSelf = Align.FlexEnd;
            panel.Add(close);
        }

        VisualElement CodexEntry(TowerEvent ev, EventCodex codex)
        {
            var box = UiStyle.PanelBox($"codex-{ev.Id}");
            box.style.marginBottom = 8;
            UiStyle.Padding(box, 12);
            if (!codex.Seen(ev.Id))
            {
                box.Add(UiStyle.Label("???", 20, UiStyle.TextDim, bold: true));
                box.Add(Text("아직 만나지 않은 이벤트", 14, UiStyle.TextDim, top: 2));
                return box;
            }
            bool full = codex.IsComplete(ev);
            var head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.Add(UiStyle.Label(ev.Title, 20, full ? UiStyle.Accent : UiStyle.Text, bold: true));
            var kind = Text(ev.FormName + (full ? $" · {TowerEvent.TendencyName(ev.Tendency)} (기대값 {ev.Value:+0.#;-0.#;0}장)" : ""), 14, UiStyle.TextDim);
            kind.style.marginLeft = 12;
            head.Add(kind);
            box.Add(head);
            var text = Text(ev.Text, 15, top: 4);
            text.style.whiteSpace = WhiteSpace.Normal;
            box.Add(text);
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var choice = ev.Choices[i];
                bool known = codex.Chosen(ev.Id, i);
                string condition = choice.HasCondition ? $" [조건: {RunState.ConditionText(choice, ev.Scope)}]" : "";
                string forced = choice.IsForced ? " [다른 선택지가 모두 잠겼을 때]" : "";
                string result = known ? choice.DescribeEffects() : "결과: ???";
                var line = Text($"· {choice.Label}{condition}{forced} — {result}", 14, known ? UiStyle.Text : UiStyle.TextDim, top: 2);
                line.style.whiteSpace = WhiteSpace.Normal;
                box.Add(line);
            }
            if (full)
            {
                var tags = Text("등장 조건: " + ev.DescribeTags(), 14, UiStyle.TextDim, top: 4);
                tags.style.whiteSpace = WhiteSpace.Normal;
                box.Add(tags);
            }
            return box;
        }
    }
}

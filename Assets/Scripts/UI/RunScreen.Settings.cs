// 런 화면: 설정 창. 처음 화면과 탑(허브) 메뉴의 "설정"에서 연다. RunScreen.cs의 머리말 참고.
// 지금은 효과음·배경음 음량(0~10, 0이면 끔)만 있다. 값은 Sound가 PlayerPrefs에 둔다 (런 저장 파일과 상관없다).
// 키보드: 위아래로 줄을 고르고 좌우로 음량을 바꾼다. 취소 키로 닫는다. 마우스는 줄마다 −·+ 버튼.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        VisualElement _settingsPanel;
        int _settingsIndex;
        readonly List<(VisualElement Row, Label Mark, Action<int> Adjust)> _settingsRows = new List<(VisualElement, Label, Action<int>)>();

        bool SettingsOpen => _settingsPanel != null && _settingsPanel.panel != null;

        void OpenSettings()
        {
            var panel = OpenSlab("settings", 900);
            _settingsPanel = panel;
            _settingsRows.Clear();
            panel.Add(WindowTitle("설정"));
            var note = Text("음량은 0부터 10까지 고릅니다. 0이면 끕니다.", 19, UiStyle.TextDim, top: 6);
            note.style.marginBottom = 14;
            panel.Add(note);
            panel.Add(VolumeRow("sfx", "효과음", () => Sound.SfxLevel, level =>
            {
                Sound.SfxLevel = level;
                Sound.Play(Sfx.Flip); // 바꾼 크기로 들려 준다
            }));
            panel.Add(VolumeRow("music", "배경음", () => Sound.MusicLevel, level => Sound.MusicLevel = level));
            var hints = Hints(("↑", "W", null), ("↓", "S", "고르기"), ("←", "A", null), ("→", "D", "음량"), ("X", "K", "닫기"));
            panel.Add(WindowFooter(hints, KeyButton("settings-close", "닫기", CloseModal, "X", "K", 112)));
            SelectSettingsRow(0);
        }

        /// <summary>음량 한 줄: ▶, 이름, 10칸 눈금, 값(끔·10%~100%), −·+ 버튼.</summary>
        VisualElement VolumeRow(string key, string title, Func<int> get, Action<int> set)
        {
            int index = _settingsRows.Count;
            var row = Row();
            row.name = $"settings-{key}";
            row.style.alignItems = Align.Center;
            row.style.height = 76;
            row.style.marginTop = index == 0 ? 0 : 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 14;
            UiStyle.Radius(row, 6);
            UiStyle.BorderColor(row, Color.clear, 2);
            var mark = UiStyle.Label("▶", 17, Color.clear);
            mark.style.width = 20;
            mark.style.marginRight = 12;
            mark.pickingMode = PickingMode.Ignore;
            row.Add(mark);
            var name = UiStyle.Label(title, 27, UiStyle.Text, bold: true);
            name.style.width = 150;
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var gauge = Row();
            gauge.style.alignItems = Align.Center;
            var blocks = new List<VisualElement>();
            for (int i = 0; i < Sound.MaxLevel; i++)
            {
                var block = new VisualElement { pickingMode = PickingMode.Ignore };
                block.style.width = 24;
                block.style.height = 16 + i * 2; // 갈수록 높아지는 눈금
                block.style.marginRight = 5;
                block.style.alignSelf = Align.FlexEnd;
                UiStyle.Radius(block, 3);
                UiStyle.BorderColor(block, UiStyle.Stone5, 1);
                blocks.Add(block);
                gauge.Add(block);
            }
            gauge.style.height = 36;
            row.Add(gauge);

            var value = UiStyle.Label("", 23, UiStyle.Gold, bold: true);
            value.name = $"settings-{key}-value";
            value.style.width = 80;
            value.style.marginLeft = 12;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.pickingMode = PickingMode.Ignore;
            row.Add(value);

            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            row.Add(gap);
            Button down = null, up = null;
            down = UiStyle.StoneButton("−", () => Adjust(-1), small: true);
            down.name = $"settings-{key}-down";
            down.style.width = 56;
            row.Add(down);
            up = UiStyle.StoneButton("+", () => Adjust(1), small: true);
            up.name = $"settings-{key}-up";
            up.style.width = 56;
            up.style.marginLeft = 8;
            row.Add(up);

            void Refresh()
            {
                int level = get();
                value.text = level == 0 ? "끔" : $"{level * 10}%";
                for (int i = 0; i < blocks.Count; i++)
                    blocks[i].style.backgroundColor = i < level ? UiStyle.Gold : UiStyle.Stone9;
                down.SetEnabled(level > 0);
                up.SetEnabled(level < Sound.MaxLevel);
            }

            void Adjust(int delta)
            {
                int level = Mathf.Clamp(get() + delta, 0, Sound.MaxLevel);
                if (level != get())
                    set(level);
                Refresh();
            }

            row.RegisterCallback<MouseEnterEvent>(_ => SelectSettingsRow(index));
            _settingsRows.Add((row, mark, Adjust));
            Refresh();
            return row;
        }

        void SelectSettingsRow(int index)
        {
            if (_settingsRows.Count == 0)
                return;
            _settingsIndex = (index + _settingsRows.Count) % _settingsRows.Count;
            for (int i = 0; i < _settingsRows.Count; i++)
            {
                bool on = i == _settingsIndex;
                var (row, mark, _) = _settingsRows[i];
                row.style.backgroundColor = on ? UiStyle.Alpha(UiStyle.Gold, 0.1f) : Color.clear;
                UiStyle.BorderColor(row, on ? UiStyle.Gold : Color.clear, 2);
                mark.style.color = on ? UiStyle.Gold : Color.clear;
                row.EnableInClassList("selected", on);
            }
        }

        void SettingsKey(Nav nav)
        {
            switch (nav)
            {
                case Nav.Up:
                    SelectSettingsRow(_settingsIndex - 1);
                    break;
                case Nav.Down:
                    SelectSettingsRow(_settingsIndex + 1);
                    break;
                case Nav.Left:
                    _settingsRows[_settingsIndex].Adjust(-1);
                    break;
                case Nav.Right:
                    _settingsRows[_settingsIndex].Adjust(1);
                    break;
                case Nav.Cancel:
                    CloseModal();
                    break;
            }
        }
    }
}

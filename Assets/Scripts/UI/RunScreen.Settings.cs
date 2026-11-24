// 런 화면: 설정 창. 처음 화면과 탑(허브) 메뉴의 "설정"에서 연다. RunScreen.cs의 머리말 참고.
// 효과음·배경음 음량(0~10, 0이면 끔). 값은 Sound가 PlayerPrefs에 둔다 (런 저장 파일과 상관없다).
// 처음 화면에서 열었을 때만 맨 아래에 "게임 데이터 초기화" 줄이 있다 (하드 리셋, GameSaveFile.Reset). 확인 창은 처음에 "그만둔다"가 골라져 있다.
// 키보드: 위아래로 줄을 고르고 좌우로 음량을 바꾼다. 초기화 줄은 결정 키로 확인 창을 연다. 취소 키로 닫는다.
// 마우스는 음량 줄마다 −·+ 버튼, 초기화 줄은 "초기화" 버튼.
using System;
using System.Collections.Generic;
using DuelRoguelike.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace DuelRoguelike.UI
{
    public sealed partial class RunScreen
    {
        VisualElement _settingsPanel;
        int _settingsIndex;
        readonly List<(VisualElement Row, Label Mark, Action<int> Adjust, Action Activate)> _settingsRows = new List<(VisualElement, Label, Action<int>, Action)>();

        /// <summary>초기화를 끝내고 처음 화면에 보여 줄 알림 (다음에 저장하면 지운다). 없으면 null.</summary>
        string _resetNotice;

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
            bool atTitle = _run == null; // 탑(허브)에서 연 설정에는 초기화가 없다
            if (atTitle)
                panel.Add(ResetRow());
            var hints = atTitle
                ? Hints(("↑", "W", null), ("↓", "S", "고르기"), ("←", "A", null), ("→", "D", "음량"), ("Z", "J", "결정"), ("X", "K", "닫기"))
                : Hints(("↑", "W", null), ("↓", "S", "고르기"), ("←", "A", null), ("→", "D", "음량"), ("X", "K", "닫기"));
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
            _settingsRows.Add((row, mark, Adjust, null));
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
                var (row, mark, _, _) = _settingsRows[i];
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
                    _settingsRows[_settingsIndex].Adjust?.Invoke(-1);
                    break;
                case Nav.Right:
                    _settingsRows[_settingsIndex].Adjust?.Invoke(1);
                    break;
                case Nav.Confirm:
                    _settingsRows[_settingsIndex].Activate?.Invoke();
                    break;
                case Nav.Cancel:
                    CloseModal();
                    break;
            }
        }

        // ================= 게임 데이터 초기화 (하드 리셋) =================

        /// <summary>설정 창 맨 아래 줄: 가는 선 아래에 "게임 데이터", 설명, 빨간 "초기화" 버튼.</summary>
        VisualElement ResetRow()
        {
            int index = _settingsRows.Count;
            var block = new VisualElement();
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.height = 1;
            line.style.marginTop = 18;
            line.style.marginBottom = 10;
            line.style.backgroundColor = UiStyle.Stone5;
            block.Add(line);

            var row = Row();
            row.name = "settings-reset";
            row.style.alignItems = Align.Center;
            row.style.height = 76;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 14;
            UiStyle.Radius(row, 6);
            UiStyle.BorderColor(row, Color.clear, 2);
            var mark = UiStyle.Label("▶", 17, Color.clear);
            mark.style.width = 20;
            mark.style.marginRight = 12;
            mark.pickingMode = PickingMode.Ignore;
            row.Add(mark);
            var name = UiStyle.Label("게임 데이터", 27, UiStyle.Text, bold: true);
            name.style.width = 190;
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);
            var what = Text("모든 진행을 지우고 처음 상태로", 19, UiStyle.TextDim);
            what.style.flexGrow = 1;
            what.pickingMode = PickingMode.Ignore;
            row.Add(what);
            var open = UiStyle.StoneButton("초기화", OpenHardResetConfirm, small: true);
            open.name = "settings-reset-open";
            open.style.width = 140;
            open.style.color = UiStyle.Bad;
            row.Add(open);
            row.RegisterCallback<MouseEnterEvent>(_ => SelectSettingsRow(index));
            _settingsRows.Add((row, mark, null, OpenHardResetConfirm));
            block.Add(row);
            return block;
        }

        /// <summary>
        /// 초기화 확인 창: 지우는 것(지금 저장의 수치)과 남는 것(설정)을 보여 준다.
        /// 런 포기·전부 분해 창처럼 처음에는 "그만둔다"가 골라져 있어서, 결정 키를 두 번 눌러도 초기화되지 않는다.
        /// </summary>
        void OpenHardResetConfirm()
        {
            var lost = new List<string>
            {
                $"컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장",
                $"골드 {_save.Gold}",
                $"레시피 {_save.Recipes.Count}개",
                $"시작 사이드 덱 {_save.SideDeckLimit}장",
                $"이벤트 도감 {_save.Codex.SeenCount}개",
            };
            if (_save.Run != null)
                lost.Add($"진행 중인 런 {_save.Run.Floor}층");

            var panel = OpenSlab("hard-reset-confirm", 940);
            panel.style.alignItems = Align.Center;
            panel.Add(WindowTitle("게임 데이터 초기화"));
            var what = Text("처음 시작한 상태로 돌아간다. 지금까지의 진행이 모두 사라진다.", 26, top: 18);
            what.name = "hard-reset-text";
            what.style.whiteSpace = WhiteSpace.Normal;
            what.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(what);
            var gone = Text("지우는 것: " + string.Join(" · ", lost), 19, UiStyle.TextDim, top: 10);
            gone.name = "hard-reset-lost";
            gone.style.whiteSpace = WhiteSpace.Normal;
            gone.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(gone);
            panel.Add(Text("남는 것: 음량·듀얼 속도·체인 확인 설정", 19, UiStyle.TextDim, top: 4));
            var warn = Text("되돌릴 수 없다. 바로 전 저장도 함께 지운다.", 19, UiStyle.Opponent, top: 10);
            warn.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(warn);

            var buttons = new ButtonRow();
            buttons.Root.style.marginTop = 22;
            var confirm = buttons.Add("hard-reset-confirm-button", "초기화한다", HardReset, width: 300);
            confirm.style.color = UiStyle.Bad;
            buttons.Add("hard-reset-cancel", "그만둔다", BackToSettingsReset, gold: true, width: 300);
            buttons.Select(1);
            _windowRow = buttons;
            panel.Add(buttons.Root);
            var hints = Hints(("←", "A", null), ("→", "D", "고르기"), ("Z", "J", "결정"), ("X", "K", "그만둔다"));
            hints.name = "modal-hints";
            hints.style.marginTop = 16;
            panel.Add(hints);
            _windowCancel = BackToSettingsReset;
            ApplyKeyCaps();
        }

        /// <summary>확인 창에서 그만두면 설정 창의 초기화 줄로 돌아간다.</summary>
        void BackToSettingsReset()
        {
            OpenSettings();
            SelectSettingsRow(_settingsRows.Count - 1);
        }

        void HardReset()
        {
            try
            {
                _save = GameSaveFile.Reset(_saveDir);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _saveNotice = $"초기화하지 못했습니다 ({GameSaveFile.FilePath(_saveDir)}): {e.Message}";
                CloseModal();
                ShowStart();
                return;
            }
            Sound.Play(Sfx.Destroy);
            Debug.Log($"[설정] 게임 데이터 초기화: {GameSaveFile.FilePath(_saveDir)} 새 게임 (컬렉션 {_collection.Cards.Count}종 {_collection.TotalCount}장)");
            _saveNotice = null;
            _forfeitNotice = null;
            _resetNotice = "게임 데이터를 초기화했습니다. 처음 시작한 상태입니다.";
            _startRecipeId = _save.LastStartDeckId;
            _titleMenuIndex = 0;
            _recipeScrollOffset = Vector2.zero;
            _seedText = NewSeed().ToString();
            _confirmAbandon = false;
            _recipeWindow = RecipeWindow.Closed;
            CloseModal();
            ShowStart();
        }
    }
}

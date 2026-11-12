// 엔진의 선택 요청을 "카드를 직접 누르는" 조작으로 바꾼다.
// - 행동(소환·세트·발동·공격 등): 그 카드를 누르면 나오는 메뉴
// - 카드 고르기(대상·릴리스·합 등): 고를 수 있는 카드를 강조하고 눌러서 고른다 (필드·패 밖의 카드가 섞이면 카드 목록 창)
// - 칸 고르기(놓을 칸·무효로 할 칸): 고를 수 있는 필드 칸을 강조하고 눌러서 고른다
// - 순서 정하기(덱 위의 카드 순서 등): 카드 목록 창에서 누른 순서대로 번호를 붙인다
// - 페이즈 넘기기·체인하지 않음·확인/취소: 필드 가운데 줄의 버튼
// - 예/아니오, 효과·표시 형식 고르기, 선언: 가운데 대화창
using System;
using System.Collections.Generic;
using System.Linq;
using DuelRoguelike.Engine.Duel;
using DuelRoguelike.Engine.Ocg;

namespace DuelRoguelike.UI
{
    /// <summary>카드 자리 (표시 형식 제외). 요청의 카드·칸과 화면의 칸을 맞출 때 쓴다.</summary>
    internal readonly struct CardKey : IEquatable<CardKey>
    {
        public readonly byte Controller;
        public readonly OcgLocation Location;
        public readonly uint Sequence;

        public CardKey(CardLocation loc)
        {
            Controller = loc.Controller;
            Location = loc.Location;
            Sequence = loc.Sequence;
        }

        public CardKey(byte controller, OcgLocation location, uint sequence)
        {
            Controller = controller;
            Location = location;
            Sequence = sequence;
        }

        public bool Equals(CardKey o) => Controller == o.Controller && Location == o.Location && Sequence == o.Sequence;
        public override bool Equals(object obj) => obj is CardKey o && Equals(o);
        public override int GetHashCode() => (Controller << 28) ^ ((int)Location << 16) ^ (int)Sequence;
    }

    internal sealed class CardAction
    {
        public string Label;
        public byte[] Response;
    }

    internal sealed class StripButton
    {
        public string Label;
        public byte[] Response;
    }

    internal sealed class SelectionCandidate
    {
        public int Index;
        public CardRef Card;
        public string Extra;
        public bool Selected;
    }

    internal sealed class CardSelection
    {
        /// <summary>true면 누를 때마다 바로 응답한다 (MSG_SELECT_UNSELECT_CARD).</summary>
        public bool Immediate;
        /// <summary>true면 한 장만 고르는 경우에도 확인 버튼으로 보낸다.</summary>
        public bool AlwaysConfirm;
        /// <summary>
        /// true면 누른 순서를 기억한다 (카드 순서 정하기). 카드에 누른 순서대로 번호가 붙고, 확인하면 누른 순서대로 Build에 넘긴다.
        /// 같은 카드가 두 번 나올 수 있어(체인 순서 정하기) 언제나 목록 창으로 고른다.
        /// </summary>
        public bool Ordered;
        public string ConfirmLabel = "확인";
        public int Min;
        public int Max;
        public bool Cancelable;
        public readonly List<SelectionCandidate> Candidates = new List<SelectionCandidate>();
        public readonly HashSet<int> Chosen = new HashSet<int>();
        /// <summary>누른 순서 (Ordered일 때만 쓴다).</summary>
        public readonly List<int> Order = new List<int>();
        public Func<IReadOnlyCollection<int>, bool> IsValid = _ => true;
        public Func<List<int>, byte[]> Build;
        public Func<int, byte[]> BuildImmediate;
        public byte[] CancelResponse;

        /// <summary>필드·패 밖(덱, 묘지, 제외, 엑스트라 덱, 엑시즈 소재)의 카드가 섞여 있으면 목록 창으로 고른다.</summary>
        public bool NeedsWindow => Ordered || Candidates.Any(c => !IsOnBoard(c.Card.Location));

        /// <summary>순서 정하기에서 이 카드의 번호 (1부터). 아직 누르지 않았으면 0.</summary>
        public int OrderOf(SelectionCandidate c) => Ordered ? Order.IndexOf(c.Index) + 1 : 0;

        /// <summary>안내 줄에 쓰는 진행 상황.</summary>
        public string Progress => Ordered ? $"{Chosen.Count} / {Candidates.Count}장 순서를 정했다" : $"{Chosen.Count}장 골랐다";

        public SelectionCandidate Find(CardKey key) => Candidates.FirstOrDefault(c => new CardKey(c.Card.Location).Equals(key));

        public bool IsChosen(SelectionCandidate c) => Immediate ? c.Selected : Chosen.Contains(c.Index);

        /// <summary>후보를 눌렀을 때 보낼 응답. 아직 보낼 때가 아니면(여러 장 고르는 중) null을 돌려주고 선택만 바꾼다.</summary>
        public byte[] Click(SelectionCandidate c)
        {
            if (Immediate)
                return BuildImmediate(c.Index);
            if (IsSingle)
                return Build(new List<int> { c.Index });
            // 순서 정하기에서 번호가 붙은 카드를 다시 누르면 번호가 빠지고 뒤 번호가 하나씩 당겨진다.
            if (Chosen.Remove(c.Index))
                Order.Remove(c.Index);
            else
            {
                Chosen.Add(c.Index);
                Order.Add(c.Index);
            }
            return null;
        }

        /// <summary>한 장을 누르자마자 보내는 경우.</summary>
        public bool IsSingle => !AlwaysConfirm && Min == 1 && Max == 1;

        /// <summary>확인 버튼이 필요한 선택인지.</summary>
        public bool NeedsConfirm => !Immediate && !IsSingle;

        public bool CanConfirm => NeedsConfirm && IsValid(Chosen);

        public byte[] Confirm() => Build(Ordered ? Order.ToList() : Chosen.OrderBy(i => i).ToList());

        public static bool IsOnBoard(CardLocation loc) =>
            !loc.IsOverlay && (loc.Location == OcgLocation.Hand || loc.Location == OcgLocation.MonsterZone || loc.Location == OcgLocation.SpellTrapZone);
    }

    /// <summary>
    /// 칸 고르기 (MSG_SELECT_PLACE / MSG_SELECT_DISFIELD). 고를 수 있는 칸을 강조하고 눌러서 고른다.
    /// 한 칸이면 누르자마자 보내고, 여러 칸이면 정확히 Count칸을 고른 뒤 확인으로 보낸다. 엔진이 취소를 받지 않으므로 취소는 없다.
    /// </summary>
    internal sealed class PlaceSelection
    {
        public int Count;
        /// <summary>true면 카드가 있는 칸도 고를 수 있다 (무효로 할 칸·지정할 칸).</summary>
        public bool IsDisableField;
        public readonly List<CardKey> Candidates = new List<CardKey>();
        /// <summary>고른 순서대로.</summary>
        public readonly List<CardKey> Chosen = new List<CardKey>();

        public bool Contains(CardKey zone) => Candidates.Contains(zone);

        public bool IsChosen(CardKey zone) => Chosen.Contains(zone);

        /// <summary>칸을 눌렀을 때 보낼 응답. 여러 칸을 고르는 중이면 null을 돌려주고 선택만 바꾼다.</summary>
        public byte[] Click(CardKey zone)
        {
            if (!Contains(zone))
                return null;
            if (Count <= 1)
                return Build(new List<CardKey> { zone });
            if (!Chosen.Remove(zone) && Chosen.Count < Count)
                Chosen.Add(zone);
            return null;
        }

        public bool NeedsConfirm => Count > 1;

        public bool CanConfirm => NeedsConfirm && Chosen.Count == Count;

        public byte[] Confirm() => Build(Chosen);

        static byte[] Build(IEnumerable<CardKey> zones) =>
            DuelResponses.Places(zones.Select(z => new DuelResponses.Place(z.Controller, z.Location, (byte)z.Sequence)).ToList());

        /// <summary>화면에 칸이 있는 자리: 메인 몬스터 존 0~4, 마법·함정 존 0~4, 필드 존(5). 엑스트라 몬스터 존과 펜듈럼 존은 두지 않는다.</summary>
        public static bool IsOnScreen(OcgLocation location, int sequence) =>
            location == OcgLocation.MonsterZone ? sequence < 5 : location == OcgLocation.SpellTrapZone && sequence <= 5;
    }

    internal sealed class DialogSpec
    {
        public string Title;
        public string Detail;
        public readonly List<StripButton> Buttons = new List<StripButton>();
        /// <summary>여러 개를 켜고 확인하는 선언 (종족·속성). null이면 쓰지 않는다.</summary>
        public List<(string Label, ulong Bit)> Toggles;
        public int ToggleCount;
        public Func<ulong, byte[]> BuildToggles;
        /// <summary>카드 코드를 입력하는 선언.</summary>
        public bool AskCardCode;
    }

    internal sealed class DuelInteraction
    {
        public DuelRequest Request;
        public string Prompt;
        public string PromptDetail;
        public readonly Dictionary<CardKey, List<CardAction>> Actions = new Dictionary<CardKey, List<CardAction>>();
        public readonly List<StripButton> Buttons = new List<StripButton>();
        public CardSelection Selection;
        public PlaceSelection Places;
        public DialogSpec Dialog;

        public bool HasAction(CardKey key) => Actions.ContainsKey(key);

        /// <summary>이 자리(플레이어 + 위치)에 행동이나 고를 카드가 있는지 (묘지·제외·덱 더미 강조용).</summary>
        public bool Touches(byte controller, OcgLocation location) =>
            Actions.Keys.Any(k => k.Controller == controller && k.Location == location)
            || (Selection != null && Selection.Candidates.Any(c => c.Card.Location.Controller == controller && c.Card.Location.Location == location));

        public static DuelInteraction Build(DuelRequest request, DuelState state, DuelText text, bool isRetry)
        {
            var it = new DuelInteraction { Request = request };
            it.PromptDetail = isRetry ? "앞의 선택은 받아들여지지 않았다. 다시 고른다." : null;
            switch (request)
            {
                case SelectIdleCmdRequest r: it.Idle(r, state, text); break;
                case SelectBattleCmdRequest r: it.Battle(r, text); break;
                case SelectChainRequest r: it.Chain(r, state, text); break;
                case SelectEffectYesNoRequest r:
                    it.YesNo($"「{text.CardName(r.Code)}」의 효과를 발동할까요?", text.Description(r.Description));
                    break;
                case SelectYesNoRequest r:
                    it.YesNo(text.Description(r.Description) ?? "예/아니오를 고르세요.", null);
                    break;
                case SelectOptionRequest r:
                    it.Dialog = new DialogSpec { Title = HintTitle(r, text, "효과를 고르세요") };
                    for (int i = 0; i < r.Options.Count; i++)
                        it.Dialog.Buttons.Add(new StripButton { Label = text.Description(r.Options[i]) ?? $"선택지 {i + 1}", Response = DuelResponses.Option(i) });
                    break;
                case SelectPositionRequest r:
                    it.Dialog = new DialogSpec { Title = $"「{text.CardName(r.Code)}」의 표시 형식을 고르세요" };
                    foreach (var p in new[] { OcgPosition.FaceUpAttack, OcgPosition.FaceUpDefense, OcgPosition.FaceDownAttack, OcgPosition.FaceDownDefense })
                        if ((r.Positions & p) != 0)
                            it.Dialog.Buttons.Add(new StripButton { Label = DuelText.Position(p), Response = DuelResponses.Position(p) });
                    break;
                case SelectCardRequest r:
                    it.Select(HintTitle(r, text, "카드를 고르세요") + CountSuffix(r.Min, r.Max), r.Min, r.Max, r.Cancelable,
                        r.Cards.Select((c, i) => new SelectionCandidate { Index = i, Card = c }),
                        chosen => DuelSelectionRules.IsValidCards(r, chosen));
                    break;
                case SelectTributeRequest r:
                    it.Select(HintTitle(r, text, "릴리스할 몬스터를 고르세요") + $" (릴리스 {r.Min}장분)", r.Min, r.Max, r.Cancelable,
                        r.Cards.Select((c, i) => new SelectionCandidate
                        {
                            Index = i, Card = new CardRef(c.Code, c.Location), Extra = c.ReleaseParam > 1 ? $"×{c.ReleaseParam}" : null,
                        }),
                        chosen => DuelSelectionRules.IsValidTribute(r, chosen));
                    break;
                case SelectSumRequest r:
                    it.Select(HintTitle(r, text, "카드를 고르세요") + $" (합 {r.Target}{(r.Exact ? "" : " 이상")})", r.Min, Math.Max(r.Max, r.Selectable.Count), false,
                        r.Selectable.Select((c, i) => new SelectionCandidate { Index = i, Card = new CardRef(c.Code, c.Location), Extra = (c.Param & 0xffff).ToString() }),
                        chosen => DuelSelectionRules.IsValidSum(r, chosen));
                    it.Selection.AlwaysConfirm = true; // 합 조건은 한 장이라도 확인을 눌러 보낸다.
                    break;
                case SelectUnselectCardRequest r: it.Unselect(r, text); break;
                case SelectPlaceRequest r: it.Place(r, text); break;
                case SortCardRequest r: it.Sort(r, state); break;
                case SelectCounterRequest r:
                    it.Dialog = new DialogSpec { Title = $"카운터 {r.Count}개를 제거할 카드를 정하세요", Detail = "지금은 자동으로 나눠서 뺀다." };
                    it.Dialog.Buttons.Add(new StripButton { Label = "자동으로 나누기", Response = new SimpleAutoPlayer().Respond(r, state) });
                    break;
                case AnnounceNumberRequest r:
                    it.Dialog = new DialogSpec { Title = HintTitle(r, text, "숫자를 선언하세요") };
                    for (int i = 0; i < r.Options.Count; i++)
                        it.Dialog.Buttons.Add(new StripButton { Label = r.Options[i].ToString(), Response = DuelResponses.AnnounceNumber(i) });
                    break;
                case AnnounceRaceRequest r:
                    it.Dialog = Toggles($"종족을 {r.Count}개 선언하세요", r.Count, r.Available, b => DuelText.Race(b), m => DuelResponses.AnnounceRace(m));
                    break;
                case AnnounceAttributeRequest r:
                    it.Dialog = Toggles($"속성을 {r.Count}개 선언하세요", r.Count, r.Available, b => DuelText.Attribute((OcgAttribute)b), m => DuelResponses.AnnounceAttribute((uint)m));
                    break;
                case AnnounceCardRequest r:
                    it.Dialog = new DialogSpec { Title = HintTitle(r, text, "카드를 선언하세요"), Detail = "카드 코드(숫자)를 입력한다.", AskCardCode = true };
                    break;
                case RockPaperScissorsRequest _:
                    it.Dialog = new DialogSpec { Title = "가위바위보" };
                    it.Dialog.Buttons.Add(new StripButton { Label = "가위", Response = DuelResponses.RockPaperScissors(1) });
                    it.Dialog.Buttons.Add(new StripButton { Label = "바위", Response = DuelResponses.RockPaperScissors(2) });
                    it.Dialog.Buttons.Add(new StripButton { Label = "보", Response = DuelResponses.RockPaperScissors(3) });
                    break;
                default:
                    it.Dialog = new DialogSpec { Title = $"아직 화면이 없는 선택: {request.Type}", Detail = "아래 자동 선택을 쓴다." };
                    break;
            }
            if (it.Prompt == null)
                it.Prompt = it.Dialog?.Title ?? "";
            return it;
        }

        void Idle(SelectIdleCmdRequest r, DuelState state, DuelText text)
        {
            Prompt = $"{DuelText.Phase(state.Phase)}: 카드를 눌러 행동을 고르세요";
            for (int i = 0; i < r.Summonable.Count; i++) Add(r.Summonable[i].Location, "일반 소환", DuelResponses.Summon(i));
            for (int i = 0; i < r.MonsterSetable.Count; i++) Add(r.MonsterSetable[i].Location, "세트", DuelResponses.MonsterSet(i));
            for (int i = 0; i < r.SpecialSummonable.Count; i++) Add(r.SpecialSummonable[i].Location, "특수 소환", DuelResponses.SpecialSummon(i));
            AddEffects(r.Activatable, text, DuelResponses.IdleActivate);
            for (int i = 0; i < r.SpellSetable.Count; i++) Add(r.SpellSetable[i].Location, "세트", DuelResponses.SpellSet(i));
            for (int i = 0; i < r.Repositionable.Count; i++) Add(r.Repositionable[i].Location, "표시 형식 변경", DuelResponses.Reposition(i));
            if (r.CanBattlePhase) Buttons.Add(new StripButton { Label = "배틀 페이즈", Response = DuelResponses.ToBattlePhase() });
            if (r.CanEndPhase) Buttons.Add(new StripButton { Label = "턴 종료", Response = DuelResponses.IdleToEndPhase() });
        }

        void Battle(SelectBattleCmdRequest r, DuelText text)
        {
            Prompt = "배틀 페이즈: 공격할 몬스터를 누르세요";
            for (int i = 0; i < r.Attackers.Count; i++)
                Add(r.Attackers[i].Location, r.Attackers[i].CanDirectAttack ? "직접 공격" : "공격", DuelResponses.Attack(i));
            AddEffects(r.Activatable, text, DuelResponses.BattleActivate);
            if (r.CanMain2) Buttons.Add(new StripButton { Label = "메인 페이즈 2", Response = DuelResponses.ToMain2() });
            if (r.CanEndPhase) Buttons.Add(new StripButton { Label = "턴 종료", Response = DuelResponses.BattleToEndPhase() });
        }

        void Chain(SelectChainRequest r, DuelState state, DuelText text)
        {
            var top = state.Chain.LastOrDefault();
            Prompt = top != null
                ? $"체인 {state.Chain.Count}({text.CardName(top.Code)})에 체인할 수 있어요. 발동할 카드를 누르세요"
                : r.Forced ? "발동할 효과를 고르세요 (꼭 발동해야 함)" : "지금 발동할 수 있는 카드가 있어요. 발동할 카드를 누르세요";
            AddEffects(r.Chains, text, DuelResponses.Chain);
            if (!r.Forced)
            {
                Buttons.Add(new StripButton { Label = "발동하지 않음", Response = DuelResponses.NoChain() });
                PromptDetail = (PromptDetail != null ? PromptDetail + " " : "") + "우클릭: 발동하지 않음"; // DuelScreen.OnRightClick
            }
        }

        void YesNo(string title, string detail)
        {
            Dialog = new DialogSpec { Title = title, Detail = detail };
            Dialog.Buttons.Add(new StripButton { Label = "예", Response = DuelResponses.YesNo(true) });
            Dialog.Buttons.Add(new StripButton { Label = "아니오", Response = DuelResponses.YesNo(false) });
        }

        void Select(string prompt, int min, int max, bool cancelable, IEnumerable<SelectionCandidate> candidates, Func<IReadOnlyCollection<int>, bool> isValid)
        {
            Prompt = prompt;
            Selection = new CardSelection
            {
                Min = min, Max = max, Cancelable = cancelable, IsValid = isValid,
                Build = DuelResponses.Cards, CancelResponse = cancelable ? DuelResponses.CancelCards() : null,
            };
            Selection.Candidates.AddRange(candidates);
            if (cancelable)
                Buttons.Add(new StripButton { Label = "취소", Response = DuelResponses.CancelCards() });
        }

        void Unselect(SelectUnselectCardRequest r, DuelText text)
        {
            Prompt = HintTitle(r, text, "카드를 고르세요") + CountSuffix(r.Min, r.Max);
            Selection = new CardSelection { Immediate = true, Min = r.Min, Max = r.Max, BuildImmediate = DuelResponses.SelectUnselect };
            for (int i = 0; i < r.Selectable.Count; i++)
                Selection.Candidates.Add(new SelectionCandidate { Index = i, Card = r.Selectable[i] });
            for (int i = 0; i < r.Unselectable.Count; i++)
                Selection.Candidates.Add(new SelectionCandidate { Index = r.Selectable.Count + i, Card = r.Unselectable[i], Selected = true });
            if (r.Finishable)
                Buttons.Add(new StripButton { Label = "선택 끝내기", Response = DuelResponses.FinishSelectUnselect() });
            else if (r.Cancelable)
                Buttons.Add(new StripButton { Label = "취소", Response = DuelResponses.FinishSelectUnselect() });
        }

        /// <summary>
        /// 카드 순서 정하기 (MSG_SORT_CARD / MSG_SORT_CHAIN). 카드를 누른 순서대로 번호를 붙이고, 모두 누르면 확정한다.
        /// 덱 위·아래에 둘 카드(Duel.SortDecktop / SortDeckbottom)는 먼저 누른 카드가 위로 간다.
        /// 동시에 처리하는 효과(MSG_SORT_CHAIN, 공격할 때의 비용)는 먼저 누른 효과부터 처리한다.
        /// </summary>
        void Sort(SortCardRequest r, DuelState state)
        {
            int count = r.Cards.Count;
            string how;
            if (r.Type == OcgMessageType.SortChain)
            {
                Prompt = "효과를 처리할 순서를 정하세요";
                how = "먼저 처리할 효과부터 누른다.";
            }
            else
            {
                byte owner = count > 0 ? r.Cards[0].Location.Controller : r.Player;
                bool deck = count > 0 && r.Cards.All(c => c.Location.Location == OcgLocation.Deck && c.Location.Controller == owner);
                // 덱은 순번 0이 맨 아래다. 맨 아래 카드들을 정할 때(Duel.SortDeckbottom)는 고를 카드가 순번 0부터 count-1까지다.
                bool bottom = deck && owner < state.Players.Length && count < state.Players[owner].DeckCount
                              && r.Cards.All(c => c.Location.Sequence < count);
                string whose = owner == r.Player ? "내" : "상대";
                Prompt = !deck ? "카드의 순서를 정하세요" : bottom ? $"{whose} 덱 맨 아래 카드의 순서를 정하세요" : $"{whose} 덱 위 카드의 순서를 정하세요";
                how = bottom ? "위에 둘 카드부터 누른다 (마지막에 누른 카드가 덱 맨 아래)." : "위에 둘 카드부터 누른다 (먼저 누른 카드가 덱 맨 위).";
            }
            PromptDetail = (PromptDetail != null ? PromptDetail + " " : "") + how + " 다시 누르면 번호가 빠진다.";
            Selection = new CardSelection
            {
                Ordered = true, AlwaysConfirm = true, Min = count, Max = count, ConfirmLabel = "이 순서로 확정",
                IsValid = chosen => chosen.Count == count,
                Build = picked => DuelResponses.Sort(SortOrder(picked, count)),
            };
            for (int i = 0; i < count; i++)
                Selection.Candidates.Add(new SelectionCandidate { Index = i, Card = r.Cards[i] });
            Buttons.Add(new StripButton { Label = "그대로 두기", Response = DuelResponses.DefaultSort() });
        }

        /// <summary>누른 순서(picked[k] = k번째로 누른 카드)를 엔진 응답(order[i] = i번째 카드의 새 순번, 0이 맨 위·먼저)으로 바꾼다.</summary>
        public static int[] SortOrder(IReadOnlyList<int> picked, int count)
        {
            var order = new int[count];
            for (int position = 0; position < picked.Count; position++)
                order[picked[position]] = position;
            return order;
        }

        void Place(SelectPlaceRequest r, DuelText text)
        {
            int count = Math.Max(r.Count, 1);
            var zones = DuelResponses.AvailablePlaces(r)
                .Where(p => PlaceSelection.IsOnScreen(p.Location, p.Sequence))
                .Select(p => new CardKey(p.Player, p.Location, p.Sequence))
                .ToList();
            if (zones.Count < count)
            {
                // 이 룰(엑스트라 몬스터 존·펜듈럼 존 없음)에서는 나오지 않는다.
                Dialog = new DialogSpec { Title = "화면에 없는 칸만 고를 수 있다", Detail = "첫 빈칸을 자동으로 고른다." };
                Dialog.Buttons.Add(new StripButton { Label = "자동으로 고르기", Response = SimpleAutoPlayer.FirstPlaces(r) });
                return;
            }
            Places = new PlaceSelection { Count = count, IsDisableField = r.IsDisableField };
            Places.Candidates.AddRange(zones);
            Prompt = PlaceTitle(r, text) + (count > 1 ? $" ({count}칸)" : "");
        }

        static string PlaceTitle(SelectPlaceRequest r, DuelText text)
        {
            // 카드를 필드에 낼 때 엔진은 고르기 안내(HINT_SELECTMSG)에 그 카드의 코드를 넣는다.
            if (r.SelectHint != 0 && r.SelectHint <= uint.MaxValue && text.Record((uint)r.SelectHint) != null)
                return $"「{text.CardName((uint)r.SelectHint)}」을(를) 놓을 칸을 고르세요";
            return HintTitle(r, text, r.IsDisableField ? "사용할 수 없게 할 칸을 고르세요" : "카드를 놓을 칸을 고르세요");
        }

        void Add(CardLocation loc, string label, byte[] response)
        {
            var key = new CardKey(loc);
            if (!Actions.TryGetValue(key, out var list))
                Actions[key] = list = new List<CardAction>();
            list.Add(new CardAction { Label = label, Response = response });
        }

        /// <summary>발동할 수 있는 효과. 한 카드에 효과가 여럿이면 효과 문구를 붙여 구분한다.</summary>
        void AddEffects(List<EffectOption> effects, DuelText text, Func<int, byte[]> response)
        {
            var perCard = effects.GroupBy(e => new CardKey(e.Location)).ToDictionary(g => g.Key, g => g.Count());
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                string desc = text.Description(e.Description);
                bool showDesc = perCard[new CardKey(e.Location)] > 1 && !string.IsNullOrEmpty(desc);
                Add(e.Location, showDesc ? $"발동: {desc}" : "발동", response(i));
            }
        }

        static DialogSpec Toggles(string title, int count, ulong available, Func<ulong, string> name, Func<ulong, byte[]> build)
        {
            var d = new DialogSpec { Title = title, Toggles = new List<(string, ulong)>(), ToggleCount = count, BuildToggles = build };
            for (int bit = 0; bit < 64; bit++)
            {
                ulong b = 1UL << bit;
                if ((available & b) != 0)
                    d.Toggles.Add((name(b), b));
            }
            return d;
        }

        static string HintTitle(DuelRequest r, DuelText text, string fallback) =>
            r.SelectHint != 0 ? text.Description(r.SelectHint) ?? fallback : fallback;

        static string CountSuffix(int min, int max) =>
            min == max ? (min > 1 ? $" ({min}장)" : "") : $" ({min}~{max}장)";
    }
}

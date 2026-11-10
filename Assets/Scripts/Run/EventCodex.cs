// 이벤트 도감 (규칙 결정 문서 "이벤트 도감"): 만난 이벤트와 골라 본 선택지를 런을 넘어 모은다.
// - 만난 이벤트는 도감에 나온다. 골라 본 선택지는 다음에 만날 때 결과가 보인다.
// - "지나간다"처럼 결과가 없는 선택지와 강제 선택지를 뺀 선택지(TowerEvent.CodexChoices)를 모두 골라 보면 등장 조건과 성향이 보인다.
// 저장 파일에는 프로필 줄 "event-seen Id 골라 본 선택지 번호…"로 적는다. 모르는 Id(지워진 이벤트)도 버리지 않고 그대로 둔다.
// 이 파일은 듀얼 엔진·Unity를 모른다.
using System.Collections.Generic;
using System.Linq;

namespace DuelRoguelike.Run
{
    public sealed class EventCodex
    {
        readonly SortedDictionary<string, SortedSet<int>> _seen = new SortedDictionary<string, SortedSet<int>>(System.StringComparer.Ordinal);

        /// <summary>만난 이벤트 Id (저장 파일 순서).</summary>
        public IEnumerable<string> SeenIds => _seen.Keys;

        public int SeenCount => _seen.Count;

        public bool Seen(string id) => id != null && _seen.ContainsKey(id);

        /// <summary>이 이벤트의 이 선택지를 골라 본 적이 있는지.</summary>
        public bool Chosen(string id, int index) => id != null && _seen.TryGetValue(id, out var chosen) && chosen.Contains(index);

        public IEnumerable<int> ChosenOf(string id) => id != null && _seen.TryGetValue(id, out var chosen) ? chosen : Enumerable.Empty<int>();

        /// <summary>도감에 쓰는 선택지를 모두 골라 봐서 등장 조건과 성향이 보이는지.</summary>
        public bool IsComplete(TowerEvent ev) => ev != null && Seen(ev.Id) && ev.CodexChoices.All(i => Chosen(ev.Id, i));

        public void MarkSeen(string id)
        {
            if (!string.IsNullOrEmpty(id) && !_seen.ContainsKey(id))
                _seen[id] = new SortedSet<int>();
        }

        public void MarkChosen(string id, int index)
        {
            if (string.IsNullOrEmpty(id) || index < 0)
                return;
            MarkSeen(id);
            _seen[id].Add(index);
        }

        /// <summary>런의 기록에서 만난 이벤트(FloorEntered)와 고른 선택지(EventResolved)를 더한다. 여러 번 불러도 같다.</summary>
        public void Absorb(RunState run)
        {
            if (run == null)
                return;
            foreach (var e in run.Events)
            {
                if (e.Kind == RunEventKind.FloorEntered && e.EventId != null)
                    MarkSeen(e.EventId);
                else if (e.Kind == RunEventKind.EventResolved)
                    MarkChosen(e.EventId, e.Count);
            }
        }

        internal void Write(SaveWriter w)
        {
            if (_seen.Count == 0)
                return;
            w.Comment($"이벤트 도감 {_seen.Count}개: event-seen 이벤트Id 골라 본 선택지 번호(0부터)");
            foreach (var pair in _seen)
                w.Line("event-seen", new object[] { pair.Key }.Concat(pair.Value.Cast<object>()));
        }

        internal void Read(SaveLine line, SaveReader r)
        {
            if (line.Values.Length < 1)
                throw r.Error(line, "이벤트 Id가 없음");
            string id = line.Values[0];
            MarkSeen(id);
            for (int i = 1; i < line.Values.Length; i++)
                MarkChosen(id, r.Int(line, line.Values[i], 0));
        }
    }
}

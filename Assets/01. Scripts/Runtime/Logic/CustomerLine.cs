using System.Collections.Generic;

namespace CardShop.Logic
{
    /// <summary>
    /// 계산대 줄의 칸 순서 (순수 C#, CustomerLineTests). 움직임 · 대사는 CustomerDirector가 맡습니다.
    /// 규칙 (11-18 기기 테스트에서 손님이 겹쳐 선 뒤로 정함):
    /// - 결제를 마치고 인사하거나 화내는 손님도 <see cref="Leave"/>로 걸어 나가기 시작할 때까지는 자기 칸을 계속 차지합니다.
    ///   그래서 다음 손님이 앞사람과 겹쳐 서지 않습니다.
    /// - 계산은 맨 앞 칸(0번) 손님만 시작합니다.
    /// - 새 손님은 언제나 맨 끝 칸에 섭니다 (줄이 당겨지는 중에도).
    /// </summary>
    public sealed class CustomerLine<T> where T : class
    {
        readonly List<T> line = new List<T>();

        public int Count => line.Count;
        /// <summary>앞에서부터. 칸 번호 = 목록 위치.</summary>
        public IReadOnlyList<T> Members => line;

        /// <summary>맨 끝에 서고 그 칸 번호를 돌려줍니다.</summary>
        public int Join(T customer)
        {
            line.Add(customer);
            return line.Count - 1;
        }

        /// <summary>지금 칸 번호. 줄에 없으면 -1.</summary>
        public int SlotOf(T customer) => line.IndexOf(customer);

        /// <summary>맨 앞 칸인가 (계산을 시작할 수 있는가).</summary>
        public bool IsFront(T customer) => line.Count > 0 && ReferenceEquals(line[0], customer);

        /// <summary>줄에서 빠집니다(뒤 손님들의 칸 번호가 하나씩 당겨짐). 빠지기 전 칸 번호를 돌려주고, 줄에 없었으면 -1.</summary>
        public int Leave(T customer)
        {
            int index = line.IndexOf(customer);
            if (index >= 0) line.RemoveAt(index);
            return index;
        }

        public void Clear() => line.Clear();
    }
}

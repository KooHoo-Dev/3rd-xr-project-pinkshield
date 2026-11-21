using System;
using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>한 분야의 택배 상태 (DEC-031 Q3 → DEC-040 U5: 사면 그날 밤 초인종과 함께 택배 → 잡아서 열면 바뀜, 효과는 다음 날 영업부터).</summary>
    public enum UpgradeStatus
    {
        /// <summary>주문한 것 없음</summary>
        None = 0,
        /// <summary>(DEC-040 U5 뒤로는 쓰지 않음 — 주문하면 바로 Arrived) 예전: 오늘 샀음 → 다음 날 아침 도착</summary>
        Ordered = 1,
        /// <summary>방에 택배 상자가 와 있음 (열면 단계가 오름)</summary>
        Arrived = 2,
    }

    /// <summary>
    /// 방 업그레이드 진행 (GameSession.Upgrades, 순수 C#). 가격 · 효과는 모르고 단계와 택배 상태, 쓴 돈만 들고 있습니다(UpgradeRules가 판단).
    /// - 단계(효과 · 가구 모양)는 상자를 연 뒤에 오릅니다. 상자를 안 열고 하루가 지나면 아침에 저절로 열린 것으로 칩니다(효과를 잃지 않게).
    /// - 쓴 돈(Spent)은 최종 점수의 "가구 값" (DEC-031 Q2: 산 가격 100%를 자산으로).
    /// </summary>
    public sealed class UpgradeState
    {
        readonly Dictionary<UpgradeKind, int> levels = new Dictionary<UpgradeKind, int>();
        readonly Dictionary<UpgradeKind, UpgradeStatus> status = new Dictionary<UpgradeKind, UpgradeStatus>();

        /// <summary>업그레이드에 쓴 돈 합계 (= 최종 점수의 가구 값)</summary>
        public int Spent { get; private set; }

        /// <summary>단계 · 택배 상태가 바뀔 때 (방 가구 · 택배 상자 · 상점 화면이 다시 그리는 신호)</summary>
        public event Action Changed;

        /// <summary>지금 단계 (처음 = 1)</summary>
        public int LevelOf(UpgradeKind kind) => levels.TryGetValue(kind, out int l) ? l : 1;

        public UpgradeStatus StatusOf(UpgradeKind kind) => status.TryGetValue(kind, out UpgradeStatus s) ? s : UpgradeStatus.None;

        /// <summary>방에 와 있는(아직 안 연) 택배 상자들</summary>
        public List<UpgradeKind> Arrived()
        {
            var list = new List<UpgradeKind>();
            foreach (KeyValuePair<UpgradeKind, UpgradeStatus> e in status)
                if (e.Value == UpgradeStatus.Arrived) list.Add(e.Key);
            list.Sort();
            return list;
        }

        /// <summary>주문 기록 (돈은 UpgradeRules가 지갑에서 뺌). 그날 밤 바로 택배가 옴 (DEC-040 U5, 초인종 연출은 RoomUpgradeView)</summary>
        internal void Order(UpgradeKind kind, int price)
        {
            status[kind] = UpgradeStatus.Arrived;
            Spent += price;
            Changed?.Invoke();
        }

        /// <summary>택배 상자를 엶 → 단계 +1. 와 있는 상자가 없으면 false.</summary>
        public bool Open(UpgradeKind kind)
        {
            if (StatusOf(kind) != UpgradeStatus.Arrived) return false;
            status[kind] = UpgradeStatus.None;
            levels[kind] = LevelOf(kind) + 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 다음 날 아침: 어제 안 연 상자는 저절로 열고, (예전 형식의) 주문 중인 것은 도착. 저절로 열린 분야를 돌려줍니다.
        /// GameSession.AdvanceDay가 부릅니다.
        /// </summary>
        public List<UpgradeKind> AdvanceDay()
        {
            List<UpgradeKind> autoOpened = Arrived();
            foreach (UpgradeKind k in autoOpened)
            {
                status[k] = UpgradeStatus.None;
                levels[k] = LevelOf(k) + 1;
            }
            var ordered = new List<UpgradeKind>();
            foreach (KeyValuePair<UpgradeKind, UpgradeStatus> e in status)
                if (e.Value == UpgradeStatus.Ordered) ordered.Add(e.Key);
            foreach (UpgradeKind k in ordered) status[k] = UpgradeStatus.Arrived;
            if (autoOpened.Count > 0 || ordered.Count > 0) Changed?.Invoke();
            return autoOpened;
        }
    }
}

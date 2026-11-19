using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardShop.Cards;
using CardShop.Logic;

namespace CardShop.Presentation
{
    /// <summary>
    /// 공개 대기열의 한 칸 = 카드 한 장 (시연 피드백 B3). 갓팩도 일반 팩과 같이 한 장씩입니다 (DEC-004).
    /// </summary>
    public readonly struct RevealItem : IEquatable<RevealItem>
    {
        /// <summary>몇 번째 팩인지 (0부터)</summary>
        public readonly int Pack;
        /// <summary>팩 안에서 몇 번째 카드인지 (0부터)</summary>
        public readonly int Card;

        public RevealItem(int pack, int card)
        {
            Pack = pack;
            Card = card;
        }

        public bool Equals(RevealItem other) => Pack == other.Pack && Card == other.Card;
        public override bool Equals(object obj) => obj is RevealItem o && Equals(o);
        public override int GetHashCode() => Pack * 397 ^ Card;
        public override string ToString() => $"({Pack},{Card})";
    }

    /// <summary>
    /// 상자·단일 팩 공통 "공개 대기열" 규칙 (시연 피드백 B3). 순수 계산이라 EditMode 테스트로 고정합니다 (BoxRevealTests).
    /// 파이프라인: [개봉 연출] → [공개 대기열] → [결과판].
    /// - 모든 카드를 한 장씩 (갓팩 포함). 1.5초 꾹 누르면 남은 카드를 전 등급 건너뜁니다 (DEC-004). 단 갓팩은 건너뛸 수 없음 (DEC-008).
    /// 판정은 개봉 시점에 끝나 있으므로 여기서 무엇을 빼도 데이터(보유 목록)는 바뀌지 않습니다.
    /// </summary>
    /// <summary>상자 10팩 뜯기 장면의 팩별 연출 단계 (FIX D2 · DEC-029). 값이 클수록 좋은 팩</summary>
    public enum BoxPackTier { Plain = 0, Gold = 1, Rainbow = 2, God = 3 }

    public static class RevealQueue
    {
        public static bool IsGodPack(PackResult pack) => pack != null && pack.Special == SpecialPackType.GodPack;

        /// <summary>팩의 최고 등급으로 정한 뜯기 연출: 레어 이하 없음 · 수퍼레어 · 유니크 금빛 · 레전더리 무지개 · 갓팩</summary>
        public static BoxPackTier TierOf(PackResult pack) =>
            IsGodPack(pack) ? BoxPackTier.God
            : pack.BestRarity >= CardRarity.Legendary ? BoxPackTier.Rainbow
            : pack.BestRarity >= CardRarity.SuperRare ? BoxPackTier.Gold
            : BoxPackTier.Plain;

        /// <summary>상자 팩 순서: 좋은 팩일수록 뒤로 (같은 단계는 원래 순서, G19 "좋은 건 마지막")</summary>
        public static List<PackResult> GoodLast(IReadOnlyList<PackResult> packs) => packs.OrderBy(TierOf).ToList();

        /// <summary>
        /// 꾹 눌러 건너뛰어도 남기는 카드: 갓팩 카드(DEC-008), 상자면 수퍼레어 이상도(DEC-029). 순서 유지.
        /// </summary>
        public static List<RevealItem> KeptOnSkip(IReadOnlyList<PackResult> packs, IReadOnlyList<RevealItem> queue, int from, bool isBox)
        {
            var kept = new List<RevealItem>();
            for (int i = Math.Max(0, from); i < queue.Count; i++)
                if (Keeps(packs, queue[i], isBox)) kept.Add(queue[i]);
            return kept;
        }

        public static bool Keeps(IReadOnlyList<PackResult> packs, RevealItem item, bool isBox)
        {
            PackResult p = packs[item.Pack];
            return IsGodPack(p) || (isBox && p.Cards[item.Card].rarity >= CardRarity.SuperRare);
        }

        /// <summary>기본 대기열: 모든 카드를 팩 순서 · 카드 순서대로.</summary>
        public static List<RevealItem> BuildAll(IReadOnlyList<PackResult> packs)
        {
            var queue = new List<RevealItem>();
            for (int p = 0; p < packs.Count; p++)
                for (int c = 0; c < packs[p].Cards.Length; c++) queue.Add(new RevealItem(p, c));
            return queue;
        }

        /// <summary>등급 기호: 노말·레어 ●, 수퍼레어 ◆, 유니크·레전더리 ★</summary>
        public static string Symbol(CardRarity rarity) => rarity switch
        {
            CardRarity.SuperRare => "◆",
            CardRarity.Unique or CardRarity.Legendary => "★",
            _ => "●",
        };

        /// <summary>카드 한 장당 기호 하나, 공백으로 구분. colorize가 등급별 색(RRGGBB, null이면 색 없음)을 줍니다.</summary>
        public static string Dots(PackResult pack, Func<CardRarity, string> colorize = null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pack.Cards.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                CardRarity r = pack.Cards[i].rarity;
                string hex = colorize?.Invoke(r);
                string symbol = Symbol(r);
                sb.Append(string.IsNullOrEmpty(hex) ? symbol : $"<color=#{hex}>{symbol}</color>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 결과판의 팩 한 줄: <c>팩 3  ● ● ● ◆ ★  새 2</c>. 새 카드가 없으면 "새" 칸은 생략합니다.
        /// </summary>
        /// <param name="packNumber">1부터 세는 팩 번호</param>
        /// <param name="newColorHex">"새 N" 글자 색 (RRGGBB). null이면 색 없음</param>
        public static string DotLine(PackResult pack, int packNumber, Func<CardRarity, string> colorize = null, string newColorHex = null)
        {
            int newCount = pack.IsNew != null ? pack.IsNew.Count(n => n) : 0;
            var sb = new StringBuilder();
            sb.Append("팩 ").Append(packNumber).Append("  ").Append(Dots(pack, colorize));
            if (newCount > 0)
            {
                string label = $"새 {newCount}";
                sb.Append("  ").Append(string.IsNullOrEmpty(newColorHex) ? label : $"<color=#{newColorHex}>{label}</color>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 결과판 아래에 펼칠 하이라이트 카드 (팩 번호, 카드 번호): 높은 등급 → 새 카드 순.
        /// 같은 조건이면 팩·카드 순서(정렬이 안정적이라 항상 같은 결과).
        /// </summary>
        public static List<(int pack, int card)> PickHighlights(IReadOnlyList<PackResult> packs, int count)
        {
            var all = new List<(int pack, int card)>();
            for (int p = 0; p < packs.Count; p++)
                for (int c = 0; c < packs[p].Cards.Length; c++)
                    all.Add((p, c));

            CardRarity R((int pack, int card) x) => packs[x.pack].Cards[x.card].rarity;
            bool N((int pack, int card) x) => packs[x.pack].IsNew != null && packs[x.pack].IsNew[x.card];
            return all.OrderByDescending(R).ThenByDescending(N).Take(Math.Max(0, count)).ToList();
        }
    }
}

using CardShop.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardShop.Cards;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 공개 연출에 띄우는 글(상자 결과판 · 테이블 더미 정보 · 결과함)의 문구와 색.
    /// 연출(CardRevealer)과 나눠 둔 순수 문자열 함수라 EditMode 테스트로 확인할 수 있습니다.
    /// </summary>
    public static class RevealText
    {
        public const string GoldHex = "FFD54A";
        public const string NewHex = "7CF29A";
        const string HintHex = "C9C3D9";

        /// <summary>등급 이름을 등급 색으로 (결과판 · 결과함 공용). 스타일이 없으면 enum 이름.</summary>
        public static string RarityTag(CardRarityStyles styles, CardRarity r)
        {
            if (styles == null) return r.ToString();
            CardRarityStyles.Style s = styles.Get(r);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(s.accentColor)}>{s.label}</color>";
        }

        /// <summary>5점 줄 기호 색 (RRGGBB). 수퍼레어는 결과판에서 눈에 띄도록 금빛 고정. 스타일이 없으면 null(색 없음).</summary>
        public static string RarityHex(CardRarityStyles styles, CardRarity r)
        {
            if (r == CardRarity.SuperRare) return GoldHex;
            return styles == null ? null : ColorUtility.ToHtmlStringRGB(styles.Get(r).accentColor);
        }

        public static string SpecialLabel(SpecialPackType t) => t switch
        {
            SpecialPackType.GodPack => "갓팩",
            SpecialPackType.LegendaryPack => "레전더리 확정",
            _ => t.ToString(),
        };

        /// <summary>배경판 없이도 읽히도록 글자 뒤에 어두운 띠를 깝니다.</summary>
        public static string Mark(string text) => $"<mark={UiTheme.Mark}>{text}</mark>";

        /// <summary>상자 결과판의 팩 한 줄 (끝에 줄바꿈 포함).</summary>
        public static string BoxRow(PackResult pack, int packNumber, CardRarityStyles styles)
        {
            var sb = new StringBuilder(RevealQueue.DotLine(pack, packNumber, r => RarityHex(styles, r), NewHex));
            if (pack.Special != SpecialPackType.None) sb.Append($"  <color=#{GoldHex}>{SpecialLabel(pack.Special)}!</color>");
            return sb.Append('\n').ToString();
        }

        /// <summary>팩 줄을 하나씩 채우는 동안의 결과판.</summary>
        public static string BoxProgress(string rows, int shown, int total) => Mark($"<b>상자 개봉 {shown}/{total}</b>\n{rows}");

        /// <summary>다 채운 결과판: 팩 줄 + 합계 + 기호 설명.</summary>
        public static string BoxSummary(IReadOnlyList<PackResult> packs, string rows, CardRarityStyles styles)
        {
            int total = packs.Sum(p => p.Cards.Length);
            int newCount = packs.Sum(p => p.IsNew.Count(n => n));
            CardRarity top = packs.Max(p => p.BestRarity);
            int superRares = packs.Sum(p => p.Cards.Count(c => c.rarity == CardRarity.SuperRare));
            string legend = $"<size=70%><color=#{HintHex}>● 노말·레어  <color=#{GoldHex}>◆</color> 수퍼레어  ★ 유니크 이상</color></size>";
            return Mark($"<b>상자 개봉 완료 {packs.Count}/{packs.Count}</b>\n{rows}" +
                        $"<b>합계 {total}장 · 새 카드 {newCount}장 · 최고 {RarityTag(styles, top)}</b>" +
                        (superRares > 0 ? $"  <color=#{GoldHex}>수퍼레어 {superRares}장</color>" : "") +
                        $"\n{legend}");
        }

        /// <summary>테이블 카드 더미 정보: 장수 · 새 카드 · 등급별 수 · 집은 카드 · 수퍼레어 이상 목록(최대 6줄).</summary>
        public static string PileInfo(IReadOnlyList<CardData> cards, int newCount, CardData held, bool heldIsNew, CardRarityStyles styles)
        {
            var sb = new StringBuilder();
            sb.Append($"<b>테이블 카드 더미 {cards.Count}장</b>");
            if (newCount > 0) sb.Append($"  <color=#{NewHex}>새 카드 {newCount}장</color>");
            sb.Append('\n');

            var counts = new List<string>();
            for (CardRarity r = CardRarity.Legendary; r >= CardRarity.Normal; r--)
            {
                int n = cards.Count(c => c.rarity == r);
                if (n > 0) counts.Add($"{RarityTag(styles, r)} {n}");
            }
            sb.Append(string.Join("  ", counts)).Append('\n');

            if (held != null)
                sb.Append($"집은 카드  {RarityTag(styles, held.rarity)}  {held.displayName}{(heldIsNew ? $"  <color=#{NewHex}><b>NEW</b></color>" : "")}\n");

            foreach (var g in cards.Where(c => c.rarity >= CardRarity.SuperRare)
                         .GroupBy(c => c.id)
                         .OrderByDescending(g => g.First().rarity).ThenBy(g => g.First().displayName).Take(6))
                sb.Append($"{RarityTag(styles, g.First().rarity)}  {g.First().displayName}{(g.Count() > 1 ? $" ×{g.Count()}" : "")}\n");

            sb.Append($"<size=70%><color=#{HintHex}>퇴근하면 보관함으로 옮겨져요</color></size>");
            return sb.ToString();
        }
    }
}

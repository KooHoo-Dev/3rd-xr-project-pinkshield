using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CardShop.Cards;
using CardShop.Logic;
using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 결과판 (시연 피드백 B3): 팩마다 5점 줄을 하나씩 채운 뒤 합계, 그리고 카운터 위 한 줄에 하이라이트 카드.
    /// 단일 팩 결과함과 같은 어두운 띠 · 등급 색을 씁니다(문구는 <see cref="RevealText"/>, 순수 함수 · 테스트됨).
    /// 결과판 · 하이라이트는 다음 상자 · 영업 시작 때 CardRevealer가 정리합니다.
    /// CardRevealer(개봉 진행)가 부르고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class BoxResultBoard
    {
        /// <summary>인스펙터에서 조정하는 값 (CardRevealer 인스펙터에 이 묶음으로 보임).</summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("결과판에 팩 한 줄이 추가되는 간격 (초)")]
            public float RowInterval = 0.12f; // 11-21 2차 H3: 0.25 → 0.12 (오래 기다림)
            [Tooltip("카운터 위에 놓는 하이라이트 카드 수")]
            public int HighlightCount = 5;
            [Tooltip("하이라이트 카드 사이 간격 (m)")]
            public float Spacing = 0.095f;
            [Tooltip("하이라이트 카드 크기 배율 (카운터 더미 카드와 같게)")]
            public float CardScale = 1.3f;
            [Tooltip("하이라이트 카드가 한 장씩 놓이는 간격 (초). 낮은 등급부터, 가장 좋은 카드가 마지막 (11-19 피드백: 등급 연출을 단일 팩과 같게)")]
            public float HighlightInterval = 0.4f;
        }

        readonly Transform parent;
        readonly Transform anchor;       // 카운터 기준점 (카드 앞면 -Z가 플레이어 쪽)
        readonly CardRevealPool pool;
        readonly Func<TMP_FontAsset> font;
        readonly Func<CardRarityStyles> styles;
        readonly Settings settings;
        TextMeshPro board;
        TextPlate plate;

        public BoxResultBoard(Transform parent, Transform anchor, CardRevealPool pool,
            Func<TMP_FontAsset> font, Func<CardRarityStyles> styles, Settings settings)
        {
            this.parent = parent;
            this.anchor = anchor;
            this.pool = pool;
            this.font = font;
            this.styles = styles;
            this.settings = settings;
        }

        /// <summary>팩 줄을 하나씩 채우고(코루틴) 합계 · 하이라이트 카드를 놓습니다.</summary>
        /// <param name="cardShown">하이라이트 카드 한 장이 놓일 때 (등급 연출: 폭죽 · 흔들림 · 진동은 단일 팩과 같은 RevealFx)</param>
        /// <param name="hurry">이 프레임에 서두르라는 입력이 있는가 (트리거). 한 번 오면 남은 줄 · 카드를 기다림 없이 (11-21 2차 H3)</param>
        public IEnumerator Show(IReadOnlyList<PackResult> packs, Action<CardRevealView, CardData> cardShown = null, Func<bool> hurry = null)
        {
            Settings s = settings;
            hurried = false;
            this.hurry = hurry;
            CardRarityStyles st = styles();
            TextMeshPro text = Board();
            text.gameObject.SetActive(true);

            var rows = new StringBuilder();
            for (int i = 0; i < packs.Count; i++)
            {
                rows.Append(RevealText.BoxRow(packs[i], i + 1, st));
                SetText(text, RevealText.BoxProgress(rows.ToString(), i + 1, packs.Count), i + 1, packs.Count);
                yield return Wait(s.RowInterval);
            }
            SetText(text, RevealText.BoxSummary(packs, rows.ToString(), st), packs.Count, packs.Count);

            // 가장 좋은 카드가 맨 앞(0번)이므로 뒤에서부터 놓아 마지막에 터지게 (폭죽은 하나라 앞 카드 폭죽은 다음 카드가 이어받음)
            List<(int pack, int card)> best = RevealQueue.PickHighlights(packs, s.HighlightCount);
            for (int i = best.Count - 1; i >= 0; i--)
            {
                PackResult p = packs[best[i].pack];
                CardData card = p.Cards[best[i].card];
                CardRevealView v = pool.Show(card, p.IsNew[best[i].card], SlotPosition(i, best.Count, s.Spacing), anchor.rotation, s.CardScale);
                cardShown?.Invoke(v, card);
                if (i > 0) yield return Wait(s.HighlightInterval);
            }
        }

        Func<bool> hurry;
        bool hurried;

        // 기다리되, 트리거가 한 번 눌리면 그 뒤로는 기다리지 않음
        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !hurried; t += Time.deltaTime)
            {
                if (hurry != null && hurry()) hurried = true;
                else yield return null;
            }
        }

        /// <summary>결과판 글을 숨김 (하이라이트 카드는 풀 정리와 함께 거둬짐).</summary>
        public void Hide()
        {
            if (board != null) board.gameObject.SetActive(false);
        }

        // 글을 바꾸고 판 크기를 맞춤. RevealText의 어두운 띠(<mark>)는 카드 재질 판이 대신하므로 벗기고,
        // 제목 줄 아래에 "10팩 중 몇 번째" 칸 줄을 끼움 (지금 연 팩까지 금색)
        void SetText(TextMeshPro text, string markup, int opened, int total)
        {
            markup = markup.Replace($"<mark={UiTheme.Mark}>", "").Replace("</mark>", "");
            int nl = markup.IndexOf('\n');
            string counter = Counter(opened, total);
            text.text = nl < 0 ? markup + "\n" + counter : markup.Substring(0, nl + 1) + counter + markup.Substring(nl);
            if (plate != null) plate.Fit();
        }

        /// <summary>
        /// 분홍 막대 칸 10개 + 「3 / 10 팩」 (11-20 샘플 5번). 칸은 글꼴 기호(■□) 대신 전각 공백에 &lt;mark&gt; 색을 칠해 그림 —
        /// 기호 글자가 글꼴에 없어 네모로 깨지는 일이 없음.
        /// </summary>
        static string Counter(int opened, int total)
        {
            int n = Mathf.Min(opened, total);
            var sb = new StringBuilder();
            for (int i = 0; i < total; i++)
                sb.Append(i < n ? "<mark=#FF8CBFFF>　</mark> " : "<mark=#4A3873FF>　</mark> "); // 　 = 전각 공백
            string gold = ColorUtility.ToHtmlStringRGB(UiTheme.Gold);
            return sb.Append($" <color=#{gold}>{n} / {total} 팩</color>").ToString();
        }

        // 카운터 위 가로 한 줄의 index번째 자리
        Vector3 SlotPosition(int index, int count, float spacing) =>
            anchor.position + anchor.right * ((index - (count - 1) * 0.5f) * spacing);

        // 결과판: 카드 위쪽에 카드와 같은 방향으로. 한글 글꼴은 카드 프리팹의 것을 빌려 씀
        TextMeshPro Board()
        {
            if (board != null) return board;
            var go = new GameObject("BoxResultBoard", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(anchor.position + anchor.up * 0.09f, anchor.rotation);
            board = go.AddComponent<TextMeshPro>();
            TMP_FontAsset f = font();
            if (f != null) board.font = f;
            board.fontSize = 0.22f;
            board.alignment = TextAlignmentOptions.BottomLeft;
            board.color = Color.white;
            board.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0.3f, 0.3f);
            // 팩 결과판과 같은 카드 재질: 진보라 판 + 금박 테두리 (UI_개편_설계 C안)
            plate = TextPlate.Attach(board, UiTheme.CardBack, new Vector2(0.022f, 0.016f), 0.014f, UiTheme.Gold, 0.003f);
            return board;
        }
    }
}

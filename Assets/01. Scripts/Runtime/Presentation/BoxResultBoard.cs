using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CardShop.Cards;
using CardShop.Logic;
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
            public float RowInterval = 0.25f;
            [Tooltip("카운터 위에 놓는 하이라이트 카드 수")]
            public int HighlightCount = 5;
            [Tooltip("하이라이트 카드 사이 간격 (m)")]
            public float Spacing = 0.095f;
            [Tooltip("하이라이트 카드 크기 배율 (카운터 더미 카드와 같게)")]
            public float CardScale = 1.3f;
        }

        readonly Transform parent;
        readonly Transform anchor;       // 카운터 기준점 (카드 앞면 -Z가 플레이어 쪽)
        readonly CardRevealPool pool;
        readonly Func<TMP_FontAsset> font;
        readonly Func<CardRarityStyles> styles;
        readonly Settings settings;
        TextMeshPro board;

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
        public IEnumerator Show(IReadOnlyList<PackResult> packs)
        {
            Settings s = settings;
            CardRarityStyles st = styles();
            TextMeshPro text = Board();
            text.gameObject.SetActive(true);

            var rows = new StringBuilder();
            for (int i = 0; i < packs.Count; i++)
            {
                rows.Append(RevealText.BoxRow(packs[i], i + 1, st));
                text.text = RevealText.BoxProgress(rows.ToString(), i + 1, packs.Count);
                yield return new WaitForSeconds(s.RowInterval);
            }
            text.text = RevealText.BoxSummary(packs, rows.ToString(), st);

            List<(int pack, int card)> best = RevealQueue.PickHighlights(packs, s.HighlightCount);
            for (int i = 0; i < best.Count; i++)
            {
                PackResult p = packs[best[i].pack];
                pool.Show(p.Cards[best[i].card], p.IsNew[best[i].card], SlotPosition(i, best.Count, s.Spacing), anchor.rotation, s.CardScale);
            }
        }

        /// <summary>결과판 글을 숨김 (하이라이트 카드는 풀 정리와 함께 거둬짐).</summary>
        public void Hide()
        {
            if (board != null) board.gameObject.SetActive(false);
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
            return board;
        }
    }
}

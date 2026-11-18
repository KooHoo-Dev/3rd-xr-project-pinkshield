using System;
using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 더미 정보 판: 카운터 더미의 카드를 집으면 테이블에 쌓인 카드 전체(덩어리로 줄인 오래된 카드 포함)의 요약을 옆에 띄우고,
    /// 놓으면 잠시 뒤 숨깁니다. 문구는 <see cref="RevealText.PileInfo"/>(순수 함수 · 테스트됨).
    /// CardRevealer(개봉 진행)가 카드 잡기 · 놓기를 넘겨주고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class PileInfoPopup
    {
        /// <summary>CardRevealer 인스펙터 값 (씬에 저장된 값을 그대로 쓰기 위해 CardRevealer에 둠).</summary>
        public struct Settings
        {
            public float SideOffset;     // 결과함에서 옆으로 떨어지는 거리 (m)
            public float HoldSeconds;    // 카드를 놓은 뒤 사라지기까지 (초)
            public float Height;         // 더미 맨 위에서 판 아래쪽까지 높이 (m)
            public float FontSize;       // TMP 3D 글자 크기 (1 ≈ 10cm)
        }

        readonly MonoBehaviour host; // 잠시 뒤 숨기기(코루틴) · 판 부모
        readonly Transform anchor;
        readonly CounterPileView pile;
        readonly Func<TMP_FontAsset> font;
        readonly Func<CardRarityStyles> styles;
        readonly Func<Settings> settings;
        PackResultPanel panel;
        Coroutine hideRoutine;

        public PileInfoPopup(MonoBehaviour host, Transform anchor, CounterPileView pile,
            Func<TMP_FontAsset> font, Func<CardRarityStyles> styles, Func<Settings> settings)
        {
            this.host = host;
            this.anchor = anchor;
            this.pile = pile;
            this.font = font;
            this.styles = styles;
            this.settings = settings;
        }

        /// <summary>더미의 카드를 잡음: 정보 판을 띄움. 더미 카드가 아니면 무시 (개봉 중인지는 부르는 쪽이 거름).</summary>
        public void OnGrabbed(CardRevealView held)
        {
            if (!pile.Contains(held)) return;
            Settings s = settings();
            StopHide();
            if (panel == null) panel = PackResultPanel.Create(host.transform, font());
            float top = pile.HeightAt(pile.NextIndex);
            Vector3 at = pile.BasePose.position + anchor.right * s.SideOffset + Vector3.up * (top + s.Height);
            panel.ShowText(Text(held), at, anchor.rotation, s.FontSize, 0.15f);
        }

        /// <summary>카드를 놓음: 잠시 뒤 정보 판을 숨김.</summary>
        public void OnReleased()
        {
            if (panel == null || !panel.IsShown || !host.isActiveAndEnabled) return;
            StopHide();
            hideRoutine = host.StartCoroutine(HideAfter(settings().HoldSeconds));
        }

        public void Hide()
        {
            StopHide();
            if (panel != null) panel.Hide();
        }

        IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hideRoutine = null;
            if (panel != null) panel.Hide();
        }

        void StopHide()
        {
            if (hideRoutine != null) { host.StopCoroutine(hideRoutine); hideRoutine = null; }
        }

        string Text(CardRevealView held)
        {
            var cards = new List<CardData>();
            int newCount = pile.CollectCards(cards);
            return RevealText.PileInfo(cards, newCount, held != null ? held.Card : null, held != null && held.IsNew, styles());
        }
    }
}

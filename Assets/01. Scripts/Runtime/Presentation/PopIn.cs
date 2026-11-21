using DG.Tweening;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 가구 등장 · 택배 상자 열기 연출 (11-21 2차 V5, 사용자 요청 DOTween): 작게 → 크게(1.15배) → 원래.
    /// 크기만 바꾸므로 자리 · 콜라이더 · 다른 연출과 부딪히지 않습니다. 같은 물건에 다시 부르면 앞 연출을 끊고 원래 크기에서 다시.
    /// </summary>
    public static class PopIn
    {
        const float GrowSeconds = 0.22f;
        const float SettleSeconds = 0.13f;
        const float Overshoot = 1.15f;
        const float StartScale = 0.05f;

        /// <summary>t를 거의 0에서 1.15배까지 키웠다가 원래 크기로.</summary>
        public static void Play(Transform t)
        {
            if (t == null) return;
            t.DOKill(complete: true); // 앞 연출이 남아 있으면 끝 크기로 맞춘 뒤 시작
            Vector3 size = t.localScale;
            t.localScale = size * StartScale;
            DOTween.Sequence()
                .Append(t.DOScale(size * Overshoot, GrowSeconds).SetEase(Ease.OutBack))
                .Append(t.DOScale(size, SettleSeconds).SetEase(Ease.InOutSine))
                .SetTarget(t)
                .SetLink(t.gameObject); // 물건이 사라지면 연출도 같이 정리
        }

        /// <summary>택배 상자 열기: 살짝 부풀었다가(1.2배) 작아져 사라짐. 끝나면 done.</summary>
        public static void PopOut(Transform t, float seconds, System.Action done)
        {
            if (t == null) { done?.Invoke(); return; }
            t.DOKill();
            Vector3 size = t.localScale;
            DOTween.Sequence()
                .Append(t.DOScale(size * 1.2f, seconds * 0.3f).SetEase(Ease.OutQuad))
                .Append(t.DOScale(Vector3.zero, seconds * 0.7f).SetEase(Ease.InBack))
                .SetTarget(t)
                .SetLink(t.gameObject)
                .OnComplete(() => done?.Invoke());
        }
    }
}

using System.Collections;
using CardShop.Core;
using CardShop.Decor;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 타이틀 첫 등장 연출 (DEC-027 첫 등장 A, 플레이어는 움직이지 않음 → 멀미 없음).
    /// 화면이 밝아지는 동안(DayTransition) 간판 글자가 하나씩 켜지고 → 타이틀 판이 아래에서 떠오릅니다. 다 떠오르기 전에는 버튼이 눌리지 않습니다.
    /// 타이틀 판 캔버스에 붙입니다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TitleIntro : PhaseListener
    {
        [Tooltip("비우면 씬에서 찾음")]
        [SerializeField] ShopFrontSign sign;
        [Tooltip("타이틀이 된 뒤(장면 전환이 끝난 뒤) 첫 글자까지 기다리는 시간 (초)")]
        [SerializeField] float startDelay = 0.5f;
        [SerializeField] float letterSeconds = 0.09f;
        [SerializeField] float riseSeconds = 0.7f;
        [Tooltip("판이 떠오르기 시작하는 깊이 (m, 제자리 아래)")]
        [SerializeField] float riseFrom = 0.35f;

        CanvasGroup group;
        Vector3 home;
        Coroutine routine;
        bool played; // 이번 타이틀에서 이미 시작했는지 (새로 그릴 때마다 다시 하지 않게)

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            home = transform.position;
        }

        ShopFrontSign Sign => sign != null ? sign : sign = FindAnyObjectByType<ShopFrontSign>();

        protected override void Refresh()
        {
            if (CurrentPhase == GamePhaseId.Title)
            {
                if (!played) { played = true; routine = StartCoroutine(Play()); }
                return;
            }
            played = false;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Show(1f);
            if (Sign != null) Sign.SetLit(Sign.Length);
        }

        IEnumerator Play()
        {
            Show(0f);
            if (Sign != null) Sign.SetLit(0);
            while (Flow != null && Flow.IsTransitioning) yield return null;
            yield return new WaitForSecondsRealtime(startDelay);

            if (Sign != null)
                for (int i = 1; i <= Sign.Length; i++)
                {
                    Sign.SetLit(i);
                    yield return new WaitForSecondsRealtime(letterSeconds);
                }

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / riseSeconds)
            {
                Show(Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            Show(1f);
            routine = null;
        }

        // 0 = 아래에 숨음, 1 = 제자리 · 누를 수 있음
        void Show(float k)
        {
            transform.position = home - Vector3.up * (riseFrom * (1f - k));
            group.alpha = k;
            group.interactable = group.blocksRaycasts = k >= 1f;
        }
    }
}

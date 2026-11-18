using System;
using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 장면 전환 연출 (시연 피드백: 퇴근 후 집에서 쉬는 느낌). 화면을 검게 덮고 → 플레이어를 가게 · 집 · 가게 앞 인도(타이틀) 구역으로 옮기고 → 다시 밝힙니다.
    /// 같은 Shop 씬 안의 다른 구역으로 옮기는 방식이라 씬 로딩이 없습니다.
    /// - 이동은 화면이 완전히 어두운 동안에만 일어나 멀미 영향이 없습니다.
    /// - 손에 든 물건이 집까지 따라가지 않도록 이동 전에 모든 잡기를 해제합니다.
    /// - 화면을 덮는 면은 카메라 앞 Screen Space - Camera 캔버스(별도 머티리얼 불필요).
    /// - 글자는 덮개 캔버스에 넣지 않습니다: 카메라 바로 앞(12cm)이라 두 눈에 서로 다른 위치로 보여 겹쳐 보였음(Quest 시연).
    ///   대신 페이드가 끝난 뒤 눈앞 1m에 월드 말풍선으로 띄웁니다.
    /// </summary>
    public sealed class DayTransition : MonoBehaviour, IPhaseTransition
    {
        [SerializeField] GameFlow flow;
        [SerializeField] XROrigin origin;
        [Tooltip("집 구역의 시작 위치·방향 (앞이 +Z 방향)")]
        [SerializeField] Transform homeSpawn;
        [Tooltip("타이틀에서 서는 가게 앞 인도 자리·방향 (앞이 +Z 방향)")]
        [SerializeField] Transform titleSpawn;
        [SerializeField] TMP_FontAsset font;
        [Tooltip("아침 혼잣말 목록(전날 카드팩 결과별). 비워 두면 기본 문장")]
        [SerializeField] MorningLineSet morningLines;
        Dictionary<MorningMood, List<string>> parsedLines;
        [Tooltip("퇴근 혼잣말 목록(오늘 일 · 카드팩 결과별). 비워 두면 기본 문장")]
        [SerializeField] EveningLineSet eveningLines;
        Dictionary<EveningMood, List<string>> parsedEvening;
        [SerializeField] float fadeSeconds = 1.3f;
        [Tooltip("완전히 어두운 채로 머무는 시간. 순간이동처럼 느껴지지 않게 시간이 흐르는 틈을 줌")]
        [SerializeField] float holdSeconds = 0.8f;

        Image veil;
        WorldToast toast;
        Vector3 shopPosition;
        Quaternion shopRotation;

        // 플레이어가 서 있는 구역. 가게(계산대 자리) · 집 · 가게 앞 인도(타이틀)
        enum Zone { Shop, Home, Title }
        Zone zone = Zone.Shop;
        GameContext subscribed;

        void Awake()
        {
            if (flow != null) flow.Transition = this;
            if (origin != null)
            {
                shopPosition = origin.transform.position;
                shopRotation = origin.transform.rotation;
            }
        }

        // 페이드 없이 바뀐 페이즈(앱 시작 때의 타이틀)도 구역을 맞춤. 페이드 중에는 Play가 화면이 가려진 뒤 옮기므로 같은 결과.
        void Start()
        {
            subscribed = GameContext.Instance;
            if (subscribed != null) subscribed.Events.PhaseChanged += OnPhaseChanged;
            if (flow != null && flow.CurrentPhase != GamePhaseId.None) Relocate(flow.CurrentPhase);
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to) => Relocate(to);

        void OnDestroy()
        {
            if (subscribed != null) subscribed.Events.PhaseChanged -= OnPhaseChanged;
            if (flow != null && flow.Transition == (IPhaseTransition)this) flow.Transition = null;
        }

        public IEnumerator Play(Action switchPhase)
        {
            EnsureVeil();
            yield return Fade(1f);

            ReleaseAllGrabs();
            switchPhase(); // 구역 이동은 PhaseChanged를 받은 Relocate가 같은 프레임에 함

            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(0f);

            string text = MessageFor(flow.CurrentPhase);
            if (text != null) ShowMessage(text);
        }

        string MessageFor(GamePhaseId phase)
        {
            GameContext ctx = GameContext.Instance;
            switch (phase)
            {
                case GamePhaseId.Home when ctx != null:
                    // 4차 기기 피드백: 한 문장뿐이던 퇴근 메시지를 오늘 일 · 카드팩 결과에 따라 두 줄로
                    parsedEvening ??= EveningLines.Parse(eveningLines != null ? eveningLines.lines : null);
                    return EveningLines.For(ctx.Session.Day, ctx.Session.Ledger, parsedEvening);
                case GamePhaseId.Home: return "퇴근, 이제 쉴 시간이다.";
                case GamePhaseId.Business when ctx != null && ctx.Session.Previous != null: // 1일차는 '어제'가 없으니 아침 대사 없음
                    parsedLines ??= MorningLines.Parse(morningLines != null ? morningLines.lines : null);
                    return MorningLines.For(ctx.Session.Day, ctx.Campaign.DayCount, ctx.Session.Previous, parsedLines);
                default: return null;
            }
        }

        // 플레이어 눈앞 1m, 월드 고정 말풍선 (머리에 붙이지 않음)
        void ShowMessage(string message)
        {
            Camera cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (cam == null) return;
            if (toast == null)
            {
                toast = WorldToast.Create(transform, font);
                toast.transform.localScale = Vector3.one * 2f; // 1m 거리에서 읽히도록 크게
            }
            toast.Show(message, HeadPose.InFront(cam.transform, 1.0f, 0.05f).position);
        }

        static Zone ZoneOf(GamePhaseId phase) =>
            phase == GamePhaseId.Home ? Zone.Home : phase == GamePhaseId.Title ? Zone.Title : Zone.Shop;

        // 구역이 바뀔 때만 옮깁니다. 가게로 돌아올 때는 처음 계산대 자리 · 방향. 높이(눈높이 보정값)는 그대로 둡니다.
        void Relocate(GamePhaseId phase)
        {
            if (origin == null) return;
            Zone target = ZoneOf(phase);
            if (target == zone) return;
            Transform rig = origin.transform;
            Transform spawn = target == Zone.Home ? homeSpawn : target == Zone.Title ? titleSpawn : null;
            if (target != Zone.Shop && spawn == null) return;

            ReleaseAllGrabs(); // 손에 든 물건이 다른 구역까지 따라가지 않게
            if (spawn != null)
                rig.SetPositionAndRotation(new Vector3(spawn.position.x, rig.position.y, spawn.position.z), Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
            else
                rig.SetPositionAndRotation(new Vector3(shopPosition.x, rig.position.y, shopPosition.z), shopRotation);
            zone = target;
        }

        static void ReleaseAllGrabs()
        {
            foreach (XRBaseInputInteractor hand in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                if (hand.hasSelection && hand.interactionManager != null)
                    hand.interactionManager.CancelInteractorSelection((IXRSelectInteractor)hand);
        }

        IEnumerator Fade(float target)
        {
            float start = veil.color.a;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeSeconds)
            {
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetAlpha(target);
        }

        void SetAlpha(float a)
        {
            Color c = veil.color;
            c.a = a;
            veil.color = c;
            veil.gameObject.SetActive(a > 0.001f);
        }

        void EnsureVeil()
        {
            if (veil != null) return;
            Camera cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;

            var canvasGo = new GameObject("FadeCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.12f;
            canvas.sortingOrder = short.MaxValue;

            var veilGo = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veilGo.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)veilGo.transform);
            veil = veilGo.GetComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0f);
            veil.raycastTarget = false;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}

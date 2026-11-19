using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.UI
{
    /// <summary>
    /// 왼손 손목 보유 재화 (11-19 플레이 피드백 F: 게임 중 내 돈을 볼 방법이 없음).
    /// 시계를 보듯 왼손 손목 안쪽을 얼굴 쪽으로 돌리면 "보유 재화"가 떠오르고, 손을 내리면 사라집니다.
    /// 늘 떠 있지 않아 시야를 가리지 않고, 메뉴를 열 필요도 없습니다.
    ///
    /// 왼손 컨트롤러 자식에 작은 TMP 글자를 붙이고, 글자 면이 머리를 향하는 정도(내적)와 거리로 보일지 정합니다.
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 XR Origin의 "Left Controller"에 스스로 붙습니다.
    /// 타이틀 · 최종 결과 화면에서는 숨깁니다.
    /// FIX B2: 이름 "내 자산", 달러 표시, 글자 0.12 → 0.2. 돈이 바뀌면 왼손이 짧게 떨리고 +$40 같은 변화가 잠깐 붙어 "손목을 봐"라는 신호가 됩니다.
    /// </summary>
    public sealed class WristWallet : MonoBehaviour
    {
        [Tooltip("손목 안쪽 자리 (왼손 컨트롤러 기준, m). +X = 몸 안쪽(엄지 쪽), −Z = 손목 쪽")]
        [SerializeField] Vector3 localOffset = new Vector3(0.035f, 0.005f, -0.075f);
        [Tooltip("글자 면이 머리를 이만큼 이상 향해야 보임 (내적 0~1)")]
        [SerializeField, Range(0f, 1f)] float faceThreshold = 0.55f;
        [Tooltip("머리에서 이 거리 안이어야 보임 (m)")]
        [SerializeField] float maxDistance = 0.75f;
        [SerializeField] float fadeSpeed = 6f;

        TextMeshPro text;
        Transform panel;
        Camera cam;
        GameContext context;
        float alpha;
        int shownMoney = int.MinValue;
        GameSession shownSession;
        string change;          // 방금 바뀐 만큼 (+$40). 잠깐만 보임
        float changeUntil, popAt = -1f;
        XRBaseInputInteractor hand;

        const float ChangeSeconds = 2.5f;
        const float PopSeconds = 0.25f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig == null) return;
            Transform left = FindChild(rig.transform, "Left Controller");
            if (left != null && left.GetComponent<WristWallet>() == null) left.gameObject.AddComponent<WristWallet>();
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        void Start()
        {
            var go = new GameObject("WristWallet", typeof(RectTransform));
            panel = go.transform;
            panel.SetParent(transform, false);
            panel.localPosition = localOffset;
            // TMP 글자는 −Z 쪽에서 읽히므로 forward를 −X로: 컨트롤러 +X(손목 안쪽) 쪽에서 읽힘
            panel.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            text = go.AddComponent<TextMeshPro>();
            CardRevealer revealer = FindAnyObjectByType<CardRevealer>(); // 한글 글꼴은 카드 프리팹 것을 빌림
            if (revealer != null && revealer.Font != null) text.font = revealer.Font;
            text.fontSize = 0.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)panel).sizeDelta = new Vector2(0.15f, 0.07f);
            text.alpha = 0f;
            hand = GetComponentInChildren<XRBaseInputInteractor>();
        }

        void LateUpdate()
        {
            if (text == null) return;
            if (cam == null) cam = Camera.main;
            if (context == null) context = GameContext.Instance;

            Watch();
            float pop = popAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - popAt) / PopSeconds);
            panel.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, pop); // 돈이 바뀌면 툭 커졌다 돌아옴

            bool want = Visible();
            if (want && !wasVisible) context?.Events.RaiseHappened(GameMoment.WalletViewed); // 튜토리얼 "손목 보기" (FIX C4)
            wasVisible = want;
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            text.alpha = alpha;
            text.enabled = alpha > 0.001f;
        }

        // 돈이 바뀌었나: 글자를 다시 쓰고, 같은 게임 안의 변화면 진동 + 변화량 표시 (새 게임 시작은 알리지 않음)
        void Watch()
        {
            GameSession session = context != null ? context.Session : null;
            int money = session != null ? session.Wallet.Money : 0;
            bool expired = change != null && Time.unscaledTime >= changeUntil;
            if (money == shownMoney && session == shownSession && !expired) return;

            if (expired) change = null;
            if (session != null && session == shownSession && money != shownMoney)
            {
                change = MoneyText.Signed(money - shownMoney);
                changeUntil = Time.unscaledTime + ChangeSeconds;
                popAt = Time.unscaledTime;
                if (hand != null) hand.SendHapticImpulse(0.7f, 0.18f); // 11-20 F7: 0.35 · 0.08초는 못 느낌
            }
            shownMoney = money;
            shownSession = session;

            string line = change == null ? "" : $"\n<size=60%><color={(change[0] == '+' ? "#7CF29A" : "#FF8C8C")}>{change}</color></size>";
            text.text = $"<mark={UiTheme.MarkStrong} padding=\"8,8,4,4\"><size=80%>내 자산</size>\n<b>{MoneyText.Format(money)}</b>{line}</mark>";
        }

        bool wasVisible;

        // 손목 안쪽(글자 면)이 머리를 향하고, 손이 얼굴 가까이 있을 때
        bool Visible()
        {
            if (cam == null || context == null || context.Session == null) return false;
            GameFlow flow = context.GetComponent<GameFlow>();
            if (flow != null && (flow.CurrentPhase == GamePhaseId.Title || flow.CurrentPhase == GamePhaseId.Final || flow.CurrentPhase == GamePhaseId.None)) return false;

            Vector3 toHead = cam.transform.position - panel.position;
            float distance = toHead.magnitude;
            if (distance > maxDistance || distance < 0.0001f) return false;
            Vector3 readSide = -panel.forward; // 글자를 읽을 수 있는 쪽
            return Vector3.Dot(readSide, toHead / distance) >= faceThreshold;
        }
    }
}

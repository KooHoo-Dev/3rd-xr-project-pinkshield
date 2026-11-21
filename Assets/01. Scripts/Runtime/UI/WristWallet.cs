using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.UI
{
    /// <summary>
    /// 왼손 손목 보유 재화 (11-19 플레이 피드백 F: 게임 중 내 돈을 볼 방법이 없음).
    /// 시계를 보듯 왼손 손목 안쪽을 얼굴 쪽으로 돌리면 SLIME PAY 카드(<see cref="WalletCard"/>)가 떠오르고, 손을 내리면 사라집니다.
    /// 늘 떠 있지 않아 시야를 가리지 않고, 메뉴를 열 필요도 없습니다.
    ///
    /// 왼손 컨트롤러 자식에 카드를 붙이고, 카드 글자 면이 머리를 향하는 정도(내적)와 거리로 보일지 정합니다.
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 XR Origin의 "Left Controller"에 스스로 붙습니다.
    /// 타이틀 · 최종 결과 화면에서는 숨깁니다.
    /// 돈이 바뀌면 왼손이 짧게 떨리고, 숫자가 굴러 오르며(<see cref="CountUpRules"/>) +$40 같은 변화가 튀어 올라 "손목을 봐"라는 신호가 됩니다.
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

        WalletCard card;
        Transform panel;
        Camera cam;
        GameContext context;
        float alpha;
        int targetMoney = int.MinValue;   // 지금 가진 돈
        int rollFrom, rollTo;             // 굴러 오르는 구간
        float rollStart = -999f;
        int shownMoney = int.MinValue;    // 카드에 보이는 숫자 (굴러 오르는 중이면 중간 값)
        GameSession shownSession;
        float popAt = -1f;
        XRBaseInputInteractor hand;
        bool wasVisible;

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
            var go = new GameObject("WristWallet");
            panel = go.transform;
            panel.SetParent(transform, false);
            panel.localPosition = localOffset;
            // TMP 글자는 −Z 쪽에서 읽히므로 forward를 −X로: 컨트롤러 +X(손목 안쪽) 쪽에서 읽힘
            panel.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            card = WalletCard.Create(panel); // SLIME PAY 카드 (UI 개편 C안)
            card.SetAlpha(0f);
            hand = GetComponentInChildren<XRBaseInputInteractor>();
        }

        void LateUpdate()
        {
            if (card == null) return;
            if (cam == null) cam = Camera.main;
            if (context == null) context = GameContext.Instance;

            Watch();
            float pop = popAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - popAt) / PopSeconds);
            panel.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, pop); // 돈이 바뀌면 툭 커졌다 돌아옴

            bool want = Visible();
            if (want && !wasVisible) context?.Events.RaiseHappened(GameMoment.WalletViewed); // 튜토리얼 "손목 보기" (FIX C4)
            wasVisible = want;
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            card.SetAlpha(alpha);
        }

        // 돈이 바뀌었나: 같은 게임 안의 변화면 숫자를 굴리고 + 진동 + 변화량 표시 (새 게임 시작은 알리지 않고 바로 맞춤)
        void Watch()
        {
            GameSession session = context != null ? context.Session : null;
            int money = session != null ? session.Wallet.Money : 0;

            if (money != targetMoney || session != shownSession)
            {
                if (session != null && session == shownSession && targetMoney != int.MinValue)
                {
                    rollFrom = shownMoney; // 굴러 오르는 중에 또 바뀌어도 지금 보이는 값에서 이어감
                    rollTo = money;
                    rollStart = Time.unscaledTime;
                    popAt = Time.unscaledTime;
                    card.ShowDelta(money - targetMoney);
                    if (hand != null) hand.SendHapticImpulse(0.7f, 0.18f); // 11-20 F7: 0.35 · 0.08초는 못 느낌
                }
                else
                {
                    rollFrom = rollTo = money;
                    rollStart = -999f;
                }
                targetMoney = money;
                shownSession = session;
            }

            int value = CountUpRules.Value(rollFrom, rollTo, CountUpRules.Progress(Time.unscaledTime - rollStart));
            if (value != shownMoney)
            {
                shownMoney = value;
                card.SetAmount(MoneyText.Format(value));
            }
        }

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

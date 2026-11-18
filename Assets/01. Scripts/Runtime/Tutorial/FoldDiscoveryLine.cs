using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Interaction;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 접힌 팩 발견 보상 (시연피드백 A3 · 튜토리얼_설계 5-2): 접힘 단서가 있는 팩을 손에 들고 눈앞 가까이(30cm)에서
    /// 1초 넘게 바라보면, 한 판에 한 번 사장님이 "영업비밀을 알아채버렸군…" 하고 혼잣말합니다.
    /// 단서가 "좋은 팩"이라는 사실은 직접 말하지 않습니다(확률 노출 금지). 뒤집힘 단서는 숨김 유지(9장 Q4)라 반응하지 않습니다.
    /// 안내가 아니라 발견 보상이라 튜토리얼을 꺼도 나옵니다. 씬이 열리면 스스로 생깁니다.
    /// </summary>
    public sealed class FoldDiscoveryLine : MonoBehaviour
    {
        [Tooltip("눈에서 팩까지 이 거리 안이어야 '자세히 봄' (m)")]
        [SerializeField] float lookDistance = 0.3f;
        [Tooltip("시선과 팩 방향의 각도가 이 안이어야 함 (도)")]
        [SerializeField] float lookAngle = 25f;
        [Tooltip("이만큼 계속 바라보면 반응 (초)")]
        [SerializeField] float lookSeconds = 1f;
        [Tooltip("말풍선이 보이는 시간 (초)")]
        [SerializeField] float showSeconds = 4f;
        [SerializeField] string line = "그냥 알바생인 줄 알았는데,\n영업비밀을 알아채버렸군…";

        GameContext ctx;
        BossBubble bubble;
        readonly TutorialGrabWatcher grabs = new TutorialGrabWatcher();
        readonly List<TutorialSignal> ignored = new List<TutorialSignal>();
        bool sharedThisGame;
        float looked, hideAt = -1f, refreshIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<FoldDiscoveryLine>() != null) return;
            new GameObject("FoldDiscoveryLine").AddComponent<FoldDiscoveryLine>();
        }

        void Start()
        {
            ctx = GameContext.Instance;
            if (ctx == null) { enabled = false; return; }
            ctx.Events.SessionStarted += OnSessionStarted;
            var revealer = FindAnyObjectByType<CardRevealer>();
            var go = new GameObject("Bubble");
            go.transform.SetParent(transform, false);
            bubble = go.AddComponent<BossBubble>();
            bubble.Configure(revealer != null ? revealer.Font : null);
        }

        void OnDestroy()
        {
            if (ctx != null) ctx.Events.SessionStarted -= OnSessionStarted;
        }

        void OnSessionStarted(GameSession _) => sharedThisGame = false;

        void Update()
        {
            if (hideAt > 0f && Time.unscaledTime > hideAt) { hideAt = -1f; bubble.Hide(); }
            if (sharedThisGame || Camera.main == null) return;

            // 손 목록은 가끔만 다시 찾음 (리그가 바뀌는 페이즈 이동 대비)
            refreshIn -= Time.unscaledDeltaTime;
            if (refreshIn <= 0f) { refreshIn = 2f; grabs.Refresh(); }
            grabs.Scan(ignored, null);

            if (LookingAtFoldedPack(Camera.main.transform)) looked += Time.unscaledDeltaTime;
            else looked = 0f;
            if (looked < lookSeconds) return;

            sharedThisGame = true;
            bubble.Show(TutorialSpeaker.Boss, line, null);
            hideAt = Time.unscaledTime + showSeconds;
        }

        bool LookingAtFoldedPack(Transform head)
        {
            foreach (Transform t in grabs.Held)
            {
                var pack = t.GetComponentInParent<PackView>();
                if (pack == null || (pack.Clue & PackClue.FoldedTop) == 0) continue;
                Vector3 to = pack.transform.position - head.position;
                if (to.magnitude <= lookDistance && Vector3.Angle(head.forward, to) <= lookAngle) return true;
            }
            return false;
        }
    }
}

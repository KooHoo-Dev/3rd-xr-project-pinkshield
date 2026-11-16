using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 개봉한 카드를 카운터 위에 펼쳐 보여줍니다. 단일 팩은 뒷면으로 놓이고, 플레이어가 집으면 뒤집힙니다.
    /// 상자는 가장 좋은 카드 몇 장을 앞면으로 보여주고 요약을 안내합니다 (전체 요약 화면은 3일차).
    /// 카드 오브젝트는 풀에서 재사용합니다. 카드 데이터는 개봉 시점에 이미 보유 목록에 들어가 있습니다.
    /// </summary>
    public sealed class CardRevealer : MonoBehaviour
    {
        [SerializeField] CardView cardPrefab;
        [Tooltip("카드가 놓일 기준점. 카드 앞면(-Z)이 플레이어를 향하도록 회전")]
        [SerializeField] Transform anchor;
        [SerializeField] float spacing = 0.075f;
        [SerializeField] int boxHighlightCount = 5;

        readonly List<CardRevealView> shown = new List<CardRevealView>();
        readonly Stack<CardRevealView> pool = new Stack<CardRevealView>();
        GameContext context;

        /// <summary>카드가 뒤집혀 공개될 때 (3일차 연출·사운드 연결 지점).</summary>
        public event Action<CardRevealView> CardRevealed;

        public void Configure(CardView prefab, Transform cardAnchor)
        {
            cardPrefab = prefab;
            anchor = cardAnchor;
        }

        void OnEnable()
        {
            context = GameContext.Instance;
            if (context != null) context.Events.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (context != null) context.Events.PhaseChanged -= OnPhaseChanged;
        }

        // 다음 영업이 시작되면 카운터를 비웁니다.
        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            if (to == GamePhaseId.Business || to == GamePhaseId.Title) Clear();
        }

        public void RevealPack(PackResult pack)
        {
            Clear();
            for (int i = 0; i < pack.Cards.Length; i++)
                Show(pack.Cards[i], pack.IsNew[i], i, pack.Cards.Length, faceDown: true);
        }

        public void RevealBox(PackResult[] packs)
        {
            Clear();
            var all = new List<(CardData card, bool isNew)>();
            foreach (PackResult p in packs)
                for (int i = 0; i < p.Cards.Length; i++)
                    all.Add((p.Cards[i], p.IsNew[i]));

            var best = all.OrderByDescending(x => x.card.rarity).ThenByDescending(x => x.isNew).Take(boxHighlightCount).ToList();
            for (int i = 0; i < best.Count; i++)
                Show(best[i].card, best[i].isNew, i, best.Count, faceDown: false);

            int newCount = all.Count(x => x.isNew);
            int specials = packs.Count(p => p.Special != SpecialPackType.None);
            context?.Events.RaiseNotice($"상자 개봉: {all.Count}장, 새 카드 {newCount}장, 최고 {best[0].card.rarity}" +
                                        (specials > 0 ? $", 특수 팩 {specials}개!" : ""));
        }

        public void Clear()
        {
            foreach (CardRevealView v in shown)
            {
                v.Hide();
                pool.Push(v);
            }
            shown.Clear();
        }

        void Show(CardData card, bool isNew, int index, int count, bool faceDown)
        {
            CardRevealView view = pool.Count > 0 ? pool.Pop() : Create();
            float x = (index - (count - 1) * 0.5f) * spacing;
            Vector3 pos = anchor.position + anchor.right * x;
            view.Show(card, isNew, pos, anchor.rotation, faceDown);
            shown.Add(view);
        }

        CardRevealView Create()
        {
            CardView card = Instantiate(cardPrefab, transform);
            var view = card.gameObject.AddComponent<CardRevealView>();
            view.Revealed += v => CardRevealed?.Invoke(v);
            return view;
        }
    }

    /// <summary>
    /// 공개 연출용 카드 상태. 앞/뒤 뒤집기는 물리와 무관하게 "Visual" 자식만 회전합니다(손에 든 채로도 뒤집힘).
    /// </summary>
    [RequireComponent(typeof(CardView), typeof(XRGrabInteractable))]
    public sealed class CardRevealView : MonoBehaviour
    {
        const float FlipSeconds = 0.25f;

        CardView view;
        XRGrabInteractable grab;
        Rigidbody body;
        Transform visual;
        bool faceDown;

        public CardData Card => view.Data;
        public bool IsNew { get; private set; }
        public event Action<CardRevealView> Revealed;

        void Awake()
        {
            view = GetComponent<CardView>();
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            visual = transform.Find("Visual");
            if (visual == null) Debug.LogWarning("[CardRevealView] 카드 프리팹에 Visual 자식이 없습니다. Build Card Prefab 을 다시 실행하세요.", this);
            grab.selectEntered.AddListener(OnGrabbed);
        }

        public void Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, bool startFaceDown)
        {
            gameObject.SetActive(true);
            view.Bind(card);
            IsNew = isNew;
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.isKinematic = true; // 놓아도 그 자리에 떠 있음 (잡았다 놓으면 XRI가 이 상태로 되돌림)
                body.useGravity = false;
            }
            faceDown = startFaceDown;
            if (visual != null) visual.localRotation = faceDown ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            if (!faceDown) Revealed?.Invoke(this);
        }

        public void Hide()
        {
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (!faceDown) return;
            faceDown = false;
            if (visual != null) StartCoroutine(Flip());
            Revealed?.Invoke(this);
        }

        IEnumerator Flip()
        {
            Quaternion from = visual.localRotation, to = Quaternion.identity;
            for (float t = 0f; t < 1f; t += Time.deltaTime / FlipSeconds)
            {
                visual.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            visual.localRotation = to;
        }
    }
}

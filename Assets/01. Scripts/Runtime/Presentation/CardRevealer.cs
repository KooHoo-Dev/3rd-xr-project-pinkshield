using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Interaction;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 단일 팩: 팩이 있던 자리(눈앞)에 카드 5장이 앞면 묶음으로 나오고, 오른손 트리거를 누를 때마다
    /// 맨 앞 카드가 카운터로 날아가며 다음 카드가 보입니다 (Pocket의 "한 장씩 넘기기", 개봉연출 설계 2장 ③).
    /// 좋은 등급일수록 다음 카드로 넘어가기 전 멈춤이 길고 진동이 셉니다 (3장 등급별 연출 예산, RarityRevealProfile).
    /// 상자는 결과판에 10팩을 한 줄씩 채워 "몇 팩이 열렸는지" 보여준 뒤, 가장 좋은 카드 몇 장을 앞면으로 펼칩니다.
    /// (50장 전체 요약 화면·팩별 연출은 3일차, 설계: 98. Doc/카드팩_개봉연출_설계.md)
    /// 카드 오브젝트는 풀에서 재사용합니다. 카드 데이터는 개봉 시점에 이미 보유 목록에 들어가 있습니다.
    /// </summary>
    public sealed class CardRevealer : MonoBehaviour
    {
        [SerializeField] CardView cardPrefab;
        [Tooltip("카드가 놓일 기준점. 카드 앞면(-Z)이 플레이어를 향하도록 회전")]
        [SerializeField] Transform anchor;
        [SerializeField] float spacing = 0.075f;
        [SerializeField] int boxHighlightCount = 5;
        [Tooltip("상자 결과판에 팩 한 줄이 추가되는 간격(초)")]
        [SerializeField] float boxRowInterval = 0.25f;

        [Header("한 장씩 넘기기")]
        [Tooltip("묶음에서 카드 사이 간격 (m)")]
        [SerializeField] float stackSpacing = 0.003f;
        [Tooltip("넘긴 카드가 카운터까지 날아가는 시간 (초)")]
        [SerializeField] float flySeconds = 0.35f;
        [Tooltip("등급별 멈춤·진동·소리. 비워 두면 설계 기본값")]
        [SerializeField] RarityRevealProfile revealProfile;

        readonly List<CardRevealView> shown = new List<CardRevealView>();
        readonly Stack<CardRevealView> pool = new Stack<CardRevealView>();
        GameContext context;
        TextMeshPro boxBoard;
        Coroutine boxRoutine;
        Coroutine packRoutine;

        /// <summary>상자 결과판이 채워지거나 카드를 넘기는 중. 이때 새 개봉을 받으면 연출이 끊깁니다.</summary>
        public bool IsBusy => boxRoutine != null || packRoutine != null;

        /// <summary>카운터 위 위치 (상자 안내 말풍선 자리).</summary>
        public Vector3 AnchorPosition => anchor.position;

        /// <summary>한글 글꼴. 씬에 따로 연결하지 않도록 카드 프리팹의 글꼴을 빌려 씁니다.</summary>
        public TMP_FontAsset Font
        {
            get
            {
                TMP_Text t = cardPrefab != null ? cardPrefab.GetComponentInChildren<TMP_Text>(true) : null;
                return t != null ? t.font : null;
            }
        }

        /// <summary>카드가 뒤집혀 공개될 때 (3일차 연출·사운드 연결 지점).</summary>
        public event Action<CardRevealView> CardRevealed;

        public void Configure(CardView prefab, Transform cardAnchor, RarityRevealProfile profile = null)
        {
            cardPrefab = prefab;
            anchor = cardAnchor;
            revealProfile = profile;
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

        /// <param name="stackPose">카드 묶음이 나올 자리. 보통 개봉한 팩의 위치·방향(앞면 -Z가 플레이어 쪽).</param>
        public void RevealPack(PackResult pack, Pose stackPose)
        {
            Clear();
            packRoutine = StartCoroutine(PackSequence(pack, stackPose));
        }

        IEnumerator PackSequence(PackResult pack, Pose stack)
        {
            int n = pack.Cards.Length;
            var views = new CardRevealView[n];
            Vector3 back = stack.rotation * Vector3.forward; // 플레이어 반대쪽
            for (int i = 0; i < n; i++)
            {
                views[i] = Show(pack.Cards[i], pack.IsNew[i], stack.position + back * (stackSpacing * i), stack.rotation, false, announce: false);
                views[i].SetGrabbable(false);
            }

            XRBaseInputInteractor hand = HandFinder.Find(InteractorHandedness.Right);
            context?.Events.RaiseNotice("오른손 트리거로 한 장씩 넘기세요");

            for (int i = 0; i < n; i++)
            {
                // i번째 카드가 맨 앞에 보이는 중
                RarityRevealProfile.Entry fx = Profile.Get(pack.Cards[i].rarity);
                views[i].Announce();
                if (fx.sound != null) AudioSource.PlayClipAtPoint(fx.sound, views[i].transform.position, fx.volume);
                if (hand != null) StartCoroutine(Haptic(hand, fx));
                yield return new WaitForSeconds(fx.pause);

                // 오른손 트리거를 새로 누를 때까지 (손을 못 찾으면 1초 뒤 자동)
                float autoAt = Time.time + 1f;
                yield return new WaitUntil(() => hand != null ? hand.activateInput.ReadWasPerformedThisFrame() : Time.time >= autoAt);

                views[i].FlyTo(SlotPosition(i, n), anchor.rotation, flySeconds);
            }

            yield return new WaitForSeconds(flySeconds);
            packRoutine = null;
        }

        RarityRevealProfile Profile
        {
            get
            {
                if (revealProfile == null) revealProfile = ScriptableObject.CreateInstance<RarityRevealProfile>();
                return revealProfile;
            }
        }

        static IEnumerator Haptic(XRBaseInputInteractor hand, RarityRevealProfile.Entry fx)
        {
            for (int k = 0; k < fx.hapticCount; k++)
            {
                if (k > 0) yield return new WaitForSeconds(fx.hapticSeconds + 0.06f);
                hand.SendHapticImpulse(fx.hapticAmplitude, fx.hapticSeconds);
            }
        }

        public void RevealBox(PackResult[] packs)
        {
            Clear();
            boxRoutine = StartCoroutine(BoxSequence(packs));
        }

        // 결과판에 팩을 한 줄씩 채움 → 합계 → 하이라이트 카드
        IEnumerator BoxSequence(PackResult[] packs)
        {
            TextMeshPro board = EnsureBoard();
            board.gameObject.SetActive(true);
            CardRarityStyles styles = cardPrefab.Styles;

            var rows = new System.Text.StringBuilder();
            for (int i = 0; i < packs.Length; i++)
            {
                PackResult p = packs[i];
                int newInPack = p.IsNew.Count(n => n);
                rows.Append($"팩 {i + 1,2}  {RarityTag(styles, p.BestRarity)}");
                if (newInPack > 0) rows.Append($"  <color=#7CF29A>새 {newInPack}</color>");
                if (p.Special != SpecialPackType.None) rows.Append($"  <color=#FFD54A>{SpecialLabel(p.Special)}!</color>");
                rows.Append('\n');
                board.text = Mark($"<b>상자 개봉 {i + 1}/{packs.Length}</b>\n{rows}");
                yield return new WaitForSeconds(boxRowInterval);
            }

            var all = new List<(CardData card, bool isNew)>();
            foreach (PackResult p in packs)
                for (int i = 0; i < p.Cards.Length; i++)
                    all.Add((p.Cards[i], p.IsNew[i]));
            int newCount = all.Count(x => x.isNew);
            CardRarity top = all.Max(x => x.card.rarity);

            board.text = Mark($"<b>상자 개봉 완료 {packs.Length}/{packs.Length}</b>\n{rows}" +
                              $"<b>합계 {all.Count}장 · 새 카드 {newCount}장 · 최고 {RarityTag(styles, top)}</b>");
            context?.Events.RaiseNotice($"상자 개봉: {packs.Length}팩 {all.Count}장, 새 카드 {newCount}장");

            var best = all.OrderByDescending(x => x.card.rarity).ThenByDescending(x => x.isNew).Take(boxHighlightCount).ToList();
            for (int i = 0; i < best.Count; i++)
                Show(best[i].card, best[i].isNew, SlotPosition(i, best.Count), anchor.rotation, false);
            boxRoutine = null;
        }

        static string RarityTag(CardRarityStyles styles, CardRarity r)
        {
            if (styles == null) return r.ToString();
            CardRarityStyles.Style s = styles.Get(r);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(s.accentColor)}>{s.label}</color>";
        }

        static string SpecialLabel(SpecialPackType t) => t switch
        {
            SpecialPackType.GodPack => "갓팩",
            SpecialPackType.LegendaryPack => "레전더리 확정",
            _ => t.ToString(),
        };

        // 배경판 없이도 읽히도록 글자 뒤에 어두운 띠를 깝니다.
        static string Mark(string text) => $"<mark=#140F1ECC>{text}</mark>";

        // 결과판: 카드 위쪽에 카드와 같은 방향으로. 한글 폰트는 카드 프리팹의 글꼴을 빌려 씁니다.
        TextMeshPro EnsureBoard()
        {
            if (boxBoard != null) return boxBoard;
            var go = new GameObject("BoxResultBoard", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(anchor.position + anchor.up * 0.09f, anchor.rotation);
            boxBoard = go.AddComponent<TextMeshPro>();
            if (Font != null) boxBoard.font = Font;
            boxBoard.fontSize = 0.22f;
            boxBoard.alignment = TextAlignmentOptions.BottomLeft;
            boxBoard.color = Color.white;
            boxBoard.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0.3f, 0.3f);
            return boxBoard;
        }

        public void Clear()
        {
            if (boxRoutine != null) { StopCoroutine(boxRoutine); boxRoutine = null; }
            if (packRoutine != null) { StopCoroutine(packRoutine); packRoutine = null; }
            if (boxBoard != null) boxBoard.gameObject.SetActive(false);
            foreach (CardRevealView v in shown)
            {
                v.Hide();
                pool.Push(v);
            }
            shown.Clear();
        }

        // 카운터 위 가로 한 줄의 index번째 자리
        Vector3 SlotPosition(int index, int count) => anchor.position + anchor.right * ((index - (count - 1) * 0.5f) * spacing);

        CardRevealView Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, bool faceDown, bool announce = true)
        {
            CardRevealView view = pool.Count > 0 ? pool.Pop() : Create();
            view.Show(card, isNew, position, rotation, faceDown, announce);
            shown.Add(view);
            return view;
        }

        CardRevealView Create()
        {
            CardView card = Instantiate(cardPrefab, transform);
            var view = card.gameObject.AddComponent<CardRevealView>();
            view.Revealed += v => CardRevealed?.Invoke(v);
            return view;
        }
    }
}

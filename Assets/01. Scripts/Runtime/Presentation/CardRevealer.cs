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
    /// 카드 공개 연출. 단일 팩과 상자(10팩)가 같은 진행기 <see cref="PackSequence"/> 하나를 씁니다 (시연 피드백 A4 · B1 · B2 · B3).
    /// 파이프라인: [개봉 연출] → [공개 대기열] → [결과].
    /// - 개봉 연출: 단일 팩은 뜯김(PackTearEffect) 후 카드 묶음이 솟아오름. 상자는 눈앞 아치에서 10팩이 0.12초 간격으로 뜯김(BoxOpenEffect).
    /// - 공개 대기열(RevealQueue): 오른손 트리거 1번 = 1장. 갓팩도 같은 한 장씩. 묶음은 뒷면이 보이다가 맨 앞에서 뒤집혀 공개 (등급 색 미리 노출 X).
    ///   트리거를 3초(fastForwardHoldSeconds) 꾹 누르면 전 등급 건너뛰기 (DEC-004): 남은 카드는 모두 더미로.
    ///   단 갓팩은 건너뛸 수 없음 (DEC-008): 갓팩 카드는 특수 효과와 함께 한 장씩 열고, 건너뛰면 마지막에 열어 봄.
    /// - 결과: 단일 팩은 카운터 더미 위 결과함(PackResultPanel). 개봉한 카드는 퇴근 전까지 카운터에 계속 쌓아 두고(잡아서 볼 수 있음),
    ///   퇴근하면 한꺼번에 보관함(ICardStash)으로 갑니다. 오래된 카드는 옆면 덩어리로 줄여 Quest 성능을 지킵니다 (pileVisibleCards).
    ///   상자는 결과판(팩마다 5점 줄) + 하이라이트 카드 5장 (다음 상자 · 퇴근 · 영업 시작 때 정리).
    /// 카드 오브젝트는 풀에서 재사용하고, 묶음은 맨 앞 몇 장(stackDepth)만 실체화합니다(Quest 성능). 풀은 퇴근 후 페이즈에 미리 데웁니다.
    /// 카드 데이터는 개봉 시점에 이미 보유 목록에 들어가 있어, 여기의 연출은 보여 주기만 합니다(건너뛰거나 끊겨도 데이터는 같음).
    /// </summary>
    public sealed class CardRevealer : MonoBehaviour
    {
        enum PackStage { None, Tearing, Flipping, Result, Summary }
        enum Advance { Next, Skip }

        static readonly Color SpecialGold = new Color(1f, 0.82f, 0.3f);

        [SerializeField] CardView cardPrefab;
        [Tooltip("카드가 놓일 기준점. 카드 앞면(-Z)이 플레이어를 향하도록 회전")]
        [SerializeField] Transform anchor;
        [SerializeField] float spacing = 0.095f;
        [Tooltip("카운터에 놓인 카드 크기 배율 (시연 피드백 A1: 카드가 작아 보임)")]
        [SerializeField] float counterScale = 1.3f;
        [SerializeField] int boxHighlightCount = 5;
        [Tooltip("상자 결과판에 팩 한 줄이 추가되는 간격(초)")]
        [SerializeField] float boxRowInterval = 0.25f;

        [Header("한 장씩 넘기기")]
        [Tooltip("묶음에서 카드 사이 간격 (m)")]
        [SerializeField] float stackSpacing = 0.003f;
        [Tooltip("눈앞 카드 묶음 크기 배율 (팩 크기 배율과 같게)")]
        [SerializeField] float stackScale = 1.5f;
        [Tooltip("단일 팩: 카드 묶음 크기 = 눈앞 팩 크기 × 이 값. 카드(63×88mm)가 팩 몸통(75×102mm) 안에 맞게 — 팩과 카드 크기 이질감 제거")]
        [SerializeField] float packFitRatio = 1.1f;
        [Tooltip("단일 팩: 팩 크기로 시작한 카드 묶음이 위로 솟으며 이 배율만큼 커짐 (VR 실기기 피드백: 개봉 카드가 다시 작아 보임 → 1.5배)")]
        [SerializeField] float singlePackRevealGrow = 1.5f;
        float curStackScale = 1.5f; // 이번 개봉에 쓰는 묶음 크기 (단일 팩은 팩 크기에 맞춤, 상자는 stackScale)
        [Tooltip("넘긴 카드가 더미까지 날아가는 시간 (초)")]
        [SerializeField] float flySeconds = 0.35f;
        [Tooltip("등급별 멈춤·진동·소리. 비워 두면 설계 기본값")]
        [SerializeField] RarityRevealProfile revealProfile;
        [Tooltip("묶음에서 실제로 만들어 두는 카드 수 (맨 앞부터). 나머지는 넘길 때마다 풀에서 꺼냄 (Quest 성능)")]
        [SerializeField, Min(1)] int stackDepth = 3;
        [Tooltip("앞 카드가 빠진 뒤 뒤 카드들이 앞으로 당겨지는 시간 (초)")]
        [SerializeField] float restackSeconds = 0.12f;
        [Header("등급 연출 (DEC-004: 수퍼레어~유니크 통일, 레전더리만 더 크게 — 카메라 셰이크 대신 카드 흔들림 + 진동)")]
        [Tooltip("레전더리 카드가 부르르 떨리는 크기 (m)와 시간 (초)")]
        [SerializeField] float legendaryShakeAmount = 0.004f;
        [SerializeField] float legendaryShakeSeconds = 0.9f;
        [Tooltip("레전더리 확정 팩의 레전더리 카드는 평소보다 이만큼 더 멈춤 (초) + 진동 1번 더")]
        [SerializeField] float legendaryPackExtraPause = 0.6f;

        [Header("빠르게 넘기기 (3초 꾹)")]
        [Tooltip("오른손 트리거를 이 시간 이상 누르고 있으면 건너뛰기 (초). 실기기에서 조정")]
        [SerializeField] float fastForwardHoldSeconds = 3f;
        [Tooltip("이보다 짧은 눌림(넘기기 탭)에는 게이지를 보이지 않음 (초)")]
        [SerializeField] float holdGaugeGrace = 0.25f;
        [Tooltip("꾹 누르는 동안 진동 세기: 처음 → 끝")]
        [SerializeField, Range(0f, 1f)] float holdHapticMin = 0.05f;
        [SerializeField, Range(0f, 1f)] float holdHapticMax = 0.5f;
        [Tooltip("게이지 자리: 카드 묶음 기준(묶음 회전) 오프셋 (m). 기본은 묶음 바로 아래")]
        [SerializeField] Vector3 gaugeOffset = new Vector3(0f, -0.1f, 0f);
        [Tooltip("상자: 카드 묶음이 떠오르는 시간 (초)")]
        [SerializeField] float popInSeconds = 0.3f;
        [Tooltip("건너뛴 카드들이 더미로 날아갈 때 카드 사이 시차 (초, 단일 팩)")]
        [SerializeField] float skipFlyStagger = 0.04f;

        [Header("상자 일괄 개봉 (B3)")]
        [Tooltip("상자 카드 묶음 자리: 머리에서 정면으로 (m). 단일 팩을 눈앞에 들었을 때(TearGestureOpen)와 같게")]
        [SerializeField] float boxStackDistance = 0.45f;
        [SerializeField] float boxStackDrop = 0.08f;
        [Tooltip("팩마다 윗부분이 뜯기는 간격 (초)")]
        [SerializeField] float boxTearInterval = 0.12f;
        [Tooltip("상자 팩 뜯는 소리 / 특수 팩(갓팩 · 레전더리 확정) 예고음. 비워 두면 없음")]
        [SerializeField] AudioClip boxTearSound;
        [SerializeField] AudioClip specialTearSound;
        [Tooltip("상자 더미에서 카드 한 장당 높이 (m). 50장이 쌓이므로 단일 팩보다 얇게")]
        [SerializeField] float boxPileThickness = 0.0008f;
        [Tooltip("상자 더미에 실제 카드로 남겨 두는 수 (위에서부터). 그 아래는 카드 옆면 덩어리 하나로 표시")]
        [SerializeField, Min(1)] int boxPileVisible = 3;
        [Tooltip("퇴근 후 페이즈가 되면 미리 만들어 두는 카드 수 (상자 개봉 때 끊김 방지)")]
        [SerializeField] int warmPoolCount = 12;

        [Header("팩에서 솟아오르기 (A4)")]
        [Tooltip("뜯긴 팩에서 카드 묶음이 위로 솟는 거리 (m)")]
        [SerializeField] float cardRise = 0.07f;
        [SerializeField] float riseSeconds = 0.45f;

        [Header("카드 더미 (B1)")]
        [Tooltip("더미 맨 아래 카드 자리: 기준점(anchor)에서 월드 좌표로 얼마나 떨어졌는지. 기본은 카운터 윗면 바로 위")]
        [SerializeField] Vector3 pileOffset = new Vector3(0f, -0.199f, 0f);
        [Tooltip("카드 한 장이 쌓일 때마다 높아지는 정도 (m). 카드 두께 1.2mm × 크기 배율보다 살짝 크게")]
        [SerializeField] float pileThickness = 0.002f;
        [Tooltip("장마다 비뚤어지는 최대 각도 (도)")]
        [SerializeField] float pileJitterAngle = 7f;
        [Tooltip("장마다 어긋나는 최대 거리 (m)")]
        [SerializeField] float pileJitterOffset = 0.004f;

        [Header("결과함 UI (B2)")]
        [Tooltip("더미 바닥에서 결과함 UI 아래쪽까지 높이 (m)")]
        [SerializeField] float resultPanelHeight = 0.07f;
        [Tooltip("TMP 3D 글자 크기 (1 ≈ 10cm)")]
        [SerializeField] float resultFontSize = 0.2f;
        [SerializeField] float resultPopSeconds = 0.25f;

        [Header("갓팩 (건너뛰기 불가, 특수 효과 후 한 장씩 — DEC-008)")]
        [SerializeField] float godIntroSeconds = 1f;
        [Tooltip("갓팩 예고 효과음 (비워 두면 없음)")]
        [SerializeField] AudioClip godPackSound;

        [Header("카드 더미 정보 (더미 카드를 잡으면 표시)")]
        [Tooltip("더미 정보 판이 결과함에서 옆으로 떨어지는 거리 (m)")]
        [SerializeField] float pileInfoOffset = 0.36f;
        [Tooltip("카드를 놓은 뒤 정보 판이 사라지기까지 (초)")]
        [SerializeField] float pileInfoHoldSeconds = 4f;

        [Header("테이블 더미 (퇴근 전까지 쌓아 둠)")]
        [Tooltip("카운터에 실제 카드 오브젝트로 두는 수 (최근 것부터). 이보다 오래된 카드는 옆면 덩어리 하나로 줄여 성능(오브젝트 · TMP · 잡기 컴포넌트)을 아낌")]
        [SerializeField, Min(5)] int pileVisibleCards = 15;
        [Tooltip("카운터에 쌓아 둘 수 있는 카드 수(덩어리 포함). 넘으면 쌓인 카드를 보관함으로 옮기고 새로 쌓음")]
        [SerializeField, Min(10)] int pileMaxCards = 60;

        [Header("임시 보관함 연출 (바인더 전)")]
        [SerializeField] float stashRise = 0.5f;
        [SerializeField] float stashSeconds = 0.45f;
        [SerializeField] float stashStagger = 0.05f;

        readonly struct BoxPileEntry
        {
            public readonly CardRevealView View;
            public readonly int Index;
            public readonly float ArriveAt;
            public BoxPileEntry(CardRevealView view, int index, float arriveAt) { View = view; Index = index; ArriveAt = arriveAt; }
        }

        readonly List<CardRevealView> shown = new List<CardRevealView>();
        readonly List<CardRevealView> pile = new List<CardRevealView>();       // 단일 팩: shown 중 더미에 놓인 카드 (아래부터)
        int tableNew;                                                         // 덩어리로 줄인 카드 중 새 카드 수
        PackResultPanel pileInfo;
        Coroutine pileInfoHide;
        readonly List<CardData> tableCards = new List<CardData>();            // 덩어리로 줄여 둔 오래된 카드 (보관함 알림용)
        readonly List<CardRevealView> stashing = new List<CardRevealView>();   // 보관함으로 날아가는 중
        readonly List<CardRevealView> stackViews = new List<CardRevealView>(); // 눈앞 묶음에 실체화된 카드 (맨 앞부터)
        readonly List<BoxPileEntry> boxPile = new List<BoxPileEntry>();        // 상자: 더미 위쪽에 실제 카드로 남은 것
        readonly Stack<CardRevealView> pool = new Stack<CardRevealView>();
        GameContext context;
        TextMeshPro boxBoard;
        PackResultPanel resultPanel;
        BoxOpenEffect boxEffect;
        SkipHoldGauge gauge;
        RevealBurst burst;
        Transform pileBlock;
        Coroutine packRoutine;
        Coroutine warmRoutine;
        PackStage stage;
        GameObject tearingPack;
        ICardStash stash;
        HoldToSkipTimer skipTimer;
        Advance advance;
        Pose pileBasePose;
        int pileIndex;   // 더미에 놓인(또는 건너뛰어 놓인 것으로 친) 카드 수 = 다음 카드의 높이 순번
        float blockThickness = 0.002f;
        int blockTop;    // 상자 더미에서 덩어리 하나로 표시하는 아래쪽 카드 수
        int createdCount;

        // 연출 중 오른손 레이의 UI 클릭을 잠시 끔 (트리거 하나가 넘기기 · 넣기 · UI 클릭을 동시에 하지 않게)
        readonly RayUiSuppressor rayUi = new RayUiSuppressor();

        /// <summary>팩이 뜯기거나, 카드를 넘기는 중이거나, 상자 결과판이 채워지는 중. 결과함 UI만 떠 있는 동안은 바쁘지 않습니다(새 개봉 시 자동 보관).</summary>
        public bool IsBusy => stage == PackStage.Tearing || stage == PackStage.Flipping || stage == PackStage.Summary;

        /// <summary>카운터에 아직 보관하지 않은 카드 더미가 있음 (단일 팩).</summary>
        public bool HasPile => pile.Count > 0 || tableCards.Count > 0;

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

        /// <summary>
        /// "보관함에 넣기" 연출. 바인더가 생기면 여기에 바인더(ICardStash 구현)를 넣습니다.
        /// 비워 두면 더미가 위로 날아가 사라지는 임시 연출(FlyAwayCardStash).
        /// </summary>
        public ICardStash Stash
        {
            get => stash ??= new FlyAwayCardStash(stashRise, stashSeconds, stashStagger);
            set => stash = value;
        }

        /// <summary>카드가 뒤집혀 공개될 때 (3일차 연출·사운드 연결 지점).</summary>
        public event Action<CardRevealView> CardRevealed;

        /// <summary>더미가 보관함으로 보내졌을 때 (아래 카드부터). 데이터는 이미 보유 목록에 있으므로 표시용입니다.</summary>
        public event Action<IReadOnlyList<CardData>> PileStashed;

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
            warmRoutine = null; // 코루틴은 비활성화와 함께 멈춤
            rayUi.Restore();
        }

        // 다음 영업이 시작되면 카운터를 비웁니다. 집으로 갈 때 남은 더미는 보관함으로. 퇴근 후가 되면 풀을 미리 데웁니다.
        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            if (to == GamePhaseId.Business || to == GamePhaseId.Title) Clear();
            else if (to == GamePhaseId.Home) StashOrClear();
            else if (to == GamePhaseId.AfterHours) WarmUp();
        }

        // ------------------------------------------------------------------ 시작

        /// <param name="stackPose">카드 묶음이 나올 자리. 보통 개봉한 팩의 위치·방향(앞면 -Z가 플레이어 쪽).</param>
        /// <param name="tear">뜯김 연출을 할 팩. 없으면 카드 묶음이 바로 솟아오릅니다.</param>
        public void RevealPack(PackResult pack, Pose stackPose, PackTearEffect tear = null)
        {
            // 이전 더미는 퇴근 전까지 테이블에 그대로 둠. 너무 많이 쌓였을 때만 보관함으로 옮김.
            if (tableCards.Count + pile.Count >= pileMaxCards)
            {
                StashPile();
                context?.Events.RaiseNotice("카드 더미가 가득 차서 보관함으로 옮겼어요");
            }
            ClearShown(keepPile: true);
            packRoutine = StartCoroutine(PackSequence(new List<PackResult> { pack }, stackPose, tear));
        }

        /// <summary>상자 10팩: 일괄 개봉 연출 → 50장 한 장씩(3초 꾹 = 건너뛰기) → 결과판 + 하이라이트.</summary>
        public void RevealBox(PackResult[] packs)
        {
            if (packs == null || packs.Length == 0) return;
            StashOrClear(); // 남은 단일 팩 더미는 보관함으로
            ClearShown();
            packRoutine = StartCoroutine(PackSequence(new List<PackResult>(packs), BoxStackPose(), null));
        }

        /// <summary>
        /// 카드 오브젝트(TMP · Rigidbody · XRGrabInteractable)를 미리 만들어 풀에 넣습니다. 한 프레임에 한 장씩이라 끊기지 않습니다.
        /// 퇴근 후(AfterHours) 진입 때 저절로 불립니다.
        /// </summary>
        public void WarmUp()
        {
            EnsureBoxEffect();
            EnsureGauge();
            EnsureBurst();
            if (warmRoutine == null && isActiveAndEnabled) warmRoutine = StartCoroutine(WarmPool());
        }

        IEnumerator WarmPool()
        {
            while (cardPrefab != null && createdCount < warmPoolCount)
            {
                CardRevealView v = Create();
                v.Hide();
                pool.Push(v);
                yield return null;
            }
            warmRoutine = null;
        }

        // ------------------------------------------------------------------ 진행기 (단일 팩 = 길이 1)

        IEnumerator PackSequence(List<PackResult> packs, Pose stack, PackTearEffect tear)
        {
            bool isBox = packs.Count > 1;
            blockThickness = isBox ? boxPileThickness : pileThickness;
            curStackScale = !isBox && tear != null ? tear.transform.lossyScale.y * packFitRatio : stackScale;
            stage = PackStage.Tearing;
            XRBaseInputInteractor hand = HandFinder.Find(InteractorHandedness.Right);
            rayUi.Suppress(hand); // 연출 중 오른손 트리거가 Ray UI 클릭까지 하지 않게
            skipTimer = new HoldToSkipTimer(fastForwardHoldSeconds, holdGaugeGrace);
            skipTimer.RequireRelease(); // 자르기 제스처로 트리거를 누른 채 열렸을 수 있음
            pileBasePose = PileBase();
            List<RevealItem> queue = RevealQueue.BuildAll(packs);

            // ① 개봉 연출
            if (!isBox)
            {
                // 카드 묶음은 팩 안(팩 앞면 뒤)에서 시작해 팩이 빠지는 동안 위로 솟습니다.
                FillStack(packs, queue, 0, stack, stackDepth, popIn: false);
                if (tear != null)
                {
                    tearingPack = tear.gameObject;
                    float delay = tear.Play(Color.white, false); // 뜯는 순간 등급이 미리 드러나지 않게 (DEC-004)
                    if (delay > 0f) yield return new WaitForSeconds(delay);
                }
                Vector3 up = stack.rotation * Vector3.up;
                curStackScale *= singlePackRevealGrow; // 팩에서 빠져나오며 커짐. 이후 넘기기 · 새로 채우는 카드도 이 크기
                foreach (CardRevealView v in stackViews)
                    v.FlyTo(v.transform.position + up * cardRise, stack.rotation, riseSeconds, curStackScale, arcHeight: 0f, grabbableOnArrive: false);
                stack = new Pose(stack.position + up * cardRise, stack.rotation);
                yield return new WaitForSeconds(riseSeconds);
            }
            else
            {
                Transform head = Camera.main != null ? Camera.main.transform : null;
                Vector3 headPos = head != null ? head.position : stack.position - stack.rotation * Vector3.forward * boxStackDistance;
                Vector3 forward = stack.rotation * Vector3.forward;
                float seconds = EnsureBoxEffect().Play(packs, headPos, forward, stack, boxTearInterval,
                    PackBeamColor, boxTearSound, specialTearSound);
                yield return new WaitForSeconds(seconds);
                FillStack(packs, queue, 0, stack, stackDepth, popIn: true);
                yield return new WaitForSeconds(popInSeconds);
            }

            // ② 공개 대기열: 트리거 1번 = 1칸, 3초 꾹 = 건너뛰기
            stage = PackStage.Flipping;
            context?.Events.RaiseNotice(isBox
                ? $"오른손 트리거로 한 장씩 넘기세요 ({queue.Count}칸) · {fastForwardHoldSeconds:0.#}초 꾹: 건너뛰기"
                : $"오른손 트리거로 한 장씩 넘기세요 · {fastForwardHoldSeconds:0.#}초 꾹: 건너뛰기");
            var gaugePose = new Pose(stack.position + stack.rotation * gaugeOffset, stack.rotation);
            bool skipped = false;
            int qi = 0;
            while (qi < queue.Count)
            {
                RevealItem item = queue[qi];
                PackResult p = packs[item.Pack];
                if (stackViews.Count == 0) FillStack(packs, queue, qi, stack, stackDepth, popIn: skipped);

                bool godPack = RevealQueue.IsGodPack(p);
                if (godPack && item.Card == 0) yield return GodIntro(stack, hand); // 갓팩: 특수 효과 후 한 장씩 (DEC-008)

                CardRevealView front = stackViews[0];
                CardData card = p.Cards[item.Card];
                bool confirmed = p.Special == SpecialPackType.LegendaryPack && card.rarity == CardRarity.Legendary;
                float pause = AnnounceCard(front, card, confirmed, hand);
                yield return WaitForAdvance(hand, pause, !godPack, gaugePose);
                stackViews.RemoveAt(0);
                SendToPile(front, isBox, 0f);
                qi++;

                if (advance == Advance.Skip && !skipped)
                {
                    queue = ApplySkip(packs, queue, qi, stack, isBox);
                    qi = 0;
                    skipped = true;
                    context?.Events.RaiseNotice(queue.Count > 0 ? "건너뛰기! 갓팩은 끝까지 열어 봐요" : "건너뛰기!");
                }
                else FillStack(packs, queue, qi, stack, stackDepth, popIn: false);
            }
            if (gauge != null) gauge.Hide();

            // ③ 결과
            if (isBox)
            {
                yield return BoxSummary(packs, skipped);
                yield break;
            }

            yield return new WaitForSeconds(flySeconds); // 마지막 카드가 더미에 내려앉는 것까지
            CompressPile();

            // 결과함 UI: 이번 팩 카드 더미 위, 월드 고정. 카드는 퇴근 전까지 테이블에 남아 있고 잡아서 볼 수 있음.
            stage = PackStage.Result;
            float packBase = CardPileLayout.Height(pileIndex - packs[0].Cards.Length, pileThickness);
            EnsureResultPanel().Show(packs[0], cardPrefab != null ? cardPrefab.Styles : null,
                pileBasePose.position + Vector3.up * (packBase + resultPanelHeight), anchor.rotation, resultFontSize, resultPopSeconds);
            foreach (CardRevealView v in pile) v.SetGrabbable(true);
            rayUi.Restore(); // 연출 중 꺼 둔 오른손 레이 UI 복귀 (결과 단계에서는 트리거를 쓰지 않음)
            packRoutine = null;
        }

        // ---- 카드 더미 정보: 더미의 카드를 집으면 테이블에 쌓인 카드 전체의 요약을 옆에 띄움 (줄어든 오래된 카드도 포함)
        void OnCardGrabbed(CardRevealView v)
        {
            if (!pile.Contains(v) || stage == PackStage.Tearing || stage == PackStage.Flipping) return;
            if (pileInfoHide != null) { StopCoroutine(pileInfoHide); pileInfoHide = null; }
            if (pileInfo == null) pileInfo = PackResultPanel.Create(transform, Font);
            float top = CardPileLayout.Height(pileIndex, pileThickness);
            Vector3 at = pileBasePose.position + anchor.right * pileInfoOffset + Vector3.up * (top + resultPanelHeight);
            pileInfo.ShowText(BuildPileInfo(v), at, anchor.rotation, resultFontSize, 0.15f);
        }

        void OnCardReleased(CardRevealView v)
        {
            if (pileInfo == null || !pileInfo.IsShown || !isActiveAndEnabled) return;
            if (pileInfoHide != null) StopCoroutine(pileInfoHide);
            pileInfoHide = StartCoroutine(HidePileInfoAfter(pileInfoHoldSeconds));
        }

        IEnumerator HidePileInfoAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            pileInfoHide = null;
            if (pileInfo != null) pileInfo.Hide();
        }

        void HidePileInfo()
        {
            if (pileInfoHide != null) { StopCoroutine(pileInfoHide); pileInfoHide = null; }
            if (pileInfo != null) pileInfo.Hide();
        }

        string BuildPileInfo(CardRevealView held)
        {
            var cards = new List<CardData>(tableCards);
            int newCount = tableNew;
            foreach (CardRevealView v in pile)
            {
                cards.Add(v.Card);
                if (v.IsNew) newCount++;
            }
            return RevealText.PileInfo(cards, newCount, held != null ? held.Card : null, held != null && held.IsNew,
                cardPrefab != null ? cardPrefab.Styles : null);
        }

        // 눈앞 · 테이블에 실제 카드로 두는 수를 넘으면 가장 오래된 카드부터 옆면 덩어리로 바꿉니다 (손에 든 카드는 건드리지 않음).
        void CompressPile()
        {
            while (pile.Count > pileVisibleCards)
            {
                CardRevealView v = pile[0];
                if (v.IsHeld) break;
                pile.RemoveAt(0);
                tableCards.Add(v.Card);
                if (v.IsNew) tableNew++;
                blockTop = tableCards.Count;
                Recycle(v);
            }
            UpdatePileBlock();
        }

        // 트리거를 새로 누르면 다음(Next), 3초 꾹이면 건너뛰기(Skip). 결과는 advance 필드에.
        // pause 동안은 넘기지 않음(실수로 두 장을 넘기지 않게). 손을 못 찾으면 pause + 1초 뒤 자동으로 다음.
        IEnumerator WaitForAdvance(XRBaseInputInteractor hand, float pause, bool allowSkip, Pose gaugePose)
        {
            float readyAt = Time.time + pause;
            float autoAt = readyAt + 1f;
            float nextPulse = 0f;
            while (true)
            {
                float now = Time.time;
                bool held = hand != null && hand.activateInput.ReadIsPerformed();
                bool fired = skipTimer.Update(held, now);
                if (allowSkip)
                {
                    if (fired)
                    {
                        if (gauge != null) gauge.Hide();
                        if (hand != null) hand.SendHapticImpulse(0.8f, 0.12f);
                        advance = Advance.Skip;
                        yield break;
                    }
                    if (skipTimer.IsCharging(now))
                    {
                        float progress = skipTimer.Progress(now);
                        EnsureGauge().Show(gaugePose, progress);
                        if (hand != null && now >= nextPulse)
                        {
                            // 누를수록 조금씩 세짐 (눌림이 전달되고 있다는 신호)
                            hand.SendHapticImpulse(Mathf.Lerp(holdHapticMin, holdHapticMax, progress), 0.1f);
                            nextPulse = now + 0.09f;
                        }
                    }
                    else if (gauge != null) gauge.Hide();
                }

                if (now >= readyAt && (hand != null ? hand.activateInput.ReadWasPerformedThisFrame() : now >= autoAt))
                {
                    advance = Advance.Next;
                    yield break;
                }
                yield return null;
            }
        }

        // 맨 앞 카드 공개: 뒷면이 뒤집히며 등급 연출. 수퍼레어~유니크는 같은 연출(DEC-004), 레전더리만 더 크게:
        // 큰 폭죽 + 카드 흔들림 + 진동 (카메라 셰이크는 VR 멀미 우려로 쓰지 않음). 다음으로 넘길 수 있기까지의 멈춤(초)을 돌려줍니다.
        float AnnounceCard(CardRevealView view, CardData card, bool legendaryPack, XRBaseInputInteractor hand)
        {
            CardRarity tier = card.rarity == CardRarity.Unique ? CardRarity.SuperRare : card.rarity; // 연출 등급 통일
            RarityRevealProfile.Entry fx = Profile.Get(tier);
            view.Announce();
            if (fx.sound != null) AudioSource.PlayClipAtPoint(fx.sound, view.transform.position, fx.volume);
            int count = fx.hapticCount + (legendaryPack ? 1 : 0);
            if (hand != null) StartCoroutine(Haptic(hand, fx.hapticAmplitude, fx.hapticSeconds, count));

            // 카드 앞면을 가리지 않게 카드 1cm 뒤에서 퍼짐
            Vector3 behind = view.transform.position + view.transform.forward * 0.01f;
            if (card.rarity == CardRarity.Legendary)
            {
                EnsureBurst().Play(behind, view.transform.rotation, legendaryPack ? SpecialGold : RarityColor(CardRarity.Legendary),
                    legendaryPack ? 0.34f : 0.3f, legendaryPack ? 1.5f : 1.3f);
                view.Shake(legendaryShakeAmount, legendaryShakeSeconds);
            }
            else if (card.rarity >= CardRarity.SuperRare)
            {
                EnsureBurst().Play(behind, view.transform.rotation, SpecialGold, 0.11f, 0.7f);
            }
            return fx.pause + (legendaryPack ? legendaryPackExtraPause : 0f);
        }

        // 건너뛰기: queue[from..]의 남은 카드를 전 등급 건너뜁니다 (DEC-004).
        // 단일 팩이면 모두 앞면으로 뒤집혀 더미로 날아가고(결과함에 5장이 모두 있어야 하므로), 상자면 더미 높이로만 셉니다(오브젝트 없음).
        List<RevealItem> ApplySkip(List<PackResult> packs, List<RevealItem> queue, int from, Pose stack, bool isBox)
        {
            List<RevealItem> kept = RevealQueue.GodOnly(packs, queue, from);
            Vector3 back = stack.rotation * Vector3.forward;
            int flyIndex = 0;
            for (int k = from; k < queue.Count; k++)
            {
                int s = k - from;
                CardRevealView v = s < stackViews.Count ? stackViews[s] : null;
                RevealItem item = queue[k];
                if (RevealQueue.IsGodPack(packs[item.Pack]))
                {
                    if (v != null) Recycle(v); // 갓팩 카드는 마지막에 다시 하나씩 떠오르며 공개됨
                    continue;
                }
                if (isBox)
                {
                    if (v != null) Recycle(v);
                    CountOnPile(1);
                    continue;
                }
                if (v == null)
                {
                    PackResult p = packs[item.Pack];
                    v = Show(p.Cards[item.Card], p.IsNew[item.Card], stack.position + back * (stackSpacing * stackDepth), stack.rotation,
                        false, announce: false, scale: curStackScale);
                    v.SetGrabbable(false);
                }
                else v.Reveal(); // 뒷면이던 카드는 날아가며 앞면으로
                SendToPile(v, false, flyIndex++ * skipFlyStagger);
            }
            stackViews.Clear();
            return kept;
        }

        // 갓팩 예고: 큰 금빛 폭죽 + 진동 + (있으면) 효과음. 카메라는 흔들지 않음 (VR 멀미 방지).
        IEnumerator GodIntro(Pose stack, XRBaseInputInteractor hand)
        {
            context?.Events.RaiseNotice("갓팩!");
            Vector3 center = stack.position + stack.rotation * Vector3.forward * 0.01f;
            EnsureBurst().Play(center, stack.rotation, SpecialGold, 0.3f, 1.4f);
            if (godPackSound != null) AudioSource.PlayClipAtPoint(godPackSound, center, 1f);
            if (hand != null) StartCoroutine(Haptic(hand, 0.8f, 0.12f, 3));
            yield return new WaitForSeconds(godIntroSeconds);
        }

        // 맨 앞 depth장만 실체화 (Quest 성능). stackViews[k] = queue[from + k].
        void FillStack(List<PackResult> packs, List<RevealItem> queue, int from, Pose stack, int depth, bool popIn)
        {
            Vector3 back = stack.rotation * Vector3.forward;
            // 앞 카드가 빠졌으면 남은 카드를 한 칸씩 앞으로
            for (int k = 0; k < stackViews.Count; k++)
                stackViews[k].FlyTo(stack.position + back * (stackSpacing * k), stack.rotation, restackSeconds, curStackScale,
                    arcHeight: 0f, grabbableOnArrive: false);

            while (stackViews.Count < depth && from + stackViews.Count < queue.Count)
            {
                RevealItem item = queue[from + stackViews.Count];
                PackResult p = packs[item.Pack];
                Vector3 pos = stack.position + back * (stackSpacing * stackViews.Count);
                // 떠오르기: 조금 아래에서 작게 시작
                Vector3 start = popIn ? pos - (stack.rotation * Vector3.up) * 0.03f : pos;
                // 앞면으로 쌓음 (뒤집기 연출은 눈이 아파 뺌, DEC-004 수정). 묶음 뒤 카드는 앞 카드에 가려 보이지 않음
                CardRevealView v = Show(p.Cards[item.Card], p.IsNew[item.Card], start, stack.rotation, false,
                    announce: false, scale: popIn ? curStackScale * 0.6f : curStackScale);
                v.SetGrabbable(false);
                if (popIn) v.FlyTo(pos, stack.rotation, popInSeconds, curStackScale, arcHeight: 0f, grabbableOnArrive: false);
                stackViews.Add(v);
            }
        }

        // 넘긴 카드를 카운터 더미로. 단일 팩은 모두 남기고(결과함 · 보관함), 상자는 위쪽 몇 장만 남기고 아래는 덩어리로 바꿉니다.
        void SendToPile(CardRevealView v, bool isBox, float delay)
        {
            int index = pileIndex++;
            Pose p = CardPileLayout.GetPose(index, pileBasePose, isBox ? boxPileThickness : pileThickness, pileJitterAngle, pileJitterOffset);
            v.FlyTo(p.position, p.rotation, flySeconds, counterScale, grabbableOnArrive: false, delay: delay);
            if (!isBox)
            {
                pile.Add(v);
                return;
            }
            boxPile.Add(new BoxPileEntry(v, index, Time.time + delay + flySeconds));
            TrimBoxPile();
        }

        // 상자 더미: 다 내려앉은 아래쪽 카드부터 풀로 돌려보내고, 그 높이만큼 덩어리를 키웁니다.
        void TrimBoxPile()
        {
            float now = Time.time;
            while (boxPile.Count > boxPileVisible && boxPile[0].ArriveAt <= now)
            {
                BoxPileEntry e = boxPile[0];
                boxPile.RemoveAt(0);
                blockTop = Mathf.Max(blockTop, e.Index + 1);
                Recycle(e.View);
            }
            UpdatePileBlock();
        }

        // 건너뛴 카드는 오브젝트 없이 더미 높이로만 셉니다.
        void CountOnPile(int count)
        {
            pileIndex += count;
            blockTop = Mathf.Max(blockTop, pileIndex);
            UpdatePileBlock();
        }

        // 카드 옆면 색 상자 하나로 더미 아래쪽을 표시 (카드 50장을 다 두지 않음)
        void UpdatePileBlock()
        {
            if (blockTop <= 0)
            {
                if (pileBlock != null) pileBlock.gameObject.SetActive(false);
                return;
            }
            Transform block = EnsurePileBlock();
            float cardT = CardView.Thickness * counterScale;
            float h = CardPileLayout.Height(blockTop - 1, blockThickness) + cardT;
            block.gameObject.SetActive(true);
            block.SetPositionAndRotation(pileBasePose.position + Vector3.up * (h * 0.5f - cardT * 0.5f), pileBasePose.rotation);
            // 눕힌 카드 기준: 로컬 X = 가로, Y = 세로, Z = 두께(아래쪽)
            block.localScale = new Vector3(CardView.Width * counterScale * 0.98f, CardView.Height * counterScale * 0.98f, h);
        }

        Transform EnsurePileBlock()
        {
            if (pileBlock != null) return pileBlock;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BoxPileBlock";
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>()); // 보기만 함 (잡기 · 물리 X)
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material edge = EdgeMaterial();
            if (edge != null) r.sharedMaterial = edge;
            pileBlock = go.transform;
            return pileBlock;
        }

        // 카드 옆면 머티리얼(CardPrefabBuilder의 "CardEdge")을 빌려 써서 더미 덩어리가 카드 옆면처럼 보이게
        Material EdgeMaterial()
        {
            if (cardPrefab == null) return null;
            foreach (Renderer r in cardPrefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith("CardEdge")) return m;
            return null;
        }

        // 상자 결과판: 팩마다 5점 줄 → 합계 → 하이라이트 카드 (단일 팩 결과함과 같은 어두운 띠 · 등급 색)
        IEnumerator BoxSummary(List<PackResult> packs, bool skipped)
        {
            stage = PackStage.Summary;
            yield return new WaitForSeconds(flySeconds); // 마지막 카드가 더미에 내려앉는 것까지 보여 줌
            RecycleRevealArea(); // 눈앞 묶음 · 더미 정리 (카드는 모두 보유 목록에 있음)

            TextMeshPro board = EnsureBoard();
            board.gameObject.SetActive(true);
            CardRarityStyles styles = cardPrefab != null ? cardPrefab.Styles : null;

            var rows = new System.Text.StringBuilder();
            for (int i = 0; i < packs.Count; i++)
            {
                rows.Append(RevealText.BoxRow(packs[i], i + 1, styles));
                board.text = RevealText.BoxProgress(rows.ToString(), i + 1, packs.Count);
                yield return new WaitForSeconds(boxRowInterval);
            }

            board.text = RevealText.BoxSummary(packs, rows.ToString(), styles);
            int total = packs.Sum(p => p.Cards.Length);
            int newCount = packs.Sum(p => p.IsNew.Count(n => n));
            context?.Events.RaiseNotice($"상자 개봉: {packs.Count}팩 {total}장, 새 카드 {newCount}장");

            List<(int pack, int card)> best = RevealQueue.PickHighlights(packs, boxHighlightCount);
            for (int i = 0; i < best.Count; i++)
            {
                PackResult p = packs[best[i].pack];
                Show(p.Cards[best[i].card], p.IsNew[best[i].card], SlotPosition(i, best.Count), anchor.rotation, false, scale: counterScale);
            }

            // 끝: 결과판 · 하이라이트는 다음 상자 · 영업 시작 때 정리 (기존 동작)
            stage = PackStage.None;
            rayUi.Restore();
            packRoutine = null;
        }

        // ------------------------------------------------------------------ 보관 · 정리

        Pose PileBase() => new Pose(anchor.position + pileOffset, CardPileLayout.FlatRotation(anchor.forward));

        // 상자 카드 묶음 자리: 단일 팩을 눈앞에 들었을 때와 같은 곳(머리 정면 45cm, 8cm 아래). 카메라가 없으면 카운터 위.
        Pose BoxStackPose()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
                if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
                return new Pose(anchor.position + Vector3.up * 0.15f, Quaternion.LookRotation(flat.normalized, Vector3.up));
            }
            return HeadPose.InFront(cam.transform, boxStackDistance, boxStackDrop);
        }

        Color RarityColor(CardRarity rarity)
        {
            CardRarityStyles styles = cardPrefab != null ? cardPrefab.Styles : null;
            if (rarity == CardRarity.Normal || styles == null) return Color.white;
            return styles.Get(rarity).accentColor;
        }

        // 팩 틈 빛줄기: 등급이 미리 드러나지 않게 모든 팩 같은 흰색 (DEC-004)
        static Color PackBeamColor(PackResult pack) => Color.white;

        /// <summary>
        /// 카운터의 카드 더미를 보관함으로 보냅니다(연출만, 데이터는 이미 보유 목록에 있음).
        /// 결과함 UI가 닫히고, 연출이 끝나면 카드 오브젝트는 풀로 돌아갑니다.
        /// </summary>
        public void StashPile()
        {
            EndPackRoutine();
            if (pile.Count == 0 && tableCards.Count == 0) return;

            var batch = new List<CardRevealView>(pile);
            var cards = new List<CardData>(tableCards);
            cards.AddRange(batch.Select(v => v.Card));
            pile.Clear();
            tableCards.Clear();
            tableNew = 0;
            blockTop = 0;
            if (pileBlock != null) pileBlock.gameObject.SetActive(false);
            pileIndex = 0;
            foreach (CardRevealView v in batch)
            {
                shown.Remove(v);
                stashing.Add(v);
            }
            float seconds = Stash.Send(batch);
            StartCoroutine(RecycleAfter(batch, seconds));
            PileStashed?.Invoke(cards);
        }

        // 뜯기 · 넘기기 · 결과판 채우는 도중이면(더미가 다 쌓이지 않음) 보관 대신 그냥 비웁니다.
        void StashOrClear()
        {
            if (IsBusy) Clear();
            else StashPile();
        }

        IEnumerator RecycleAfter(List<CardRevealView> batch, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            foreach (CardRevealView v in batch)
            {
                if (!stashing.Remove(v)) continue; // 이미 Clear로 거둬 감
                v.Hide();
                pool.Push(v);
            }
        }

        RarityRevealProfile Profile
        {
            get
            {
                if (revealProfile == null) revealProfile = ScriptableObject.CreateInstance<RarityRevealProfile>();
                return revealProfile;
            }
        }

        static IEnumerator Haptic(XRBaseInputInteractor hand, float amplitude, float seconds, int count)
        {
            for (int k = 0; k < count; k++)
            {
                if (k > 0) yield return new WaitForSeconds(seconds + 0.06f);
                if (hand == null) yield break;
                hand.SendHapticImpulse(amplitude, seconds);
            }
        }

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

        PackResultPanel EnsureResultPanel()
        {
            if (resultPanel == null) resultPanel = PackResultPanel.Create(transform, Font);
            return resultPanel;
        }

        BoxOpenEffect EnsureBoxEffect()
        {
            if (boxEffect == null) boxEffect = BoxOpenEffect.Create(transform, 10);
            return boxEffect;
        }

        SkipHoldGauge EnsureGauge()
        {
            if (gauge == null) gauge = SkipHoldGauge.Create(transform, Font);
            return gauge;
        }

        RevealBurst EnsureBurst()
        {
            if (burst == null) burst = RevealBurst.Create(transform);
            return burst;
        }

        /// <summary>모든 연출을 멈추고 카운터를 비웁니다(보관 연출 중인 카드도 바로 거둠).</summary>
        public void Clear()
        {
            ClearShown();
            foreach (CardRevealView v in stashing)
            {
                v.Hide();
                pool.Push(v);
            }
            stashing.Clear();
        }

        // 보관함으로 날아가는 중인 카드는 건드리지 않고, 나머지 연출 · 카드만 정리합니다.
        // keepPile: 테이블에 쌓아 둔 카드(더미)는 남기고 나머지 연출만 정리
        void ClearShown(bool keepPile = false)
        {
            EndPackRoutine();
            if (tearingPack != null) Destroy(tearingPack);
            tearingPack = null;
            if (boxBoard != null) boxBoard.gameObject.SetActive(false);
            if (boxEffect != null) boxEffect.Stop();
            if (burst != null) burst.Stop();
            RecycleRevealArea(keepPile);
            for (int i = shown.Count - 1; i >= 0; i--)
            {
                CardRevealView v = shown[i];
                if (keepPile && pile.Contains(v)) continue;
                v.Hide();
                pool.Push(v);
                shown.RemoveAt(i);
            }
            if (keepPile) return;
            pile.Clear();
            tableCards.Clear();
            tableNew = 0;
            pileIndex = 0;
        }

        // 눈앞 묶음과 상자 더미(실제 카드 + 덩어리)를 풀로 돌려보냅니다. 단일 팩 더미(pile)는 그대로.
        void RecycleRevealArea(bool keepBlock = false)
        {
            foreach (CardRevealView v in stackViews) Recycle(v);
            stackViews.Clear();
            foreach (BoxPileEntry e in boxPile) Recycle(e.View);
            boxPile.Clear();
            if (keepBlock) return;
            blockTop = 0;
            if (pileBlock != null) pileBlock.gameObject.SetActive(false);
        }

        // 진행을 끝냄: 코루틴 정지, 결과함 · 게이지 닫기, 오른손 레이 UI 되돌리기
        void EndPackRoutine()
        {
            if (packRoutine != null) { StopCoroutine(packRoutine); packRoutine = null; }
            stage = PackStage.None;
            if (resultPanel != null) resultPanel.Hide();
            HidePileInfo();
            if (gauge != null) gauge.Hide();
            rayUi.Restore();
        }

        // 카운터 위 가로 한 줄의 index번째 자리 (상자 하이라이트)
        Vector3 SlotPosition(int index, int count) => anchor.position + anchor.right * ((index - (count - 1) * 0.5f) * spacing);

        CardRevealView Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, bool faceDown, bool announce = true, float scale = 1f)
        {
            CardRevealView view = pool.Count > 0 ? pool.Pop() : Create();
            view.Show(card, isNew, position, rotation, faceDown, announce, scale);
            shown.Add(view);
            return view;
        }

        // shown에서 빼서 숨기고 풀로
        void Recycle(CardRevealView view)
        {
            if (view == null) return;
            shown.Remove(view);
            view.Hide();
            pool.Push(view);
        }

        CardRevealView Create()
        {
            CardView card = Instantiate(cardPrefab, transform);
            var view = card.gameObject.AddComponent<CardRevealView>();
            view.Revealed += v => CardRevealed?.Invoke(v);
            view.GrabStarted += OnCardGrabbed;
            view.GrabEnded += OnCardReleased;
            createdCount++;
            return view;
        }
    }
}

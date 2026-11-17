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
    /// - 결과: 단일 팩은 카운터 더미 위 결과함(PackResultPanel) → 트리거로 보관함(ICardStash).
    ///   상자는 결과판(팩마다 5점 줄) + 하이라이트 카드 5장 (다음 상자 · 퇴근 · 영업 시작 때 정리).
    /// 카드 오브젝트는 풀에서 재사용하고, 묶음은 맨 앞 몇 장(stackDepth)만 실체화합니다(Quest 성능). 풀은 퇴근 후 페이즈에 미리 데웁니다.
    /// 카드 데이터는 개봉 시점에 이미 보유 목록에 들어가 있어, 여기의 연출은 보여 주기만 합니다(건너뛰거나 끊겨도 데이터는 같음).
    /// </summary>
    public sealed class CardRevealer : MonoBehaviour
    {
        enum PackStage { None, Tearing, Flipping, Result, Summary }
        enum Advance { Next, Skip }

        // 카드 프리팹 크기 (CardPrefabBuilder.Width · Height, 두께 1.2mm). 에디터 어셈블리라 직접 참조하지 못해 같은 값을 둡니다.
        const float CardWidth = 0.063f, CardHeight = 0.088f, CardThickness = 0.0012f;
        static readonly Color SpecialGold = new Color(1f, 0.82f, 0.3f);
        const string GoldHex = "FFD54A";
        const string NewHex = "7CF29A";

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
        [Tooltip("마지막 장을 넘긴 뒤 '보관함에 넣기' 입력을 무시하는 시간 (초). 넘기기 눌림이 곧바로 넣기가 되지 않게")]
        [SerializeField] float resultInputLock = 0.4f;

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
        readonly List<CardRevealView> stashing = new List<CardRevealView>();   // 보관함으로 날아가는 중
        readonly List<CardRevealView> stackViews = new List<CardRevealView>(); // 눈앞 묶음에 실체화된 카드 (맨 앞부터)
        readonly List<BoxPileEntry> boxPile = new List<BoxPileEntry>();        // 상자: 더미 위쪽에 실제 카드로 남은 것
        readonly Stack<CardRevealView> pool = new Stack<CardRevealView>();
        readonly RevealInputGate inputGate = new RevealInputGate();
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
        int blockTop;    // 상자 더미에서 덩어리 하나로 표시하는 아래쪽 카드 수
        int createdCount;

        // 연출 중 오른손 레이의 UI 클릭을 잠시 끔 (트리거 하나가 넘기기 · 넣기 · UI 클릭을 동시에 하지 않게)
        XRBaseInputInteractor uiSuppressedHand;
        bool uiWasEnabled;

        /// <summary>팩이 뜯기거나, 카드를 넘기는 중이거나, 상자 결과판이 채워지는 중. 결과함 UI만 떠 있는 동안은 바쁘지 않습니다(새 개봉 시 자동 보관).</summary>
        public bool IsBusy => stage == PackStage.Tearing || stage == PackStage.Flipping || stage == PackStage.Summary;

        /// <summary>카운터에 아직 보관하지 않은 카드 더미가 있음 (단일 팩).</summary>
        public bool HasPile => pile.Count > 0;

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
            RestoreRightRayUI();
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
            StashOrClear(); // 이전 더미는 자동으로 보관함에
            ClearShown();
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
            stage = PackStage.Tearing;
            XRBaseInputInteractor hand = HandFinder.Find(InteractorHandedness.Right);
            SuppressRightRayUI(hand);
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
                foreach (CardRevealView v in stackViews)
                    v.FlyTo(v.transform.position + up * cardRise, stack.rotation, riseSeconds, stackScale, arcHeight: 0f, grabbableOnArrive: false);
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
                if (stackViews.Count == 0) FillStack(packs, queue, qi, stack, stackDepth, popIn: false);

                CardRevealView front = stackViews[0];
                CardData card = p.Cards[item.Card];
                bool confirmed = p.Special == SpecialPackType.LegendaryPack && card.rarity == CardRarity.Legendary;
                float pause = AnnounceCard(front, card, confirmed, hand);
                yield return WaitForAdvance(hand, pause, true, gaugePose);
                stackViews.RemoveAt(0);
                SendToPile(front, isBox, 0f);
                qi++;

                if (advance == Advance.Skip && !skipped)
                {
                    ApplySkip(packs, queue, qi, stack, isBox);
                    qi = queue.Count;
                    skipped = true;
                    context?.Events.RaiseNotice("건너뛰기!");
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

            inputGate.Lock(Time.time, resultInputLock); // 마지막 장을 넘긴 눌림이 곧바로 "넣기"가 되지 않게
            yield return new WaitForSeconds(flySeconds);

            // 결과함 UI: 더미 위, 월드 고정
            stage = PackStage.Result;
            EnsureResultPanel().Show(packs[0], cardPrefab != null ? cardPrefab.Styles : null,
                pileBasePose.position + Vector3.up * resultPanelHeight, anchor.rotation, resultFontSize, resultPopSeconds);
            inputGate.Lock(Time.time, resultPopSeconds); // 결과함이 완전히 뜬 뒤에만 반응
            if (hand == null) { packRoutine = null; yield break; } // 손이 없으면 다음 개봉 · 퇴근 때 자동 보관

            // 결과함이 떠 있는 동안 더미 카드를 잡아서 놓으며 살펴볼 수 있음 (잡았다 놓으면 놓은 자리에 둠)
            foreach (CardRevealView v in pile) v.SetGrabbable(true);

            yield return new WaitUntil(() => hand == null || (hand.activateInput.ReadWasPerformedThisFrame() && inputGate.IsOpen(Time.time)));
            if (hand != null) hand.SendHapticImpulse(0.35f, 0.06f);
            packRoutine = null; // 아래 StashPile이 실행 중인 자기 자신을 멈추지 않게
            StashPile();
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
        void ApplySkip(List<PackResult> packs, List<RevealItem> queue, int from, Pose stack, bool isBox)
        {
            Vector3 back = stack.rotation * Vector3.forward;
            int flyIndex = 0;
            for (int k = from; k < queue.Count; k++)
            {
                int s = k - from;
                CardRevealView v = s < stackViews.Count ? stackViews[s] : null;
                RevealItem item = queue[k];
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
                        false, announce: false, scale: stackScale);
                    v.SetGrabbable(false);
                }
                else v.Reveal(); // 뒷면이던 카드는 날아가며 앞면으로
                SendToPile(v, false, flyIndex++ * skipFlyStagger);
            }
            stackViews.Clear();
        }

        // 맨 앞 depth장만 실체화 (Quest 성능). stackViews[k] = queue[from + k].
        void FillStack(List<PackResult> packs, List<RevealItem> queue, int from, Pose stack, int depth, bool popIn)
        {
            Vector3 back = stack.rotation * Vector3.forward;
            // 앞 카드가 빠졌으면 남은 카드를 한 칸씩 앞으로
            for (int k = 0; k < stackViews.Count; k++)
                stackViews[k].FlyTo(stack.position + back * (stackSpacing * k), stack.rotation, restackSeconds, stackScale,
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
                    announce: false, scale: popIn ? stackScale * 0.6f : stackScale);
                v.SetGrabbable(false);
                if (popIn) v.FlyTo(pos, stack.rotation, popInSeconds, stackScale, arcHeight: 0f, grabbableOnArrive: false);
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
            float cardT = CardThickness * counterScale;
            float h = CardPileLayout.Height(blockTop - 1, boxPileThickness) + cardT;
            block.gameObject.SetActive(true);
            block.SetPositionAndRotation(pileBasePose.position + Vector3.up * (h * 0.5f - cardT * 0.5f), pileBasePose.rotation);
            // 눕힌 카드 기준: 로컬 X = 가로, Y = 세로, Z = 두께(아래쪽)
            block.localScale = new Vector3(CardWidth * counterScale * 0.98f, CardHeight * counterScale * 0.98f, h);
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
            Func<CardRarity, string> colorize = r => RarityHex(styles, r);

            var rows = new System.Text.StringBuilder();
            for (int i = 0; i < packs.Count; i++)
            {
                PackResult p = packs[i];
                rows.Append(RevealQueue.DotLine(p, i + 1, colorize, NewHex));
                if (p.Special != SpecialPackType.None) rows.Append($"  <color=#{GoldHex}>{SpecialLabel(p.Special)}!</color>");
                rows.Append('\n');
                board.text = Mark($"<b>상자 개봉 {i + 1}/{packs.Count}</b>\n{rows}");
                yield return new WaitForSeconds(boxRowInterval);
            }

            int total = packs.Sum(p => p.Cards.Length);
            int newCount = packs.Sum(p => p.IsNew.Count(n => n));
            CardRarity top = packs.Max(p => p.BestRarity);
            int superRares = packs.Sum(p => p.Cards.Count(c => c.rarity == CardRarity.SuperRare));
            string legend = $"<size=70%><color=#C9C3D9>● 노말·레어  <color=#{GoldHex}>◆</color> 수퍼레어  ★ 유니크 이상</color></size>";
            board.text = Mark($"<b>상자 개봉 완료 {packs.Count}/{packs.Count}</b>\n{rows}" +
                              $"<b>합계 {total}장 · 새 카드 {newCount}장 · 최고 {RarityTag(styles, top)}</b>" +
                              (superRares > 0 ? $"  <color=#{GoldHex}>수퍼레어 {superRares}장</color>" : "") +
                              $"\n{legend}");
            context?.Events.RaiseNotice($"상자 개봉: {packs.Count}팩 {total}장, 새 카드 {newCount}장");

            List<(int pack, int card)> best = RevealQueue.PickHighlights(packs, boxHighlightCount);
            for (int i = 0; i < best.Count; i++)
            {
                PackResult p = packs[best[i].pack];
                Show(p.Cards[best[i].card], p.IsNew[best[i].card], SlotPosition(i, best.Count), anchor.rotation, false, scale: counterScale);
            }

            // 끝: 결과판 · 하이라이트는 다음 상자 · 영업 시작 때 정리 (기존 동작)
            stage = PackStage.None;
            inputGate.Reset();
            RestoreRightRayUI();
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
            Transform head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = head.up; // 바로 아래를 보고 있을 때
            forward.Normalize();
            return new Pose(head.position + forward * boxStackDistance + Vector3.down * boxStackDrop, Quaternion.LookRotation(forward, Vector3.up));
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
            if (pile.Count == 0) return;

            var batch = new List<CardRevealView>(pile);
            var cards = batch.Select(v => v.Card).ToList();
            pile.Clear();
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

        /// <summary>등급 이름을 등급 색으로 (결과판 · 결과함 공용).</summary>
        internal static string RarityTag(CardRarityStyles styles, CardRarity r)
        {
            if (styles == null) return r.ToString();
            CardRarityStyles.Style s = styles.Get(r);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(s.accentColor)}>{s.label}</color>";
        }

        /// <summary>5점 줄 기호 색 (RRGGBB). 수퍼레어는 결과판에서 눈에 띄도록 금빛 고정. 스타일이 없으면 null(색 없음).</summary>
        internal static string RarityHex(CardRarityStyles styles, CardRarity r)
        {
            if (r == CardRarity.SuperRare) return GoldHex;
            return styles == null ? null : ColorUtility.ToHtmlStringRGB(styles.Get(r).accentColor);
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
        void ClearShown()
        {
            EndPackRoutine();
            if (tearingPack != null) Destroy(tearingPack);
            tearingPack = null;
            if (boxBoard != null) boxBoard.gameObject.SetActive(false);
            if (boxEffect != null) boxEffect.Stop();
            if (burst != null) burst.Stop();
            RecycleRevealArea();
            foreach (CardRevealView v in shown)
            {
                v.Hide();
                pool.Push(v);
            }
            shown.Clear();
            pile.Clear();
            pileIndex = 0;
        }

        // 눈앞 묶음과 상자 더미(실제 카드 + 덩어리)를 풀로 돌려보냅니다. 단일 팩 더미(pile)는 그대로.
        void RecycleRevealArea()
        {
            foreach (CardRevealView v in stackViews) Recycle(v);
            stackViews.Clear();
            foreach (BoxPileEntry e in boxPile) Recycle(e.View);
            boxPile.Clear();
            blockTop = 0;
            if (pileBlock != null) pileBlock.gameObject.SetActive(false);
        }

        // 진행을 끝냄: 코루틴 정지, 결과함 · 게이지 닫기, 오른손 레이 UI 되돌리기
        void EndPackRoutine()
        {
            if (packRoutine != null) { StopCoroutine(packRoutine); packRoutine = null; }
            stage = PackStage.None;
            inputGate.Reset();
            if (resultPanel != null) resultPanel.Hide();
            if (gauge != null) gauge.Hide();
            RestoreRightRayUI();
        }

        // 오른손 트리거는 Ray UI 클릭에도 쓰입니다. 연출 중에는 오른손 레이의 UI 상호작용만 끄고(잡기 · 근접은 그대로), 끝나면 원래대로.
        void SuppressRightRayUI(XRBaseInputInteractor hand)
        {
            if (uiSuppressedHand != null || hand == null) return;
            if (hand is NearFarInteractor nearFar)
            {
                uiWasEnabled = nearFar.enableUIInteraction;
                nearFar.enableUIInteraction = false;
            }
            else if (hand is XRRayInteractor ray)
            {
                uiWasEnabled = ray.enableUIInteraction;
                ray.enableUIInteraction = false;
            }
            else return;
            uiSuppressedHand = hand;
        }

        void RestoreRightRayUI()
        {
            if (uiSuppressedHand == null) { uiSuppressedHand = null; return; } // 파괴된 경우도 정리
            if (uiSuppressedHand is NearFarInteractor nearFar) nearFar.enableUIInteraction = uiWasEnabled;
            else if (uiSuppressedHand is XRRayInteractor ray) ray.enableUIInteraction = uiWasEnabled;
            uiSuppressedHand = null;
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
            createdCount++;
            return view;
        }
    }
}

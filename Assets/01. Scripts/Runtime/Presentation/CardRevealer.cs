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
    /// 카드 공개 연출의 진행 순서. 단일 팩과 상자(10팩)가 같은 진행기 <see cref="PackSequence"/> 하나를 씁니다 (시연 피드백 A4 · B1 · B2 · B3).
    /// 파이프라인: ① 개봉 연출 → ② 공개 대기열 → ③ 결과.
    /// - ① 단일 팩은 뜯김(PackTearEffect) 후 카드 묶음이 솟아오름. 상자는 눈앞 한 줄에서 10팩이 차례로 뜯김(BoxOpenEffect).
    /// - ② 오른손 트리거 1번 = 1장, 꾹 누르면 전 등급 건너뛰기 (DEC-004). 갓팩은 건너뛸 수 없음 (DEC-008).
    /// - ③ 단일 팩은 카운터 더미 위 결과함. 개봉한 카드는 퇴근 전까지 카운터에 쌓아 두고, 퇴근하면 보관함으로. 상자는 결과판 + 하이라이트,
    ///   상자 카드 더미도 단일 팩처럼 카운터에 남음 (11-19 플레이 피드백).
    /// 이 클래스는 순서 · 페이즈 반응 · 정리만 맡고, 일은 부품이 합니다 (AI-005: 거대 클래스 방지).
    ///   <see cref="RevealStack"/> 눈앞 묶음 · <see cref="RevealInput"/> 넘기기 입력 · <see cref="RevealFx"/> 등급 연출 ·
    ///   <see cref="CounterPileView"/> 카운터 더미 · <see cref="BoxResultBoard"/> 상자 결과판 · <see cref="PileInfoPopup"/> 더미 정보 판 ·
    ///   <see cref="CardRevealPool"/> 카드 재사용. 부품은 이 클래스를 모르고, 숫자는 부품마다 인스펙터 묶음(Settings)으로 둡니다.
    /// 카드 데이터는 개봉 시점에 이미 보유 목록에 들어가 있어, 연출은 보여 주기만 합니다(건너뛰거나 끊겨도 데이터는 같음).
    /// </summary>
    public sealed class CardRevealer : MonoBehaviour
    {
        enum PackStage { None, Tearing, Flipping, Result, Summary }

        [SerializeField] CardView cardPrefab;
        [Tooltip("카드가 놓일 기준점. 카드 앞면(-Z)이 플레이어를 향하도록 회전")]
        [SerializeField] Transform anchor;
        [Tooltip("퇴근 후 페이즈가 되면 미리 만들어 두는 카드 수 (상자 개봉 때 끊김 방지)")]
        [SerializeField] int warmPoolCount = 12;

        [Header("상자 일괄 개봉 (B3)")]
        [Tooltip("상자 카드 묶음 자리: 머리에서 정면으로 (m). 단일 팩을 눈앞에 들었을 때(TearGestureOpen)와 같게")]
        [SerializeField] float boxStackDistance = 0.45f;
        [SerializeField] float boxStackDrop = 0.08f;
        [Tooltip("상자 팩 뜯는 소리. 비워 두면 없음")]
        [SerializeField] AudioClip boxTearSound;

        [Header("결과함 UI (B2)")]
        [Tooltip("더미 바닥에서 결과함 UI 아래쪽까지 높이 (m)")]
        [SerializeField] float resultPanelHeight = 0.07f;
        [Tooltip("TMP 3D 글자 크기 (1 ≈ 10cm)")]
        [SerializeField] float resultFontSize = 0.2f;
        [SerializeField] float resultPopSeconds = 0.25f;

        [Header("부품별 설정")]
        [Tooltip("눈앞 카드 묶음 (크기 · 간격 · 솟아오르기)")]
        [SerializeField] RevealStack.Settings stackSettings = new RevealStack.Settings();
        [Tooltip("넘기기 입력 (꾹 누르기 시간 · 게이지 · 진동)")]
        [SerializeField] RevealInput.Settings inputSettings = new RevealInput.Settings();
        [Tooltip("등급 연출 (DEC-004: 수퍼레어~유니크 통일, 레전더리만 더 크게)")]
        [SerializeField] RevealFx.Settings fxSettings = new RevealFx.Settings();
        [Tooltip("카운터 카드 더미 (B1: 쌓기 · 옆면 덩어리 · 보관함 연출)")]
        [SerializeField] CounterPileView.Settings pileSettings = new CounterPileView.Settings();
        [Tooltip("상자 결과판 · 하이라이트 카드")]
        [SerializeField] BoxResultBoard.Settings boardSettings = new BoxResultBoard.Settings();
        [Tooltip("더미 카드를 잡으면 뜨는 정보 판")]
        [SerializeField] PileInfoPopup.Settings pileInfoSettings = new PileInfoPopup.Settings();

        CardRevealPool cardPool;
        RevealStack revealStack;
        RevealInput input;
        RevealFx fx;
        CounterPileView counterPile;
        BoxResultBoard boxBoard;
        PileInfoPopup pileInfo;
        PackResultPanel resultPanel;
        BoxOpenEffect boxEffect;
        GameContext context;
        Coroutine packRoutine;
        Coroutine warmRoutine;
        PackStage stage;
        GameObject tearingPack;

        // 연출 중 오른손 레이의 UI 클릭을 잠시 끔 (트리거 하나가 넘기기 · 넣기 · UI 클릭을 동시에 하지 않게)
        readonly RayUiSuppressor rayUi = new RayUiSuppressor();

        /// <summary>팩이 뜯기거나, 카드를 넘기는 중이거나, 상자 결과판이 채워지는 중. 결과함 UI만 떠 있는 동안은 바쁘지 않습니다(새 개봉 시 자동 보관).</summary>
        public bool IsBusy => stage == PackStage.Tearing || stage == PackStage.Flipping || stage == PackStage.Summary;

        /// <summary>카운터에 아직 보관하지 않은 카드 더미가 있음 (단일 팩 · 다 넘긴 상자).</summary>
        public bool HasPile => Pile.HasCards;

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

        /// <summary>더미가 보관함으로 보내졌을 때 (아래 카드부터). 데이터는 이미 보유 목록에 있으므로 표시용입니다.</summary>
        public event Action<IReadOnlyList<CardData>> PileStashed;

        public void Configure(CardView prefab, Transform cardAnchor, RarityRevealProfile profile = null)
        {
            cardPrefab = prefab;
            anchor = cardAnchor;
            fxSettings.Profile = profile;
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
            // 이전 더미는 퇴근 전까지 테이블에 둠. 한 더미가 다 차면 옆에 새 더미를 쌓음 (11-19 피드백: 가득 차도 사라지지 않게).
            // 자리가 다 차면 마지막 더미에 얇게 계속 쌓음
            ClearShown(keepPile: true);
            if (Pile.ColumnFull && Pile.CanAddColumn) Pile.StartNewColumn();
            packRoutine = StartCoroutine(PackSequence(new List<PackResult> { pack }, stackPose, tear));
        }

        /// <summary>상자 10팩: 일괄 개봉 연출 → 50장 한 장씩(꾹 = 건너뛰기) → 결과판 + 하이라이트.</summary>
        public void RevealBox(PackResult[] packs)
        {
            if (packs == null || packs.Length == 0) return;
            // 상자는 빈 더미에서 시작: 쌓인 카드가 있으면 옆에 새 더미로, 더 놓을 자리가 없으면 보관함으로
            if (Pile.CurrentHasCards)
            {
                if (Pile.CanAddColumn) Pile.StartNewColumn();
                else StashOrClear();
            }
            ClearShown(keepPile: true);
            packRoutine = StartCoroutine(PackSequence(new List<PackResult>(packs), BoxStackPose(), null));
        }

        /// <summary>
        /// 카드 오브젝트(TMP · Rigidbody · XRGrabInteractable)를 미리 만들어 풀에 넣습니다. 한 프레임에 한 장씩이라 끊기지 않습니다.
        /// 퇴근 후(AfterHours) 진입 때 저절로 불립니다.
        /// </summary>
        public void WarmUp()
        {
            EnsureBoxEffect();
            TriggerInput.Prewarm();
            Fx.Prewarm();
            if (warmRoutine == null && isActiveAndEnabled) warmRoutine = StartCoroutine(WarmPool());
        }

        IEnumerator WarmPool()
        {
            while (cardPrefab != null && Pool.CreatedCount < warmPoolCount)
            {
                Pool.AddSpare();
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
            rayUi.Suppress(hand); // 연출 중 오른손 트리거가 Ray UI 클릭까지 하지 않게
            TriggerInput.Begin(); // 자르기 제스처로 트리거를 누른 채 열렸을 수 있어 한 번 뗄 때까지 꾹 누르기를 세지 않음
            Pile.Begin(anchor, isBox);
            CardStack.Begin(stack, !isBox && tear != null ? tear.transform.lossyScale.y : (float?)null);
            List<RevealItem> queue = RevealQueue.BuildAll(packs);

            // ① 개봉 연출
            if (!isBox)
            {
                // 카드 묶음은 팩 안(팩 앞면 뒤)에서 시작해 팩이 빠지는 동안 위로 솟습니다.
                CardStack.Fill(packs, queue, 0, popIn: false);
                if (tear != null)
                {
                    tearingPack = tear.gameObject;
                    float delay = tear.Play(); // 뜯는 순간 등급이 미리 드러나지 않게 흰 빛 없이 (DEC-004)
                    if (delay > 0f) yield return new WaitForSeconds(delay);
                }
                float rise = CardStack.Rise(); // 팩에서 빠져나오며 커짐. 이후 넘기기 · 새로 채우는 카드도 이 크기
                stack = CardStack.Pose;
                yield return new WaitForSeconds(rise);
            }
            else
            {
                Transform head = Camera.main != null ? Camera.main.transform : null;
                Vector3 headPos = head != null ? head.position : stack.position - stack.rotation * Vector3.forward * boxStackDistance;
                Vector3 forward = stack.rotation * Vector3.forward;
                float seconds = EnsureBoxEffect().Play(packs.Count, headPos, forward, stack, boxTearSound);
                yield return new WaitForSeconds(seconds);
                CardStack.Fill(packs, queue, 0, popIn: true);
                yield return new WaitForSeconds(CardStack.PopInSeconds);
            }

            // ② 공개 대기열: 트리거 1번 = 1칸, 꾹 = 건너뛰기
            stage = PackStage.Flipping;
            float hold = TriggerInput.HoldSeconds;
            context?.Events.RaiseNotice(isBox
                ? $"오른손 트리거로 한 장씩 넘기세요 ({queue.Count}칸) · {hold:0.#}초 꾹: 건너뛰기"
                : $"오른손 트리거로 한 장씩 넘기세요 · {hold:0.#}초 꾹: 건너뛰기");
            Pose gaugePose = TriggerInput.GaugePose(stack);
            bool skipped = false;
            int qi = 0;
            while (qi < queue.Count)
            {
                RevealItem item = queue[qi];
                PackResult p = packs[item.Pack];
                if (CardStack.IsEmpty) CardStack.Fill(packs, queue, qi, popIn: skipped);

                bool godPack = RevealQueue.IsGodPack(p);
                if (godPack && item.Card == 0) // 갓팩: 특수 효과 후 한 장씩 (DEC-008)
                {
                    context?.Events.RaiseNotice("갓팩!");
                    yield return new WaitForSeconds(Fx.GodIntro(stack, hand));
                }

                CardData card = p.Cards[item.Card];
                bool confirmed = p.Special == SpecialPackType.LegendaryPack && card.rarity == CardRarity.Legendary;
                float pause = Fx.Announce(CardStack.Front, card, confirmed, hand);
                yield return TriggerInput.WaitForAdvance(hand, pause, !godPack, gaugePose);
                Pile.Send(CardStack.TakeFront(), isBox, 0f);
                qi++;

                if (TriggerInput.Last == RevealInput.Result.Skip && !skipped)
                {
                    queue = CardStack.Skip(packs, queue, qi, isBox);
                    qi = 0;
                    skipped = true;
                    context?.Events.RaiseNotice(queue.Count > 0 ? "건너뛰기! 갓팩은 끝까지 열어 봐요" : "건너뛰기!");
                }
                else CardStack.Fill(packs, queue, qi, popIn: false);
            }
            TriggerInput.HideGauge();

            // ③ 결과
            if (isBox)
            {
                yield return BoxSummary(packs);
                yield break;
            }

            // 마지막 카드가 더미에 내려앉는 것까지 (건너뛰면 카드가 조금씩 늦게 출발하므로 그만큼 더 기다림 — 날아가는 중에 잡히지 않게)
            yield return new WaitForSeconds(Pile.FlySeconds + (skipped ? CardStack.SkipDelay(packs[0].Cards.Length) : 0f));
            Pile.Compress(); // 실제 카드로 두는 수를 넘으면 오래된 카드부터 옆면 덩어리로

            // 결과함 UI: 이번 팩 카드 더미 위, 월드 고정. 카드는 퇴근 전까지 테이블에 남아 있고 잡아서 볼 수 있음.
            stage = PackStage.Result;
            float packBase = Pile.HeightAt(Pile.NextIndex - packs[0].Cards.Length);
            EnsureResultPanel().Show(packs[0], Styles,
                Pile.BasePose.position + Vector3.up * (packBase + resultPanelHeight), anchor.rotation, resultFontSize, resultPopSeconds);
            foreach (CardRevealView v in Pile.Cards) v.SetGrabbable(true);
            rayUi.Restore(); // 연출 중 꺼 둔 오른손 레이 UI 복귀 (결과 단계에서는 트리거를 쓰지 않음)
            packRoutine = null;
            context?.Events.RaiseHappened(GameMoment.RevealFinished);
        }

        // 상자 결과판: 팩마다 5점 줄 → 합계 → 하이라이트 카드. 결과판 · 하이라이트는 다음 상자 · 영업 시작 때 정리
        IEnumerator BoxSummary(List<PackResult> packs)
        {
            stage = PackStage.Summary;
            yield return new WaitForSeconds(Pile.FlySeconds); // 마지막 카드가 더미에 내려앉는 것까지 보여 줌
            CardStack.Clear();
            Pile.SettleBox(); // 상자 더미는 단일 팩처럼 카운터에 남김: 위 카드는 잡아 볼 수 있고, 이어서 여는 팩은 그 위에, 퇴근 때 보관함으로

            XRBaseInputInteractor hand = HandFinder.Find(InteractorHandedness.Right);
            yield return BoxBoard.Show(packs, (v, card) => Fx.Announce(v, card, false, hand)); // 하이라이트 카드도 단일 팩과 같은 등급 연출
            int total = packs.Sum(p => p.Cards.Length);
            int newCount = packs.Sum(p => p.IsNew.Count(n => n));
            context?.Events.RaiseNotice($"상자 개봉: {packs.Count}팩 {total}장, 새 카드 {newCount}장");

            stage = PackStage.None;
            rayUi.Restore();
            packRoutine = null;
            context?.Events.RaiseHappened(GameMoment.RevealFinished);
        }

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

        // ------------------------------------------------------------------ 보관 · 정리

        /// <summary>
        /// 카운터의 카드 더미를 보관함으로 보냅니다(연출만, 데이터는 이미 보유 목록에 있음).
        /// 결과함 UI가 닫히고, 연출이 끝나면 카드 오브젝트는 풀로 돌아갑니다.
        /// </summary>
        public void StashPile()
        {
            EndPackRoutine();
            List<CardData> cards = Pile.Stash();
            if (cards != null) PileStashed?.Invoke(cards);
        }

        // 뜯기 · 넘기기 · 결과판 채우는 도중이면(더미가 다 쌓이지 않음) 보관 대신 그냥 비웁니다.
        void StashOrClear()
        {
            if (IsBusy) Clear();
            else StashPile();
        }

        /// <summary>모든 연출을 멈추고 카운터를 비웁니다(보관 연출 중인 카드도 바로 거둠).</summary>
        public void Clear()
        {
            ClearShown();
            Pile.StopStashing();
        }

        // 보관함으로 날아가는 중인 카드는 건드리지 않고, 나머지 연출 · 카드만 정리합니다.
        // keepPile: 테이블에 쌓아 둔 카드(더미)는 남기고 나머지 연출만 정리
        void ClearShown(bool keepPile = false)
        {
            EndPackRoutine();
            if (tearingPack != null) Destroy(tearingPack);
            tearingPack = null;
            if (boxBoard != null) boxBoard.Hide();
            if (boxEffect != null) boxEffect.Stop();
            Fx.Stop();
            RecycleRevealArea(keepPile);
            if (keepPile) Pool.RecycleAll(Pile.Contains);
            else
            {
                Pool.RecycleAll();
                Pile.Reset();
            }
        }

        // 눈앞 묶음과 상자 더미(실제 카드 + 덩어리)를 풀로 돌려보냅니다. 단일 팩 더미는 그대로.
        void RecycleRevealArea(bool keepBlock = false)
        {
            CardStack.Clear();
            Pile.ClearBox(keepBlock);
        }

        // 진행을 끝냄: 코루틴 정지, 결과함 · 정보 판 · 게이지 닫기, 오른손 레이 UI 되돌리기
        void EndPackRoutine()
        {
            if (packRoutine != null) { StopCoroutine(packRoutine); packRoutine = null; }
            stage = PackStage.None;
            if (resultPanel != null) resultPanel.Hide();
            if (pileInfo != null) pileInfo.Hide();
            TriggerInput.HideGauge();
            rayUi.Restore();
        }

        // 더미 카드를 잡으면 정보 판 (카드를 넘기는 중에는 띄우지 않음)
        void OnCardGrabbed(CardRevealView v)
        {
            if (stage == PackStage.Tearing || stage == PackStage.Flipping) return;
            PileInfo.OnGrabbed(v);
        }

        // ------------------------------------------------------------------ 부품 (처음 쓸 때 만듦)

        CardRarityStyles Styles => cardPrefab != null ? cardPrefab.Styles : null;

        CardRevealPool Pool => cardPool ??= new CardRevealPool(Create);
        RevealStack CardStack => revealStack ??= new RevealStack(Pool, Pile, stackSettings);
        RevealInput TriggerInput => input ??= new RevealInput(() => SkipHoldGauge.Create(transform, Font), inputSettings);
        RevealFx Fx => fx ??= new RevealFx(this, () => Styles, fxSettings);
        CounterPileView Pile => counterPile ??= new CounterPileView(this, Pool, EdgeMaterial, pileSettings);
        BoxResultBoard BoxBoard => boxBoard ??= new BoxResultBoard(transform, anchor, Pool, () => Font, () => Styles, boardSettings);
        PileInfoPopup PileInfo => pileInfo ??= new PileInfoPopup(this, anchor, Pile, () => Font, () => Styles, pileInfoSettings);

        PackResultPanel EnsureResultPanel() => resultPanel != null ? resultPanel : resultPanel = PackResultPanel.Create(transform, Font);
        BoxOpenEffect EnsureBoxEffect() => boxEffect != null ? boxEffect : boxEffect = BoxOpenEffect.Create(transform, 10);

        CardRevealView Create()
        {
            CardView card = Instantiate(cardPrefab, transform);
            var view = card.gameObject.AddComponent<CardRevealView>();
            view.GrabStarted += OnCardGrabbed;
            view.GrabEnded += _ => PileInfo.OnReleased();
            return view;
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
    }
}

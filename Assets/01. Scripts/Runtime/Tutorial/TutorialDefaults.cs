using System.Collections.Generic;
using CardShop.Core;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 기본 튜토리얼 대본 (튜토리얼_설계 5장). TutorialSequence 에셋을 넣지 않으면 이 내용을 씁니다.
    /// 사장님은 가게에서 "알바야"라고 부르며 반말로 가르치고(9장 Q6), 집에서는 알바생 혼잣말로 안내합니다.
    /// 정지(Hold)는 처음 하는 업무마다: 첫 계산 · 첫 쓰레기 · 첫 마감 청소 · 첫 흐트러진 팩 · 첫 얼룩 · 판매 등록 카드 첫 손님 · 첫 도둑
    /// (DEC-028이 DEC-026의 "2번만"을 바꿈, 테스트로 고정). 정지마다 행동 1개만 시키고 바로 풉니다.
    /// 숫자는 {packPrice} 같은 자리표시로 두고 TutorialDirector가 설정값으로 채웁니다.
    /// </summary>
    public static class TutorialDefaults
    {
        // HighlightTarget 키 (TutorialTargets가 씬 물건에 붙임)
        public const string GiftPack = "gift-pack";
        public const string ShopClock = "shop-clock";
        public const string CounterItems = "counter-items";
        public const string Scanner = "scanner";
        public const string PaperBag = "paper-bag";
        public const string PosPay = "pos-pay";
        public const string Trash = "trash";
        public const string TrashBin = "trash-bin";
        public const string MessyPack = "messy-pack";
        public const string PackRack = "pack-rack";
        public const string Thief = "thief";
        public const string Broom = "broom";
        public const string Stain = "stain";
        public const string SettlementBoard = "settlement-board";
        public const string FreePack = "free-pack";
        public const string PackShelf = "pack-shelf";
        public const string ClockOut = "clock-out";
        public const string BoxButton = "box-button";
        public const string Binder = "binder";
        public const string BedButton = "bed-button";
        public const string SalesPanel = "sales-panel";
        public const string Community = "community-screen";

        /// <summary>첫 계산 정지 단계 key (끝나기 전이면 첫 손님 물건을 1개로 고정, 9장 Q2)</summary>
        public const string PayHoldKey = "pay-hold";

        /// <summary>기다림 · 정지 단계의 안전 시간 (초). 신호가 끝내 오지 않아도 진행이 영영 멈추지 않게 (Intro는 이게 없으면 출시 빌드에서 못 넘어감)</summary>
        public const float SafetySeconds = 120f;

        public static List<TutorialStep> Steps() => new List<TutorialStep>
        {
            // ---------------------------------------------------------------- Intro (1일차 출근, 시간 없음)
            Say("intro-hello", GamePhaseId.Intro, "어서 와, 알바야! 오늘부터 7일이야.\n잘 버티면 정직원 시켜 줄게!", 4.5f, day: 1),
            Then(Say("intro-menu", GamePhaseId.Intro, "급할 땐 왼손 메뉴 버튼.\n다 아는 거면 거기서 안내를 건너뛰어.", 4.5f, day: 1), "intro-hello"),
            Then(Wait("intro-grab", GamePhaseId.Intro, TutorialSignal.GiftPackGrabbed,
                "첫날 기념 선물이야!\n왼손으로 이 팩을 꽉 잡아 봐.", "왼손 가운뎃손가락 그립 버튼을 꾹!", GiftPack), "intro-menu"),
            Then(Wait("intro-tear", GamePhaseId.Intro, TutorialSignal.GiftPackOpened,
                "오른손 트리거를 누른 채\n윗부분 흰 선을 쭉 그어.", "오른손 검지 트리거를 누른 채 흰 선을 따라 옆으로!", GiftPack), "intro-grab"),
            Then(Wait("intro-flip", GamePhaseId.Intro, TutorialSignal.RevealFinished,
                "트리거 한 번에 한 장씩.\n꾹 누르면 빨리 넘어가.", "오른손 트리거를 한 번 눌러 봐."), "intro-tear"),
            Then(Say("intro-done", GamePhaseId.Intro, "뽑은 카드는 퇴근할 때 바인더로 가.\n자, 이제 문 연다!", 4.5f, day: 1), "intro-flip"),

            // ---------------------------------------------------------------- 영업 1일차
            Say("biz-open", GamePhaseId.Business, "시계가 다 돌면 문 닫는 거야.\n손님 오면 계산부터!", 4.5f, day: 1, targets: ShopClock),
            // 정지 1: 첫 계산 (찍기 → 봉투 → 결제)
            new TutorialStep
            {
                key = PayHoldKey, phase = GamePhaseId.Business, startOn = TutorialSignal.CheckoutStarted, kind = TutorialStepKind.Hold,
                seconds = 3.5f, line = "첫 손님이다! 잠깐 기다려 달라고\n할게. 같이 해 보자.", targets = new[] { CounterItems },
            },
            Then(Hold("pay-scan", TutorialSignal.AllScanned, "스캐너를 잡고 트리거로\n물건 바코드를 찍어.",
                "스캐너를 그립으로 잡고, 빨간 빛을 바코드에 대고 트리거!", Scanner, CounterItems), PayHoldKey),
            Then(Hold("pay-bag", TutorialSignal.AllBagged, "찍은 물건을 봉투 위로 가져가서\n손을 놓으면 쏙 들어가.",
                "물건을 그립으로 잡고 봉투 위에서 그립을 놓아!", PaperBag), "pay-scan"),
            Then(Hold("pay-pay", TutorialSignal.CheckoutPaid, "다 담았으면 결제 버튼을\n손끝으로 꾹!",
                "초록 결제 버튼을 손끝으로 눌러!", PosPay), "pay-bag"),
            Then(Say("pay-done", GamePhaseId.Business, "잘했어! 다음 손님부턴 오래 못 기다려.\n빨리 하면 보너스야.", 4.5f), "pay-pay"),

            // 청소 (처음 생길 때 정지, DEC-028)
            On(Hold("trash-1", TutorialSignal.TrashBinned, "누가 쓰레기를 버렸네…\n집어서 쓰레기통 위에서 놓아.",
                "멀면 레이저로 잡고 왼손 트리거를 꾹 → 당겨져!", Trash, TrashBin), TutorialSignal.TrashSpawned),
            Say("trash-done", GamePhaseId.Business, "깨끗! 치운 만큼 알바비 +{cleanupBonus}야.", 3.5f, startOn: TutorialSignal.TrashBinned),
            Say("closing", GamePhaseId.Business, "문 닫았어! 남은 손님 계산하고\n{closingCleanupSeconds}초 안에 청소 마무리!", 5f,
                day: 1, startOn: TutorialSignal.BusinessClosing),
            // 첫 마감 청소: 읽는 동안만 마감 시계를 멈춤 (행동 없이 시간으로 끝남)
            On(Hold("closing-cleanup", TutorialSignal.None, "손님 다 갔다! {closingCleanupSeconds}초 안에\n남은 곳을 다 치우자.",
                null, Trash, MessyPack, Stain), TutorialSignal.ClosingCleanupStarted, seconds: 4.5f),

            // 2일차부터: 흐트러진 팩 (1일차는 쓰레기만 나옴, CampaignConfig)
            On(Hold("messy-1", TutorialSignal.MessyPackFixed, "팩이 떨어졌어! 진열대\n빈 칸에 다시 꽂아.",
                "떨어진 팩을 그립으로 잡아 빈 칸에 꽂아!", MessyPack, PackRack), TutorialSignal.MessyPackSpawned),
            // 바닥 얼룩 (FIX F5 · F6 ④)
            On(Hold("stain-1", TutorialSignal.StainCleaned, "음료를 쏟았네! 오른쪽 대걸레로\n얼룩 위를 쓱쓱 문질러.",
                "대걸레를 그립으로 잡고 바닥에 대고 문질러!", Broom, Stain), TutorialSignal.StainSpawned),
            // 판매 등록 카드 첫 손님: 읽는 동안만 멈춤, 계산은 평소처럼
            On(Hold("listed-1", TutorialSignal.None, "판매 등록한 카드를 사 가는 손님이야!\n찍어서 팔면 카드 값이 들어와.",
                null, CounterItems), TutorialSignal.ListedCardCheckout, seconds: 4.5f),

            // 3일차부터: 첫 도둑
            new TutorialStep
            {
                key = "thief-hold", phase = GamePhaseId.Business, startOn = TutorialSignal.ThiefAppeared, doneOn = TutorialSignal.BroomGrabbed,
                kind = TutorialStepKind.Hold, seconds = 2f, timeout = SafetySeconds, line = "저 복면, 도둑이야!\n대걸레를 잡고 한 대 쳐!",
                idleHint = "카운터 옆 대걸레를 그립으로 잡아!", targets = new[] { Thief, Broom },
            },
            Say("thief-caught", GamePhaseId.Business, "역시 우리 알바!", 3f, startOn: TutorialSignal.ThiefCaught),
            Say("thief-escaped", GamePhaseId.Business, "오늘 무료 팩은 날아갔네…\n다음엔 꼭 잡자!", 4f, startOn: TutorialSignal.ThiefEscaped),

            // ---------------------------------------------------------------- 정산 · 퇴근 전
            Say("settle-1", GamePhaseId.Settlement, "오늘 번 돈이야. 청소를 놓치면\n한 곳에 -{cleanupPenalty}니까 조심.", 6f,
                day: 1, targets: SettlementBoard),
            new TutorialStep
            {
                key = "after-free", phase = GamePhaseId.AfterHours, toDay = 1, doneOn = TutorialSignal.PackOpenedFree,
                seconds = 5f, timeout = 30f, line = "수고했어! 계산대 위 팩은\n오늘 수고비야. 열어 봐!", targets = new[] { FreePack },
            },
            new TutorialStep
            {
                key = "after-paid", phase = GamePhaseId.AfterHours, after = "after-free", startOn = TutorialSignal.RevealFinished,
                seconds = 6f, line = "선반 팩은 하나에 {packPrice}.\n다 했으면 퇴근 버튼!", targets = new[] { PackShelf, ClockOut },
            },
            Say("money-short", GamePhaseId.AfterHours, "돈이 모자라네.\n내일 더 벌어 오자.", 4f, startOn: TutorialSignal.PackDeniedMoney),
            new TutorialStep
            {
                key = "box-1", phase = GamePhaseId.AfterHours, fromDay = 2, startOn = TutorialSignal.BoxAffordable,
                seconds = 6f, line = "돈 좀 모였네? 상자 버튼이면\n{boxPrice}에 10팩을 한 번에!", targets = new[] { BoxButton },
            },

            // ---------------------------------------------------------------- 집 (알바생 혼잣말)
            new TutorialStep
            {
                key = "home-binder", phase = GamePhaseId.Home, doneOn = TutorialSignal.BinderBrowsed, speaker = TutorialSpeaker.Self,
                seconds = 4f, timeout = 20f, line = "오늘 뽑은 카드, 바인더에 들어왔겠지?\n책상으로 가 보자.", targets = new[] { Binder },
            },
            Then(Self("home-bed", "★ 표시가 오늘 인기 속성이었지.\n이제 불 끄고 자자.", 5f, BedButton), "home-binder"),
            Self("home-instant", "급하면 즉시 판매도 돼.\n대신 값은 좀 덜 받아.", 5f, SalesPanel, fromDay: 2),
            Then(Self("home-community", "컴퓨터로 내일 인기 속성을\n미리 볼 수 있었지!", 5f, Community, fromDay: 2), "home-instant"),
            new TutorialStep
            {
                key = "home-sales", phase = GamePhaseId.Home, fromDay = 2, after = "home-community", startOn = TutorialSignal.DisplayableCardOwned,
                doneOn = TutorialSignal.SalesRegistered, speaker = TutorialSpeaker.Self, seconds = 5f, timeout = 20f,
                line = "수퍼레어 이상은 판매 등록하면\n내일 손님이 사 갈지도.", targets = new[] { SalesPanel },
            },
        };

        /// <summary>일차별 카운터 메모 (0번 = 1일차). 빈 칸은 메모 없음 (튜토리얼_설계 5-7).</summary>
        public static List<string> Memos() => new List<string>
        {
            "첫날 환영해!\n알바 수칙은 뒤쪽 벽에\n붙여 놨어. — 사장",
            "손님들이 팩 꽂이를 흐트러뜨려.\n떨어진 팩은 빈 칸에 꽂아 줘.\n— 사장",
            "요즘 복면 쓴 녀석이\n무료 팩을 노린대.\n대걸레는 카운터 옆! — 사장",
            "",
            "주말이라 손님이 많을 거야.\n계산은 빠르게! — 사장",
            "",
            "오늘이 마지막 날.\n끝나면 7일 평가 있다! — 사장",
        };

        // ---------------------------------------------------------------- 만들기 도우미

        static TutorialStep Say(string key, GamePhaseId phase, string line, float seconds, int day = 0,
            TutorialSignal startOn = TutorialSignal.PhaseEntered, string targets = null) =>
            Say(key, phase, line, seconds, startOn, targets != null ? new[] { targets } : null, day);

        static TutorialStep Say(string key, GamePhaseId phase, string line, float seconds, TutorialSignal startOn, string[] targets, int day = 0) =>
            new TutorialStep
            {
                key = key, phase = phase, fromDay = day > 0 ? day : 1, toDay = day, startOn = startOn,
                seconds = seconds, line = line, targets = targets ?? System.Array.Empty<string>(),
            };

        static TutorialStep Wait(string key, GamePhaseId phase, TutorialSignal doneOn, string line, string hint, params string[] targets) =>
            new TutorialStep
            {
                key = key, phase = phase, toDay = 1, startOn = TutorialSignal.None, doneOn = doneOn,
                kind = TutorialStepKind.Wait, seconds = 1.5f, timeout = SafetySeconds, line = line, idleHint = hint, targets = targets,
            };

        static TutorialStep Hold(string key, TutorialSignal doneOn, string line, string hint, params string[] targets) =>
            new TutorialStep
            {
                key = key, phase = GamePhaseId.Business, startOn = TutorialSignal.None, doneOn = doneOn,
                kind = TutorialStepKind.Hold, seconds = 1.5f, timeout = SafetySeconds, line = line, idleHint = hint, targets = targets,
            };

        static TutorialStep Self(string key, string line, float seconds, string target, int fromDay = 1) =>
            new TutorialStep
            {
                key = key, phase = GamePhaseId.Home, fromDay = fromDay, speaker = TutorialSpeaker.Self,
                seconds = seconds, line = line, targets = new[] { target },
            };

        // 이 신호가 오면 시작 (Hold 도우미는 startOn None). seconds > 0이면 보여 주는 시간도 바꿈
        static TutorialStep On(TutorialStep step, TutorialSignal startOn, float seconds = 0f)
        {
            step.startOn = startOn;
            if (seconds > 0f) step.seconds = seconds;
            return step;
        }

        // 앞 단계가 끝나면 바로 이어서 (startOn None + after)
        static TutorialStep Then(TutorialStep step, string afterKey)
        {
            step.after = afterKey;
            step.startOn = TutorialSignal.None;
            return step;
        }
    }
}

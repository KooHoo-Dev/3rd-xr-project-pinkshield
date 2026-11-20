using System.Collections.Generic;
using CardShop.Core;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 경제 · 가구 튜토리얼 단계 (튜토리얼_설계 13-3, 추천안 T1~T9: 모두 말 안내, 정지 없음).
    /// 그 일이 처음 일어나는 순간에 짧게 — 매입가 vs 가게 판매 · 인기 강도 · 택배 · 진열장 세트 · 꾸미기 · 중고 장터 · 무한 모드.
    /// TutorialDefaults.Steps()가 끝에 이어 붙입니다 (TutorialDefaults 300줄 점검선, AI-005). 신호는 TutorialEconomyWatch · GameMoment.
    /// 새 대상 키(가격표 · 택배 상자 등)는 아직 없어 기존 대상(판매 판 · 모니터 · 정산 판)을 가리키거나 눈앞에 띄웁니다.
    /// </summary>
    public static class TutorialEconomyDefaults
    {
        public static IEnumerable<TutorialStep> Steps() => new List<TutorialStep>
        {
            // ---------------------------------------------------------------- ① 매입가 vs 가게 판매 (F1~F3, F7)
            new TutorialStep
            {
                key = "home-keep-rare", phase = GamePhaseId.Home, after = "home-sales", startOn = TutorialSignal.RareCardPicked,
                speaker = TutorialSpeaker.Self, seconds = 5f,
                line = "가게에 올리면 {pickedShopPrice}쯤인데\n바로 팔면 {pickedBuyback}뿐이네.", targets = new[] { TutorialDefaults.SalesPanel },
            },
            Self("home-bulk-zero", TutorialSignal.InstantSoldZero, "노멀 낱장은 0원이네.\n{normalBulkCount}장씩 모아서 '모두 팔기'!", TutorialDefaults.SalesPanel),
            Boss("settle-listed", GamePhaseId.Settlement, TutorialSignal.ListedSaleSettled,
                "등록 카드 판 돈은 이 줄이야.\n좋은 카드는 가게에서 팔아야 남아.", TutorialDefaults.SettlementBoard),
            Self("home-slots", TutorialSignal.SalesSlotsFull, "판매 칸이 꽉 찼네. 칸이 많으면\n손님이 더 사 가. 바인더를 늘려 볼까?", TutorialDefaults.Community),

            // ---------------------------------------------------------------- ② 인기 속성 · 강도 (F4~F6)
            Day(Boss("biz-popular", GamePhaseId.Business, TutorialSignal.ListedCardsOnShelf,
                "인기 속성 카드는 손님이 먼저 집어.\n1순위는 값도 오르고 더 잘 팔려.", null), 2),
            Boss("biz-surge", GamePhaseId.Business, TutorialSignal.MarketSurgeToday,
                "{top1Strength} 날엔 값이 확 뛰어. 강도는\n소폭 · 상승 · 급등 · 폭등 순이야.", null),
            new TutorialStep
            {
                key = "home-forecast", phase = GamePhaseId.Home, after = "biz-surge", startOn = TutorialSignal.PhaseEntered,
                doneOn = TutorialSignal.ForecastStrengthVisible, speaker = TutorialSpeaker.Self, seconds = 5f, timeout = 6f, // 컴퓨터 2단계면 생략
                line = "내일 강도도 미리 알면 좋을 텐데.\n컴퓨터를 올리면 보인댔지.", targets = new[] { TutorialDefaults.Community },
            },
            new TutorialStep
            {
                key = "home-strength", phase = GamePhaseId.Home, fromDay = 5, startOn = TutorialSignal.PhaseEntered,
                doneOn = TutorialSignal.MarketSurgeToday, speaker = TutorialSpeaker.Self, seconds = 5f, timeout = 6f, // 급등 날을 겪었으면 생략 (대비책, T7)
                line = "커뮤니티 강도 말 봤어? 소폭부터\n폭등까지, 셀수록 값이 더 뛰어.", targets = new[] { TutorialDefaults.Community },
            },
            Self("decor-poster", TutorialSignal.PosterInstalled, "포스터에 내일 인기 1·2순위가\n붙어 있네. 벽만 봐도 알겠다!", null),

            // ---------------------------------------------------------------- ③ 가구 (F8~F12)
            Self("home-delivery", TutorialSignal.DeliveryArrived, "띵동! 상자를 손으로 뜯으면\n설치 끝. 효과는 내일 영업부터.", null),
            new TutorialStep
            {
                key = "home-lastnight", phase = GamePhaseId.Home, fromDay = 6, toDay = 6, speaker = TutorialSpeaker.Self, seconds = 5f,
                line = "내일이 마지막 날이지.\n가구 주문은 오늘 밤이 마지막!", targets = new[] { TutorialDefaults.Community },
            },
            new TutorialStep
            {
                key = "showcase-ready", phase = GamePhaseId.Home, startOn = TutorialSignal.ShowcaseSetPossible,
                doneOn = TutorialSignal.ShowcaseSetCompleted, speaker = TutorialSpeaker.Self, seconds = 5f, timeout = 6f, // 이미 세트가 있으면 생략
                line = "같은 속성 수퍼레어 이상 3장이면\n진열장에 세워서 세트! +{setBonus}점.", targets = new[] { TutorialDefaults.Binder },
            },
            Self("showcase-set", TutorialSignal.ShowcaseSetCompleted, "{setElement} 세트 완성! 홈이 빛난다.\n세트는 {maxSets}개까지 모을 수 있어.", null),
            Self("decor-first", TutorialSignal.DecorInstalled, "꾸미기 {decorCount}/5. 다섯 개를 다 모으면\n매일 무료 팩이 하나 더래!", null),
            Self("decor-cozy", TutorialSignal.CozyRoomCompleted, "아늑한 방 완성! 내일부터\n무료 팩이 하나 더 나온다.", null),
            Self("stall-ready", TutorialSignal.OnlineStallInstalled, "모니터 중고 장터에 카드 한 장 두면\n자는 동안 팔릴지도 몰라.", TutorialDefaults.Community),
            Boss("settle-credit", GamePhaseId.Settlement, TutorialSignal.UpgradeCreditShown,
                "집에 가구 들였다며? 그 덕에 번 건\n여기 따로 적어 뒀어.", TutorialDefaults.SettlementBoard),

            // ---------------------------------------------------------------- ④ 무한 모드 (F13) — 연장 날 처음 한 번씩
            Boss("endless-volatile", GamePhaseId.Business, TutorialSignal.EndlessFirstDay,
                "연장 근무부턴 유행이 빨리 바뀌어.\n1~3일마다, 오르내림도 더 커져.", null),
            Boss("endless-falling", GamePhaseId.Business, TutorialSignal.MarketFallingToday,
                "{fallElement} 슬라임은 요즘 값이 내렸어.\n오늘은 그 카드가 덜 팔려.", null),
            Boss("endless-record", GamePhaseId.Business, TutorialSignal.RecordSurgeToday,
                "역대급 {top1Element}! 이런 날은\n다시 없을지도 몰라.", null),
        };

        static TutorialStep Self(string key, TutorialSignal startOn, string line, string target) => new TutorialStep
        {
            key = key, phase = GamePhaseId.Home, startOn = startOn, speaker = TutorialSpeaker.Self, seconds = 5f,
            line = line, targets = target != null ? new[] { target } : System.Array.Empty<string>(),
        };

        static TutorialStep Boss(string key, GamePhaseId phase, TutorialSignal startOn, string line, string target) => new TutorialStep
        {
            key = key, phase = phase, startOn = startOn, speaker = TutorialSpeaker.Boss, seconds = 5f,
            line = line, targets = target != null ? new[] { target } : System.Array.Empty<string>(),
        };

        static TutorialStep Day(TutorialStep s, int fromDay)
        {
            s.fromDay = fromDay;
            return s;
        }
    }
}

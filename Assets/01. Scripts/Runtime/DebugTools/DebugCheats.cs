using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.DebugTools
{
    /// <summary>
    /// 촬영·테스트용 치트 (기획서 10장: 등급·갓팩·레전더리 확정 팩 강제). 에디터와 Development 빌드에서만 동작하고,
    /// Release 빌드에서는 이 오브젝트를 스스로 끕니다. 버튼(UnityEvent)에서 호출합니다.
    /// 기기(Development 빌드)에서는 패널을 숨긴 채 시작합니다: 출시 빌드처럼 타이틀 · 정산 판만으로 진행되는지 보다가,
    /// 필요할 때 일시정지 메뉴의 [디버그 패널 보이기]로 켭니다. 에디터에서는 켜진 채 시작합니다.
    /// </summary>
    public sealed class DebugCheats : MonoBehaviour
    {
        [SerializeField] GameFlow flow;

        public void Configure(GameFlow gameFlow) => flow = gameFlow;

        /// <summary>이 빌드에 디버그 기능이 들어 있는가 (에디터 · Development 빌드)</summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool Available => true;
#else
        public static bool Available => false;
#endif

        /// <summary>디버그 패널(이 오브젝트)이 보이는가. Release 빌드에서는 항상 false.</summary>
        public bool PanelVisible
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value && Available);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static GameContext Ctx => GameContext.Instance;

        void Awake()
        {
            if (!Application.isEditor) gameObject.SetActive(false); // 기기에서는 숨긴 채 시작
        }

        public void ForceGodPack() => Force(r => r.ForceNextSpecial(SpecialPackType.GodPack), "다음 팩: 갓팩");
        public void ForceLegendaryPack() => Force(r => r.ForceNextSpecial(SpecialPackType.LegendaryPack), "다음 팩: 레전더리 확정");
        public void ForceUniqueHit() => Force(r => r.ForceNextHitRarity(CardRarity.Unique), "다음 팩: 히트 슬롯 유니크");

        public void AddMoney100()
        {
            Ctx.Session.Wallet.Add(100);
            Ctx.Events.RaiseNotice($"[치트] 재화 +100 → {Ctx.Session.Wallet.Money}");
        }

        public void SkipToAfterHours()
        {
            // 영업 → 정산 → 퇴근 전까지 넘김
            for (int i = 0; i < 4 && flow.CurrentPhase != GamePhaseId.AfterHours; i++)
            {
                if (flow.CurrentPhase == GamePhaseId.Title || flow.CurrentPhase == GamePhaseId.Final) break;
                flow.RequestAdvance();
                if (flow.CurrentPhase == GamePhaseId.Business) flow.Current.Tick(float.MaxValue);
            }
            Ctx.Events.RaiseNotice($"[치트] {flow.CurrentPhase}");
        }

        public void PauseNow()
        {
            flow.Pause();
            Ctx.Events.RaiseNotice("[치트] 일시정지");
        }

        public void SpawnThiefNow()
        {
            var thief = FindAnyObjectByType<CardShop.Customers.ThiefDirector>();
            bool ok = thief != null && thief.DebugSpawnNow();
            Ctx.Events.RaiseNotice(ok ? "[치트] 도둑 등장" : "[치트] 도둑은 영업 중 · 팩이 제자리 · 도둑이 없을 때만");
        }

        public void SpawnCustomerNow()
        {
            var customers = FindAnyObjectByType<CardShop.Customers.CustomerDirector>();
            bool ok = customers != null && customers.DebugSpawnNow();
            Ctx.Events.RaiseNotice(ok ? "[치트] 손님 1명" : "[치트] 손님은 영업 중에만");
        }

        public void CalibrateHeight()
        {
            var calibrator = FindAnyObjectByType<CardShop.Interaction.PlayerHeightCalibrator>();
            if (calibrator == null) return;
            calibrator.Calibrate();
            Ctx.Events.RaiseNotice($"[치트] 눈높이 {calibrator.TargetEyeHeight:0.0#}m로 맞춤");
        }

        static void Force(System.Action<PackRoller> apply, string message)
        {
            apply(Ctx.PackRoller);
            Ctx.Events.RaiseNotice($"[치트] {message}");
        }
#else
        void Awake()
        {
            gameObject.SetActive(false);
        }

        public void ForceGodPack() { }
        public void ForceLegendaryPack() { }
        public void ForceUniqueHit() { }
        public void AddMoney100() { }
        public void SkipToAfterHours() { }
        public void CalibrateHeight() { }
        public void PauseNow() { }
        public void SpawnThiefNow() { }
        public void SpawnCustomerNow() { }
#endif
    }
}

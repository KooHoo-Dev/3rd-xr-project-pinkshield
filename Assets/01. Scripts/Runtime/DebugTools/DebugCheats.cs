using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.DebugTools
{
    /// <summary>
    /// 촬영·테스트용 치트 (기획서 10장: 등급·갓팩·레전더리 확정 팩 강제). 에디터와 Development 빌드에서만 동작하고,
    /// Release 빌드에서는 이 오브젝트를 스스로 끕니다. 버튼(UnityEvent)에서 호출합니다.
    /// </summary>
    public sealed class DebugCheats : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [Tooltip("Release 빌드에서 함께 숨길 오브젝트 (디버그 패널 등)")]
        [SerializeField] GameObject[] hideInRelease;

        public void Configure(GameFlow gameFlow, GameObject[] hide)
        {
            flow = gameFlow;
            hideInRelease = hide;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static GameContext Ctx => GameContext.Instance;

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
            Ctx.Events.RaiseNotice(flow.IsPaused ? "[치트] 일시정지" : "[치트] 영업 중에만 일시정지돼요");
        }

        public void CalibrateHeight()
        {
            var calibrator = FindAnyObjectByType<CardShop.Interaction.PlayerHeightCalibrator>();
            if (calibrator != null) calibrator.Calibrate();
            Ctx.Events.RaiseNotice("[치트] 눈높이 1.6m로 맞춤");
        }

        static void Force(System.Action<PackRoller> apply, string message)
        {
            apply(Ctx.PackRoller);
            Ctx.Events.RaiseNotice($"[치트] {message}");
        }
#else
        void Awake()
        {
            foreach (GameObject go in hideInRelease) if (go != null) go.SetActive(false);
            gameObject.SetActive(false);
        }

        public void ForceGodPack() { }
        public void ForceLegendaryPack() { }
        public void ForceUniqueHit() { }
        public void AddMoney100() { }
        public void SkipToAfterHours() { }
        public void CalibrateHeight() { }
        public void PauseNow() { }
#endif
    }
}

using System.Collections;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Customers;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CardShop.Interaction
{
    /// <summary>
    /// 퇴근 후 계산대 무료 팩 받침 위에 "실제로 열 수 있는" 무료 팩을 올려 둡니다 (11-19 플레이 피드백 I).
    /// 영업 중 지킨 팩(ThiefDirector의 금색 팩)은 잡기 · 도둑 전용이라, 퇴근하면 그 자리에 선반 팩과 같은 프리팹을 놓고
    /// 잡아서 자르면 무료로 열립니다(PackOpeningService.TryOpenFree). 도둑에게 뺏긴 날은 놓지 않습니다.
    /// 손에서 놓으면 잠시 뒤 받침으로 돌아갑니다(바닥에 떨어져 못 줍는 일 방지). 퇴근 후가 끝나면 열지 않은 팩은 치웁니다.
    ///
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 선반(ShelfController) 오브젝트에 스스로 붙고 프리팹 · 개봉 연결은 선반 것을 빌립니다.
    /// </summary>
    public sealed class FreePackOffer : PhaseListener
    {
        [Tooltip("손에서 놓은 뒤 받침으로 돌아가기까지 (초)")]
        [SerializeField] float returnDelay = 2f;

        ShelfController shelf;
        ThiefDirector thief;
        PackView pack;
        Pose home;
        Coroutine routine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var s = FindAnyObjectByType<ShelfController>();
            if (s != null && s.GetComponent<FreePackOffer>() == null) s.gameObject.AddComponent<FreePackOffer>();
        }

        void Awake()
        {
            shelf = GetComponent<ShelfController>();
            thief = FindAnyObjectByType<ThiefDirector>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Remove();
        }

        protected override void Refresh()
        {
            if (CurrentPhase == GamePhaseId.AfterHours)
            {
                // 페이즈 이벤트는 AfterHours.Enter(무료 팩 수 정하기)보다 먼저 오고, 도둑 쪽이 받침을 끄는 것도 같은 순간이라 한 프레임 뒤에
                if (routine == null && pack == null) routine = StartCoroutine(OfferNextFrame());
            }
            else Remove();
        }

        IEnumerator OfferNextFrame()
        {
            yield return null;
            routine = null;
            if (CurrentPhase != GamePhaseId.AfterHours || Context == null || Context.Session.FreePacksLeft <= 0) yield break;
            if (shelf == null || shelf.PackPrefab == null || shelf.Opener == null) yield break;
            if (thief == null || !thief.TryGetFreePackHome(out home)) yield break;

            thief.ShowFreePackStand(true);
            pack = Instantiate(shelf.PackPrefab, home.position, home.rotation);
            pack.name = "FreePack_Openable";
            pack.Init(PackClue.None, false, null, isFree: true);
            pack.OpenRequested += shelf.Opener.TryOpen;
            pack.Grab.selectExited.AddListener(OnReleased);
            Hold(pack);
        }

        // 열렸으면(잡기가 꺼짐) 받침도 끔. 팩 오브젝트는 뜯김 연출이 지움
        void Update()
        {
            if (pack == null || pack.Grab.enabled) return;
            pack = null;
            if (thief != null) thief.ShowFreePackStand(false);
        }

        void OnReleased(SelectExitEventArgs _)
        {
            if (pack != null && isActiveAndEnabled) StartCoroutine(ReturnLater(pack));
        }

        IEnumerator ReturnLater(PackView p)
        {
            yield return new WaitForSeconds(returnDelay);
            // 열린 팩(잡기가 꺼짐)이나 다시 잡은 팩은 그대로
            if (p == null || p != pack || !p.Grab.enabled || p.Grab.isSelected) yield break;
            p.transform.SetPositionAndRotation(home.position, home.rotation);
            Hold(p);
        }

        // 받침 위에 떠 있게 (중력 없음). 잡았다 놓으면 XRI가 잡기 전 상태(키네마틱)로 되돌림
        static void Hold(PackView p)
        {
            var body = p.GetComponent<Rigidbody>();
            if (body == null) return;
            body.isKinematic = true;
            body.useGravity = false;
            body.position = p.transform.position;
            body.rotation = p.transform.rotation;
        }

        // 퇴근 후가 끝남: 열지 않은 팩은 치우고 받침도 끔. 열린 팩은 뜯김 연출(CardRevealer)이 스스로 지움
        void Remove()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (pack != null && pack.Grab.enabled)
            {
                pack.OpenRequested -= shelf.Opener.TryOpen;
                Destroy(pack.gameObject);
            }
            pack = null;
            if (thief != null && CurrentPhase != GamePhaseId.Business) thief.ShowFreePackStand(false);
        }
    }
}

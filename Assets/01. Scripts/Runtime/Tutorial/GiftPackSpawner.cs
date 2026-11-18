using System.Collections;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Customers;
using CardShop.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 1일차 Intro의 사장님 선물 팩 (튜토리얼_설계 5-2, F3). 계산대 무료 팩 받침 자리에 선반 팩과 같은 프리팹을 놓고
    /// IsGift로 표시합니다(PackOpener가 Intro에서만, 돈 없이 열어 줌). 손에서 놓으면 잠시 뒤 받침으로 돌아갑니다.
    /// Intro가 끝날 때 열지 않은 팩은 치우고, 그 사실을 알려 줍니다(건너뛰면 퇴근 후 무료 팩 +1, 9장 Q5).
    /// </summary>
    public sealed class GiftPackSpawner : MonoBehaviour
    {
        [Tooltip("손에서 놓은 뒤 받침으로 돌아가기까지 (초)")]
        [SerializeField] float returnDelay = 2f;

        ShelfController shelf;
        ThiefDirector thief;
        PackView pack;
        Pose home;
        bool opened;

        /// <summary>지금 카운터에 놓인 선물 팩 (없거나 열렸으면 null)</summary>
        public PackView Pack => pack != null && pack.Grab.enabled ? pack : null;
        public bool Opened => opened || (pack != null && !pack.Grab.enabled);

        public void Configure(ShelfController shelfController, ThiefDirector thiefDirector)
        {
            shelf = shelfController;
            thief = thiefDirector;
        }

        /// <summary>선물 팩을 놓습니다. 이미 있거나 이번 판에 열었으면 아무 일도 하지 않습니다.</summary>
        public void Spawn()
        {
            if (pack != null || opened || shelf == null || shelf.PackPrefab == null || shelf.Opener == null) return;
            if (thief == null || !thief.TryGetFreePackHome(out home))
                home = new Pose(ShopLayout.FreePackPos + Vector3.up * 0.05f, Quaternion.identity);

            pack = Instantiate(shelf.PackPrefab, home.position, home.rotation);
            pack.name = "GiftPack_Tutorial";
            pack.Init(PackClue.None, true, null);
            pack.OpenRequested += shelf.Opener.TryOpen;
            pack.Grab.selectExited.AddListener(OnReleased);
            Float(pack);
        }

        /// <summary>Intro가 끝남: 열지 않은 팩을 치웁니다. 치운 팩이 있었으면 true.</summary>
        public bool Remove()
        {
            bool unopened = pack != null && pack.Grab.enabled;
            if (unopened)
            {
                pack.OpenRequested -= shelf.Opener.TryOpen;
                Destroy(pack.gameObject);
            }
            else if (pack != null) opened = true;
            pack = null;
            return unopened;
        }

        /// <summary>새 게임</summary>
        public void ResetForNewGame()
        {
            Remove();
            opened = false;
        }

        void Update()
        {
            if (pack != null && !pack.Grab.enabled) { opened = true; pack = null; } // 열림: 오브젝트는 뜯김 연출이 지움
        }

        void OnReleased(SelectExitEventArgs _)
        {
            if (pack != null && isActiveAndEnabled) StartCoroutine(ReturnLater(pack));
        }

        IEnumerator ReturnLater(PackView p)
        {
            yield return new WaitForSeconds(returnDelay);
            if (p == null || p != pack || !p.Grab.enabled || p.Grab.isSelected) yield break;
            p.transform.SetPositionAndRotation(home.position, home.rotation);
            Float(p);
        }

        // 받침 위에 떠 있게 (중력 없음) — FreePackOffer와 같은 방식
        static void Float(PackView p)
        {
            var body = p.GetComponent<Rigidbody>();
            if (body == null) return;
            body.isKinematic = true;
            body.useGravity = false;
            body.position = p.transform.position;
            body.rotation = p.transform.rotation;
        }
    }
}

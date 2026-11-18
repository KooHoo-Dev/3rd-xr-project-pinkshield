using System;
using System.Collections;
using CardShop.Cards;
using CardShop.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 표지 소켓에 꽂은 카드를 자기 칸으로 날려 보내고 사라지게 합니다(데이터는 개봉 때 이미 보유 목록에 있음, 정리 연출).
    /// BinderView에서 떼어 냄 (큰_클래스_나누기_제안 3장). 날아가는 동안은 소켓을 잠가 같은 카드가 다시 꽂히지 않게 합니다.
    /// DEC-023으로 바인더는 레이 클릭 방식이 기본이고, 이 소켓은 씬에 남아 있는 보조 기능입니다.
    /// </summary>
    public sealed class BinderSocketStore
    {
        readonly MonoBehaviour host;
        readonly BinderCoverSocket socket;
        readonly Func<CardData, BinderSlotView> slotFor; // 그 카드 칸을 펼치고 그 칸 (없으면 null)
        readonly Func<float> flySeconds;
        int storing;

        public BinderSocketStore(MonoBehaviour host, BinderCoverSocket socket, Func<CardData, BinderSlotView> slotFor, Func<float> flySeconds)
        {
            this.host = host;
            this.socket = socket;
            this.slotFor = slotFor;
            this.flySeconds = flySeconds;
        }

        public void Enable()
        {
            if (socket != null) socket.selectEntered.AddListener(OnSocketed);
        }

        public void Disable()
        {
            if (socket == null) return;
            socket.selectEntered.RemoveListener(OnSocketed);
            storing = 0;
            socket.socketActive = true;
        }

        void OnSocketed(SelectEnterEventArgs args)
        {
            var card = args.interactableObject.transform.GetComponent<CardRevealView>();
            if (card != null) host.StartCoroutine(StoreFromSocket(card));
        }

        IEnumerator StoreFromSocket(CardRevealView card)
        {
            // 선택 이벤트 처리 도중에 선택을 풀면 XRI 내부 순서가 꼬일 수 있어 한 프레임 뒤에 풉니다.
            storing++;
            socket.socketActive = false;
            yield return null;
            if (socket.hasSelection && socket.interactionManager != null)
                socket.interactionManager.CancelInteractorSelection((IXRSelectInteractor)socket);

            yield return Store(card);

            storing--;
            if (storing <= 0)
            {
                storing = 0;
                socket.socketActive = true;
            }
        }

        IEnumerator Store(CardRevealView card)
        {
            CardData data = card.Card;
            card.SetGrabbable(false);
            BinderSlotView slot = data != null ? slotFor(data) : null;
            if (slot == null)
            {
                card.Hide(); // 바인더에 칸이 없는 카드(데이터 오류)는 그냥 정리
                yield break;
            }

            var body = card.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            // 칸(UI)과 카드 모두 앞면이 -Z. 날기는 CardRevealView.FlyTo (Transform · Rigidbody를 함께 옮김, BUG-003)
            float seconds = flySeconds();
            Vector3 fromScale = card.transform.localScale;
            card.FlyTo(slot.transform.position, slot.transform.rotation, seconds, fromScale.x * 0.6f, arcHeight: 0.08f, grabbableOnArrive: false);
            yield return new WaitForSeconds(seconds);

            card.Hide(); // 풀(CardRevealer)로 돌아갈 카드라 지우지 않고 끕니다
            card.transform.localScale = fromScale;
            slot.Flash();
        }
    }
}

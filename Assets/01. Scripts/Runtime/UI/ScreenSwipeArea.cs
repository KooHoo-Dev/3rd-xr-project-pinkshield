using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardShop.UI
{
    /// <summary>
    /// 레이로 화면을 잡고(트리거를 누른 채) 좌우로 끌면 쪽을 넘기게 끌기를 받는 판 (집_컴퓨터_화면_설계 J1-b).
    /// 이 컴포넌트가 붙은 오브젝트 밑의 버튼에서 시작한 끌기도 위로 올라와 여기서 받습니다(버튼은 끌기 처리를 안 하므로).
    /// 끌기가 시작되면 uGUI가 누르던 버튼의 클릭을 취소하므로, 끌다가 물건을 잘못 사는 일이 적습니다(Play 확인 필요).
    /// 끈 거리는 레이가 닿은 월드 점을 이 판의 로컬 X로 바꿔 잽니다(화면 거리 · 각도와 상관없게).
    /// </summary>
    public sealed class ScreenSwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>끌기가 끝남: 끈 거리(로컬 X, m 단위가 아니라 이 판 로컬 단위)와 판 폭</summary>
        public event Action<float, float> Swiped;
        /// <summary>끄는 중: 지금까지 끈 거리 (화면을 손 따라 살짝 미는 연출용)</summary>
        public event Action<float> Dragging;

        float startX, lastX;
        bool dragging;

        public float Width => transform is RectTransform rt ? rt.rect.width : 1f;

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = TryLocalX(e, out startX);
            lastX = startX;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging || !TryLocalX(e, out float x)) return;
            lastX = x;
            Dragging?.Invoke(x - startX);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            if (TryLocalX(e, out float x)) lastX = x; // 화면 밖에서 놓으면 마지막으로 닿은 곳 기준
            float dx = lastX - startX;
            Swiped?.Invoke(dx, Width);
        }

        bool TryLocalX(PointerEventData e, out float x)
        {
            x = 0f;
            RaycastResult hit = e.pointerCurrentRaycast;
            if (!hit.isValid) return false;
            x = transform.InverseTransformPoint(hit.worldPosition).x;
            return true;
        }
    }
}

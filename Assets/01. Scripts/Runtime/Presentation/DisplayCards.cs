using CardShop.Cards;
using CardShop.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 보기만 하는 실제 카드 (바인더 칸 · 판매 창 썸네일, UI 개편 11-20 "카드 원형 그대로").
    /// 손에 드는 카드와 같은 프리팹(UpgradeCatalog.showcaseCardPrefab)을 만들고 잡기 · 충돌 · 물리를 떼어 UI 안에 꽂습니다.
    /// 카드 앞면은 -Z를 봐서 월드 캔버스(앞면 -Z)와 방향이 같으므로, 캔버스 안 RectTransform 아래에 회전 없이 두면 됩니다.
    /// </summary>
    public static class DisplayCards
    {
        /// <summary>parent(캔버스 안, 1단위 = 1mm) 아래에 카드 하나. 프리팹이 없으면 null</summary>
        public static CardView Create(Transform parent, string name)
        {
            UpgradeCatalog catalog = GameContext.Instance != null ? GameContext.Instance.Upgrades.Catalog : null;
            if (catalog == null || catalog.showcaseCardPrefab == null) return null;
            CardView card = Object.Instantiate(catalog.showcaseCardPrefab, parent, false);
            card.name = name;
            // 잡기(Rigidbody가 필요한 부품)를 먼저 떼고 물리 · 충돌을 뗌 → 레이는 뒤의 UI 칸이 받음
            foreach (var grab in card.GetComponentsInChildren<XRBaseInteractable>(true)) Object.DestroyImmediate(grab);
            foreach (var body in card.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (var col in card.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            return card;
        }

        /// <summary>캔버스 단위(mm)로 카드 높이 · 가운데 자리를 맞춤. 앞(-Z)으로 조금 띄워 판과 겹쳐 깜빡이지 않게</summary>
        public static void Fit(CardView card, float heightMm, Vector2 centerMm, float liftMm = 4f)
        {
            if (card == null) return;
            Transform t = card.transform;
            // 보는 사람이 판의 어느 쪽(-Z / +Z)에 있는지 보고 카드 앞면(카드 -Z)과 띄우는 쪽을 맞춤
            // (11-20 Play: 바인더 칸에 카드 뒷면이 판 뒤로 들어가 흰 슬리브에 덮여 보였음)
            bool viewerAtMinusZ = true;
            Camera cam = Camera.main;
            if (cam != null && t.parent != null)
                viewerAtMinusZ = Vector3.Dot(t.parent.forward, cam.transform.position - t.parent.position) < 0f;
            t.localRotation = viewerAtMinusZ ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            t.localScale = Vector3.one * (heightMm / CardView.Height); // 카드 높이 0.088 → heightMm (캔버스 1단위 = 1mm)
            t.localPosition = new Vector3(centerMm.x, centerMm.y, viewerAtMinusZ ? -liftMm : liftMm);
        }
    }
}

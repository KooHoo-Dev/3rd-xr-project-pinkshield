using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 방 책장 앞의 카드 진열대 (집_업그레이드_설계 2장 E1). GameSession.Showcase의 카드를 스탠드에 세운 실물 카드로 보여 줍니다.
    /// - 카드는 손(레이)으로 꺼내 볼 수 있고, 놓으면 제자리로 날아갑니다. 바인더 표지 소켓에는 꽂히지 않습니다(Default 층만).
    /// - 진열장 단계(UpgradeRules.ShowcaseSlots)에 따라 선반 줄이 늘고(3 → 6 → 10칸), 3단계에는 조명이 켜집니다.
    /// - 집 실내(HomeInterior) 아래에 있어 집에서만 켜지고, 켜져 있는 동안 진열 목록이 바뀌었는지 매 프레임 비교합니다(최대 10장).
    /// 카드 오브젝트는 공개 연출과 같은 카드 프리팹 + CardRevealView를 씁니다(UpgradeCatalog.showcaseCardPrefab).
    /// </summary>
    public sealed class ShowcaseShelf : MonoBehaviour
    {
        const int PerRow = 5;
        const float CardScale = 1.3f;
        const float Spacing = 0.1f;          // 카드 사이 (카드 폭 63mm × 1.3 = 82mm)
        const float TiltDegrees = 10f;       // 스탠드에 기대 세운 각도
        const float ReturnSeconds = 0.4f;
        static readonly float[] RowHeights = { 1.40f, 1.08f }; // 카드 가운데 높이 (집 바닥 기준)
        static readonly Color LedgeColor = new Color(0.36f, 0.24f, 0.16f);

        GameContext ctx;
        UpgradeCatalog catalog;
        Transform interior;
        bool built;
        GameObject[] ledges;
        Light spot;
        readonly List<CardRevealView> views = new List<CardRevealView>();
        readonly List<string> shown = new List<string>();

        /// <summary>책장 앞에 진열대를 붙입니다 (HomeUpgradeInstaller). 책장이 없으면 왼쪽 벽 기본 자리.</summary>
        public static ShowcaseShelf Create(Transform homeInterior, UpgradeCatalog catalog)
        {
            var go = new GameObject("ShowcaseShelf");
            go.transform.SetParent(homeInterior, false);
            var shelf = go.AddComponent<ShowcaseShelf>();
            shelf.interior = homeInterior;
            shelf.catalog = catalog;
            return shelf;
        }

        void OnEnable() => ctx = GameContext.Instance;

        // ------------------------------------------------------------------ 진열대 만들기 (처음 켜질 때 한 번)

        void Build()
        {
            built = true;
            Transform shelf = HomeRoom.FindDeep(interior, "Bookshelf");
            Vector3 floor = interior.position;
            Vector3 front, normal;
            if (shelf != null)
            {
                Bounds b = HomeRoom.Bounds(shelf.gameObject);
                // 방 가운데 쪽을 보는 면 = 앞면 (책장은 벽에 등을 대고 있음)
                Vector3 toRoom = interior.position - b.center;
                normal = Mathf.Abs(toRoom.x) > Mathf.Abs(toRoom.z) ? new Vector3(Mathf.Sign(toRoom.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toRoom.z));
                front = b.center + Vector3.Scale(b.extents, normal);
            }
            else
            {
                normal = Vector3.right;
                front = interior.TransformPoint(new Vector3(ShopLayout.HomeMinX + 0.45f, 0f, -1.75f));
            }
            transform.SetPositionAndRotation(new Vector3(front.x, floor.y, front.z), Quaternion.LookRotation(normal));

            Material source = shelf != null ? shelf.GetComponentInChildren<Renderer>()?.sharedMaterial : null;
            Material wood = HomeRoom.Tinted(source, LedgeColor);
            ledges = new GameObject[RowHeights.Length];
            for (int r = 0; r < RowHeights.Length; r++)
            {
                GameObject ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledge.name = $"Ledge_{r}";
                Destroy(ledge.GetComponent<Collider>());
                ledge.transform.SetParent(transform, false);
                ledge.transform.localPosition = new Vector3(0f, RowHeights[r] - CardView.Height * CardScale * 0.55f, 0.04f);
                ledge.transform.localScale = new Vector3(PerRow * Spacing + 0.04f, 0.012f, 0.07f);
                if (wood != null) ledge.GetComponent<Renderer>().sharedMaterial = wood;
                ledges[r] = ledge;
            }

            var lightGo = new GameObject("ShowcaseLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, RowHeights[0] + 0.45f, 0.45f);
            spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Point;
            spot.color = new Color(1f, 0.9f, 0.7f);
            spot.intensity = 1.6f;
            spot.range = 1.4f;
            spot.shadows = LightShadows.None;
            lightGo.SetActive(false);
        }

        // ------------------------------------------------------------------ 진열 목록 따라가기

        void LateUpdate()
        {
            if (ctx == null || ctx.Session == null || interior == null) return;
            if (!built) Build(); // 처음 켜졌을 때 한 번 (경계 상자는 집 실내가 켜져 있을 때만 정확)
            GameSession s = ctx.Session;
            int capacity = ctx.Upgrades.ShowcaseSlots(s.Upgrades);
            for (int r = 0; r < ledges.Length; r++)
                ledges[r].SetActive(r == 0 || capacity > r * PerRow);
            spot.gameObject.SetActive(ctx.Upgrades.ShowcaseLight(s.Upgrades));

            for (int i = 0; i < s.Showcase.Count; i++)
                if (i >= shown.Count || shown[i] != s.Showcase[i]) ShowAt(i, s.Showcase[i]);
            for (int i = shown.Count - 1; i >= s.Showcase.Count; i--)
            {
                views[i].Hide();
                shown.RemoveAt(i);
            }
        }

        void ShowAt(int index, string cardId)
        {
            CardData card = ctx.Cards.Get(cardId);
            while (views.Count <= index)
            {
                CardRevealView v = CreateView();
                if (v == null) return; // 카드 프리팹이 없음 (경고는 CreateView)
                views.Add(v);
            }
            while (shown.Count <= index) shown.Add(null);
            shown[index] = cardId;
            if (card == null) { views[index].Hide(); return; }
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            views[index].Show(card, false, pos, rot, CardScale);
        }

        CardRevealView CreateView()
        {
            if (catalog == null || catalog.showcaseCardPrefab == null)
            {
                if (views.Count == 0 && enabled)
                    Debug.LogWarning("[ShowcaseShelf] UpgradeCatalog에 카드 프리팹이 없습니다 (Tools > CardShop > Data > Build Upgrade Catalog).", this);
                enabled = false;
                return null;
            }
            CardView card = Instantiate(catalog.showcaseCardPrefab, transform);
            card.name = $"ShowcaseCard_{views.Count}";
            var view = card.gameObject.AddComponent<CardRevealView>();
            var grab = card.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.interactionLayers = 1; // Default만: 바인더 표지(Card 층)에 꽂혀 들어가지 않게
            view.GrabEnded += ReturnToSlot;
            return view;
        }

        // 손에서 놓으면 제자리로
        void ReturnToSlot(CardRevealView view)
        {
            int index = views.IndexOf(view);
            if (index < 0 || index >= shown.Count || !isActiveAndEnabled) return;
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            view.FlyTo(pos, rot, ReturnSeconds, CardScale);
        }

        /// <summary>칸 번호 → 카드 자리. 위 줄부터 왼쪽 → 오른쪽, 앞면(-Z)이 방을 보고 살짝 뒤로 기댐.</summary>
        void SlotPose(int index, out Vector3 position, out Quaternion rotation)
        {
            int row = Mathf.Min(index / PerRow, RowHeights.Length - 1);
            int col = index % PerRow;
            var local = new Vector3((col - (PerRow - 1) / 2f) * Spacing, RowHeights[row], 0.045f);
            position = transform.TransformPoint(local);
            rotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(TiltDegrees, 0f, 0f);
        }
    }
}

using CardShop.Cleanup;
using CardShop.Core;
using CardShop.Customers;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 카드 도둑(카드도둑_설계 5장)에 필요한 씬 오브젝트를 만듭니다. ShopSceneBuilder.Build가 손님 동선 · 청소 다음에 호출합니다.
    ///   빗자루(카운터 오른쪽 뒤) · 오늘 무료 팩 받침(계산대 손님 쪽 왼쪽) · 도둑 서는 자리 · 복면 원본(꺼 둠) · ThiefDirector
    /// 모양은 기본 도형. 빗자루 · 무료 팩 · 도둑 서는 자리는 PlacementKey라 씬에서 옮겨도 다시 빌드할 때 유지됩니다.
    /// </summary>
    public static class ThiefBuilder
    {
        static readonly Vector3 PackSize = new Vector3(0.08f, 0.125f, 0.014f);

        public static void Create(GameFlow flow, TMP_FontAsset font)
        {
            var root = new GameObject("Thief").transform;

            Broom broom = CreateBroom(root, font);
            GameObject stand = CreateFreePackStand(root, font, out HeldItem pack);

            var spot = new GameObject("StealSpot").transform;
            spot.SetParent(root);
            spot.SetPositionAndRotation(ShopLayout.ThiefStealPos, Quaternion.LookRotation(Vector3.back)); // 계산대 쪽을 봄
            PlacementKey.Mark(spot.gameObject, "Thief/StealSpot");

            GameObject mask = CreateMaskTemplate(root);

            var director = root.gameObject.AddComponent<ThiefDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("customers").objectReferenceValue = Object.FindAnyObjectByType<CustomerDirector>();
            so.FindProperty("freePack").objectReferenceValue = pack;
            so.FindProperty("display").objectReferenceValue = stand;
            so.FindProperty("stealSpot").objectReferenceValue = spot;
            so.FindProperty("broom").objectReferenceValue = broom;
            so.FindProperty("maskTemplate").objectReferenceValue = mask;
            so.FindProperty("sidewalkZ").floatValue = ShopLayout.SidewalkMidZ;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 바닥에 세운 빗자루: 원점 = 빗자루 바닥 (HeldItem이 놓을 때 이 높이로 섬). 막대 1.1m + 머리
        static Broom CreateBroom(Transform root, TMP_FontAsset font)
        {
            var go = new GameObject("Broom");
            go.transform.SetParent(root);
            go.transform.SetPositionAndRotation(ShopLayout.BroomPos, Quaternion.Euler(0f, 90f, 0f)); // 머리 넓은 면이 손님 쪽
            PlacementKey.Mark(go, "Thief/Broom");

            GameObject handle = CleanupBuilder.Visual(go.transform, PrimitiveType.Cylinder, new Vector3(0.03f, 0.55f, 0.03f), new Color(0.55f, 0.38f, 0.22f));
            handle.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            GameObject block = CleanupBuilder.Visual(go.transform, PrimitiveType.Cube, new Vector3(0.28f, 0.05f, 0.07f), new Color(0.75f, 0.2f, 0.2f));
            block.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            GameObject bristles = CleanupBuilder.Visual(go.transform, PrimitiveType.Cube, new Vector3(0.3f, 0.075f, 0.05f), new Color(0.9f, 0.78f, 0.45f));
            bristles.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            var head = new GameObject("HeadPoint").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 0.07f, 0f);

            var stick = go.AddComponent<CapsuleCollider>();
            stick.center = new Vector3(0f, 0.65f, 0f);
            stick.radius = 0.035f;
            stick.height = 1.1f;
            stick.direction = 1;
            var brush = go.AddComponent<BoxCollider>();
            brush.center = new Vector3(0f, 0.065f, 0f);
            brush.size = new Vector3(0.3f, 0.13f, 0.08f);

            CleanupBuilder.MakeGrabbable(go);
            Broom broom = go.AddComponent<Broom>();
            broom.Configure(head);

            CustomerFloorBuilder.Label(root, "대걸레\n(청소 · 도둑 잡기)", ShopLayout.BroomPos + new Vector3(0f, 1.4f, 0f), font, 0.3f, "Thief/BroomLabel");
            return broom;
        }

        // 계산대 손님 쪽 받침 + 비스듬히 세운 금색 팩(앞면이 플레이어 쪽 -Z) + 안내 글자. 받침 묶음은 영업 중에만 켜짐
        static GameObject CreateFreePackStand(Transform root, TMP_FontAsset font, out HeldItem pack)
        {
            var stand = new GameObject("FreePackStand");
            stand.transform.SetParent(root);
            stand.transform.position = ShopLayout.FreePackPos;
            PlacementKey.Mark(stand, "Thief/FreePack");

            GameObject plate = CleanupBuilder.Visual(stand.transform, PrimitiveType.Cube, new Vector3(0.13f, 0.012f, 0.08f), new Color(0.3f, 0.22f, 0.16f));
            plate.transform.localPosition = new Vector3(0f, 0.006f, 0f);

            var go = new GameObject("FreePack");
            go.transform.SetParent(stand.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.012f + PackSize.y / 2f, 0f);
            go.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f); // 윗부분이 뒤(+Z)로 기댐
            CleanupBuilder.Visual(go.transform, PrimitiveType.Cube, PackSize, new Color(1f, 0.82f, 0.25f));
            GameObject stripe = CleanupBuilder.Visual(go.transform, PrimitiveType.Cube, new Vector3(PackSize.x + 0.002f, 0.025f, PackSize.z + 0.002f), new Color(0.85f, 0.25f, 0.35f));
            stripe.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.1f, 0.14f, 0.04f); // 바닥에 떨어졌을 때 레이저로 맞히기 쉽게 두껍게
            CleanupBuilder.MakeGrabbable(go);
            pack = go.GetComponent<HeldItem>();

            CustomerFloorBuilder.Label(stand.transform, "오늘 무료 팩\n(퇴근 후 개봉)", ShopLayout.FreePackPos + new Vector3(0f, 0.24f, 0f), font, 0.22f);
            return stand;
        }

        // 머리 전체를 덮는 검은 복면 + 눈 구멍 띠 (도둑 머리 뼈에 붙여 씀). 원점 = 머리 가운데, +Z = 얼굴 앞
        static GameObject CreateMaskTemplate(Transform root)
        {
            var go = new GameObject("ThiefMask_Template (꺼 둔 원본)");
            go.transform.SetParent(root);
            go.transform.position = new Vector3(0f, -5f, 0f);

            Color black = new Color(0.08f, 0.08f, 0.1f);
            CleanupBuilder.Visual(go.transform, PrimitiveType.Sphere, new Vector3(0.21f, 0.25f, 0.23f), black);
            GameObject knot = CleanupBuilder.Visual(go.transform, PrimitiveType.Sphere, new Vector3(0.06f, 0.05f, 0.06f), black);
            knot.transform.localPosition = new Vector3(0f, 0.13f, -0.02f); // 정수리 매듭 (코믹)
            GameObject band = CleanupBuilder.Visual(go.transform, PrimitiveType.Cube, new Vector3(0.15f, 0.04f, 0.02f), new Color(0.95f, 0.85f, 0.75f));
            band.transform.localPosition = new Vector3(0f, 0.02f, 0.105f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = CleanupBuilder.Visual(go.transform, PrimitiveType.Sphere, new Vector3(0.022f, 0.022f, 0.01f), black);
                eye.transform.localPosition = new Vector3(0.035f * side, 0.02f, 0.116f);
            }
            go.SetActive(false);
            return go;
        }
    }
}

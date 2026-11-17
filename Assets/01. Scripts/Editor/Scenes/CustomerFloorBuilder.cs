using CardShop.Cards;
using CardShop.Core;
using CardShop.Customers;
using CardShop.EditorTools.Interior;
using CardShop.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 매장 손님 동선과 결제 버튼을 만듭니다. ShopSceneBuilder.Build가 카운터 버튼 다음에 호출합니다.
    ///
    /// 배치 (플레이어는 원점에서 +Z를 봄, 카운터 x -1.11~1.11 · z 0.385~1.015, 가게 9 × 6m — ShopLayout):
    ///   밖(5.6, 5.6: 문 오른쪽 벽 뒤, 계산대에서 안 보임) → 문 바깥(3.0, 5.0) → 문 안(3.0, 3.5)
    ///   → 판매 진열대(왼쪽 안쪽) 앞 구경 → 카운터 건너편 줄(z 1.4부터 뒤로) → 문 안 → 문 바깥 → 밖
    /// 진열대 모델은 ShopInterior 프리팹(SalesStand). 등록 카드 표시는 판매 진열대 작업에서.
    /// </summary>
    public static class CustomerFloorBuilder
    {
        const float CounterTop = 0.9f; // ShopSceneBuilder와 같게
        static readonly Vector3 OutsidePos = ShopLayout.OutsidePos;
        static readonly Vector3 DoorOutsidePos = ShopLayout.DoorOutsidePos;
        static readonly Vector3 DoorPos = ShopLayout.DoorInsidePos;
        static readonly Vector3 StandPos = ShopLayout.StandPos;
        static readonly Vector3 StandSize = ShopLayout.StandSize;
        static readonly Vector3[] BrowsePos = ShopLayout.BrowsePositions;
        static readonly Vector3[] QueuePos = ShopLayout.QueuePositions;
        static readonly Vector3 PayButtonPos = new Vector3(-0.35f, CounterTop, 0.5f);
        // 손님이 물건을 올리는 자리 (카운터 손님 쪽 가장자리 근처, 플레이어 손이 닿는 거리)
        static readonly Vector3 ItemSpotPos = new Vector3(0.05f, CounterTop + 0.002f, 0.78f);
        // 스캐너 제자리: 손잡이를 세워 둠, 머리는 손님 쪽(+Z)
        static readonly Vector3 ScannerHomePos = new Vector3(-0.15f, CounterTop + 0.051f, 0.55f);

        public static void Create(GameFlow flow, TMP_FontAsset font)
        {
            CustomerRoster roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(CustomerRosterBuilder.RosterPath);
            if (roster == null) roster = CustomerRosterBuilder.Build();

            var root = new GameObject("Customers").transform;

            Transform outside = Point(root, "Outside", OutsidePos, Quaternion.LookRotation(Vector3.back));
            Transform doorOutside = Point(root, "DoorOutside", DoorOutsidePos, Quaternion.LookRotation(Vector3.back));
            Transform door = Point(root, "Door", DoorPos, Quaternion.LookRotation(Vector3.back));

            // 진열대 모델은 인테리어 프리팹에 있음. 인테리어가 없으면(에셋 미임포트) 예전 블록아웃 상자
            if (AssetDatabase.LoadAssetAtPath<GameObject>(InteriorBuilder.ShopPath) == null)
            {
                DoorMarker(root, DoorPos);
                GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = "SalesStand (Blockout)";
                stand.transform.SetParent(root);
                stand.transform.position = StandPos + Vector3.up * (StandSize.y / 2f);
                stand.transform.localScale = StandSize;
            }
            Label(root, "판매 진열대", StandPos + new Vector3(0f, StandSize.y + 0.15f, -0.15f), font);

            var browse = new Transform[BrowsePos.Length];
            Vector3 standFront = StandPos + new Vector3(0f, 0f, -StandSize.z / 2f);
            for (int i = 0; i < BrowsePos.Length; i++)
            {
                Vector3 look = standFront - BrowsePos[i];
                look.y = 0f;
                browse[i] = Point(root, $"Browse_{i}", BrowsePos[i], Quaternion.LookRotation(look.normalized));
            }

            var queue = new Transform[QueuePos.Length];
            for (int i = 0; i < QueuePos.Length; i++)
                queue[i] = Point(root, $"Queue_{i}", QueuePos[i], Quaternion.LookRotation(Vector3.back)); // 플레이어 쪽

            // 바코드 계산: 물건 자리 · 스캐너 (플레이 피드백)
            CustomerItemBuilder.Build(font);
            Transform itemSpot = Point(root, "ItemSpot", ItemSpotPos, Quaternion.identity);
            var scannerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerItemBuilder.ScannerPath);
            if (scannerPrefab != null)
            {
                var scanner = (GameObject)PrefabUtility.InstantiatePrefab(scannerPrefab);
                scanner.transform.SetPositionAndRotation(ScannerHomePos, Quaternion.identity);
                Label(root, "스캐너\n(잡고 트리거)", ScannerHomePos + new Vector3(0f, 0.17f, 0f), font, 0.25f);
            }

            var director = root.gameObject.AddComponent<CustomerDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("itemSpot").objectReferenceValue = itemSpot;
            so.FindProperty("packItemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScannableItem>(CustomerItemBuilder.PackItemPath);
            so.FindProperty("cardItemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScannableItem>(CustomerItemBuilder.CardItemPath);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("roster").objectReferenceValue = roster;
            so.FindProperty("outside").objectReferenceValue = outside;
            so.FindProperty("doorOutside").objectReferenceValue = doorOutside;
            so.FindProperty("door").objectReferenceValue = door;
            SetArray(so.FindProperty("browseSpots"), browse);
            SetArray(so.FindProperty("queueSlots"), queue);
            so.ApplyModifiedPropertiesWithoutUndo();

            PokeButton pay = ShopSceneBuilder.CreatePokeButton("Button_Pay", PayButtonPos, "결제", new Color(0.4f, 0.9f, 0.45f));
            UnityEventTools.AddPersistentListener(pay.pressed, director.ServeFront);
        }

        static Transform Point(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.SetPositionAndRotation(pos, rot);
            return t;
        }

        // 바닥의 문 표시 (얇은 판)
        static void DoorMarker(Transform parent, Vector3 pos)
        {
            GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "DoorMat";
            mat.transform.SetParent(parent);
            mat.transform.position = pos + Vector3.up * 0.005f;
            mat.transform.localScale = new Vector3(1.0f, 0.01f, 0.6f);
            Object.DestroyImmediate(mat.GetComponent<Collider>());
            mat.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.45f, 0.3f, 0.2f) };
        }

        static void Label(Transform parent, string text, Vector3 pos, TMP_FontAsset font, float size = 0.5f)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity; // 글자 앞면이 -Z(플레이어 쪽)
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1.2f, 0.3f);
        }

        static void SetArray(SerializedProperty prop, Object[] items)
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}

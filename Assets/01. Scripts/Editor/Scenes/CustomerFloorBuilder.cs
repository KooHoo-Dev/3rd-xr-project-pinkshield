using CardShop.Cards;
using CardShop.Cards.EditorTools;
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
    /// 매장 손님 동선과 계산대(물건 자리 · 포스기 · 결제 버튼)를 만듭니다. ShopSceneBuilder.Build가 카운터 버튼 다음에 호출합니다.
    ///
    /// 배치 (플레이어는 원점에서 +Z를 봄, 카운터 x -1.11~1.11 · z 0.385~1.015, 가게 9 × 6m — ShopLayout):
    ///   밖(5.6, 5.6: 문 오른쪽 벽 뒤, 계산대에서 안 보임) → 문 바깥(3.0, 5.0) → 문 안(3.0, 3.5)
    ///   → 판매 진열대(왼쪽 안쪽) 앞 구경 → 카운터 건너편 줄(z 1.4부터 뒤로) → 문 안 → 문 바깥 → 밖
    /// 진열대 모델은 ShopInterior 프리팹(SalesStand). 등록 카드 표시는 판매 진열대 작업에서.
    /// </summary>
    public static class CustomerFloorBuilder
    {
        const float CounterTop = ShopLayout.CounterTop;
        static readonly Vector3 OutsidePos = ShopLayout.OutsidePos;
        static readonly Vector3 DoorOutsidePos = ShopLayout.DoorOutsidePos;
        static readonly Vector3 DoorPos = ShopLayout.DoorInsidePos;
        static readonly Vector3 StandPos = ShopLayout.StandPos;
        static readonly Vector3 StandSize = ShopLayout.StandSize;
        static readonly Vector3[] BrowsePos = ShopLayout.BrowsePositions;
        static readonly Vector3[] QueuePos = ShopLayout.QueuePositions;
        // 포스기: 스캐너 왼쪽. 결제 버튼은 포스기 바로 앞(플레이어 쪽) (계산시스템_확장_설계 6장)
        static readonly Vector3 PosTerminalPos = new Vector3(-0.56f, CounterTop, 0.62f);
        static readonly Vector3 PayButtonPos = new Vector3(-0.38f, CounterTop, 0.47f);
        // 손님이 물건을 올리는 자리 4칸 (카운터 손님 쪽 가장자리 근처, 플레이어 손이 닿는 거리). 장바구니 최대 4개
        static readonly Vector3[] ItemSpotPositions =
        {
            new Vector3(-0.13f, CounterTop + 0.002f, 0.78f),
            new Vector3(0.05f, CounterTop + 0.002f, 0.78f),
            new Vector3(0.23f, CounterTop + 0.002f, 0.78f),
            new Vector3(0.41f, CounterTop + 0.002f, 0.78f),
        };
        // 종이봉투: 카운터 오른쪽 끝, 상자 열기 · 퇴근 버튼(x 0.35 · 0.55)보다 오른쪽 (계산시스템_확장_설계 6장: 한 팔 거리 안)
        static readonly Vector3 PaperBagPos = new Vector3(0.82f, CounterTop, 0.6f);
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

            // 진열대 모델은 인테리어 프리팹(SalesStand)에 있음
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
            var scannerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerItemBuilder.ScannerPath);
            if (scannerPrefab != null)
            {
                var scanner = (GameObject)PrefabUtility.InstantiatePrefab(scannerPrefab);
                scanner.transform.SetPositionAndRotation(ScannerHomePos, Quaternion.identity);
                Label(root, "스캐너\n(잡고 트리거)", ScannerHomePos + new Vector3(0f, 0.17f, 0f), font, 0.25f);
            }

            CheckoutCounter counter = CreateCheckout(root, flow, font);

            var director = root.gameObject.AddComponent<CustomerDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("counter").objectReferenceValue = counter;
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("roster").objectReferenceValue = roster;
            so.FindProperty("outside").objectReferenceValue = outside;
            so.FindProperty("doorOutside").objectReferenceValue = doorOutside;
            so.FindProperty("door").objectReferenceValue = door;
            SetArray(so.FindProperty("browseSpots"), browse);
            SetArray(so.FindProperty("queueSlots"), queue);
            so.ApplyModifiedPropertiesWithoutUndo();

        }

        // 계산대: 물건 자리 4칸 · 포스기(화면) · 결제 버튼 · 가게 재고 품목 설정
        static CheckoutCounter CreateCheckout(Transform root, GameFlow flow, TMP_FontAsset font)
        {
            var go = new GameObject("Checkout");
            go.transform.SetParent(root);
            var spots = new Transform[ItemSpotPositions.Length];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = Point(go.transform, $"ItemSpot_{i}", ItemSpotPositions[i], Quaternion.identity);

            // 카드팩 품목에 계산대용 팩 프리팹을 채움 (SO는 손으로 고친 값을 지키고 빈 칸만 채움)
            var shopItems = AssetDatabase.LoadAssetAtPath<ShopItemConfig>(DataPaths.ShopItems);
            FillShopItems(shopItems);

            PosTerminal pos = CreatePosTerminal(go.transform, font);
            PaperBag bag = CreatePaperBag(go.transform, font);

            var counter = go.AddComponent<CheckoutCounter>();
            var so = new SerializedObject(counter);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("shopItems").objectReferenceValue = shopItems;
            so.FindProperty("cardItemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScannableItem>(CustomerItemBuilder.CardItemPath);
            so.FindProperty("pos").objectReferenceValue = pos;
            so.FindProperty("bag").objectReferenceValue = bag;
            SetArray(so.FindProperty("itemSpots"), spots);
            so.ApplyModifiedPropertiesWithoutUndo();

            PokeButton pay = ShopSceneBuilder.CreatePokeButton("Button_Pay", PayButtonPos, "결제", new Color(0.4f, 0.9f, 0.45f));
            pay.transform.SetParent(go.transform, true);
            UnityEventTools.AddPersistentListener(pay.pressed, counter.TryPay);
            return counter;
        }

        // 가게 재고 품목 (계산시스템_확장_설계 5-1). SO는 손으로 고친 값을 지키고, 빈 프리팹 칸과 없는 품목만 채웁니다.
        // 가격 · 가중치 · 처음 나오는 일차는 잠정값 (상자 = EconomyConfig.boxPrice)
        static void FillShopItems(ShopItemConfig shopItems)
        {
            if (shopItems == null) return;
            bool firstMigration = shopItems.items.Find(i => i != null && i.displayName == "카드팩 상자") == null;
            if (shopItems.items.Count > 0 && shopItems.items[0].prefab == null)
                shopItems.items[0].prefab = AssetDatabase.LoadAssetAtPath<ScannableItem>(CustomerItemBuilder.PackItemPath);
            // 예전 기본값(카드팩 가중치 1)이면 새 품목이 들어올 때 카드팩이 여전히 가장 흔하게 6으로
            if (firstMigration && shopItems.items.Count == 1 && shopItems.items[0].weight == 1) shopItems.items[0].weight = 6;

            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataPaths.Economy);
            int[] figurePrice = { 30, 45, 60 };
            int[] figureDay = { 2, 3, 5 };
            int[] figureWeight = { 2, 1, 1 };
            string[] figureName = { "미니 피규어", "피규어", "한정 피규어" };
            for (int i = 0; i < CustomerItemBuilder.FigureArt.Length; i++)
            {
                string id = DecorFactory.ShowcaseArt[CustomerItemBuilder.FigureArt[i]];
                EnsureItem(shopItems, figureName[i], figurePrice[i], figureWeight[i], figureDay[i], CustomerItemBuilder.FigureItemPath(id));
            }
            EnsureItem(shopItems, "카드팩 상자", economy != null ? economy.boxPrice : 90, 1, 3, CustomerItemBuilder.BoxItemPath);

            EditorUtility.SetDirty(shopItems);
            AssetDatabase.SaveAssets();
        }

        static void EnsureItem(ShopItemConfig shopItems, string name, int price, int weight, int firstDay, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ScannableItem>(prefabPath);
            ShopItemConfig.Item item = shopItems.items.Find(i => i != null && i.displayName == name);
            if (item == null)
            {
                item = new ShopItemConfig.Item { displayName = name, price = price, weight = weight, firstDay = firstDay };
                shopItems.items.Add(item);
            }
            if (item.prefab == null) item.prefab = prefab;
        }

        // 종이봉투: 바닥 + 벽 4장(위가 열림), 원점 = 안쪽 바닥 가운데 (PaperBag). 콜라이더 없음 — 담기는 위치로만 판정
        static PaperBag CreatePaperBag(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("PaperBag");
            root.transform.SetParent(parent);
            root.transform.position = PaperBagPos + Vector3.up * 0.004f;
            var bag = root.AddComponent<PaperBag>();
            Vector3 inner = bag.InnerSize;
            const float wall = 0.004f;

            var kraft = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.72f, 0.56f, 0.38f) };
            kraft.SetFloat("_Smoothness", 0.1f);
            PosBox(root.transform, "Bottom", new Vector3(0f, -wall / 2f, 0f), new Vector3(inner.x + 2f * wall, wall, inner.z + 2f * wall), Quaternion.identity, kraft, false);
            // 플레이어 쪽(-Z) 벽은 낮게: 안이 들여다보이고 넣기 쉬움
            float front = inner.y * 0.6f;
            PosBox(root.transform, "WallFront", new Vector3(0f, front / 2f, -(inner.z + wall) / 2f), new Vector3(inner.x + 2f * wall, front, wall), Quaternion.identity, kraft, false);
            PosBox(root.transform, "WallBack", new Vector3(0f, inner.y / 2f, (inner.z + wall) / 2f), new Vector3(inner.x + 2f * wall, inner.y, wall), Quaternion.identity, kraft, false);
            PosBox(root.transform, "WallLeft", new Vector3(-(inner.x + wall) / 2f, inner.y / 2f, 0f), new Vector3(wall, inner.y, inner.z), Quaternion.identity, kraft, false);
            PosBox(root.transform, "WallRight", new Vector3((inner.x + wall) / 2f, inner.y / 2f, 0f), new Vector3(wall, inner.y, inner.z), Quaternion.identity, kraft, false);
            Label(root.transform, "봉투\n(찍은 물건을 넣기)", root.transform.position + new Vector3(0f, inner.y + 0.12f, 0f), font, 0.25f);
            return bag;
        }

        // 포스기: 받침 + 뒤로 기운 화면판 + 화면 글자 (플레이어 쪽 -Z를 봄)
        static PosTerminal CreatePosTerminal(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("PosTerminal");
            root.transform.SetParent(parent);
            root.transform.position = PosTerminalPos;

            var dark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.16f, 0.17f, 0.2f) };
            var glass = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glass.SetColor("_BaseColor", new Color(0.05f, 0.08f, 0.12f));

            PosBox(root.transform, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.22f, 0.06f, 0.16f), Quaternion.identity, dark, true);
            PosBox(root.transform, "Stand", new Vector3(0f, 0.1f, 0.04f), new Vector3(0.04f, 0.1f, 0.03f), Quaternion.identity, dark, true);

            // 화면: 위쪽이 뒤로 20도 기울어 위에서 내려다보기 편하게
            Quaternion tilt = Quaternion.Euler(20f, 0f, 0f);
            Vector3 screenCenter = new Vector3(0f, 0.2f, 0.03f);
            PosBox(root.transform, "Screen", screenCenter, new Vector3(0.26f, 0.17f, 0.012f), tilt, dark, true);
            PosBox(root.transform, "Glass", screenCenter + tilt * new Vector3(0f, 0f, -0.0065f), new Vector3(0.24f, 0.15f, 0.001f), tilt, glass, false);

            var textGo = new GameObject("ScreenText", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = screenCenter + tilt * new Vector3(0f, 0f, -0.0085f);
            textGo.transform.localRotation = tilt;
            var text = textGo.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = 0.12f;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(0.22f, 0.13f);

            var pos = root.AddComponent<PosTerminal>();
            var so = new SerializedObject(pos);
            so.FindProperty("screen").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pos;
        }

        static void PosBox(Transform parent, string name, Vector3 localPos, Vector3 size, Quaternion rot, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Transform Point(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.SetPositionAndRotation(pos, rot);
            return t;
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

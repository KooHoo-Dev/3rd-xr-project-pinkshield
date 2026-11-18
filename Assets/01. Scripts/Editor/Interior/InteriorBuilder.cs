using System.Collections.Generic;
using CardShop.Core;
using CardShop.Decor;
using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEngine;
using L = CardShop.Core.ShopLayout;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 가게 · 거리 · 집 인테리어 프리팹의 "처음 배치"를 만듭니다 (가게_집_인테리어_설계, D7).
    /// 만든 뒤에는 프리팹 모드에서 손으로 다듬는 것이 원칙이라, Build Shop Scene은 프리팹이 없을 때만 만들고 있으면 그대로 씁니다.
    /// 다시 만들려면 Tools > CardShop > Interior > Rebuild … (손으로 고친 내용은 사라짐).
    ///
    /// 규칙: 외부 가구의 앞면은 로컬 +Z. yaw 0 = 앞면이 +Z(거리 쪽), 90 = +X, 180 = -Z(플레이어 쪽), -90 = -X.
    /// 게임 기능 좌표(카운터 · 팩 테이블 · 진열대 · 출입구)는 ShopLayout 값을 씁니다.
    /// </summary>
    public static class InteriorBuilder
    {
        public const string ShopPath = DecorFactory.PrefabDir + "/ShopInterior.prefab";
        public const string StreetPath = DecorFactory.PrefabDir + "/ShopStreet.prefab";
        public const string HomePath = DecorFactory.PrefabDir + "/HomeInterior.prefab";
        /// <summary>거리 보강 (2026-11-18 기기 테스트 피드백 M2): 가게 같은 편 건물 줄 · 거리 연장 · 거리 양 끝 막음. ShopStreet와 따로 둬서 손으로 고친 ShopStreet를 덮어쓰지 않음</summary>
        public const string BlocksPath = DecorFactory.PrefabDir + "/ShopStreetBlocks.prefab";
        const string WindowViewBase = "Assets/05. Shaders/WindowView/WindowView_Street.mat";

        static readonly Vector3 Up = new Vector3(0f, 1f, 0f);
        static readonly Vector3 Down = new Vector3(0f, -1f, 0f);

        // ------------------------------------------------------------------ 메뉴

        [MenuItem("Tools/CardShop/Interior/Rebuild Shop Interior Prefab", priority = 61)]
        static void RebuildShopMenu() { if (Confirm("ShopInterior")) Save(BuildShop(), ShopPath); }

        [MenuItem("Tools/CardShop/Interior/Rebuild Shop Street Prefab", priority = 62)]
        static void RebuildStreetMenu() { if (Confirm("ShopStreet")) Save(BuildStreet(), StreetPath); }

        [MenuItem("Tools/CardShop/Interior/Rebuild Home Interior Prefab", priority = 63)]
        static void RebuildHomeMenu() { if (Confirm("HomeInterior")) Save(BuildHome(), HomePath); }

        static bool Confirm(string name)
        {
            ExternalArt.EnsureUrpMaterials();
            return EditorUtility.DisplayDialog("Interior", $"{name} 프리팹을 처음 배치로 다시 만들까요?\n프리팹 모드에서 손으로 고친 내용은 사라집니다.\n(씬에 반영하려면 Build Shop Scene을 다시 실행)", "다시 만들기", "취소");
        }

        /// <summary>Build Shop Scene에서 호출: URP 변환 후, 없는 프리팹만 만듭니다.</summary>
        public static void EnsurePrefabs()
        {
            ExternalArt.EnsureUrpMaterials();
            DataPaths.EnsureFolder(DecorFactory.PrefabDir);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopPath) == null) Save(BuildShop(), ShopPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StreetPath) == null) Save(BuildStreet(), StreetPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HomePath) == null) Save(BuildHome(), HomePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BlocksPath) == null) Save(BuildBlocks(), BlocksPath);
        }

        [MenuItem("Tools/CardShop/Interior/Rebuild Street Blocks Prefab", priority = 64)]
        static void RebuildBlocksMenu() { if (Confirm("ShopStreetBlocks")) Save(BuildBlocks(), BlocksPath); }

        /// <summary>배치 모드(-executeMethod): 확인 창 없이 거리 보강 프리팹을 다시 만들고 Build Shop Scene까지 실행</summary>
        public static void RebuildBlocksAndSceneFromCommandLine()
        {
            ExternalArt.EnsureUrpMaterials();
            Save(BuildBlocks(), BlocksPath);
            ShopSceneBuilder.Build();
        }

        static void Save(GameObject root, string path)
        {
            if (root == null) return;
            DataPaths.EnsureFolder(DecorFactory.PrefabDir);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[InteriorBuilder] {path} 저장");
        }

        // ------------------------------------------------------------------ 가게 (D1: 껍데기 1.2배 → 9 × 6m, 천장 3m)

        static GameObject BuildShop()
        {
            if (ExternalArt.LowPolyRoot == null) return null;
            var root = new GameObject("ShopInterior");
            Transform shell = Placer.Group(root.transform, "Shell").transform;
            Transform furn = Placer.Group(root.transform, "Furniture").transform;
            Transform display = Placer.Group(root.transform, "Display").transform;
            Transform decor = Placer.Group(root.transform, "Decor").transform;
            Transform gameplay = Placer.Group(root.transform, "GameplayColliders").transform;
            float s = L.ShellScale, M = L.Module;

            // 바닥(타일) · 천장: 3 × 2 칸
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = L.MinX + M * (i + 0.5f), z = L.BackZ + M * (j + 0.5f);
                    Placer.Place(shell, ExternalArt.LowPoly("Floor/Floor_10"), new Vector3(x, 0f, z), Up, scale: s, name: $"Floor_{i}{j}");
                    // 천장은 그림자를 드리워야 함: 해가 천장을 뚫지 않고 창으로만 들어오게 (인테리어 설계 5-1)
                    // Floor_06은 면 하나짜리 판이라 뒤집으면 해 쪽이 뒷면 → 양면 그림자로 설정
                    Placer.TwoSidedShadows(Placer.Place(shell, ExternalArt.LowPoly("Floor/Floor_06"), new Vector3(x, L.CeilingY, z), Down, scale: s, euler: new Vector3(180f, 0f, 0f), name: $"Ceiling_{i}{j}"));
                }
            Box(gameplay, "FloorCollider", new Vector3(0f, -0.05f, (L.BackZ + L.FrontZ) / 2f), new Vector3(L.MaxX - L.MinX, 0.1f, L.FrontZ - L.BackZ));

            // 벽 (WallFloor2 계열 한 세트로 색 통일). 걸레받이가 있는 쪽(+Z)이 실내를 보게 돌림.
            Wall(shell, "Walls/WallFloor2_09", (L.MinX + L.MaxX - M) / 2f, L.FrontZ, 180f, "Wall_Front_Panorama"); // 5m×1.2 = 6m 창 3개
            Wall(shell, "Walls/WallFloor2_04", L.DoorX, L.FrontZ, 180f, "Wall_Front_Door");
            for (int i = 0; i < 3; i++) Wall(shell, "Walls/WallFloor2_01", L.MinX + M * (i + 0.5f), L.BackZ, 0f, $"Wall_Back_{i}");
            Wall(shell, "Walls/WallFloor2_01", L.MinX, L.BackZ + M * 0.5f, 90f, "Wall_Left_0");
            Wall(shell, "Walls/WallFloor2_01", L.MinX, L.BackZ + M * 1.5f, 90f, "Wall_Left_1");
            Wall(shell, "Walls/WallFloor2_01", L.MaxX, L.BackZ + M * 0.5f, -90f, "Wall_Right_0");
            Wall(shell, "Walls/WallFloor2_03", L.MaxX, L.BackZ + M * 1.5f, -90f, "Wall_Right_Window");
            foreach (Vector2 c in new[] { new Vector2(L.MinX, L.BackZ), new Vector2(L.MaxX, L.BackZ), new Vector2(L.MinX, L.FrontZ), new Vector2(L.MaxX, L.FrontZ) })
                Placer.Place(shell, ExternalArt.LowPoly("Walls/Corner_11"), new Vector3(c.x, 0f, c.y), Placer.Bottom, scale: s, name: "Corner");

            // 카운터 (KitchenTabletop 2개 = 2.22m, 서랍이 플레이어 쪽). 윗면 0.88 → 2cm 띄워 0.90
            for (int k = -1; k <= 1; k += 2)
                Placer.Place(furn, ExternalArt.LowPoly("Kitchen/KitchenTabletop1_03"), L.CounterCenter + new Vector3(k * L.CounterSize.x / 4f, 0.02f, 0f), Placer.Bottom, 180f, name: "Counter");
            Box(gameplay, "CounterCollider", L.CounterCenter + Vector3.up * (L.CounterTop / 2f), L.CounterSize);

            // 팩 테이블 · 판매 진열대: 게임 좌표에 크기를 맞춤
            Placer.PlaceFit(furn, ExternalArt.LowPoly("Room/RoomFurniture_03"), L.PackTablePos, Placer.Bottom, L.PackTableSize, 180f, Placer.Col.Box, "PackTable");
            Placer.PlaceFit(furn, ExternalArt.LowPoly("Room/RoomFurniture_05"), L.StandPos, Placer.Bottom, L.StandSize, 180f, Placer.Col.Box, "SalesStand");

            // 카운터 뒤 벽장: Library 7개 (가운데는 피규어 · 스탠드 진열장)
            float backFace = L.BackZ + L.WallHalf;
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 1.15f;
                GameObject lib = Placer.Place(furn, ExternalArt.LowPoly("Room/Library"), new Vector3(x, 0f, backFace), new Vector3(0f, -1f, -1f), 0f, name: $"BackShelf_{i}");
                FillShelves(display, lib, 0f, i == 3 ? ShelfTheme.Figures : (ShelfTheme)(i % 3), i);
            }
            // 왼쪽 벽장: Library 2개 + 낮은 서랍장(부스터 박스 · 피규어)
            float leftFace = L.MinX + L.WallHalf;
            for (int i = 0; i < 2; i++)
            {
                GameObject lib = Placer.Place(furn, ExternalArt.LowPoly("Room/Library"), new Vector3(leftFace, 0f, -0.7f + i * 1.2f), new Vector3(-1f, -1f, 0f), 90f, name: $"LeftShelf_{i}");
                FillShelves(display, lib, 90f, i == 0 ? ShelfTheme.Packs : ShelfTheme.Mixed, 10 + i);
            }
            GameObject dresser = Placer.Place(furn, ExternalArt.LowPoly("Room/RoomFurniture_05"), new Vector3(leftFace, 0f, 2.1f), new Vector3(-1f, -1f, 0f), 90f, col: Placer.Col.Box, name: "LeftDresser");
            FillTop(display, dresser, 90f, 3);

            // 장식
            Placer.Place(decor, ExternalArt.LowPoly("Bathroom/Trash_01"), L.TrashBinPos, Placer.Bottom, col: Placer.Col.Box, name: "TrashCan"); // S5 (청소 소켓 자리)
            Placer.Place(decor, ExternalArt.LowPoly("Room/Catpet_05"), new Vector3(L.DoorX, 0.002f, L.FrontZ - 0.65f), Placer.Bottom, scale: 2f, name: "DoorMat");
            Placer.Place(decor, ExternalArt.LowPoly("Flowers/Flower_04"), new Vector3(L.MinX + 0.45f, 0f, L.FrontZ - 0.45f), Placer.Bottom, name: "Plant_FrontLeft");
            Placer.Place(decor, ExternalArt.LowPoly("Flowers/Flower_03"), new Vector3(L.MaxX - 0.5f, 0f, -0.9f), Placer.Bottom, name: "Plant_BackRight");
            float rightFace = L.MaxX - L.WallHalf;
            Placer.Place(decor, ExternalArt.LowPoly("Room/AirCondition"), new Vector3(rightFace, 2.6f, -1.0f), DecorFactory.BackAlign(-90f), -90f, name: "AirCondition");
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    GameObject lamp = Placer.Place(decor, ExternalArt.LowPoly("Light/Light_05"), new Vector3((i - 1) * 3f, L.CeilingY, 0.2f + j * 2.4f), Up, name: "CeilingLamp");
                    Placer.NoShadows(lamp);
                }

            // 낮은 비용 제안: 포스터(S4) · 시계(S1) · OPEN 팻말(S2) · 입구 종(S8)
            DecorFactory.Poster(decor, new Vector3(rightFace, 1.55f, -0.9f), -90f, 0, new Color(1f, 0.85f, 0.9f));
            DecorFactory.Poster(decor, new Vector3(rightFace, 1.55f, 1.3f), -90f, 1, new Color(0.85f, 0.92f, 1f));
            DecorFactory.Poster(decor, new Vector3(1.95f, 1.6f, L.FrontZ - L.WallHalf), 180f, 2, new Color(1f, 0.95f, 0.8f)); // 정면 기둥: 계산대에서 바로 보임
            DecorFactory.Clock(decor, new Vector3(rightFace, 2.15f, 0.2f), -90f);
            DecorFactory.Sign(decor, new Vector3(L.DoorX, 2.72f, L.FrontZ - L.WallHalf - 0.03f), 180f);

            var bell = new GameObject("DoorBell", typeof(AudioSource));
            bell.transform.SetParent(decor, false);
            bell.transform.localPosition = new Vector3(L.DoorX, 2.5f, L.FrontZ - 0.3f);
            bell.AddComponent<DoorBell>();

            return root;
        }

        static void Wall(Transform parent, string rel, float x, float z, float yaw, string name) =>
            Placer.Place(parent, ExternalArt.LowPoly(rel), new Vector3(x, 0f, z), Placer.Bottom, yaw, L.ShellScale, Placer.Col.Mesh, name);

        static void Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ------------------------------------------------------------------ 진열

        enum ShelfTheme { Packs = 0, Boxes = 1, Mixed = 2, Figures = 3 }

        /// <summary>책장 선반 판 높이를 메시에서 찾아 칸마다 진열품을 올림. yaw = 책장 앞면 방향.</summary>
        static void FillShelves(Transform parent, GameObject shelf, float yaw, ShelfTheme theme, int seed)
        {
            if (shelf == null) return;
            Bounds b = Placer.WorldBounds(shelf);
            List<float> levels = UsableLevels(shelf, b);
            var rng = new System.Random(seed * 31 + 7);
            for (int k = 0; k < levels.Count; k++)
            {
                // 아래 칸은 상자, 눈높이 칸은 팩, 위 칸은 피규어 위주 (테마가 섞어 줌)
                ShelfTheme t = theme;
                if (theme == ShelfTheme.Mixed) t = k == 0 ? ShelfTheme.Boxes : k >= levels.Count - 1 ? ShelfTheme.Figures : ShelfTheme.Packs;
                else if (theme != ShelfTheme.Figures && k == 0) t = ShelfTheme.Boxes;
                Row(parent, b, levels[k], yaw, 0.95f, t, rng);
            }
        }

        static void FillTop(Transform parent, GameObject furniture, float yaw, int seed)
        {
            if (furniture == null) return;
            Bounds b = Placer.WorldBounds(furniture);
            Row(parent, b, b.max.y, yaw, 1.3f, ShelfTheme.Mixed, new System.Random(seed));
        }

        // 선반 한 칸에 진열품 한 줄
        static void Row(Transform parent, Bounds b, float y, float yaw, float width, ShelfTheme theme, System.Random rng)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            Vector3 center = new Vector3(b.center.x, y, b.center.z) + fwd * 0.02f;

            float step;
            System.Func<int, GameObject> pick;
            switch (theme)
            {
                case ShelfTheme.Packs:
                    step = 0.085f; int packColor = rng.Next(4);
                    pick = i => DecorFactory.DisplayPack((packColor + i / 4) % 4); // 4개씩 같은 색 묶음
                    break;
                case ShelfTheme.Boxes:
                    step = 0.24f; int boxColor = rng.Next(4);
                    pick = i => DecorFactory.BoosterBox(boxColor + i);
                    break;
                case ShelfTheme.Figures:
                    step = 0.16f; int f0 = rng.Next(10);
                    pick = i => i % 2 == 0 ? DecorFactory.SlimeFigure(f0 + i / 2) : DecorFactory.AcrylicStand(f0 + i / 2);
                    break;
                default: // Mixed (서랍장 위)
                    step = 0.2f; int m0 = rng.Next(10);
                    pick = i => (i % 3) switch { 0 => DecorFactory.BoosterBox(m0 + i), 1 => DecorFactory.SlimeFigure(m0 + i), _ => DecorFactory.AcrylicStand(m0 + i) };
                    break;
            }

            int n = Mathf.Max(1, Mathf.FloorToInt(width / step));
            float start = -(n - 1) * step / 2f;
            for (int i = 0; i < n; i++)
            {
                if (theme == ShelfTheme.Packs && i % 4 == 3 && rng.NextDouble() < 0.3) continue; // 군데군데 빈 자리(팔린 느낌)
                float jitter = (float)(rng.NextDouble() - 0.5) * 8f;
                Vector3 pos = center + right * (start + i * step);
                Placer.Place(parent, pick(i), pos, Placer.Bottom, yaw + jitter, unpack: false);
            }
        }

        // 선반 판 높이 중 물건을 올릴 수 있는 것 (맨 위 지붕 판 · 너무 붙은 판 제외). 못 찾으면 5단 균등.
        static List<float> UsableLevels(GameObject shelf, Bounds b)
        {
            List<float> raw = Placer.ShelfLevels(shelf, 0.08f);
            var levels = new List<float>();
            foreach (float y in raw)
            {
                if (y < b.min.y + 0.02f || y > b.max.y - 0.15f) continue;
                if (levels.Count > 0 && y - levels[levels.Count - 1] < 0.18f) { levels[levels.Count - 1] = y; continue; }
                levels.Add(y);
            }
            if (levels.Count == 0)
            {
                Debug.LogWarning($"[InteriorBuilder] {shelf.name}: 선반 판을 찾지 못해 균등 5단으로 놓습니다. [확인 필요]");
                for (int k = 0; k < 5; k++) levels.Add(b.min.y + 0.05f + k * (b.size.y - 0.2f) / 5f);
            }
            return levels;
        }

        // ------------------------------------------------------------------ 바깥 거리 (PolygonCity, 가게 창 · 출입구 너머)

        static GameObject BuildStreet()
        {
            if (ExternalArt.CityRoot == null) return null;
            var root = new GameObject("ShopStreet");
            Transform ground = Placer.Group(root.transform, "Ground").transform;
            Transform blocks = Placer.Group(root.transform, "Buildings").transform;
            Transform props = Placer.Group(root.transform, "Props").transform;

            // 가게 앞 인도(z 4.2~9.2) · 도로 2차선(9.2~19.2) · 맞은편 인도(19.2~24.2), x -27.5~27.5 (5m 타일)
            const float sidewalkZ = L.FrontZ + 2.5f, roadZ0 = L.FrontZ + 7.5f, roadZ1 = L.FrontZ + 12.5f, farWalkZ = L.FrontZ + 17.5f, farFront = L.FrontZ + 20f;
            const float roadY = -0.15f;
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 5f;
                Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Sidewalk_01"), new Vector3(x, 0f, sidewalkZ), Up, 180f, name: "Sidewalk_Near");
                Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Road_01"), new Vector3(x, roadY, roadZ0), Up, name: "Road");
                Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Road_01"), new Vector3(x, roadY, roadZ1), Up, 180f, name: "Road");
                Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Sidewalk_01"), new Vector3(x, 0f, farWalkZ), Up, 0f, name: "Sidewalk_Far");
            }
            Box(ground, "SidewalkCollider", new Vector3(0f, -0.05f, sidewalkZ), new Vector3(55f, 0.1f, 5f));

            // 맞은편 건물 줄: 1층 가게 + 아파트 2층 + 지붕, 앞면이 우리 가게(-Z)를 봄
            string[] shops = { "SM_Bld_Shop_01", "SM_Bld_Shop_02", "SM_Bld_Shop_04", "SM_Bld_Shop_05", "SM_Bld_Shop_06" };
            string[] flats = { "SM_Bld_Apartment_01", "SM_Bld_Apartment_02", "SM_Bld_Apartment_03" };
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 5f;
                int h = 1 + ((i + 7) * 7) % 3; // 위층 1~3개를 섞어 스카이라인에 변화
                GameObject shop = Placer.Place(blocks, ExternalArt.City("Buildings/" + shops[(i + 5) % shops.Length]), new Vector3(x, 0f, farFront), new Vector3(0f, -1f, -1f), 180f, name: "Far_Shop");
                float top = shop != null ? Placer.WorldBounds(shop).max.y : 3f;
                for (int f = 0; f < h; f++)
                {
                    GameObject flat = Placer.Place(blocks, ExternalArt.City("Buildings/" + flats[(i + f + 5) % flats.Length]), new Vector3(x, top, farFront), new Vector3(0f, -1f, -1f), 180f, name: "Far_Floor");
                    if (flat != null) top = Placer.WorldBounds(flat).max.y;
                }
                Placer.Place(blocks, ExternalArt.City("Buildings/SM_Bld_Apartment_Roof_01"), new Vector3(x, top, farFront), new Vector3(0f, -1f, -1f), 180f, name: "Far_Roof");
            }

            // 가게 오른쪽 이웃 건물 (오른쪽 작은 창으로 보이는 골목 벽). 우리 가게와 1.5m 골목, 앞면 선은 가게 정면과 같게.
            GameObject n0 = Placer.Place(blocks, ExternalArt.City("Buildings/SM_Bld_Apartment_01"), new Vector3(L.MaxX + 1.5f, 0f, L.FrontZ), new Vector3(-1f, -1f, 1f), -90f, name: "Neighbor_Right");
            if (n0 != null)
                Placer.Place(blocks, ExternalArt.City("Buildings/SM_Bld_Apartment_02"), new Vector3(L.MaxX + 1.5f, Placer.WorldBounds(n0).max.y, L.FrontZ), new Vector3(-1f, -1f, 1f), -90f, name: "Neighbor_Right");

            // 거리 소품: 가로등 · 나무 · 벤치 · 소화전 · 정류장 · 주차된 차 (창 정면은 비워 시야 확보)
            float curbNear = L.FrontZ + 4.6f, curbFar = farWalkZ - 2.1f;
            foreach (float x in new[] { -14f, 12f }) Placer.Place(props, ExternalArt.City("Props/SM_Prop_LightPole_Base_01"), new Vector3(x, 0f, curbNear), Placer.Bottom, 0f, name: "LightPole");
            foreach (float x in new[] { -8f, 16f }) Placer.Place(props, ExternalArt.City("Props/SM_Prop_LightPole_Base_01"), new Vector3(x, 0f, curbFar), Placer.Bottom, 180f, name: "LightPole");
            foreach (float x in new[] { -20f, -9f, 20f }) Placer.Place(props, ExternalArt.City("Environments/SM_Env_Tree_01"), new Vector3(x, 0f, curbNear - 0.3f), Placer.Bottom, name: "Tree");
            foreach (float x in new[] { -2f, 9f }) Placer.Place(props, ExternalArt.City("Environments/SM_Env_Tree_02"), new Vector3(x, 0f, curbFar + 0.3f), Placer.Bottom, name: "Tree");
            Placer.Place(props, ExternalArt.City("Props/SM_Prop_ParkBench_01"), new Vector3(-6.5f, 0f, L.FrontZ + 1.2f), Placer.Bottom, 0f, name: "Bench");
            Placer.Place(props, ExternalArt.City("Props/SM_Prop_Hydrant_01"), new Vector3(8.5f, 0f, curbNear), Placer.Bottom, name: "Hydrant");
            Placer.Place(props, ExternalArt.City("Props/SM_Prop_BusStop_01"), new Vector3(-15f, 0f, farWalkZ), Placer.Bottom, 180f, name: "BusStop");
            Placer.Place(props, ExternalArt.City("Props/SM_Prop_Mailbox_01"), new Vector3(5.2f, 0f, curbNear), Placer.Bottom, name: "Mailbox");
            Placer.Place(props, ExternalArt.City("Vehicles/SM_Veh_Car_Sedan_01"), new Vector3(-4f, roadY, roadZ0 - 1.2f), Placer.Bottom, 90f, name: "ParkedCar");
            Placer.Place(props, ExternalArt.City("Vehicles/SM_Veh_Car_Taxi_01"), new Vector3(14f, roadY, roadZ1 + 1.2f), Placer.Bottom, -90f, name: "ParkedCar");
            Placer.Place(props, ExternalArt.City("Vehicles/SM_Veh_Car_Small_01"), new Vector3(-19f, roadY, roadZ1 + 1.2f), Placer.Bottom, -90f, name: "ParkedCar");
            return root;
        }

        // ------------------------------------------------------------------ 거리 보강 (M2)

        /// <summary>
        /// 건물이 이어지는 끝 (x ±). 4차 기기 피드백: 예전에는 x ±49.5에서 도로를 가로지르는 건물로 막았는데,
        /// 막힌 길에 차가 다니는 게 어색해서 막는 건물을 없애고 도로를 RoadEndX까지 늘린 뒤 안개(ShopSceneBuilder)로 흐리게 했습니다.
        /// </summary>
        public const float StreetEndX = 70f;
        /// <summary>도로 · 인도가 끝나는 x (±). 안개가 완전히 덮는 거리보다 멀게</summary>
        public const float RoadEndX = 92.5f;

        static readonly string[] BlockShops = { "SM_Bld_Shop_01", "SM_Bld_Shop_02", "SM_Bld_Shop_04", "SM_Bld_Shop_05", "SM_Bld_Shop_06" };
        static readonly string[] BlockFlats = { "SM_Bld_Apartment_01", "SM_Bld_Apartment_02", "SM_Bld_Apartment_03" };

        /// <summary>
        /// 거리 보강 (기기테스트_피드백_설계 6장 M2 + 4차 기기 피드백).
        /// - 도로 · 인도를 x ±RoadEndX까지 연장, 맞은편 건물 줄과 가게 같은 편 건물은 ±StreetEndX까지
        /// - 도로를 왕복 2차선으로: 차선 없는 타일(Road_Bare) + 가운데 노란 겹선 + 양쪽 가장자리 흰 선.
        ///   가운데 구간(ShopStreet의 Road_01, 차선이 여러 줄이라 주차장처럼 보였음)은 손으로 고친 ShopStreet를 건드리지 않도록
        ///   같은 자리에 차선 없는 타일을 5mm 위로 덮어 가립니다.
        /// - 거리 양 끝을 막던 건물은 없앰 (막힌 길에 차가 다니면 어색함)
        /// </summary>
        static GameObject BuildBlocks()
        {
            if (ExternalArt.CityRoot == null) return null;
            var root = new GameObject("ShopStreetBlocks");
            Transform ground = Placer.Group(root.transform, "Ground").transform;
            Transform near = Placer.Group(root.transform, "NearSide").transform;
            Transform far = Placer.Group(root.transform, "FarSide").transform;

            const float sidewalkZ = L.FrontZ + 2.5f, roadZ0 = L.FrontZ + 7.5f, roadZ1 = L.FrontZ + 12.5f, farWalkZ = L.FrontZ + 17.5f, farFront = L.FrontZ + 20f;
            const float roadY = -0.15f;
            const string bare = "Environments/SM_Env_Road_Bare_01";

            // 가운데 구간: ShopStreet 도로 타일 위에 차선 없는 타일을 살짝 띄워 덮음
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 5f;
                Placer.Place(ground, ExternalArt.City(bare), new Vector3(x, roadY + 0.005f, roadZ0), Up, name: "Road_Cover");
                Placer.Place(ground, ExternalArt.City(bare), new Vector3(x, roadY + 0.005f, roadZ1), Up, 180f, name: "Road_Cover");
            }

            // 거리 연장: ShopStreet 타일(-25 ~ 25) 바깥 30 ~ RoadEndX
            int lastTile = Mathf.RoundToInt((RoadEndX - 2.5f) / 5f);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 6; k <= lastTile; k++)
                {
                    float x = s * k * 5f;
                    Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Sidewalk_01"), new Vector3(x, 0f, sidewalkZ), Up, 180f, name: "Sidewalk_Near");
                    Placer.Place(ground, ExternalArt.City(bare), new Vector3(x, roadY, roadZ0), Up, name: "Road");
                    Placer.Place(ground, ExternalArt.City(bare), new Vector3(x, roadY, roadZ1), Up, 180f, name: "Road");
                    Placer.Place(ground, ExternalArt.City("Environments/SM_Env_Sidewalk_01"), new Vector3(x, 0f, farWalkZ), Up, 0f, name: "Sidewalk_Far");
                    // 맞은편 건물 줄 연장 (ShopStreet와 같은 방식)
                    if (x * s <= StreetEndX) Stack(far, new Vector3(x, 0f, farFront), new Vector3(0f, -1f, -1f), 180f, k * 3 + s);
                }

            RoadMarkings(ground, roadZ0 - 2.5f, roadZ1 + 2.5f, roadY + 0.02f);

            // 가게 같은 편 오른쪽: ShopStreet의 이웃 건물(가게 오른쪽 1.5m 골목, 옆으로 돌려 놓음) 바로 오른쪽부터
            float rightCursor = L.MaxX + 1.5f + NeighborWidth();
            for (int i = 0; rightCursor < StreetEndX - 2f && i < 30; i++)
            {
                Bounds b = Stack(near, new Vector3(rightCursor, 0f, L.FrontZ), new Vector3(-1f, -1f, 1f), 0f, i);
                if (b.size.x < 1f) break;
                rightCursor = b.max.x;
            }
            // 왼쪽: 가게 왼쪽 벽에서 1.5m 골목을 두고 왼쪽으로
            float leftCursor = L.MinX - 1.5f;
            for (int i = 0; leftCursor > -StreetEndX + 2f && i < 30; i++)
            {
                Bounds b = Stack(near, new Vector3(leftCursor, 0f, L.FrontZ), new Vector3(1f, -1f, 1f), 0f, i + 7);
                if (b.size.x < 1f) break;
                leftCursor = b.min.x;
            }
            return root;
        }

        // 왕복 2차선 표시: 가운데 노란 겹선 + 양쪽 가장자리 흰 실선 (얇은 판, 길이 = 도로 전체)
        static void RoadMarkings(Transform parent, float roadMinZ, float roadMaxZ, float y)
        {
            var group = Placer.Group(parent, "RoadMarkings").transform;
            Material yellow = Placer.Mat(DecorFactory.MatDir, "RoadLine_Yellow", new Color(0.95f, 0.75f, 0.15f));
            Material white = Placer.Mat(DecorFactory.MatDir, "RoadLine_White", new Color(0.92f, 0.92f, 0.9f));
            float length = RoadEndX * 2f;
            float center = (roadMinZ + roadMaxZ) / 2f;
            Line(group, "CenterLine_A", yellow, new Vector3(0f, y, center - 0.12f), new Vector3(length, 0.01f, 0.12f));
            Line(group, "CenterLine_B", yellow, new Vector3(0f, y, center + 0.12f), new Vector3(length, 0.01f, 0.12f));
            Line(group, "EdgeLine_Near", white, new Vector3(0f, y, roadMinZ + 0.4f), new Vector3(length, 0.01f, 0.15f));
            Line(group, "EdgeLine_Far", white, new Vector3(0f, y, roadMaxZ - 0.4f), new Vector3(length, 0.01f, 0.15f));
        }

        static void Line(Transform parent, string name, Material mat, Vector3 center, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // 1층 가게 + 위층 1~2개 + 지붕. align · yaw는 1층 기준. 1층의 월드 경계를 돌려줌 (다음 건물 자리 계산용)
        static Bounds Stack(Transform parent, Vector3 target, Vector3 align, float yaw, int seed)
        {
            var group = new GameObject("Block").transform;
            group.SetParent(parent, false);
            GameObject shop = Placer.Place(group, ExternalArt.City("Buildings/" + BlockShops[Mod(seed, BlockShops.Length)]), target, align, yaw, name: "Shop");
            if (shop == null) return new Bounds(target, Vector3.zero);
            Bounds groundFloor = Placer.WorldBounds(shop);
            float top = groundFloor.max.y;
            int floors = 1 + Mod(seed * 7, 2);
            Vector3 floorAlign = new Vector3(align.x, -1f, align.z);
            for (int f = 0; f < floors; f++)
            {
                GameObject flat = Placer.Place(group, ExternalArt.City("Buildings/" + BlockFlats[Mod(seed + f, BlockFlats.Length)]), new Vector3(target.x, top, target.z), floorAlign, yaw, name: "Floor");
                if (flat != null) top = Placer.WorldBounds(flat).max.y;
            }
            Placer.Place(group, ExternalArt.City("Buildings/SM_Bld_Apartment_Roof_01"), new Vector3(target.x, top, target.z), floorAlign, yaw, name: "Roof");
            return groundFloor;
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        // ShopStreet의 Neighbor_Right(SM_Bld_Apartment_01, yaw -90)가 x 방향으로 차지하는 폭: 임시로 놓아 재고 지움
        static float NeighborWidth()
        {
            var temp = new GameObject("Measure");
            GameObject n = Placer.Place(temp.transform, ExternalArt.City("Buildings/SM_Bld_Apartment_01"), Vector3.zero, new Vector3(-1f, -1f, 1f), -90f);
            float w = n != null ? Placer.WorldBounds(n).size.x : 10f;
            Object.DestroyImmediate(temp);
            return w;
        }

        // ------------------------------------------------------------------ 집 (로컬 좌표, Home 루트 아래에 놓임)

        static GameObject BuildHome()
        {
            if (ExternalArt.LowPolyRoot == null) return null;
            var root = new GameObject("HomeInterior");
            Transform shell = Placer.Group(root.transform, "Shell").transform;
            Transform furn = Placer.Group(root.transform, "Furniture").transform;
            Transform decor = Placer.Group(root.transform, "Decor").transform;
            const float M = 2.5f;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = L.HomeMinX + M * (i + 0.5f), z = L.HomeFrontZ + M * (j + 0.5f);
                    Placer.Place(shell, ExternalArt.LowPoly("Floor/Floor_01"), new Vector3(x, 0f, z), Up, name: "Floor");
                    Placer.TwoSidedShadows(Placer.Place(shell, ExternalArt.LowPoly("Floor/Floor_06"), new Vector3(x, L.HomeCeilingY, z), Down, euler: new Vector3(180f, 0f, 0f), name: "Ceiling"));
                }
            Box(shell, "FloorCollider", new Vector3(0f, -0.05f, (L.HomeBackZ + L.HomeFrontZ) / 2f), new Vector3(5f, 0.1f, 5f));

            HomeWall(shell, "Walls/WallFloor1_01", -1.25f, L.HomeBackZ, 180f);
            HomeWall(shell, "Walls/WallFloor1_01", 1.25f, L.HomeBackZ, 180f);
            HomeWall(shell, "Walls/WallFloor1_02", L.HomeMinX, 0.75f, 90f);   // 큰 창 → 밤 풍경
            HomeWall(shell, "Walls/WallFloor1_01", L.HomeMinX, -1.75f, 90f);
            HomeWall(shell, "Walls/WallFloor1_01", L.HomeMaxX, 0.75f, -90f);
            HomeWall(shell, "Walls/WallFloor1_01", L.HomeMaxX, -1.75f, -90f);
            HomeWall(shell, "Walls/WallFloor1_04", -1.25f, L.HomeFrontZ, 0f);  // 현관
            HomeWall(shell, "Walls/WallFloor1_01", 1.25f, L.HomeFrontZ, 0f);
            foreach (Vector2 c in new[] { new Vector2(L.HomeMinX, L.HomeBackZ), new Vector2(L.HomeMaxX, L.HomeBackZ), new Vector2(L.HomeMinX, L.HomeFrontZ), new Vector2(L.HomeMaxX, L.HomeFrontZ) })
                Placer.Place(shell, ExternalArt.LowPoly("Walls/Corner_11"), new Vector3(c.x, 0f, c.y), Placer.Bottom, name: "Corner");
            Placer.Place(shell, ExternalArt.LowPoly("Walls/Door_01"), new Vector3(-1.25f, 0f, L.HomeFrontZ), Placer.Bottom, name: "FrontDoor");

            float back = L.HomeBackZ - L.HomeWallHalf, left = L.HomeMinX + L.HomeWallHalf, right = L.HomeMaxX - L.HomeWallHalf, front = L.HomeFrontZ + L.HomeWallHalf;
            Vector3 againstBack = new Vector3(0f, -1f, 1f);

            // 바인더 책상 · 컴퓨터 책상 (ShopSceneBuilder의 Desk(-1.2, 1.6) · Monitor(1.2, 1.3, 1.8)와 맞춤)
            Placer.Place(furn, ExternalArt.LowPoly("Room/OfficeTable_01"), new Vector3(-1.2f, 0f, back), againstBack, 180f, col: Placer.Col.Box, name: "BinderDesk");
            Placer.Place(furn, ExternalArt.LowPoly("Room/PC_Table"), new Vector3(1.2f, 0f, back), againstBack, 180f, col: Placer.Col.Box, name: "ComputerDesk");
            Placer.Place(furn, ExternalArt.LowPoly("Room/PC_Keyboard"), new Vector3(1.2f, 0.71f, 1.45f), Placer.Bottom, 180f, name: "Keyboard");
            Placer.Place(furn, ExternalArt.LowPoly("Room/PC_Mouse"), new Vector3(1.55f, 0.71f, 1.45f), Placer.Bottom, 180f, name: "Mouse");
            Placer.Place(furn, ExternalArt.LowPoly("Room/PC"), new Vector3(2.05f, 0f, 1.6f), Placer.Bottom, 180f, name: "PcTower");
            Placer.Place(furn, ExternalArt.LowPoly("Room/OfficeChair_01"), new Vector3(1.2f, 0f, 0.95f), Placer.Bottom, 0f, name: "Chair_Pc");
            Placer.Place(furn, ExternalArt.LowPoly("Room/OfficeChair_02"), new Vector3(-1.2f, 0f, 0.95f), Placer.Bottom, 0f, name: "Chair_Binder");

            // 침대 (오른쪽 벽, 머리는 앞벽 쪽) · 협탁 · 스탠드
            Placer.Place(furn, ExternalArt.LowPoly("Room/Bed2_01"), new Vector3(right, 0f, front), new Vector3(1f, -1f, -1f), 0f, col: Placer.Col.Box, name: "Bed");
            GameObject table = Placer.Place(furn, ExternalArt.LowPoly("Room/Bed_Table_01"), new Vector3(0.95f, 0f, front + 0.25f), Placer.Bottom, 0f, name: "BedTable");
            if (table != null) Placer.Place(decor, ExternalArt.LowPoly("Light/Lamp_03"), new Vector3(0.95f, Placer.WorldBounds(table).max.y, front + 0.25f), Placer.Bottom, name: "BedLamp");

            // 책장(컬렉션) · 러그 · 스탠드 조명 · 화분 · 옷걸이
            GameObject lib = Placer.Place(furn, ExternalArt.LowPoly("Room/Library"), new Vector3(left, 0f, -1.75f), new Vector3(-1f, -1f, 0f), 90f, name: "Bookshelf");
            FillShelves(decor, lib, 90f, ShelfTheme.Figures, 42);
            Placer.Place(decor, ExternalArt.LowPoly("Room/Catpet_01"), new Vector3(0f, 0.002f, -0.4f), Placer.Bottom, name: "Rug");
            Placer.Place(decor, ExternalArt.LowPoly("Light/Lamp_01"), new Vector3(0.05f, 0f, back - 0.3f), Placer.Bottom, name: "FloorLamp");
            Placer.Place(decor, ExternalArt.LowPoly("Flowers/Flower_02"), new Vector3(-2.1f, 0f, 1.6f), Placer.Bottom, name: "Plant");
            Placer.Place(decor, ExternalArt.LowPoly("Room/Hook_02"), new Vector3(-2.1f, 1.7f, front), DecorFactory.BackAlign(0f), 0f, name: "CoatHook");
            Placer.Place(decor, ExternalArt.LowPoly("Room/SwitchLight"), L.HomeSleepSwitch, DecorFactory.BackAlign(0f), 0f, scale: 1.5f, name: "LightSwitchPlate"); // 실제 Poke 버튼은 ShopSceneBuilder

            // 커튼 · 창밖 밤 풍경 (H2: WindowView, 2층 높이 · 밤)
            foreach (float dz in new[] { -0.85f, 0.85f })
                Placer.Place(decor, ExternalArt.LowPoly("Curtains/Curtains_01"), new Vector3(left + 0.02f, 2.25f, 0.75f + dz), new Vector3(-1f, 1f, 0f), 90f, name: "Curtain");
            WindowQuad(shell, new Vector3(L.HomeMinX - L.HomeWallHalf - 0.03f, 1.5f, 0.75f), 90f, new Vector2(1.6f, 1.25f));

            // 시계 · 달력(H4) · 포스터
            Placer.Place(decor, ExternalArt.LowPoly("Room/Clock_02"), new Vector3(0.05f, 2.05f, back), DecorFactory.BackAlign(180f), 180f, name: "Clock");
            DecorFactory.Calendar(decor, new Vector3(-1.2f, 1.75f, back - 0.01f), 180f);
            DecorFactory.Poster(decor, new Vector3(right, 1.6f, -1.6f), -90f, 3, new Color(0.9f, 0.88f, 1f));
            return root;
        }

        static void HomeWall(Transform parent, string rel, float x, float z, float yaw) =>
            Placer.Place(parent, ExternalArt.LowPoly(rel), new Vector3(x, 0f, z), Placer.Bottom, yaw, 1f, Placer.Col.Mesh, "Wall");

        // 창 구멍 바로 바깥에 붙이는 풍경 Quad (셰이더가 시선마다 바깥 거리를 계산)
        static void WindowQuad(Transform parent, Vector3 center, float yaw, Vector2 size)
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(WindowViewBase);
            if (baseMat == null) { Debug.LogWarning($"[InteriorBuilder] {WindowViewBase} 없음: 집 창밖 풍경 생략"); return; }
            string path = DecorFactory.MatDir + "/WindowView_HomeNight.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                DataPaths.EnsureFolder(DecorFactory.MatDir);
                mat = new Material(baseMat) { name = "WindowView_HomeNight" };
                mat.SetFloat("_Night", 1f);
                mat.SetFloat("_LightsOn", 0.6f);
                mat.SetFloat("_EyeHeight", 4.5f); // 원룸 2층에서 내다본 높이
                AssetDatabase.CreateAsset(mat, path);
            }
            GameObject quad = Placer.Prim(parent, PrimitiveType.Quad, "WindowView", center, new Vector3(size.x, size.y, 1f), mat, Quaternion.Euler(0f, yaw, 0f));
            Placer.NoShadows(quad);
            quad.GetComponent<MeshRenderer>().receiveShadows = false;
        }
    }
}

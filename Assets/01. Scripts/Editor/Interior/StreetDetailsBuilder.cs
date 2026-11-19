using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = CardShop.Core.ShopLayout;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 거리 꾸미기 (11-20 K1: "골목 · 맵 디테일이 부족, 같은 건물이 많아 공간감이 없음"). 손으로 고친 ShopStreet · ShopStreetBlocks를
    /// 덮어쓰지 않도록 따로 ShopStreetDetails 프리팹을 만들어 씬의 Environment 아래에 놓습니다. 다시 만들면 씬의 것도 바뀝니다.
    /// 갈 수 없는 곳이라 "보이는 것"만: ① 연석 쪽 가로수 · 소화전 · 쓰레기통 · 주차 미터 · 버스 정류장 ② 건물 앞 화분 · 벤치 · 공중전화 · 노점
    /// ③ 가게 양옆 골목 입구 쓰레기봉투 · 상자 ④ 맞은편 가게 위 큰 간판(같은 건물이라도 다르게 보이게) ⑤ 맞은편 건물 줄 뒤 높은 빌딩 스카이라인(공간감).
    /// 가게 앞(손님 · 도둑이 걷는 길, |x| &lt; 8)은 비워 두고, 콜라이더 없이 정적(배칭)으로 놓습니다(Quest 성능).
    /// 배치는 씬에서 자유롭게 옮겨도 되지만, 이 메뉴를 다시 실행하면 처음 배치로 돌아갑니다.
    /// </summary>
    public static class StreetDetailsBuilder
    {
        public const string PrefabPath = DecorFactory.PrefabDir + "/ShopStreetDetails.prefab";
        const string ScenePath = "Assets/00. Scenes/Shop.unity";
        const float ClearHalfWidth = 8f; // 가게 앞 비워 둘 폭 (x ±)

        // 거리 z (InteriorBuilder.BuildStreet와 같은 기준): 가게 앞 인도 4.2~9.2 · 도로 9.2~19.2 · 맞은편 인도 19.2~24.2
        const float NearWall = L.FrontZ + 0.6f, NearCurb = L.FrontZ + 4.6f, FarCurb = L.FrontZ + 15.4f, FarWall = L.FrontZ + 19.6f, FarFront = L.FrontZ + 20f;

        static readonly Vector3 Bottom = new Vector3(0f, -1f, 0f);

        [MenuItem("Tools/CardShop/Interior/Build Street Details (K1)", priority = 65)]
        static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("거리 꾸미기", "ShopStreetDetails 프리팹을 처음 배치로 다시 만들고 열린 씬에 놓을까요?\n(씬에서 옮긴 꾸미기 소품은 처음 자리로 돌아갑니다)", "만들기", "취소")) return;
            if (BuildPrefab()) PlaceInOpenScene();
        }

        /// <summary>배치 모드(-executeMethod): Shop 씬을 열어 만들고 놓은 뒤 저장</summary>
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (BuildPrefab() && PlaceInOpenScene()) EditorSceneManager.SaveOpenScenes();
        }

        static bool BuildPrefab()
        {
            ExternalArt.EnsureUrpMaterials();
            if (ExternalArt.CityRoot == null) { Debug.LogError("[StreetDetailsBuilder] PolygonCity가 없습니다."); return false; }
            var root = new GameObject("ShopStreetDetails");
            var rng = new System.Random(2011);
            Curbs(Placer.Group(root.transform, "Curbs").transform, rng);
            Walls(Placer.Group(root.transform, "BuildingFronts").transform, rng);
            Alleys(Placer.Group(root.transform, "Alleys").transform);
            Signs(Placer.Group(root.transform, "Signs").transform, rng);
            Skyline(Placer.Group(root.transform, "Skyline").transform, rng);
            DataPaths.EnsureFolder(DecorFactory.PrefabDir);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return true;
        }

        static bool PlaceInOpenScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return false;
            GameObject env = GameObject.Find("Environment");
            GameObject old = GameObject.Find("ShopStreetDetails");
            if (old != null) Object.DestroyImmediate(old);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (env != null) go.transform.SetParent(env.transform, false);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[StreetDetailsBuilder] 거리 꾸미기를 씬에 놓았습니다.");
            return true;
        }

        static GameObject P(Transform parent, string rel, Vector3 at, float yaw, string name = null) =>
            Placer.Place(parent, ExternalArt.City(rel), at, Bottom, yaw, name: name);

        static bool Clear(float x) => Mathf.Abs(x) < ClearHalfWidth;

        // ① 양쪽 연석: 가로수를 기준으로 사이사이 작은 소품
        static void Curbs(Transform g, System.Random rng)
        {
            string[] trees = { "Environments/SM_Env_Tree_01", "Environments/SM_Env_Tree_02", "Environments/SM_Env_Tree_03" };
            string[] smalls = { "Props/SM_Prop_Hydrant_01", "Props/SM_Prop_Trashbin_01", "Props/SM_Prop_ParkingMeter_01", "Props/SM_Prop_ParkingMeter_02", "Props/SM_Prop_Mailbox_01" };
            for (float x = -60f; x <= 60f; x += 9f)
            {
                if (!Clear(x)) P(g, trees[rng.Next(trees.Length)], new Vector3(x, 0f, NearCurb), rng.Next(360), "Tree_Near");
                P(g, trees[rng.Next(trees.Length)], new Vector3(x + 4.5f, 0f, FarCurb), rng.Next(360), "Tree_Far");
                float sx = x + 3f;
                if (!Clear(sx)) P(g, smalls[rng.Next(smalls.Length)], new Vector3(sx, 0f, NearCurb - 0.2f), 0f, "Curb_Near");
                P(g, smalls[rng.Next(smalls.Length)], new Vector3(sx + 4.5f, 0f, FarCurb + 0.2f), 180f, "Curb_Far");
            }
            P(g, "Props/SM_Prop_BusStop_01", new Vector3(22f, 0f, NearCurb - 0.6f), 180f, "BusStop_Near");
            P(g, "Props/SM_Prop_Sign_Bustop_01", new Vector3(19.5f, 0f, NearCurb), 180f, "BusStopSign");
            P(g, "Props/SM_Prop_BusStop_01", new Vector3(-24f, 0f, FarCurb + 0.6f), 0f, "BusStop_Far");
            P(g, "Props/SM_Prop_TrafficLight_01", new Vector3(-12f, 0f, NearCurb), 180f, "TrafficLight");
            P(g, "Props/SM_Prop_TrafficLight_01", new Vector3(-12f, 0f, FarCurb), 0f, "TrafficLight");
            P(g, "Environments/SM_Env_Road_Crossing_01", new Vector3(-12f, -0.14f, (NearCurb + FarCurb) / 2f), 90f, "Crossing");
        }

        // ② 건물 앞 (벽에 붙여): 가게 같은 편은 거리(+Z)를 봄, 맞은편은 우리 가게(-Z)를 봄
        static void Walls(Transform g, System.Random rng)
        {
            string[] near = { "Props/SM_Prop_Planter_01", "Props/SM_Prop_Planter_02", "Props/SM_Prop_ParkBench_01", "Props/SM_Prop_Newspaper_01", "Props/SM_Prop_PotPlant_01", "Props/SM_Prop_ATM_01", "Props/SM_Prop_PowerBox_01" };
            for (float x = -58f; x <= 58f; x += 7f)
                if (!Clear(x)) P(g, near[rng.Next(near.Length)], new Vector3(x, 0f, NearWall), 0f, "Front_Near");

            string[] far = { "Props/SM_Prop_Planter_02", "Props/SM_Prop_PotPlant_02", "Props/SM_Prop_Newspaper_02", "Props/SM_Prop_ParkBench_01", "Props/SM_Prop_Trashbin_02" };
            for (float x = -58f; x <= 58f; x += 6f)
                P(g, far[rng.Next(far.Length)], new Vector3(x, 0f, FarWall), 180f, "Front_Far");
            // 맞은편 가게 앞 거리 풍경: 노점 · 공중전화 · 파라솔 테이블 (우리 가게 창문에서 보임)
            P(g, "Props/SM_Prop_HotdogStand_01", new Vector3(-5f, 0f, FarWall - 1.2f), 180f, "HotdogStand");
            P(g, "Props/SM_Prop_Phones_01", new Vector3(6f, 0f, FarWall), 180f, "Phones");
            P(g, "Props/SM_Prop_PicnicTable_01", new Vector3(12f, 0f, FarWall - 1.5f), 90f, "CafeTable");
            P(g, "Props/SM_Prop_Umbrella_01", new Vector3(12f, 0f, FarWall - 1.5f), 0f, "CafeUmbrella");
        }

        // ③ 가게 양옆 골목 입구 (폭 1.5m): 쓰레기봉투 · 상자 — 걷는 길(가운데)은 비움
        static void Alleys(Transform g)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (L.MaxX + 0.75f);
                P(g, "Props/SM_Prop_TrashBag_01", new Vector3(x - side * 0.35f, 0f, L.FrontZ + 0.35f), 30f, "TrashBag");
                P(g, "Props/SM_Prop_TrashBag_02", new Vector3(x - side * 0.1f, 0f, L.FrontZ + 0.25f), -20f, "TrashBag");
                P(g, "Props/SM_Prop_CardboardBox_02", new Vector3(x + side * 0.4f, 0f, L.FrontZ + 0.3f), 15f, "Box");
            }
            P(g, "Props/SM_Prop_CardboardBox_04", new Vector3(-(L.MaxX + 1.0f), 0.35f, L.FrontZ + 0.3f), 40f, "Box_Top");
        }

        // ④ 맞은편 가게 1층 위 큰 간판: 같은 가게 모델이 반복돼도 간판이 달라 서로 다른 가게로 보이게
        static void Signs(Transform g, System.Random rng)
        {
            string[] signs = { "Coffee", "Donut", "Pizza", "Burger", "Noodles", "Icecream", "Taco", "Popcorn", "Milkshake", "Hotdog", "Soda", "Guitar" };
            int k = rng.Next(signs.Length);
            for (float x = -55f; x <= 55f; x += 10f, k++)
                P(g, "Props/SM_Prop_LargeSign_" + signs[k % signs.Length] + "_01", new Vector3(x, 3.6f, FarFront - 0.4f), 180f, "Sign_Far");
            P(g, "Props/SM_Prop_Billboard_01", new Vector3(30f, 9f, FarFront + 4f), 180f, "Billboard");
            P(g, "Props/SM_Prop_Billboard_01", new Vector3(-34f, 9f, FarFront + 4f), 180f, "Billboard");
        }

        // ⑤ 맞은편 건물 줄 뒤 높은 빌딩 (안개에 반쯤 묻혀 거리가 깊어 보임)
        static void Skyline(Transform g, System.Random rng)
        {
            string[] towers = { "Buildings/SM_Bld_OfficeSquare_01", "Buildings/SM_Bld_OfficeSquare_02", "Buildings/SM_Bld_OfficeRound_01", "Buildings/SM_Bld_OfficeRound_02",
                                "Buildings/SM_Bld_OfficeOld_Large_01", "Buildings/SM_Bld_OfficeOld_Small_01", "Buildings/SM_Bld_OfficeSquare_03", "Buildings/SM_Bld_OfficeRound_03" };
            for (float x = -66f; x <= 66f; x += 14f)
                P(g, towers[rng.Next(towers.Length)], new Vector3(x + (float)rng.NextDouble() * 4f, 0f, FarFront + 16f + (float)rng.NextDouble() * 8f), 180f, "Tower");
            P(g, "Buildings/SM_Prop_Water_Tower_01", new Vector3(-18f, 10f, FarFront + 6f), 0f, "WaterTower");
        }
    }
}

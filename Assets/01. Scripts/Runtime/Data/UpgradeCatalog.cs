using System;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>방 업그레이드 분야 (집_업그레이드_설계 3장 표). 꾸미기 4종은 효과 없이 한 단계만.</summary>
    public enum UpgradeKind
    {
        Showcase = 0,
        Binder = 1,
        Computer = 2,
        Bed = 3,
        Poster = 4,
        Rug = 5,
        Lamp = 6,
        Figure = 7,
    }

    /// <summary>
    /// 방 업그레이드 목록 · 가격 · 효과 값 · 가구 모델 (집_업그레이드_설계 3장, DEC-031: 표대로 시작, E3에서 조정).
    /// 씬을 다시 빌드하지 않도록 Resources/UpgradeCatalog에서 불러옵니다(없으면 이 파일의 기본값).
    /// 모델 참조는 Tools > CardShop > Data > Build Upgrade Catalog가 modelPath를 보고 채웁니다.
    /// 단계: 1 = 처음 상태(무료), tracks[k].levels[0] = 2단계 구매, levels[1] = 3단계 구매.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        public const string ResourcePath = "UpgradeCatalog";

        /// <summary>가구 모델 하나를 방(HomeInterior 로컬 좌표)에 놓는 방법.</summary>
        [Serializable]
        public class Visual
        {
            [Tooltip("에디터 도구가 모델을 찾는 경로. 'LowPoly:Room/Bed2_02' (LowPolyInterior Prefabs 아래) 또는 'Assets/...prefab'")]
            public string modelPath;
            public GameObject model;
            [Tooltip("이 이름의 방 물건(예: Bed)을 숨기고 그 자리 · 방향 · 크기 그대로 바꿔 놓음. 비우면 아래 위치에 새로 놓음")]
            public string replaces;
            [Tooltip("집 실내 로컬 좌표. 모델 경계 상자의 align 점을 여기에 맞춤")]
            public Vector3 position;
            [Tooltip("경계 상자의 어느 점을 position에 맞출지 (각 축 -1 최소 · 0 가운데 · 1 최대). 바닥 (0,-1,0)")]
            public Vector3 align = new Vector3(0f, -1f, 0f);
            public float yaw;
            public float scale = 1f;
            [Tooltip("따뜻한 점 조명을 함께 켬 (조명 꾸미기)")]
            public bool addLight;
        }

        [Serializable]
        public class Level
        {
            public int price;
            [Tooltip("상점 화면에 보이는 효과 한 줄")]
            public string effect;
            public Visual[] visuals = new Visual[0];
        }

        [Serializable]
        public class Track
        {
            public UpgradeKind kind;
            public string displayName;
            [Tooltip("2단계부터의 구매 목록 (꾸미기는 1개)")]
            public Level[] levels = new Level[0];
        }

        public Track[] tracks = DefaultTracks();

        [Header("효과 (단계별 값, 첫 칸 = 1단계)")]
        [Tooltip("진열장 칸 수")]
        public int[] showcaseSlots = { 3, 6, 10 };
        [Tooltip("판매 등록 칸 추가 (EconomyConfig.salesDisplaySlots에 더함)")]
        public int[] extraSalesSlots = { 0, 2, 2 };
        [Tooltip("즉시 판매 비율 추가 (EconomyConfig.instantSellRate에 더함)")]
        public float[] instantSellBonus = { 0f, 0f, 0.1f };
        [Tooltip("커뮤니티 화면이 며칠 뒤 인기 속성까지 보여 주나 (1 = 내일, 2 = 모레)")]
        public int[] forecastDays = { 1, 2, 2 };
        [Tooltip("이 컴퓨터 단계부터 카드 시세표(오늘 · 내일 값)")]
        public int priceListLevel = 3;
        [Tooltip("손님 인내 시간 추가 비율 (0.1 = +10%)")]
        public float[] patienceBonus = { 0f, 0.1f, 0.2f };
        [Tooltip("매일 퇴근 후 무료 팩 추가")]
        public int[] extraFreePacks = { 0, 0, 1 };
        [Tooltip("이 바인더 단계부터 금박 바인더 UI")]
        public int goldBinderLevel = 3;
        [Tooltip("이 진열장 단계부터 진열장 조명")]
        public int showcaseLightLevel = 3;

        [Header("모델")]
        [Tooltip("진열장에 세울 카드 (03. Prefabs/Cards/Card)")]
        public CardView showcaseCardPrefab;
        [Tooltip("택배 상자 모델 (비우면 갈색 상자)")]
        public GameObject deliveryBoxPrefab;
        public string deliveryBoxPath = "LowPoly:Room/Box";

        /// <summary>Resources에서 불러오고, 없으면 코드 기본값으로 만든 것 (모델 없음).</summary>
        public static UpgradeCatalog Load()
        {
            var c = Resources.Load<UpgradeCatalog>(ResourcePath);
            return c != null ? c : CreateInstance<UpgradeCatalog>();
        }

        public Track Get(UpgradeKind kind)
        {
            if (tracks != null)
                foreach (Track t in tracks)
                    if (t != null && t.kind == kind) return t;
            return null;
        }

        /// <summary>단계별 값 (1단계 = 첫 칸). 칸이 모자라면 마지막 값.</summary>
        public static T At<T>(T[] byLevel, int level, T fallback)
        {
            if (byLevel == null || byLevel.Length == 0) return fallback;
            return byLevel[Mathf.Clamp(level - 1, 0, byLevel.Length - 1)];
        }

        // ------------------------------------------------------------------ 기본값 (설계서 3장 표, 달러)

        static Track[] DefaultTracks() => new[]
        {
            T(UpgradeKind.Showcase, "진열장",
                L(40, "진열 칸 3 → 6칸"),
                L(100, "진열 칸 6 → 10칸 + 진열장 조명")),
            T(UpgradeKind.Binder, "바인더",
                L(60, "판매 등록 칸 4 → 6칸", V("LowPoly:Room/NoteBook_02", new Vector3(-1.72f, 0.8f, 1.72f), 180f)),
                L(140, "금박 바인더 · 즉시 판매 60 → 70%", V("LowPoly:Room/Book_03", new Vector3(-1.72f, 0.8f, 1.45f), 180f))),
            T(UpgradeKind.Computer, "컴퓨터",
                L(70, "모레 인기 속성까지 보기", V("LowPoly:Light/Lamp_04", new Vector3(1.78f, 0.71f, 1.72f), 180f)),
                L(150, "카드 시세표 (오늘 · 내일 값)", V("LowPoly:Room/SmartHome", new Vector3(1.5f, 0.71f, 1.75f), 180f))),
            T(UpgradeKind.Bed, "침대",
                L(70, "손님 인내심 +10%", R("LowPoly:Room/Bed2_02", "Bed")),
                L(130, "손님 인내심 +20% · 무료 팩 매일 +1", R("LowPoly:Room/Bed2_03", "Bed"))),
            T(UpgradeKind.Poster, "포스터",
                L(15, "꾸미기", V("LowPoly:Room/Picture_03", new Vector3(0f, 1.55f, -2.88f), 0f, new Vector3(0f, 0f, -1f)))),
            T(UpgradeKind.Rug, "러그",
                L(20, "꾸미기", R("LowPoly:Room/Catpet_03", "Rug"))),
            T(UpgradeKind.Lamp, "스탠드 조명",
                L(25, "꾸미기", V("LowPoly:Light/Lamp_01", new Vector3(-2.15f, 0f, -0.75f), 90f, light: true))),
            T(UpgradeKind.Figure, "슬라임 피규어",
                L(30, "꾸미기", V("Assets/03. Prefabs/Interior/Decor/SlimeFigure_Pink.prefab", new Vector3(0.72f, 0.71f, 1.7f), 180f, scale: 3f))),
        };

        static Track T(UpgradeKind kind, string name, params Level[] levels) => new Track { kind = kind, displayName = name, levels = levels };
        static Level L(int price, string effect, params Visual[] visuals) => new Level { price = price, effect = effect, visuals = visuals };

        static Visual V(string path, Vector3 pos, float yaw, Vector3? align = null, float scale = 1f, bool light = false) =>
            new Visual { modelPath = path, position = pos, yaw = yaw, align = align ?? new Vector3(0f, -1f, 0f), scale = scale, addLight = light };

        static Visual R(string path, string replaces) => new Visual { modelPath = path, replaces = replaces };
    }
}

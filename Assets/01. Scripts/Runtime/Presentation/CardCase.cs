using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Interaction;
using CardShop.Logic;
using CardShop.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 보관함 (카드보관함_설계 J5 · J6, 예전 ShowcaseShelf 대신): 컴퓨터 모니터 위 벽에 나무 틀 + 카드 홈 10칸(2줄 × 5).
    /// 진열장 단계(3 · 6 · 10칸)보다 뒤의 칸은 덮개로 가리고, 3단계에는 조명이 켜집니다. 카드는 홈에 꽂혀 떠 보이지 않습니다.
    /// 자리는 씬의 CardCaseAnchor(있으면) → 없으면 모니터(커뮤니티 화면) 위.
    ///
    /// 손으로 하는 일:
    /// - 꺼내기: 진열 패널 [진열장에 세우기]를 누르면 그 카드가 바인더 위에 떠오름(PullOut). 집어서 보관함 칸 근처에서 놓으면 꽂힘(진열 확정).
    ///   다른 곳에 놓으면 바인더 위로 돌아가 기다림. 집을 나가면 사라짐(데이터는 꽂힐 때만 바뀜).
    /// - 옮기기 · 내리기: 꽂힌 카드를 쥐고 다른 칸에 놓으면 그 칸으로, 바인더 위에 놓으면 진열을 내림(보유로). 그 밖의 곳이면 원래 칸으로(Q7).
    /// 칸 판정은 소켓 대신 거리(종이봉투 · 쓰레기통과 같은 방식). 바인더에서 그립으로 바로 꺼내기(J5-c)는 아직 없음.
    /// </summary>
    public sealed class CardCase : MonoBehaviour
    {
        const int PerRow = 5, Rows = 2, MaxSlots = PerRow * Rows;
        const float CardScale = 1.3f;
        const float Spacing = 0.1f;          // 카드 사이 (카드 폭 63mm × 1.3 = 82mm)
        const float RowGap = 0.17f;          // 줄 사이 (카드 높이 88mm × 1.3 = 114mm)
        const float TiltDegrees = 8f;        // 홈에 꽂혀 살짝 뒤로 기댐
        const float ReturnSeconds = 0.4f;
        const float SlotSnap = 0.08f;        // 칸 가운데에서 이 거리 안에서 놓으면 그 칸
        const float CaseSnap = 0.25f;        // 틀 가운데에서 이 거리 안이면 (칸을 못 맞춰도) 첫 빈 칸
        const float BinderDrop = 0.3f;       // 바인더에서 이 거리 안에서 놓으면 진열 내림
        const float AboveMonitor = 0.05f;    // 모니터 위 끝과 틀 아래 끝 사이 (집_컴퓨터_화면_설계와 겹치지 않게)
        static readonly Color Wood = new Color(0.36f, 0.24f, 0.16f);
        static readonly Color Cover = new Color(0.16f, 0.12f, 0.1f);

        public static CardCase Instance { get; private set; }

        GameContext ctx;
        UpgradeCatalog catalog;
        BinderView binder;
        bool built;
        GameObject secondLedge;
        readonly GameObject[] covers = new GameObject[MaxSlots];
        Light spot;
        readonly List<CardRevealView> views = new List<CardRevealView>();
        readonly List<string> shown = new List<string>();
        CardRevealView loose;
        string looseId;

        public static CardCase Create(Transform homeInterior, UpgradeCatalog catalog, BinderView binder)
        {
            var go = new GameObject("CardCase");
            go.transform.SetParent(homeInterior, false);
            var c = go.AddComponent<CardCase>();
            c.catalog = catalog;
            c.binder = binder;
            return c;
        }

        void Awake() => Instance = this;

        void OnEnable() => ctx = GameContext.Instance;

        void OnDisable() => HideLoose(); // 집을 나가면 꺼낸 카드는 사라짐 (집 실내가 꺼짐)

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ 틀 만들기 (처음 켜질 때 한 번)

        void Build()
        {
            built = true;
            Pose pose = CardCaseAnchor.TryFind(out Pose anchored) ? anchored : DefaultPose();
            transform.SetPositionAndRotation(pose.position, pose.rotation);

            float width = PerRow * Spacing + 0.06f, height = Rows * RowGap + 0.04f;
            // 집 가구의 머티리얼을 복제해 색만 바꿈 (셰이더를 이름으로 찾지 않아 빌드에서 빠질 걱정 없음, 예전 진열대와 같은 방식)
            Transform shelf = transform.parent != null ? HomeRoom.FindDeep(transform.parent, "Bookshelf") : null;
            Renderer src = shelf != null ? shelf.GetComponentInChildren<Renderer>() : transform.parent != null ? transform.parent.GetComponentInChildren<Renderer>() : null;
            Material wood = HomeRoom.Tinted(src != null ? src.sharedMaterial : null, Wood);
            Material cover = HomeRoom.Tinted(src != null ? src.sharedMaterial : null, Cover);
            Box("Back", new Vector3(0f, 0f, -0.01f), new Vector3(width, height, 0.02f), wood);
            Box("Top", new Vector3(0f, height / 2f, 0.03f), new Vector3(width, 0.02f, 0.08f), wood);
            Box("Bottom", new Vector3(0f, -height / 2f, 0.03f), new Vector3(width, 0.02f, 0.08f), wood);
            Box("Left", new Vector3(-width / 2f, 0f, 0.03f), new Vector3(0.02f, height, 0.08f), wood);
            Box("Right", new Vector3(width / 2f, 0f, 0.03f), new Vector3(0.02f, height, 0.08f), wood);
            for (int r = 0; r < Rows; r++)
            {
                GameObject ledge = Box($"Groove_{r}", new Vector3(0f, RowY(r) - CardView.Height * CardScale * 0.5f, 0.045f), new Vector3(width - 0.02f, 0.01f, 0.03f), wood);
                if (r == 1) secondLedge = ledge;
            }
            for (int i = 0; i < MaxSlots; i++)
            {
                Vector3 c = SlotLocal(i);
                covers[i] = Box($"Cover_{i}", new Vector3(c.x, c.y, 0.06f), new Vector3(Spacing - 0.012f, CardView.Height * CardScale + 0.01f, 0.006f), cover);
            }

            var lightGo = new GameObject("CaseLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, height / 2f + 0.1f, 0.35f);
            spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Point;
            spot.color = new Color(1f, 0.9f, 0.7f);
            spot.intensity = 1.6f;
            spot.range = 1.2f;
            spot.shadows = LightShadows.None;
            lightGo.SetActive(false);
        }

        // 모니터(커뮤니티 화면 글자) 위, 벽 쪽. 화면이 보는 쪽(-Z)이 방 쪽
        Pose DefaultPose()
        {
            var screen = FindAnyObjectByType<CommunityScreen>(FindObjectsInactive.Include);
            if (screen == null) return new Pose(transform.parent.TransformPoint(new Vector3(1.2f, 2.0f, 1.83f)), transform.parent.rotation * Quaternion.LookRotation(Vector3.back));
            Transform t = screen.transform;
            Vector3 toRoom = t.rotation * Vector3.back;
            float half = (Rows * RowGap + 0.04f) / 2f;
            Vector3 pos = t.position + Vector3.up * (0.45f + AboveMonitor + half) - toRoom * 0.08f; // 모니터 높이 0.9m의 반, 벽에 붙게 조금 뒤로
            return new Pose(pos, Quaternion.LookRotation(toRoom, Vector3.up));
        }

        GameObject Box(string name, Vector3 local, Vector3 size, Material mat) =>
            RuntimeShapes.Create(PrimitiveType.Cube, name, transform, local, size, mat);

        static float RowY(int row) => (Rows - 1) / 2f * RowGap - row * RowGap;
        static Vector3 SlotLocal(int index) => new Vector3((index % PerRow - (PerRow - 1) / 2f) * Spacing, RowY(Mathf.Min(index / PerRow, Rows - 1)), 0.05f);

        /// <summary>칸 번호 → 카드 자리. 위 줄부터 왼쪽 → 오른쪽, 앞면이 방을 보고 살짝 뒤로 기댐.</summary>
        void SlotPose(int index, out Vector3 position, out Quaternion rotation)
        {
            position = transform.TransformPoint(SlotLocal(index));
            rotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(TiltDegrees, 0f, 0f);
        }

        // ------------------------------------------------------------------ 진열 목록 따라가기

        int Capacity => ctx.Upgrades.ShowcaseSlots(ctx.Session.Upgrades);

        void LateUpdate()
        {
            if (ctx == null || ctx.Session == null) return;
            if (!built) Build(); // 처음 켜졌을 때 한 번 (집 실내가 켜져 있을 때 자리 계산)
            GameSession s = ctx.Session;
            int capacity = Mathf.Min(Capacity, MaxSlots);
            if (secondLedge != null) secondLedge.SetActive(capacity > PerRow);
            for (int i = 0; i < MaxSlots; i++) covers[i].SetActive(i >= capacity); // 잠긴 칸은 덮개 (Q3)
            spot.gameObject.SetActive(ctx.Upgrades.ShowcaseLight(s.Upgrades));

            for (int i = 0; i < s.Showcase.Count && i < MaxSlots; i++)
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
                CardRevealView v = CreateView($"CaseCard_{views.Count}");
                if (v == null) return;
                v.GrabEnded += OnCaseCardReleased;
                views.Add(v);
            }
            while (shown.Count <= index) shown.Add(null);
            shown[index] = cardId;
            if (card == null) { views[index].Hide(); return; }
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            views[index].Show(card, false, pos, rot, CardScale);
        }

        CardRevealView CreateView(string name)
        {
            if (catalog == null || catalog.showcaseCardPrefab == null)
            {
                Debug.LogWarning("[CardCase] UpgradeCatalog에 카드 프리팹이 없습니다 (Tools > CardShop > Data > Build Upgrade Catalog).", this);
                return null;
            }
            CardView card = Instantiate(catalog.showcaseCardPrefab, transform);
            card.name = name;
            var view = card.gameObject.AddComponent<CardRevealView>();
            var grab = card.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.interactionLayers = 1; // Default만: 바인더 표지(Card 층)에 꽂혀 들어가지 않게
            return view;
        }

        // 꽂힌 카드를 놓음: 바인더 위 = 내리기, 다른 칸 = 옮기기, 그 밖 = 원래 칸으로
        void OnCaseCardReleased(CardRevealView view)
        {
            int index = views.IndexOf(view);
            GameSession s = ctx != null ? ctx.Session : null;
            if (index < 0 || s == null || index >= s.Showcase.Count || !isActiveAndEnabled) return;
            Vector3 p = view.transform.position;
            if (NearBinder(p) && ShowcaseRules.Remove(s, s.Showcase[index]))
            {
                Sfx.Play(SoundId.PileStash, p);
                return; // 목록이 줄어 LateUpdate가 숨김
            }
            int slot = NearestSlot(p, SlotSnap);
            if (slot >= 0 && slot != index && ShowcaseRules.MoveTo(s, index, slot))
            {
                Sfx.Play(SoundId.RackInsert, p);
                Buzz(0.3f);
                shown.Clear(); // 칸이 바뀌었으니 모두 다시 세움
                for (int i = 0; i < views.Count; i++) views[i].Hide();
                return;
            }
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            view.FlyTo(pos, rot, ReturnSeconds, CardScale);
        }

        // ------------------------------------------------------------------ 꺼내기 (진열 패널 버튼)

        /// <summary>바인더 위에 그 카드를 띄워 손으로 꽂게 함. 칸이 없거나 보유가 없으면 그 이유</summary>
        public ShowcaseResult PullOut(CardData card)
        {
            if (card == null || ctx == null || ctx.Session == null) return ShowcaseResult.NotOwned;
            GameSession s = ctx.Session;
            if (!ShowcaseRules.HasRoom(s, Mathf.Min(Capacity, MaxSlots))) return ShowcaseResult.Full;
            if (s.Collection.CountOf(card.id) <= 0) return ShowcaseResult.NotOwned;
            if (loose == null)
            {
                loose = CreateView("LooseCard");
                if (loose == null) return ShowcaseResult.NotOwned;
                loose.GrabEnded += OnLooseReleased;
            }
            looseId = card.id;
            LoosePose(out Vector3 pos, out Quaternion rot);
            loose.Show(card, false, pos, rot, CardScale);
            return ShowcaseResult.Ok;
        }

        void OnLooseReleased(CardRevealView view)
        {
            GameSession s = ctx != null ? ctx.Session : null;
            if (s == null || looseId == null || !isActiveAndEnabled) return;
            Vector3 p = view.transform.position;
            int capacity = Mathf.Min(Capacity, MaxSlots);
            int slot = NearestSlot(p, SlotSnap);
            bool inCase = slot >= 0 || Vector3.Distance(p, transform.position) <= CaseSnap;
            if (inCase && ShowcaseRules.TryAdd(s, looseId, capacity) == ShowcaseResult.Ok)
            {
                if (slot >= 0) ShowcaseRules.MoveTo(s, s.Showcase.Count - 1, slot);
                Sfx.Play(SoundId.RackInsert, p);
                Buzz(0.4f);
                shown.Clear();
                for (int i = 0; i < views.Count; i++) views[i].Hide();
                HideLoose();
                return;
            }
            LoosePose(out Vector3 pos, out Quaternion rot);
            view.FlyTo(pos, rot, ReturnSeconds, CardScale); // 꽂히지 않으면 바인더 위로 돌아가 기다림
        }

        void LoosePose(out Vector3 position, out Quaternion rotation)
        {
            Transform b = binder != null ? binder.transform : transform;
            position = b.position + Vector3.up * 0.3f;
            Camera cam = Camera.main;
            Vector3 toEye = cam != null ? cam.transform.position - position : -b.forward;
            toEye.y = 0f;
            rotation = Quaternion.LookRotation(toEye.sqrMagnitude > 0.0001f ? -toEye : Vector3.forward, Vector3.up);
        }

        void HideLoose()
        {
            if (loose != null) loose.Hide();
            looseId = null;
        }

        // ------------------------------------------------------------------ 판정

        bool NearBinder(Vector3 p) => binder != null && Vector3.Distance(p, binder.transform.position) <= BinderDrop;

        int NearestSlot(Vector3 world, float within)
        {
            int capacity = ctx != null && ctx.Session != null ? Mathf.Min(Capacity, MaxSlots) : 0;
            int best = -1;
            float bestD = within;
            for (int i = 0; i < capacity; i++)
            {
                float d = Vector3.Distance(world, transform.TransformPoint(SlotLocal(i)));
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static void Buzz(float amplitude) => Haptics.BothHands(amplitude, 0.06f);
    }
}

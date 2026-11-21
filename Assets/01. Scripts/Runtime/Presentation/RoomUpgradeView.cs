using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 방이 업그레이드 상태(GameSession.Upgrades)를 그대로 보여 주게 합니다 (집_업그레이드_설계 3장 E2).
    /// - 가구: 상자를 연 단계까지의 모델을 놓거나(UpgradeCatalog.Visual) 원래 가구를 바꿔 놓음(예: 침대). 새 게임이면 처음 방으로
    /// - 택배: 와 있는 상자(UpgradeStatus.Arrived)마다 DeliveryBox를 바닥에 놓고, 잡아서 열면 UpgradeState.Open → 가구가 바뀜.
    ///   방금 산 것은 DoorbellDelay 뒤 초인종 소리 · "택배 왔어요"와 함께 나타남 (DEC-040 U5)
    /// - 바인더 3단계: 바인더 · 판매 · 진열장 패널 바탕을 금색으로
    /// 집 실내(HomeInterior) 아래에 있어 집에서만 켜집니다. 모델 경계 상자는 켜져 있을 때만 정확하므로 켜진 동안에만 다시 그립니다.
    /// </summary>
    public sealed class RoomUpgradeView : MonoBehaviour
    {
        // 택배 상자 자리: 첫 상자 자리에서 오른쪽(+X)으로 한 줄 4개씩, 줄이 넘치면 뒤(−Z)로.
        // 첫 상자 자리 = 씬의 자리표("Home/DeliveryBoxes")가 있으면 그 자리 · 방향, 없으면 집 실내 로컬 BoxStart(러그 앞쪽)
        public const string BoxAnchorKey = "Home/DeliveryBoxes";
        static readonly Vector3 BoxStart = new Vector3(-0.7f, 0f, -0.05f);
        public const float BoxStepX = 0.45f, BoxStepZ = -0.5f;
        public const int BoxesPerRow = 4;

        /// <summary>첫 택배 상자 기본 자리 (자리표가 없을 때)</summary>
        public static Pose DefaultBoxPose(Transform homeInterior) =>
            new Pose(homeInterior.TransformPoint(BoxStart), Quaternion.Euler(0f, homeInterior.eulerAngles.y, 0f));
        static readonly Color GoldPanel = new Color(0.55f, 0.42f, 0.12f, 0.96f);

        UpgradeCatalog catalog;
        Transform interior;
        TMP_FontAsset font;
        BinderView binder;
        GameContext ctx;
        UpgradeState bound;
        WorldToast toast;
        bool dirty = true;

        readonly Dictionary<UpgradeKind, int> applied = new Dictionary<UpgradeKind, int>();
        readonly Dictionary<UpgradeKind, List<GameObject>> spawned = new Dictionary<UpgradeKind, List<GameObject>>();
        readonly Dictionary<UpgradeKind, List<Transform>> hidden = new Dictionary<UpgradeKind, List<Transform>>();
        readonly Dictionary<UpgradeKind, DeliveryBox> boxes = new Dictionary<UpgradeKind, DeliveryBox>();
        // 그날 밤 택배 (DEC-040 U5): 산 뒤 잠깐 있다가 초인종(현관 소리)과 함께 상자가 나타남
        public const float DoorbellDelay = 4f;
        readonly Dictionary<UpgradeKind, float> doorbellAt = new Dictionary<UpgradeKind, float>();
        bool primed; // 집에 들어온 뒤 첫 그리기는 기다리지 않고 바로 놓음
        static RoomUpgradeView current;

        /// <summary>샀지만 아직 초인종이 안 울림 (상점 리본이 "택배 도착!"을 먼저 말하지 않게)</summary>
        public static bool OnTheWay(UpgradeKind kind) => current != null && current.doorbellAt.ContainsKey(kind);
        /// <summary>초인종을 기다리는 택배 수 (바뀌면 상점 화면을 다시 그림)</summary>
        public static int WaitingCount => current != null ? current.doorbellAt.Count : 0;
        readonly Dictionary<Image, Color> panelColors = new Dictionary<Image, Color>();
        bool gold;

        public static RoomUpgradeView Create(Transform homeInterior, UpgradeCatalog catalog, TMP_FontAsset font, BinderView binder)
        {
            var go = new GameObject("RoomUpgrades");
            go.SetActive(false);
            go.transform.SetParent(homeInterior, false);
            var view = go.AddComponent<RoomUpgradeView>();
            view.interior = homeInterior;
            view.catalog = catalog;
            view.font = font;
            view.binder = binder;
            go.SetActive(true);
            return view;
        }

        void OnEnable()
        {
            current = this;
            ctx = GameContext.Instance;
            if (ctx == null || catalog == null) return;
            ctx.Events.SessionStarted += Bind;
            Bind(ctx.Session);
        }

        void OnDisable()
        {
            if (ctx != null) ctx.Events.SessionStarted -= Bind;
            Bind(null);
            if (current == this) current = null;
        }

        void Bind(GameSession session)
        {
            if (bound != null) bound.Changed -= MarkDirty;
            bound = session?.Upgrades;
            if (bound != null) bound.Changed += MarkDirty;
            dirty = true;
            primed = false;
            doorbellAt.Clear();
        }

        void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (bound == null || (!dirty && doorbellAt.Count == 0)) return;
            dirty = false;
            foreach (UpgradeCatalog.Track t in catalog.tracks)
                if (t != null) ApplyFurniture(t, bound.LevelOf(t.kind));
            SyncBoxes();
            SetGoldBinder(ctx.Upgrades.GoldBinder(bound));
        }

        // ------------------------------------------------------------------ 가구

        void ApplyFurniture(UpgradeCatalog.Track track, int level)
        {
            if (applied.TryGetValue(track.kind, out int now) && now == level) return;
            applied[track.kind] = level;

            // 처음 방으로 되돌린 뒤 1 ~ level 단계의 모델을 놓음 (놓는 규칙 · 자리표는 UpgradeFurniture, F5 · F7)
            if (spawned.TryGetValue(track.kind, out List<GameObject> old)) foreach (GameObject go in old) if (go != null) Destroy(go);
            if (hidden.TryGetValue(track.kind, out List<Transform> originals)) foreach (Transform t in originals) if (t != null) t.gameObject.SetActive(true);
            var covered = hidden[track.kind] = new List<Transform>();
            // 씬에 "분야_Tier_n"을 직접 놓았으면 그것만 켜고 카탈로그 모델은 안 씀 (지금 침대: Bed_Tier_0 · 1 · 2)
            spawned[track.kind] = SceneTiers.Apply(interior, track.kind, level)
                ? new List<GameObject>()
                : UpgradeFurniture.Build(interior, track, level, covered, UpgradeFurniture.FromScene);
            DecorReaction.Attach(track.kind, spawned[track.kind], interior, font); // 꾸미기 생활 반응 (DEC-040 U3)
        }

        // ------------------------------------------------------------------ 택배 상자

        void SyncBoxes()
        {
            List<UpgradeKind> arrived = bound.Arrived();
            var gone = new List<UpgradeKind>();
            foreach (KeyValuePair<UpgradeKind, DeliveryBox> e in boxes)
                if (!arrived.Contains(e.Key)) gone.Add(e.Key);
            foreach (UpgradeKind k in gone)
            {
                if (boxes[k] != null) Destroy(boxes[k].gameObject);
                boxes.Remove(k);
            }

            Pose start = PlacementAnchor.Find(BoxAnchorKey, DefaultBoxPose(interior));
            float startYaw = start.rotation.eulerAngles.y;
            for (int i = 0; i < arrived.Count; i++)
            {
                UpgradeKind kind = arrived[i];
                if (boxes.TryGetValue(kind, out DeliveryBox existing) && existing != null) continue;
                if (primed && !RingDoorbell(kind)) continue; // 방금 산 것: 초인종이 울릴 때까지 기다림
                var local = new Vector3((i % BoxesPerRow) * BoxStepX, 0f, (i / BoxesPerRow) * BoxStepZ);
                Vector3 world = start.position + Quaternion.Euler(0f, startYaw, 0f) * local;
                DeliveryBox box = DeliveryBox.Create(transform, world, startYaw + 15f * i, kind, TrackName(kind),
                    catalog.deliveryBoxPrefab, AnyMaterial(), font);
                box.Opened += OnBoxOpened;
                boxes[kind] = box;
            }
            primed = true;
            foreach (UpgradeKind k in new List<UpgradeKind>(doorbellAt.Keys))
                if (!arrived.Contains(k)) doorbellAt.Remove(k);
        }

        // 처음 보면 초인종 시각을 정하고 false, 그 시각이 되면 소리 · 알림을 내고 true
        bool RingDoorbell(UpgradeKind kind)
        {
            if (!doorbellAt.TryGetValue(kind, out float at))
            {
                doorbellAt[kind] = Time.time + DoorbellDelay;
                return false;
            }
            if (Time.time < at) return false;
            doorbellAt.Remove(kind);
            Pose door = PlacementAnchor.Find(BoxAnchorKey, DefaultBoxPose(interior));
            Sfx.Play(SoundId.HomeDoor, door.position);
            if (toast == null) toast = WorldToast.Create(transform, font);
            toast.Show($"띵동! {TrackName(kind)} 택배 왔어요", door.position + Vector3.up * 0.9f);
            ctx?.Events.RaiseHappened(GameMoment.DeliveryArrived); // 튜토리얼 첫 택배 안내
            return true;
        }

        void OnBoxOpened(DeliveryBox box)
        {
            boxes.Remove(box.Kind);
            if (bound == null || !bound.Open(box.Kind)) return;
            int level = bound.LevelOf(box.Kind);
            string what = UpgradeRules.IsDecor(box.Kind) ? $"{TrackName(box.Kind)} 도착! 방에 놓았어요" : $"{TrackName(box.Kind)} Lv{level} 완성!";
            if (toast == null) toast = WorldToast.Create(transform, font);
            toast.Show(what, box.transform.position + Vector3.up * 0.9f);
            Sfx.Play(SoundId.NewCard, box.transform.position);
        }

        string TrackName(UpgradeKind kind) => catalog.Get(kind)?.displayName ?? kind.ToString();

        Material AnyMaterial()
        {
            Renderer r = interior.GetComponentInChildren<Renderer>();
            return r != null ? r.sharedMaterial : null;
        }

        // ------------------------------------------------------------------ 금박 바인더 (바인더 3단계)

        void SetGoldBinder(bool on)
        {
            if (on == gold || binder == null) return;
            gold = on;
            foreach (Image img in binder.GetComponentsInChildren<Image>(true))
            {
                if (img.name != "Background") continue;
                if (!panelColors.ContainsKey(img)) panelColors[img] = img.color;
                img.color = on ? GoldPanel : panelColors[img];
            }
        }
    }
}

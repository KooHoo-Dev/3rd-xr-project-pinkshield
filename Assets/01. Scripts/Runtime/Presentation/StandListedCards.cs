using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Customers;
using CardShop.Logic;
using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 가게 진열대에 판매 등록 카드를 실제로 세움 (11-21 피드백 P1: 등록한 카드가 가게 어디에도 안 보였음).
    /// 카드마다 작은 받침 + 보기 전용 실제 카드(DisplayCards) + 가격표("$N", 오늘 인기 1순위 빨강 ▲ · 2순위 주황 ▲ · 하락 파랑 ▼).
    /// 손님이 그 카드를 사서 계산대에 올리면 진열대에서 빠지고 빈 받침이 남습니다(결정 P1 추천안). 다음 날 목록이 바뀌면 다시 세움.
    /// 자리는 씬의 ListedCardAnchor, 없으면 SalesStand 윗면 앞쪽. 씬을 다시 빌드하지 않아도 되게 계산대에 스스로 붙습니다.
    /// 이 가격표가 경제 설계의 "진열대 가격표 화살표"이고, 튜토리얼 biz-popular가 인기 카드 받침을 가리킵니다(CollectTutorialTargets).
    /// </summary>
    public sealed class StandListedCards : MonoBehaviour
    {
        const float CheckSeconds = 0.4f;
        const float MaxSpacing = 0.17f;   // 칸 사이 (카드 폭 0.063 + 여유)
        const float LeanDegrees = 15f;    // 윗부분이 뒤로
        const float BaseHeight = 0.012f;
        static readonly Color BaseColor = new Color(0.82f, 0.88f, 0.95f, 0.55f); // 아크릴 느낌 (반투명은 셰이더에 따라 불투명일 수 있음)
        static readonly Color TagColor = new Color(0.2f, 0.17f, 0.28f);          // 어두운 중간톤 판 + 미색 글자 (VR 눈 편한 색)

        sealed class Slot
        {
            public Transform Root;
            public CardView Card;
            public TextMeshPro Tag;
            public string CardId;
            public bool Popular; // 오늘 인기 1 · 2순위 (튜토리얼 biz-popular가 가리킴)
        }

        /// <summary>튜토리얼이 가리킬 것: 진열대에 남은 오늘 인기 카드 받침들, 없으면 줄 전체 (11-21 보완 B4).</summary>
        public void CollectTutorialTargets(List<GameObject> results)
        {
            foreach (Slot s in slots)
                if (s.Popular && s.Root != null && s.Card != null && s.Card.gameObject.activeSelf) results.Add(s.Root.gameObject);
            if (results.Count == 0 && row != null && slots.Count > 0) results.Add(row.gameObject);
        }

        CheckoutCounter counter;
        Transform row;
        readonly List<Slot> slots = new List<Slot>();
        readonly List<string> shown = new List<string>();
        float nextCheck;
        int shownDay = -1;
        string onCounter;
        float rowWidth = 1.3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var c = FindAnyObjectByType<CheckoutCounter>();
            if (c != null && c.GetComponent<StandListedCards>() == null) c.gameObject.AddComponent<StandListedCards>();
        }

        void Awake() => counter = GetComponent<CheckoutCounter>();

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckSeconds;
            GameContext ctx = GameContext.Instance;
            if (ctx == null || ctx.Session == null) return;
            if (row == null && !BuildRow(ctx)) return;

            CardData taken = counter != null ? counter.CardOnCounter : null;
            string takenId = taken != null ? taken.id : null;
            List<string> list = ctx.Session.SalesDisplay;
            if (shownDay == ctx.Session.Day && takenId == onCounter && SameAsShown(list)) return;

            bool newDay = shownDay != ctx.Session.Day || !ContainsAll(list);
            shownDay = ctx.Session.Day;
            onCounter = takenId;
            if (newDay) Rebuild(ctx, list);
            // 사 간 카드(계산대 위 · 결제로 목록에서 빠진 것)는 빈 받침만
            foreach (Slot s in slots)
            {
                bool there = s.CardId != null && s.CardId != takenId && list.Contains(s.CardId);
                if (s.Card != null) s.Card.gameObject.SetActive(there);
                if (s.Tag != null) s.Tag.gameObject.SetActive(there);
            }
            shown.Clear();
            shown.AddRange(list);
        }

        bool SameAsShown(List<string> list)
        {
            if (list.Count != shown.Count) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] != shown[i]) return false;
            return true;
        }

        // 지금 세운 카드가 모두 목록에 있고 새 카드만 없으면(팔려서 줄어든 것) 다시 세우지 않음
        bool ContainsAll(List<string> list)
        {
            foreach (string id in list)
            {
                bool found = false;
                foreach (Slot s in slots) if (s.CardId == id) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        void Rebuild(GameContext ctx, List<string> list)
        {
            foreach (Slot s in slots) if (s.Root != null) Destroy(s.Root.gameObject);
            slots.Clear();
            int n = list.Count;
            if (n == 0) return;
            float spacing = Mathf.Min(MaxSpacing, rowWidth / n);
            MarketDay market = ctx.MarketToday;
            for (int i = 0; i < n; i++)
            {
                CardData data = ctx.Cards.Get(list[i]);
                if (data == null) continue;
                float x = (i - (n - 1) / 2f) * spacing;
                slots.Add(MakeSlot(i, x, data, ctx, market));
            }
        }

        Slot MakeSlot(int index, float x, CardData data, GameContext ctx, MarketDay market)
        {
            var root = new GameObject($"Listed_{index}").transform;
            root.SetParent(row, false);
            root.localPosition = new Vector3(x, 0f, 0f);

            // 받침: 납작한 판 + 뒤로 기운 등판. 줄의 +Z = 보는 사람 쪽, -Z = 뒤
            Visual(root, new Vector3(0.075f, BaseHeight, 0.05f), new Vector3(0f, BaseHeight / 2f, 0f), Quaternion.identity, BaseColor);
            Visual(root, new Vector3(0.07f, 0.06f, 0.004f), new Vector3(0f, BaseHeight + 0.03f, -0.012f), Quaternion.Euler(-LeanDegrees, 0f, 0f), BaseColor);

            var slot = new Slot { Root = root, CardId = data.id, Popular = ctx.EconomyRules.PopularRank(data, market) > 0 };
            slot.Card = DisplayCards.Create(root, "ListedCard");
            if (slot.Card != null)
            {
                slot.Card.Bind(data);
                Transform t = slot.Card.transform;
                t.localScale = Vector3.one; // 프리팹 그대로 = 실제 카드 크기 (높이 0.088m)
                // 카드 앞면(-Z)이 줄의 forward(보는 사람) 쪽을 보게 180° 돌리고, 윗부분을 뒤(-Z) 등판 쪽으로 기울임
                t.localRotation = Quaternion.Euler(LeanDegrees, 180f, 0f);
                t.localPosition = new Vector3(0f, BaseHeight + CardView.Height / 2f * Mathf.Cos(LeanDegrees * Mathf.Deg2Rad), -0.009f);
            }

            // 가격표: 받침 앞에 비스듬히 눕힌 작은 판
            var tagRoot = new GameObject("PriceTag").transform;
            tagRoot.SetParent(root, false);
            tagRoot.localPosition = new Vector3(0f, BaseHeight + 0.002f, 0.03f);
            tagRoot.localRotation = Quaternion.Euler(55f, 180f, 0f); // 글자 앞면이 위 · 보는 사람 쪽
            Visual(tagRoot, new Vector3(0.07f, 0.024f, 0.002f), new Vector3(0f, 0f, 0.0012f), Quaternion.identity, TagColor);
            var tag = new GameObject("Price").AddComponent<TextMeshPro>();
            tag.transform.SetParent(tagRoot, false);
            tag.transform.localPosition = Vector3.zero;
            tag.font = UiFonts.Main;
            tag.fontSize = 0.16f;
            tag.alignment = TextAlignmentOptions.Center;
            tag.color = UiTheme.Text;
            tag.rectTransform.sizeDelta = new Vector2(0.07f, 0.024f);
            tag.text = PriceText(ctx, data, market);
            slot.Tag = tag;
            return slot;
        }

        static string PriceText(GameContext ctx, CardData card, MarketDay market)
        {
            int price = ctx.Sales.CustomerPrice(card, market);
            string arrow = "";
            if (ctx.EconomyRules.IsFalling(card, market)) arrow = " <color=#6FA8FF>▼</color>";
            else
            {
                int rank = ctx.EconomyRules.PopularRank(card, market);
                if (rank == 1) arrow = " <color=#FF6B6B>▲</color>";
                else if (rank == 2) arrow = " <color=#FFB04D>▲</color>";
            }
            return $"${price}{arrow}";
        }

        bool BuildRow(GameContext ctx)
        {
            row = new GameObject("StandListedCards").transform;
            var anchor = FindAnyObjectByType<ListedCardAnchor>();
            if (anchor != null)
            {
                row.SetParent(anchor.transform, false);
                rowWidth = anchor.rowWidth;
                return true;
            }
            // 앵커가 없으면: SalesStand 윗면의 플레이어 쪽(-Z) 가장자리에서 조금 안쪽, 카드 앞면이 -Z
            GameObject stand = GameObject.Find("SalesStand");
            Bounds b;
            if (stand != null && TryBounds(stand, out b))
            {
                row.position = new Vector3(b.center.x, b.max.y, b.min.z + 0.09f);
                rowWidth = Mathf.Max(0.3f, b.size.x - 0.16f);
            }
            else
            {
                Vector3 p = ShopLayout.StandPos;
                row.position = new Vector3(p.x, ShopLayout.StandSize.y, p.z - ShopLayout.StandSize.z / 2f + 0.09f);
                rowWidth = ShopLayout.StandSize.x - 0.16f;
            }
            row.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            return true;
        }

        static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is not MeshRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        static void Visual(Transform parent, Vector3 size, Vector3 localPos, Quaternion localRot, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

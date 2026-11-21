using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 진열장 세트 표시 (경제시스템_설계 14-3, DEC-040 U1): 같은 속성 수퍼레어 이상 3장이 모이면 그 세 칸 뒤에
    /// 속성 색 빛판이 켜지고, 새 세트가 생기면 소리 · 진동 · "○ 세트 완성! (+60점)" 알림. CardCase가 붙입니다.
    /// 세트 판정은 순수 C# ShowcaseSetRules.
    /// </summary>
    public sealed class ShowcaseSetGlow : MonoBehaviour
    {
        const int MaxSlots = 10;
        const float PlateDepth = 0.004f;   // 뒤판(−0.01) 앞, 카드(0.05) 뒤
        const float Margin = 0.012f;       // 카드보다 조금 크게 (테두리처럼 보이게)

        readonly GameObject[] plates = new GameObject[MaxSlots];
        readonly Dictionary<CardElement, Material> materials = new Dictionary<CardElement, Material>();
        readonly List<CardData> cards = new List<CardData>();
        Material source;
        GameContext ctx;
        CardRarityStyles styles;
        int lastSets = -1;
        string lastKey;

        public void Setup(Material sourceMaterial)
        {
            source = sourceMaterial;
            for (int i = 0; i < MaxSlots; i++)
            {
                Vector3 c = CardCase.SlotLocalPosition(i);
                plates[i] = RuntimeShapes.Create(PrimitiveType.Cube, $"SetGlow_{i}", transform,
                    new Vector3(c.x, c.y, PlateDepth), new Vector3(CardCase.SlotWidth + Margin, CardCase.SlotHeight + Margin, 0.004f), null);
                plates[i].SetActive(false);
            }
        }

        void OnEnable() => ctx = GameContext.Instance;

        void LateUpdate()
        {
            if (ctx == null || ctx.Session == null || plates[0] == null) return;
            List<string> ids = ctx.Session.Showcase;
            string key = string.Join("|", ids);
            if (key == lastKey) return;
            lastKey = key;

            cards.Clear();
            foreach (string id in ids) cards.Add(ctx.Cards.Get(id));
            bool[] inSet = ShowcaseSetRules.SetSlots(cards);
            for (int i = 0; i < MaxSlots; i++)
            {
                Material m = i < inSet.Length && inSet[i] ? MaterialFor(cards[i].element) : null; // 원본 머티리얼이 없으면 켜지 않음 (분홍 방지)
                plates[i].SetActive(m != null);
                if (m != null) plates[i].GetComponent<Renderer>().sharedMaterial = m;
            }

            int sets = ShowcaseSetRules.SetCount(cards);
            if (lastSets >= 0 && sets > lastSets)
            {
                List<CardElement> list = ShowcaseSetRules.Sets(cards);
                CardElement newest = list.Count > 0 ? list[list.Count - 1] : CardElement.None;
                ctx.Events.RaiseNotice($"{CommunityBoard.ElementName(newest)} 세트 완성! 진열장 세트 +{ctx.Score.pointsPerShowcaseSet}점");
                Sfx.Play(SoundId.NewCard, transform.position);
                Interaction.Haptics.BothHands(0.5f, 0.12f);
            }
            lastSets = sets;
        }

        Material MaterialFor(CardElement e)
        {
            if (materials.TryGetValue(e, out Material m)) return m;
            if (styles == null) styles = FindStyles();
            Color c = styles != null ? styles.GetElementColor(e) : Color.white;
            m = HomeRoom.Tinted(source, Color.Lerp(c, Color.white, 0.25f));
            if (m != null && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.8f); // 은은하게 빛남
            }
            materials[e] = m;
            return m;
        }

        CardRarityStyles FindStyles()
        {
            BinderView binder = FindAnyObjectByType<BinderView>(FindObjectsInactive.Include);
            return binder != null ? binder.Styles : null;
        }

        void OnDestroy()
        {
            foreach (Material m in materials.Values) if (m != null) Destroy(m);
        }
    }
}

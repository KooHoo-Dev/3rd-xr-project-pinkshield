using CardShop.Cards;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 왼쪽 아래와 같은 등급 마크 줄 (슬라임 돔 1~3개 + 유니크 이상 반짝임)을 3D 스프라이트로 그립니다.
    /// 값은 <see cref="RarityMarks"/>(카드 셰이더와 같음). 글꼴 기호(◆ · ★) 대신 쓰는 이유: 카드에 찍힌 마크와 똑같이 보이게 (11-22).
    /// 원점 = 줄 가운데. 돔마다 왼쪽 위에 흰 광택 (카드 셰이더 markHi).
    /// </summary>
    public sealed class RarityMarkRow : MonoBehaviour
    {
        const int MaxDomes = 3;

        readonly SpriteRenderer[] domes = new SpriteRenderer[MaxDomes];
        readonly SpriteRenderer[] shines = new SpriteRenderer[MaxDomes];
        SpriteRenderer spark;
        float heightM;

        /// <param name="heightM">돔 높이 (m). 폭은 4/3배</param>
        public static RarityMarkRow Create(Transform parent, float heightM, int sortOrder)
        {
            var go = new GameObject("RarityMarks");
            go.transform.SetParent(parent, false);
            var row = go.AddComponent<RarityMarkRow>();
            row.heightM = heightM;
            Vector2 domeSize = new Vector2(heightM * 4f / 3f, heightM);
            for (int i = 0; i < MaxDomes; i++)
            {
                row.domes[i] = PackResultCards.MakeSprite(go.transform, "Dome", UiSprites.Dome(), Color.white, sortOrder, domeSize, Vector3.zero);
                row.domes[i].drawMode = SpriteDrawMode.Simple;
                row.domes[i].transform.localScale = new Vector3(domeSize.x / 0.64f, domeSize.y / 0.48f, 1f); // 그림 64 × 48픽셀, ppu 100
                row.shines[i] = PackResultCards.MakeSprite(row.domes[i].transform, "Shine", UiSprites.Circle(), new Color(1f, 1f, 1f, 0.6f), sortOrder + 1,
                    Vector2.one, new Vector3(-0.105f, 0.05f, 0f));
                row.shines[i].drawMode = SpriteDrawMode.Simple;
                // 돔 그림 좌표(64 × 48픽셀 = 0.64 × 0.48)에서 셰이더 markHi 자리 · 크기: 가운데에서 왼쪽 35% · 바닥에서 62%
                row.shines[i].transform.localScale = new Vector3(0.15f, 0.07f, 1f);
            }
            row.spark = PackResultCards.MakeSprite(go.transform, "Spark", UiSprites.Sparkle(), new Color(1f, 0.97f, 0.8f), sortOrder + 1, Vector2.one, Vector3.zero);
            row.spark.drawMode = SpriteDrawMode.Simple;
            float sparkM = heightM * 0.9f;
            row.spark.transform.localScale = Vector3.one * (sparkM / 0.64f);
            return row;
        }

        /// <summary>등급에 맞게 돔 개수 · 색 · 반짝임을 바꾸고 가운데 정렬</summary>
        public void Show(CardRarity r)
        {
            int n = RarityMarks.Count(r);
            bool rainbow = RarityMarks.Rainbow(r);
            bool sparkle = RarityMarks.Sparkle(r);
            float domeW = heightM * 4f / 3f, step = domeW * 1.12f, sparkW = sparkle ? heightM * 0.9f + heightM * 0.15f : 0f;
            float total = n * domeW + (n - 1) * (step - domeW) + sparkW;
            float x = -total * 0.5f + domeW * 0.5f;
            for (int i = 0; i < MaxDomes; i++)
            {
                bool on = i < n;
                domes[i].gameObject.SetActive(on);
                if (!on) continue;
                domes[i].sprite = rainbow ? UiSprites.RainbowDome() : UiSprites.Dome();
                domes[i].color = rainbow ? Color.white : RarityMarks.Tint(r);
                domes[i].transform.localPosition = new Vector3(x + i * step, 0f, 0f);
            }
            spark.gameObject.SetActive(sparkle);
            if (sparkle) spark.transform.localPosition = new Vector3(total * 0.5f - heightM * 0.45f, heightM * 0.35f, 0f);
        }
    }
}

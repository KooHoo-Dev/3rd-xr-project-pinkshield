using CardShop.Cards;
using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 최종 판 오른쪽의 "내 방 진열장" 그림 판 (DEC-031 Q4: 진열 카드는 따로 점수 없이 최종 판에 그림만).
    /// 진열한 카드가 없으면 보이지 않습니다. HomeUpgradeInstaller가 최종 판(FinalBoard) 옆에 런타임으로 만듭니다.
    /// </summary>
    public sealed class FinalShowcaseStrip : PhaseBoard
    {
        const int Columns = 5, Rows = 2;
        const float Width = 460f, Height = 420f; // 최종 판(640 × 620)보다 작게, 옆에 붙임

        protected override GamePhaseId Phase => GamePhaseId.Final;

        RawImage[] arts;
        Image[] frames;
        CardRarityStyles styles;

        public static FinalShowcaseStrip Create(FinalBoard board, CardRarityStyles styles, TMP_FontAsset font)
        {
            var boardRt = board.transform as RectTransform;
            float boardHalfMm = boardRt != null ? boardRt.sizeDelta.x / 2f : 320f;
            RectTransform rt = WorldUiKit.CreateCanvas("FinalShowcase", board.transform.parent, Width, Height, out GameObject content);
            rt.gameObject.SetActive(false);
            rt.SetPositionAndRotation(board.transform.TransformPoint(new Vector3(boardHalfMm + 30f + Width / 2f, 0f, 0f)), board.transform.rotation);

            Transform c = content.transform;
            WorldUiKit.AddPanel(c);
            WorldUiKit.AddText(c, "Title", "내 방 진열장", new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f), 32f, UiTheme.Gold, font);

            var arts = new RawImage[Columns * Rows];
            var frames = new Image[Columns * Rows];
            const float left = 0.05f, right = 0.95f, top = 0.84f, bottom = 0.05f, gap = 0.02f;
            float w = (right - left - gap * (Columns - 1)) / Columns, h = (top - bottom - gap * (Rows - 1)) / Rows;
            for (int i = 0; i < arts.Length; i++)
            {
                int row = i / Columns, col = i % Columns;
                var min = new Vector2(left + col * (w + gap), top - (row + 1) * h - row * gap);
                Image frame = WorldUiKit.AddImage(c, $"Frame_{i}", min, min + new Vector2(w, h), Color.white);
                frame.raycastTarget = false;
                var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
                artGo.transform.SetParent(frame.transform, false);
                WorldUiKit.Stretch((RectTransform)artGo.transform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f));
                arts[i] = artGo.GetComponent<RawImage>();
                arts[i].raycastTarget = false;
                frames[i] = frame;
            }

            var strip = rt.gameObject.AddComponent<FinalShowcaseStrip>();
            strip.content = content;
            strip.arts = arts;
            strip.frames = frames;
            strip.styles = styles;
            rt.gameObject.SetActive(true);
            return strip;
        }

        protected override void Redraw()
        {
            FinalResult r = Flow.GetPhase<FinalPhase>(GamePhaseId.Final)?.Result;
            int n = r != null ? r.ShowcaseCards.Count : 0;
            if (n == 0) { content.SetActive(false); return; } // 진열한 카드가 없으면 판을 띄우지 않음
            for (int i = 0; i < frames.Length; i++)
            {
                bool has = i < n;
                frames[i].gameObject.SetActive(has);
                if (!has) continue;
                CardData card = r.ShowcaseCards[i];
                frames[i].color = styles != null ? styles.Get(card.rarity).frameColor : Color.white;
                arts[i].texture = card.character;
                arts[i].enabled = card.character != null;
            }
        }
    }
}

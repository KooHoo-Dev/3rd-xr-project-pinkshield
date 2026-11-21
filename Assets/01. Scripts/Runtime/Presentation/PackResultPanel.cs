using CardShop.UI;
using System.Collections;
using CardShop.Cards;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// 결과함 UI (시연 피드백 B1·B2): 마지막 장을 넘기면 카드 더미 위에 뜨는 판. 월드 고정(머리에 붙이지 않음, 두 눈 겹침 방지).
    /// 두 가지로 씀: 팩 결과 = 실제 카드 5장 카드판(<see cref="PackResultCards"/>, UI 개편 샘플 ⑤),
    /// 더미 정보(PileInfoPopup) = 글자 + 카드 재질 판(<see cref="ShowText"/>). 위치 · 크기는 CardRevealer가 정해 줍니다.
    /// </summary>
    public sealed class PackResultPanel : MonoBehaviour
    {
        TextMeshPro text;
        TextPlate plate;
        PackResultCards cards;
        Coroutine popRoutine;

        public bool IsShown => gameObject.activeSelf;

        public static PackResultPanel Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("PackResultPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<PackResultPanel>();
            panel.text = go.AddComponent<TextMeshPro>();
            if (font != null) panel.text.font = font;
            panel.text.alignment = TextAlignmentOptions.Bottom; // 아래(더미 쪽)를 기준으로 위로 쌓임
            panel.text.color = Color.white;
            panel.text.textWrappingMode = TextWrappingModes.NoWrap;
            panel.text.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0.32f, 0.2f);
            // 보상은 "카드" 재질 (UI_개편_설계 C안): 진보라 판 + 금박 테두리. 예전 검은 띠(<mark>) 대신
            panel.plate = TextPlate.Attach(panel.text, UiTheme.CardBack, new Vector2(0.022f, 0.016f), 0.014f, UiTheme.Gold, 0.003f);
            go.SetActive(false);
            return panel;
        }

        /// <param name="fontSize">TMP 3D 글자 크기 (1 ≈ 10cm)</param>
        /// <param name="popSeconds">작게 시작해 커지며 나타나는 시간 (초)</param>
        public void Show(PackResult pack, CardRarityStyles styles, Vector3 position, Quaternion rotation, float fontSize, float popSeconds)
        {
            if (cards == null) cards = PackResultCards.Create(transform);
            text.text = ""; // 글자 판은 비워 숨김 (TextPlate.Fit)
            // 카드판은 글자 크기 0.2를 기준 크기로 (인스펙터 resultFontSize가 판 전체 크기 손잡이)
            cards.transform.localScale = Vector3.one * (fontSize / 0.2f);
            cards.Show(pack, styles);
            Place(position, rotation, popSeconds);
        }

        /// <summary>미리 만든 글(마크업 가능)을 어두운 띠 위에 띄웁니다. 카드 더미 정보에도 씁니다.</summary>
        public void ShowText(string markup, Vector3 position, Quaternion rotation, float fontSize, float popSeconds)
        {
            if (cards != null) cards.Hide();
            text.fontSize = fontSize;
            text.text = markup;
            Place(position, rotation, popSeconds);
        }

        void Place(Vector3 position, Quaternion rotation, float popSeconds)
        {
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            transform.localScale = Vector3.one; // 판 크기는 원래 크기 기준으로 잼 (팝 중간에 다시 불려도)
            if (plate != null) plate.Fit();
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(Pop(popSeconds));
        }

        public void Hide()
        {
            if (popRoutine != null) { StopCoroutine(popRoutine); popRoutine = null; }
            gameObject.SetActive(false);
        }

        IEnumerator Pop(float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                // 살짝 넘쳤다가 돌아오는 느낌 (1.08배까지)
                float e = Mathf.SmoothStep(0f, 1f, t);
                float s = Mathf.Lerp(0.6f, 1f, e) + Mathf.Sin(t * Mathf.PI) * 0.08f;
                transform.localScale = Vector3.one * s;
                text.alpha = e;
                if (plate != null) plate.SetAlpha(e);
                yield return null;
            }
            transform.localScale = Vector3.one;
            text.alpha = 1f;
            if (plate != null) plate.SetAlpha(1f);
            popRoutine = null;
        }
    }
}

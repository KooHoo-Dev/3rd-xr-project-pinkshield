using System.Collections;
using System.Text;
using CardShop.Cards;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// 결과함 UI (시연 피드백 B1·B2): 마지막 장을 넘기면 카드 더미 위에 뜨는 이번 팩 5장 요약 + NEW 표시.
    /// 월드 고정(머리에 붙이지 않음, 두 눈 겹침 방지)이고 3D TextMeshPro 한 장이라 가볍습니다.
    /// 상자 결과판과 같은 방식(어두운 띠 위 글자)으로, 위치·크기는 CardRevealer가 정해 줍니다.
    /// </summary>
    public sealed class PackResultPanel : MonoBehaviour
    {
        TextMeshPro text;
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
            go.SetActive(false);
            return panel;
        }

        /// <param name="fontSize">TMP 3D 글자 크기 (1 ≈ 10cm)</param>
        /// <param name="popSeconds">작게 시작해 커지며 나타나는 시간 (초)</param>
        public void Show(PackResult pack, CardRarityStyles styles, Vector3 position, Quaternion rotation, float fontSize, float popSeconds)
        {
            ShowText(Build(pack, styles), position, rotation, fontSize, popSeconds);
        }

        /// <summary>미리 만든 글(마크업 가능)을 어두운 띠 위에 띄웁니다. 카드 더미 정보에도 씁니다.</summary>
        public void ShowText(string markup, Vector3 position, Quaternion rotation, float fontSize, float popSeconds)
        {
            text.fontSize = fontSize;
            text.text = $"<mark=#140F1ECC>{markup}</mark>";
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
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
                yield return null;
            }
            transform.localScale = Vector3.one;
            text.alpha = 1f;
            popRoutine = null;
        }

        static string Build(PackResult pack, CardRarityStyles styles)
        {
            int newCount = 0;
            foreach (bool n in pack.IsNew) if (n) newCount++;

            var sb = new StringBuilder();
            // 상자 결과판의 "5점 줄"과 같은 기호 (한 장당 하나, 등급 색)
            sb.Append("<b>이번 팩 결과</b>  ").Append(RevealQueue.Dots(pack, r => CardRevealer.RarityHex(styles, r)));
            if (newCount > 0) sb.Append($"  <color=#7CF29A>새 카드 {newCount}장</color>");
            sb.Append('\n');
            for (int i = 0; i < pack.Cards.Length; i++)
            {
                CardData c = pack.Cards[i];
                sb.Append(CardRevealer.RarityTag(styles, c.rarity)).Append("  ").Append(c.displayName);
                if (pack.IsNew[i]) sb.Append("  <color=#7CF29A><b>NEW</b></color>");
                sb.Append('\n');
            }
            sb.Append("<size=70%><color=#C9C3D9>카드는 퇴근할 때 보관함으로 옮겨져요</color></size>");
            return sb.ToString();
        }
    }
}

using System.Collections;
using CardShop.Cards;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 정산 영수증 연출 (UI_개편_설계 4번 판, Papers, Please의 하루 정산서 · Balatro의 마지막 "쾅").
    /// 본문 글자를 한 줄씩 드러내며(인쇄) 줄마다 작은 소리를 내고, 다 나오면 도장이 커졌다 줄며 찍힙니다.
    /// 정산 판 캔버스(항상 켜져 있음)에 붙이고 <see cref="Setup"/>으로 본문 · 도장 자리를 받습니다.
    /// </summary>
    public sealed class ReceiptPrinter : MonoBehaviour
    {
        [SerializeField] float lineSeconds = 0.14f;
        [SerializeField] float stampDelay = 0.35f;
        [SerializeField] float stampSeconds = 0.22f;

        TMP_Text body;
        RectTransform stamp;
        TMP_Text stampLabel;
        Image stampImage;
        Sprite ringSprite;
        Image.Type ringType;
        Coroutine routine;

        /// <param name="panel">판 Content (도장을 붙일 곳)</param>
        public ReceiptPrinter Setup(Transform panel, TMP_Text bodyText, TMP_FontAsset font)
        {
            body = bodyText;
            stamp = MakeStamp(panel, font, out stampLabel);
            stampImage = stamp.GetComponent<Image>();
            ringSprite = stampImage.sprite;
            ringType = stampImage.type;
            stamp.gameObject.SetActive(false);
            return this;
        }

        /// <summary>본문은 이미 채운 상태에서 부릅니다. 한 줄씩 보이고 마지막에 도장</summary>
        public void Print(string stampWord)
        {
            if (body == null) return;
            if (routine != null) StopCoroutine(routine);
            stampLabel.text = stampWord;
            ApplyArt(stampWord);
            routine = StartCoroutine(Run());
        }

        // 슬라임 얼굴 그림이 있으면 그림(등급별 잉크 색) + 문구는 그림 아래 작게 (F2). 없으면 분홍 고리 + 가운데 글자
        void ApplyArt(string word)
        {
            Sprite art = StampArt.SpriteFor(word);
            Color ink = art != null ? StampArt.InkFor(word) : UiTheme.Stamp;
            stampImage.sprite = art != null ? art : ringSprite;
            stampImage.type = art != null ? Image.Type.Simple : ringType;
            stampImage.preserveAspect = art != null;
            stampImage.color = ink;
            stampLabel.color = ink;
            var label = (RectTransform)stampLabel.transform;
            label.anchorMin = art != null ? new Vector2(-0.1f, -0.32f) : new Vector2(0.1f, 0.1f);
            label.anchorMax = art != null ? new Vector2(1.1f, -0.04f) : new Vector2(0.9f, 0.9f);
            label.offsetMin = label.offsetMax = Vector2.zero;
            stampLabel.fontSizeMax = art != null ? 24f : 34f;
        }

        void OnDisable()
        {
            // 판이 꺼지면 다음에 바로 다 보이게 (중간에 멈춘 채 남지 않게)
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (body != null) body.maxVisibleLines = 99999;
        }

        IEnumerator Run()
        {
            stamp.gameObject.SetActive(false);
            body.maxVisibleLines = 0;
            body.ForceMeshUpdate();
            int lines = Mathf.Max(1, body.textInfo.lineCount);
            for (int i = 1; i <= lines; i++)
            {
                yield return new WaitForSecondsRealtime(lineSeconds);
                body.maxVisibleLines = i;
                Sfx.Play(SoundId.UiClick, transform.position);
            }
            body.maxVisibleLines = 99999;

            yield return new WaitForSecondsRealtime(stampDelay);
            stamp.gameObject.SetActive(true);
            Sfx.Play(Sfx.Has(SoundId.ResignStamp) ? SoundId.ResignStamp : SoundId.BagPut, transform.position);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / stampSeconds)
            {
                // 크게 떨어져 "쾅" 하고 제자리
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            stamp.localScale = Vector3.one;
            routine = null;
        }

        // 오른쪽 아래 구석, [확인] 버튼(가로 25~75%) 오른쪽. 분홍 고리 + 글자, 살짝 기울임 (그림이 있으면 Print 때 ApplyArt가 바꿈)
        static RectTransform MakeStamp(Transform panel, TMP_FontAsset font, out TMP_Text label)
        {
            Image ring = WorldUiKit.AddImage(panel, "Stamp", new Vector2(0.86f, 0.11f), new Vector2(0.86f, 0.11f), UiTheme.Stamp);
            ring.raycastTarget = false;
            ring.sprite = UiSprites.Ring(10);
            var rt = (RectTransform)ring.transform;
            rt.sizeDelta = new Vector2(130f, 130f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 14f);
            label = WorldUiKit.AddText(ring.transform, "Word", "", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), 30f, UiTheme.Stamp, font);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 34f;
            return rt;
        }
    }
}

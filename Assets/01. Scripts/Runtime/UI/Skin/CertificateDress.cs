using System.Collections;
using CardShop.Cards;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 최종 정산 판을 "상장"으로 (UI_개편_설계 C안 10번 판, 빌드피드백_11-22_FIX설계 U5): 종이 판 위에
    /// 금박 이중 테 + 위 가운데 「이달의 알바생」 리본 + 오른쪽 아래 랭크 도장(정산 영수증 도장처럼 크게 떨어져 "쾅").
    /// 판의 배치 · 글자는 건드리지 않고 테두리 · 리본 · 도장만 더합니다 (씬 수정 없음). 무한 모드 요약 때는 리본 · 도장을 숨김.
    /// </summary>
    public sealed class CertificateDress : MonoBehaviour
    {
        const float StampDelay = 0.6f, StampSeconds = 0.25f;

        RectTransform stamp;
        TMP_Text stampWord;
        GameObject ribbon;
        Coroutine routine;

        public static CertificateDress Attach(Transform panel, TMP_FontAsset font)
        {
            var dress = panel.gameObject.AddComponent<CertificateDress>();
            dress.Build(panel, font);
            return dress;
        }

        void Build(Transform panel, TMP_FontAsset font)
        {
            // 금박 이중 테 (바깥 굵게 · 안 가늘게)
            Frame(panel, "GoldFrameOuter", 0.012f, 6);
            Frame(panel, "GoldFrameInner", 0.03f, 2);

            // 위 가운데 리본: 테두리에 걸쳐 놓인 금색 알약
            Image pill = WorldUiKit.AddImage(panel, "Ribbon", new Vector2(0.3f, 0.972f), new Vector2(0.7f, 1.04f), UiTheme.SoftGold);
            pill.raycastTarget = false;
            pill.sprite = UiSprites.Rounded();
            pill.type = Image.Type.Sliced;
            pill.pixelsPerUnitMultiplier = UiSprites.Radius / 20f;
            TMP_Text title = WorldUiKit.AddText(pill.transform, "Title", "이달의 알바생", Vector2.zero, Vector2.one, 30f, UiTheme.Ink, font);
            title.alignment = TextAlignmentOptions.Center;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 30f;
            ribbon = pill.gameObject;

            // 랭크 도장 (분홍 고리 + 큰 글자, 살짝 기울임): 제목 오른쪽 위 — 본문 · 버튼을 가리지 않게
            Image ring = WorldUiKit.AddImage(panel, "RankStamp", new Vector2(0.87f, 0.87f), new Vector2(0.87f, 0.87f), UiTheme.Stamp);
            ring.raycastTarget = false;
            ring.sprite = UiSprites.Ring(10);
            stamp = (RectTransform)ring.transform;
            stamp.sizeDelta = new Vector2(120f, 120f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, 12f);
            stampWord = WorldUiKit.AddText(ring.transform, "Word", "", new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), 60f, UiTheme.Stamp, font);
            stampWord.alignment = TextAlignmentOptions.Center;
            stampWord.enableAutoSizing = true;
            stampWord.fontSizeMin = 20f;
            stampWord.fontSizeMax = 60f;
            stamp.gameObject.SetActive(false);
        }

        static void Frame(Transform panel, string name, float inset, int thickness)
        {
            Image f = WorldUiKit.AddImage(panel, name, new Vector2(inset, inset * 1.4f), new Vector2(1f - inset, 1f - inset * 1.4f), UiTheme.SoftGold);
            f.raycastTarget = false;
            f.sprite = UiSprites.Frame(thickness);
            f.type = Image.Type.Sliced;
            // 글자 · 버튼보다 뒤: 판 배경(UiSkin.ApplyPanel이 만든 "Background") 바로 위
            Transform bg = panel.Find("Background");
            f.transform.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);
        }

        /// <summary>판이 보일 때: 랭크 이름이 있으면 리본 · 도장을 보이고 도장을 찍음, 없으면(무한 모드 요약) 숨김</summary>
        public void Show(string rank)
        {
            bool certificate = !string.IsNullOrEmpty(rank);
            ribbon.SetActive(certificate);
            if (certificate && stampWord.text == rank && (routine != null || stamp.gameObject.activeSelf)) return; // 다시 그려도 도장은 한 번
            if (routine != null) { StopCoroutine(routine); routine = null; }
            stamp.gameObject.SetActive(false);
            if (!certificate || !isActiveAndEnabled) return;
            stampWord.text = rank;
            routine = StartCoroutine(StampRoutine());
        }

        IEnumerator StampRoutine()
        {
            yield return new WaitForSecondsRealtime(StampDelay);
            stamp.gameObject.SetActive(true);
            Sfx.Play(Sfx.Has(SoundId.ResignStamp) ? SoundId.ResignStamp : SoundId.BagPut, transform.position);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / StampSeconds)
            {
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - t) * (1f - t)); // 크게 떨어져 "쾅"
                yield return null;
            }
            stamp.localScale = Vector3.one;
            routine = null;
        }

        void OnDisable()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (stamp != null) { stamp.localScale = Vector3.one; stamp.gameObject.SetActive(stampWord != null && !string.IsNullOrEmpty(stampWord.text)); }
        }
    }
}

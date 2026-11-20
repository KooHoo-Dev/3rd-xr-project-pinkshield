using CardShop.UI;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 메모지 · 수칙 · 조작 안내 판에 붙이는 "손글씨 느낌" 꾸밈 (UI_개편_설계 4장 9번): 위쪽 가운데 반투명 마스킹테이프 띠와 살짝 기울임.
    /// 손글씨 글꼴은 쓰지 않기로 해서(글꼴 하나) 색 · 기울임 · 테이프로 표현합니다. 판의 로컬 -Z가 읽는 면.
    /// 코드로 만든 스프라이트라 실행 중에만 입혀지고, 씬 파일에는 저장되지 않습니다.
    /// </summary>
    public static class NoteTape
    {
        /// <summary>메모지 노랑 (설계 Sticky #FFE98A)</summary>
        public static readonly Color Sticky = new Color(1f, 0.914f, 0.541f);

        static readonly Color TapeColor = new Color(1f, 1f, 1f, 0.6f); // 흰 반투명 마스킹테이프 (11-20 샘플 9번)
        static readonly Vector2 TapeSize = new Vector2(0.09f, 0.03f);
        const float CornerM = 0.004f;

        /// <summary>판 둥근 바탕 스프라이트의 모서리 반지름(m)에 맞는 ppu</summary>
        public static float PpuFor(float cornerM) => UiSprites.Radius / Mathf.Max(0.001f, cornerM);

        /// <summary>판을 읽는 면 기준으로 degrees만큼 돌립니다 (+는 반시계). 글자도 판과 함께 기울어짐</summary>
        public static void Tilt(Transform sign, float degrees)
        {
            if (!Application.isPlaying) return;
            sign.Rotate(0f, 0f, degrees, Space.Self);
        }

        /// <summary>판 위쪽 가운데에 테이프 띠를 붙입니다 (판 가장자리에 반쯤 걸침). 두 번 부르면 앞의 것을 다시 씀</summary>
        public static void Stick(Transform sign, Vector2 size)
        {
            if (!Application.isPlaying) return;
            Transform old = sign.Find("Tape");
            var go = old != null ? old.gameObject : new GameObject("Tape");
            go.transform.SetParent(sign, false);
            go.transform.localPosition = new Vector3(0f, size.y * 0.5f - TapeSize.y * 0.25f, -0.004f);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UiSprites.Rounded(PpuFor(CornerM));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = TapeSize;
            sr.color = TapeColor;
            sr.sortingOrder = 3; // 글자(1) 위
        }
    }
}

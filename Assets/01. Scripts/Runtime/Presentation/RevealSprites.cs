using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// 개봉 연출용 흰색 1×1m 스프라이트 (기준점별). 크기는 Transform 배율 = 미터, 색은 SpriteRenderer.color로.
    /// 텍스처는 빌드에 항상 들어 있는 Texture2D.whiteTexture를 씁니다(PackTearEffect의 빛줄기와 같은 방식).
    /// 양면으로 보이고 그림자가 없는 가벼운 반투명 판이라 Quest에서 싸게 쓸 수 있습니다.
    /// </summary>
    public static class RevealSprites
    {
        static Sprite center, bottom, left;

        public static Sprite Center => center != null ? center : center = Make(new Vector2(0.5f, 0.5f), "RevealCenter");
        public static Sprite Bottom => bottom != null ? bottom : bottom = Make(new Vector2(0.5f, 0f), "RevealBottom");
        public static Sprite Left => left != null ? left : left = Make(new Vector2(0f, 0.5f), "RevealLeft");

        static Sprite Make(Vector2 pivot, string name)
        {
            Texture2D tex = Texture2D.whiteTexture; // 4x4 흰색
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, tex.width);
            s.name = name;
            return s;
        }

        /// <summary>parent 아래에 판 하나를 만듭니다.</summary>
        public static SpriteRenderer Quad(Transform parent, string name, Sprite sprite, Vector3 localPosition, Vector2 size, Color color, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }
    }
}

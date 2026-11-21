using UnityEngine;

namespace CardShop.Interaction
{
    /// <summary>
    /// 누르는 버튼 둘레의 빛 판 (11-21 2차 F4: 버튼 테가 보이는 각도에서만 보임).
    /// 외곽선 껍데기는 납작한 버튼을 정면에서 보면 버튼 뒤에 숨음 → 버튼 메시의 가운데 면(가장 얇은 축에 수직)에
    /// 버튼보다 한 치수 큰 흰 판을 끼워, 버튼 몸이 가리지 않는 가장자리가 어느 쪽에서 봐도 테처럼 보이게 합니다.
    /// 앞 · 뒤를 몰라도 되고(판이 버튼 한가운데), 셰이더 · 렌더 기능이 따로 필요 없어 Quest 부담이 거의 없습니다.
    /// InteractableOutline이 PokeButton에 붙입니다.
    /// </summary>
    public sealed class ButtonGlowPlate : MonoBehaviour
    {
        static readonly Color PlateColor = new Color(1f, 1f, 1f, 0.6f);
        const float Margin = 0.012f; // 버튼 둘레로 튀어나오는 폭 (m)

        void Start()
        {
            MeshFilter f = Biggest();
            if (f == null) return;
            Bounds local = f.sharedMesh.bounds;
            Vector3 s = local.size;
            Vector3 scale = f.transform.lossyScale;
            Vector3 world = Vector3.Scale(s, Abs(scale));

            // 가장 얇은 축 = 버튼 두께. 판은 나머지 두 축이 이루는 면에
            int thin = world.x <= world.y && world.x <= world.z ? 0 : world.y <= world.z ? 1 : 2;
            Quaternion rot = thin == 2 ? Quaternion.identity : thin == 1 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 90f, 0f);
            // 판(스프라이트 XY) 크기를 메시 로컬 단위로: 월드 여백을 각 축 배율로 나눔
            float a = thin == 0 ? s.z + Margin * 2f / Mathf.Max(1e-4f, Mathf.Abs(scale.z)) : s.x + Margin * 2f / Mathf.Max(1e-4f, Mathf.Abs(scale.x));
            float b = thin == 1 ? s.z + Margin * 2f / Mathf.Max(1e-4f, Mathf.Abs(scale.z)) : s.y + Margin * 2f / Mathf.Max(1e-4f, Mathf.Abs(scale.y));

            var go = new GameObject("ButtonGlowPlate");
            go.transform.SetParent(f.transform, false);
            go.transform.localPosition = local.center;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(a, b, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CardShop.Presentation.RevealSprites.Center; // 1×1 흰 판
            // 젤리 버튼이면 흰색 대신 버튼 색의 옅은 빛 (VR 눈 편한 색: 흰 판 금지, 폴리싱 P1)
            sr.color = TryGetComponent(out PokeButtonLook look) ? new Color(look.BodyColor.r, look.BodyColor.g, look.BodyColor.b, 0.35f) : PlateColor;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }

        // 버튼의 몸 = 이 물건 아래 가장 큰 메시 (글자 · 테 껍데기 제외)
        MeshFilter Biggest()
        {
            MeshFilter best = null;
            float bestSize = 0f;
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null || f.name == "OutlineShell" || f.GetComponent<TMPro.TMP_Text>() != null) continue;
                float size = Vector3.Scale(f.sharedMesh.bounds.size, Abs(f.transform.lossyScale)).sqrMagnitude;
                if (size > bestSize) { bestSize = size; best = f; }
            }
            return best;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}

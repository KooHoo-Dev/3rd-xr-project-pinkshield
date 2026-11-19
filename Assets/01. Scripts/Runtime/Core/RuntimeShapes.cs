using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Core
{
    /// <summary>
    /// 실행 중에 만드는 보이기 전용 도형 (보관함 틀 · 대걸레 부품 · 배송 상자 대체 모양 · 팩 자르는 선).
    /// 콜라이더를 지워 잡기 · 레이를 가리지 않습니다. 에디터 빌더의 도형 함수는 콜라이더 · 저장 규칙이 달라 따로 둡니다.
    /// </summary>
    public static class RuntimeShapes
    {
        /// <param name="material">비우면 Unity 기본 머티리얼 그대로</param>
        public static GameObject Create(PrimitiveType shape, string name, Transform parent, Vector3 localPosition, Vector3 size,
            Material material = null, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            if (!castShadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}

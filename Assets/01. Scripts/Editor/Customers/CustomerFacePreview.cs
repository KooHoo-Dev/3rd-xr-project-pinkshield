using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 손님 프리팹의 얼굴만 크게 찍은 썸네일을 만들어 보관합니다 (외형 분류 창용).
    /// AssetPreview는 전신이라 얼굴이 작아, 미리보기 전용 씬에서 머리 뼈를 정면으로 비춰 찍습니다.
    /// 한 번 찍은 그림은 창이 닫힐 때까지 다시 쓰고, 창이 멈추지 않도록 한 프레임에 몇 장씩만 찍습니다.
    /// </summary>
    public sealed class CustomerFacePreview
    {
        const int RendersPerFrame = 6;

        readonly PreviewRenderUtility util;
        readonly Dictionary<GameObject, Texture> cache = new Dictionary<GameObject, Texture>();
        int budget;

        public CustomerFacePreview()
        {
            util = new PreviewRenderUtility();
            util.camera.fieldOfView = 20f;
            util.camera.nearClipPlane = 0.01f;
            util.camera.farClipPlane = 20f;
            util.camera.clearFlags = CameraClearFlags.SolidColor;
            util.camera.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            util.lights[0].intensity = 1.2f;
            util.lights[0].transform.rotation = Quaternion.Euler(20f, 160f, 0f); // 얼굴 정면 쪽에서 비춤
            util.lights[1].intensity = 0.6f;
            util.ambientColor = new Color(0.35f, 0.35f, 0.35f);
        }

        /// <summary>OnGUI 시작마다 부릅니다. 이번 프레임에 새로 찍을 수 있는 장 수를 채웁니다.</summary>
        public void BeginFrame() => budget = RendersPerFrame;

        /// <summary>얼굴 썸네일. 이번 프레임 몫을 다 써서 아직 못 찍었으면 null (다음 프레임에 찍음).</summary>
        public Texture Get(GameObject prefab, int size)
        {
            if (cache.TryGetValue(prefab, out Texture tex)) return tex;
            if (budget <= 0) return null;
            budget--;
            tex = Render(prefab, size);
            cache[prefab] = tex;
            return tex;
        }

        Texture Render(GameObject prefab, int size)
        {
            GameObject go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            util.AddSingleGO(go);

            Bounds body = BodyBounds(go);
            float height = Mathf.Max(0.5f, body.size.y);
            Transform head = FindHead(go);
            // 머리 뼈는 보통 목 위쪽(머리 아래 끝)에 있어 조금 올려 얼굴 가운데를 노림
            Vector3 face = head != null
                ? head.position + Vector3.up * height * 0.045f
                : new Vector3(body.center.x, body.max.y - height * 0.07f, body.center.z);

            float frame = height * 0.2f; // 화면 세로에 담을 길이 (머리 + 약간의 여백)
            float dist = frame * 0.5f / Mathf.Tan(util.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            util.camera.transform.position = face + Vector3.forward * dist; // 프리팹은 +Z를 보고 서 있음
            util.camera.transform.LookAt(face);

            util.BeginStaticPreview(new Rect(0, 0, size, size));
            util.Render(true);
            Texture tex = util.EndStaticPreview();
            Object.DestroyImmediate(go);
            return tex;
        }

        static Transform FindHead(GameObject go)
        {
            Animator anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform t = anim.GetBoneTransform(HumanBodyBones.Head);
                if (t != null) return t;
            }
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (n.EndsWith("head") || n.EndsWith("head_jnt") || n == "head") return t;
            }
            return null;
        }

        static Bounds BodyBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.up, Vector3.one * 1.7f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public void Dispose()
        {
            foreach (Texture t in cache.Values) if (t != null) Object.DestroyImmediate(t);
            cache.Clear();
            util.Cleanup();
        }
    }
}

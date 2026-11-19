using System.Collections.Generic;
using CardShop.UI;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼이 "오른손 트리거"처럼 버튼을 말할 때 실제 컨트롤러 모델의 그 버튼을 분홍으로 깜빡임 (FIX C3).
    /// XRI 컨트롤러 모델(UniversalController)의 부위 이름 "Trigger" · "Bumper"(그립)로 찾고, 왼손 · 오른손은 부모 이름의 Left · Right로 구분합니다.
    /// 색은 MaterialPropertyBlock으로만 바꿔서 원래 머티리얼은 건드리지 않습니다. 모델이 늦게 생길 수 있어 켤 때마다 찾습니다.
    /// </summary>
    public sealed class ControllerButtonGlow : MonoBehaviour
    {
        const float PulseSpeed = 2f; // 회/초

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        readonly List<Renderer> lit = new List<Renderer>();
        MaterialPropertyBlock block;

        /// <summary>안내 글에 맞는 버튼을 빛냄 (버튼 이름이 없으면 끔)</summary>
        public void Show(string hintText)
        {
            Clear();
            ControllerHint h = ControllerHint.Parse(hintText);
            if (!h.Any) return;
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig == null) return;
            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
            {
                string part = r.name;
                bool button = (h.Trigger && part == "Trigger") || (h.Grip && part == "Bumper");
                if (!button) continue;
                bool left = Under(r.transform, "Left"), right = Under(r.transform, "Right");
                if ((left && h.Left) || (right && h.Right)) lit.Add(r);
            }
            enabled = lit.Count > 0;
        }

        public void Clear()
        {
            foreach (Renderer r in lit) if (r != null) r.SetPropertyBlock(null);
            lit.Clear();
            enabled = false;
        }

        void OnDisable()
        {
            foreach (Renderer r in lit) if (r != null) r.SetPropertyBlock(null);
        }

        void LateUpdate()
        {
            block ??= new MaterialPropertyBlock();
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed * Mathf.PI * 2f);
            Color c = Color.Lerp(Color.white, UiTheme.Accent, 0.4f + 0.6f * wave);
            block.SetColor(BaseColor, c);
            block.SetColor(LegacyColor, c);
            foreach (Renderer r in lit) if (r != null) r.SetPropertyBlock(block);
        }

        static bool Under(Transform t, string side)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (p.name.Contains(side)) return true;
            return false;
        }
    }
}

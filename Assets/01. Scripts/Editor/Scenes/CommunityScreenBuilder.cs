using CardShop.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 집 컴퓨터 모니터에 카드 커뮤니티 화면(월드 텍스트)을 붙입니다. ShopSceneBuilder.CreateHome에서 한 줄로 호출합니다.
    /// 플레이어는 가게 쪽(-Z)에서 모니터를 보므로 글자는 모니터 앞면(-Z 쪽)에 놓습니다.
    /// 월드 텍스트 크기: TMP 3D에서 fontSize 1 ≈ 글자 높이 약 10cm.
    /// </summary>
    public static class CommunityScreenBuilder
    {
        // 모니터는 집 중앙에서 약 2.6m 떨어져 있어 블록아웃보다 크게 키웁니다.
        static readonly Vector3 MonitorCenter = new Vector3(1.2f, 1.3f, 1.8f);
        static readonly Vector3 MonitorSize = new Vector3(1.3f, 0.9f, 0.04f);
        const float TextZ = -0.03f; // 모니터 중심 기준 앞쪽 (앞면은 -0.02)
        const float TextWidth = 1.2f;

        public static void Create(Transform homeRoot, TMP_FontAsset font)
        {
            Transform monitor = homeRoot.Find("Monitor");
            if (monitor == null)
            {
                Debug.LogWarning("[CommunityScreenBuilder] 집 구역에 Monitor가 없어 커뮤니티 화면을 만들지 못했습니다.");
                return;
            }
            monitor.localPosition = MonitorCenter;
            monitor.localScale = MonitorSize;

            // 모니터 크기는 같은 오브젝트의 스케일을 쓰므로 글자는 스케일 1짜리 별도 루트에 둡니다.
            var root = new GameObject("CommunityScreen");
            root.transform.SetParent(homeRoot, false);
            root.transform.localPosition = MonitorCenter + new Vector3(0f, 0f, TextZ);

            var content = new GameObject("Content");
            content.transform.SetParent(root.transform, false);

            CreateText(content.transform, "Title", font, "카드 커뮤니티", 0.7f, TextAlignmentOptions.Center,
                0.33f, 0.12f, new Color(0.95f, 0.95f, 1f));
            TMP_Text forecast = CreateText(content.transform, "Forecast", font, "", 0.6f, TextAlignmentOptions.Center,
                0.16f, 0.2f, Color.white);
            TMP_Text top = CreateText(content.transform, "Top", font, "", 0.5f, TextAlignmentOptions.Center,
                -0.07f, 0.28f, Color.white);
            TMP_Text mood = CreateText(content.transform, "Mood", font, "", 0.42f, TextAlignmentOptions.Center,
                -0.32f, 0.22f, new Color(0.75f, 0.8f, 0.9f));

            var screen = root.AddComponent<CommunityScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("forecastLabel").objectReferenceValue = forecast;
            so.FindProperty("topLabel").objectReferenceValue = top;
            so.FindProperty("moodLabel").objectReferenceValue = mood;
            so.ApplyModifiedPropertiesWithoutUndo();

            content.SetActive(false); // 집에 들어올 때 켜짐
        }

        static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, string text, float size,
            TextAlignmentOptions align, float y, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(TextWidth, height);

            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}

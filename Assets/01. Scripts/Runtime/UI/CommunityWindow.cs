using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 모니터 첫 쪽 "슬라임 커뮤니티" SlimeOS 창 (UI 개편 8번: 커뮤니티 3D 글자도 창으로).
    /// 씬의 CommunityScreen(3D 글자)은 그대로 두고 글자 칸만 이 창의 캔버스 글자로 옮깁니다(Rehost) — 내용 계산은 CommunityScreen 그대로.
    /// </summary>
    public static class CommunityWindow
    {
        public static RectTransform Create(HomeComputer pc, CommunityScreen screen, TMP_FontAsset font)
        {
            RectTransform rt = pc.CreateWindowCanvas("CommunityWindow", 0.006f, out GameObject content);
            Transform c = content.transform;
            WorldUiKit.AddPanel(c);
            WorldUiKit.AddText(c, "Title", "슬라임 커뮤니티  <size=70%>카드 게시판 · 오늘 · 내일 시세</size>", Vector2.zero, Vector2.one, 26f, UiTheme.Text, font);

            // 위: 예보 (가장 중요) / 가운데: 인기 카드 TOP3 / 아래: 분위기 글 — 예전 3D 글자와 같은 순서
            TMP_Text forecast = Line(c, "Forecast", new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.92f), 46f, font);
            Divider(c, 0.6f);
            TMP_Text top = Line(c, "Top", new Vector2(0.04f, 0.31f), new Vector2(0.96f, 0.58f), 38f, font);
            Divider(c, 0.29f);
            TMP_Text mood = Line(c, "Mood", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.27f), 32f, font);
            mood.color = UiTheme.SubText;

            SlimeOsWindow.Apply(c, null, font);
            screen.Rehost(content, forecast, top, mood);
            return rt;
        }

        static TMP_Text Line(Transform c, string name, Vector2 min, Vector2 max, float size, TMP_FontAsset font)
        {
            TMP_Text t = WorldUiKit.AddText(c, name, "", min, max, size, UiTheme.Text, font);
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.6f;
            t.fontSizeMax = size;
            return t;
        }

        static void Divider(Transform c, float y)
        {
            var line = WorldUiKit.AddImage(c, "Divider", new Vector2(0.06f, y - 0.002f), new Vector2(0.94f, y + 0.002f), new Color(1f, 1f, 1f, 0.12f)); // 옅은 줄
            line.raycastTarget = false;
        }
    }
}

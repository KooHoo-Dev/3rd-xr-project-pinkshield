using CardShop.Core;
using CardShop.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 타이틀 · 정산 · 최종 판 (타이틀연출_손님_설계 0-2 · 0-3). 디버그 패널 없이도 한 판이 처음부터 끝까지 돌게 하는 버튼들.
    /// 판은 월드 고정, 1px = 1mm. 버튼은 WorldUi 규격(높이 56mm 이상 · 간격 30mm 이상, 레이가 닿으면 주황).
    /// </summary>
    internal static class PhaseBoardBuilder
    {
        public const string GameName = "슬라임 카드샵";
        public const string Credit = "제작: PinkshieldStudio";

        static Color Gold => UiTheme.Gold;

        /// <returns>타이틀에서 서는 자리 (DayTransition.titleSpawn에 연결)</returns>
        public static Transform Create(TMP_FontAsset font)
        {
            var root = new GameObject("PhaseBoards").transform;

            var spawn = new GameObject("TitleSpawn").transform;
            spawn.SetParent(root, false);
            spawn.SetPositionAndRotation(ShopLayout.TitleSpawnPos, Quaternion.Euler(0f, ShopLayout.TitleSpawnYaw, 0f));

            CreateTitle(root, font);
            // 가게 정면 큰 간판: 타이틀 연출(TitleIntro)이 점등하므로 여기서 씬에 둠 (ShopInterior 프리팹은 사용자가 손볼 수 있어 다시 만들지 않음)
            Interior.DecorFactory.FrontSign(root, ShopLayout.FrontSignPos, 0f, ShopLayout.FrontSignSize);
            CreateResult<SettlementBoard>(root, "SettlementBoard", "확인", font);
            CreateResult<FinalBoard>(root, "FinalBoard", "타이틀로", font);
            return spawn;
        }

        static void CreateTitle(Transform root, TMP_FontAsset font)
        {
            // 플레이어(인도)가 −Z를 보므로 판 앞면도 −Z 쪽에서 읽히게 180도
            GameObject canvas = Canvas(root, "TitleBoard", new Vector2(900f, 620f), ShopLayout.TitleBoardPos, Quaternion.Euler(0f, 180f, 0f));
            Transform c = canvas.transform.Find("Content");
            TMP_Text heading = WorldUi.AddText(c, "Heading", GameName, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.94f), 76f, Gold, font);
            heading.fontStyle = FontStyles.Bold;
            TMP_Text body = WorldUi.AddText(c, "Sub", "7일 동안 카드샵을 지키고, 퇴근 후엔 카드팩을 뜯자!", new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.68f), 30f, UiTheme.Text, font);
            WorldUi.AddText(c, "Credit", Credit, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.11f), 24f, UiTheme.SubText, font);

            // 버튼 높이 74mm(0.12) · 간격 37mm(0.06)
            Button start = WorldUi.AddButton(c, TitleBoard.StartLabel, new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.50f), font);
            start.GetComponentInChildren<TMP_Text>().fontSize = 36f;
            Button recalibrate = WorldUi.AddButton(c, "눈높이 맞추기", new Vector2(0.25f, 0.20f), new Vector2(0.75f, 0.32f), font);

            var board = canvas.AddComponent<TitleBoard>();
            canvas.AddComponent<TitleIntro>(); // 간판 점등 → 판 떠오름 (CanvasGroup은 RequireComponent로 붙음)
            var so = Wire(board, canvas, heading, body, start);
            so.FindProperty("recalibrateButton").objectReferenceValue = recalibrate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateResult<T>(Transform root, string name, string buttonText, TMP_FontAsset font) where T : PhaseBoard
        {
            // 계산대 위 손님 쪽, 플레이어(원점)가 +Z를 봄
            GameObject canvas = Canvas(root, name, new Vector2(640f, 620f), ShopLayout.ResultBoardPos, Quaternion.identity);
            Transform c = canvas.transform.Find("Content");
            TMP_Text heading = WorldUi.AddText(c, "Heading", name, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f), 44f, Gold, font);
            heading.fontStyle = FontStyles.Bold;
            TMP_Text body = WorldUi.AddText(c, "Body", "", new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.83f), 28f, UiTheme.Text, font);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.lineSpacing = 12f;
            Button ok = WorldUi.AddButton(c, buttonText, new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.16f), font);

            T board = canvas.AddComponent<T>();
            Wire(board, canvas, heading, body, ok).ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedObject Wire(PhaseBoard board, GameObject content, TMP_Text heading, TMP_Text body, Button advance)
        {
            var so = new SerializedObject(board);
            so.FindProperty("content").objectReferenceValue = content.transform.Find("Content").gameObject;
            so.FindProperty("heading").objectReferenceValue = heading;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("advanceButton").objectReferenceValue = advance;
            return so;
        }

        /// <summary>
        /// 컴포넌트는 항상 켜진 캔버스에 붙이고, 보이는 부분(배경 · 글자 · 버튼)은 Content 아래에 둬서 그것만 켜고 끕니다.
        /// 반환값은 캔버스(컴포넌트를 붙일 곳). 글자 · 버튼은 그 아래 "Content"에 붙입니다.
        /// </summary>
        static GameObject Canvas(Transform root, string name, Vector2 sizeMm, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(root, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * 0.001f;
            rt.SetPositionAndRotation(position, rotation);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            WorldUi.Stretch((RectTransform)content.transform, Vector2.zero, Vector2.one);
            WorldUi.AddPanel(content.transform);
            content.SetActive(false);
            return go;
        }
    }
}

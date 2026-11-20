using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShop.UI
{
    /// <summary>
    /// 게임 글꼴 한 곳 (11-20 사용자 결정: 글꼴은 Noto Sans KR Bold 하나로 통일, 모든 글자 굵게, 색 강조는 그대로).
    /// Bold 에셋(Resources/Fonts/NotoSansKR-Bold SDF)이 있으면 그것을 쓰고, 없으면 지금 Regular 글꼴에 굵게 표시를 켭니다
    /// (Regular를 억지로 부풀리는 방식이라 작은 글자가 조금 뭉개질 수 있음 → Bold 에셋을 만들면 자동으로 바뀜).
    /// 씬이 열릴 때 씬 안의 모든 3D · UI 글자를 이 글꼴로 바꾸고, 실행 중에 만드는 글자는 WorldUiKit · TextPlate가 <see cref="Apply"/>를 부릅니다.
    /// </summary>
    public static class UiFonts
    {
        /// <summary>Resources 아래 Bold 에셋 경로 (에디터 메뉴 CardShop/UI/Bold 글꼴 에셋 만들기가 여기에 만듦)</summary>
        public const string BoldResource = "Fonts/NotoSansKR-Bold SDF";

        // 글자 크기 3단계 (1px = 1mm 캔버스 기준). 모두 굵게라 굵기 대신 크기로 제목 · 본문 · 보조를 나눔
        public const float TitleSize = 44f;
        public const float BodySize = 28f;
        public const float SmallSize = 20f;

        static TMP_FontAsset bold;
        static bool searched;

        /// <summary>진짜 Bold 에셋이 있는가 (없으면 Regular + 굵게 표시)</summary>
        public static bool HasRealBold => Bold != null;

        static TMP_FontAsset Bold
        {
            get
            {
                if (!searched) { searched = true; bold = Resources.Load<TMP_FontAsset>(BoldResource); }
                return bold;
            }
        }

        /// <summary>게임에서 쓰는 한 가지 글꼴</summary>
        public static TMP_FontAsset Main => Bold != null ? Bold : WorldUiKit.FindSceneKoreanFont();

        /// <summary>글자 하나를 게임 글꼴 · 굵게로. 한글 글꼴이 아닌 글자(아이콘 글꼴 등)는 그대로 둠</summary>
        public static void Apply(TMP_Text t)
        {
            if (t == null) return;
            if (t.font == null || IsKorean(t.font))
            {
                TMP_FontAsset main = Main;
                if (main != null && t.font != main) t.font = main;
            }
            if (HasRealBold) t.fontStyle &= ~FontStyles.Bold; // 이미 굵은 글꼴이라 두 번 부풀리지 않음 (<b> 태그 강조는 그대로 더 굵게)
            else t.fontStyle |= FontStyles.Bold;
        }

        static bool IsKorean(TMP_FontAsset f) => f.name.Contains("KR") || (TMP_Settings.defaultFontAsset != null && f == TMP_Settings.defaultFontAsset);

        /// <summary>씬 안 모든 글자 (꺼진 것 포함)</summary>
        public static void ApplyToScene()
        {
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Apply(t);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            ApplyToScene();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();
    }
}

using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 타이틀 판의 [음악 크기] · [효과음 크기] 버튼 (11-20 H3, 체크 1-1 "볼륨 조절을 메인 메뉴에"). [영업 시작] 양옆의 빈 자리에 둡니다.
    /// 누를 때마다 끔 → 작게 → 보통 → 크게로 돌고(AudioVolume), 기기에 저장됩니다.
    /// 씬을 다시 빌드하지 않도록 실행 중에 [눈높이 맞추기] 버튼을 복제해 만듭니다 (CreditsButton과 같은 방식).
    /// </summary>
    public sealed class AudioVolumeButtons : MonoBehaviour
    {
        [SerializeField] Vector2 effectsMin = new Vector2(0.03f, 0.38f);
        [SerializeField] Vector2 effectsMax = new Vector2(0.22f, 0.50f);
        [SerializeField] Vector2 musicMin = new Vector2(0.78f, 0.38f);
        [SerializeField] Vector2 musicMax = new Vector2(0.97f, 0.50f);
        [SerializeField] float fontSize = 24f;

        TMP_Text musicLabel, effectsLabel;

        public void Configure(TitleBoard board)
        {
            if (board == null || board.RecalibrateButton == null) return;
            musicLabel = Make(board.RecalibrateButton, "Button_MusicVolume", musicMin, musicMax, () => Cycle(AudioVolume.Channel.Music));
            effectsLabel = Make(board.RecalibrateButton, "Button_EffectsVolume", effectsMin, effectsMax, () => Cycle(AudioVolume.Channel.Effects));
            UpdateLabels();
        }

        TMP_Text Make(Button source, string name, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = Instantiate(source.gameObject, source.transform.parent, false);
            go.name = name;
            WorldUiKit.Stretch((RectTransform)go.transform, min, max);
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = fontSize;
            return label;
        }

        void Cycle(AudioVolume.Channel c)
        {
            AudioVolume.Cycle(c);
            UpdateLabels();
            Sfx.Play(Cards.SoundId.UiClick); // 효과음 크기를 바로 들어 보게
        }

        void UpdateLabels()
        {
            if (musicLabel != null) musicLabel.text = "음악 " + AudioVolume.NameOf(AudioVolume.Channel.Music);
            if (effectsLabel != null) effectsLabel.text = "효과음 " + AudioVolume.NameOf(AudioVolume.Channel.Effects);
        }
    }
}

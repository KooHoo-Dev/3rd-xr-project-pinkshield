using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 바인더 오른쪽의 진열장 패널 (집_업그레이드_설계 2장 E1). 바인더에서 고른 카드를 [진열장에 세우기]로 책장 진열대에 세우고,
    /// 진열 중인 카드 버튼을 누르면 내립니다. 규칙은 순수 C# ShowcaseRules, 실물 카드는 CardCase(모니터 위 보관함)가 그립니다.
    /// 판매 패널과 같은 모양이며, 씬을 다시 빌드하지 않도록 HomeUpgradeInstaller가 런타임에 만듭니다.
    /// 만든 뒤 SalesShowcasePage가 판매 패널과 함께 모니터 [판매 · 진열] 쪽으로 옮깁니다 (DEC-042, 모니터가 없으면 바인더 오른쪽 그대로).
    /// </summary>
    public sealed class ShowcasePanel : HomePanel
    {
        // 문구는 한 곳에 모읍니다.
        const string NoSelection = "바인더에서 진열할 카드를 골라 주세요";
        const string Hint = "보관함 카드는 손으로 옮기거나 바인더 위로 내려요";

        // 패널 크기 (1px = 1mm). 판매 패널(440 × 820)과 폭을 맞춤
        const float Width = 440f, Height = 760f;
        const float ButtonH = 56f / Height, Gap = 14f / Height;

        BinderView binder;
        TMP_Text selectedLabel, statusLabel, messageLabel;
        Button addButton;
        Button[] listButtons;
        string message = "";

        /// <summary>바인더 오른쪽에 패널을 만들어 붙입니다. listSize = 진열 칸이 가장 많은 단계의 칸 수.</summary>
        public static ShowcasePanel Create(BinderView binder, TMP_FontAsset font, int listSize)
        {
            RectTransform rt = WorldUiKit.CreateCanvas("ShowcasePanel", binder.transform, Width, Height, out GameObject content);
            rt.gameObject.SetActive(false); // 설정을 다 넣은 뒤 켬 (OnEnable 순서)
            rt.localRotation = Quaternion.Euler(20f, 0f, 0f); // 바인더 페이지와 같은 독서대 각도 (SalesPanelBuilder와 같음)
            // 아래쪽이 책상 위 바인더 표지(두께 3cm)에 묻혀 비스듬히 잘려 보였음 (11-20 F1, 캡처 5) → 5cm 올림
            rt.localPosition = new Vector3(0.3f + 0.03f + Width * 0.0005f, Height * 0.0005f + 0.062f, 0f);

            Transform c = content.transform;
            WorldUiKit.AddPanel(c);
            WorldUiKit.AddText(c, "Title", "내 방 진열장", new Vector2(0.04f, 0.94f), new Vector2(0.96f, 0.99f), 30f, UiTheme.Gold, font);
            TMP_Text selected = WorldUiKit.AddText(c, "Selected", "", new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.935f), 24f, UiTheme.Text, font);
            selected.alignment = TextAlignmentOptions.TopLeft;
            float top = 0.79f;
            Button add = WorldUiKit.AddButton(c, "진열장에 세우기", new Vector2(0.06f, top - ButtonH), new Vector2(0.94f, top), font);
            TMP_Text status = WorldUiKit.AddText(c, "Status", "", new Vector2(0.04f, 0.65f), new Vector2(0.96f, 0.70f), 20f, UiTheme.SubText, font);
            TMP_Text msg = WorldUiKit.AddText(c, "Message", "", new Vector2(0.04f, 0.59f), new Vector2(0.96f, 0.645f), 21f, UiTheme.Gold, font);
            WorldUiKit.AddText(c, "ListHeader", "진열 중 (누르면 내리기)", new Vector2(0.04f, 0.545f), new Vector2(0.96f, 0.585f), 20f, UiTheme.SubText, font);

            // 진열 목록: 두 줄 (왼쪽 · 오른쪽)
            var list = new Button[Mathf.Max(1, listSize)];
            int rows = (list.Length + 1) / 2;
            float y = 0.535f, bottom = 0.06f;
            float h = Mathf.Min(ButtonH, (y - bottom - Gap * (rows - 1)) / rows);
            for (int i = 0; i < list.Length; i++)
            {
                int row = i / 2, col = i % 2;
                float yy = y - row * (h + Gap);
                list[i] = WorldUiKit.AddButton(c, $"진열 {i + 1}", new Vector2(col == 0 ? 0.06f : 0.51f, yy - h), new Vector2(col == 0 ? 0.49f : 0.94f, yy), font);
                list[i].GetComponentInChildren<TMP_Text>().fontSize = 18f;
            }
            WorldUiKit.AddText(c, "Hint", Hint, new Vector2(0.04f, 0.005f), new Vector2(0.96f, 0.05f), 17f, UiTheme.SubText, font);

            var panel = rt.gameObject.AddComponent<ShowcasePanel>();
            panel.content = content;
            panel.binder = binder;
            panel.selectedLabel = selected;
            panel.statusLabel = status;
            panel.messageLabel = msg;
            panel.addButton = add;
            panel.listButtons = list;
            rt.gameObject.SetActive(true);
            return panel;
        }

        protected override void OnEnable()
        {
            if (addButton == null) return; // Create가 설정을 넣기 전
            addButton.onClick.AddListener(OnAddClicked);
            for (int i = 0; i < listButtons.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                listButtons[i].onClick.AddListener(() => OnListClicked(index));
            }
            if (binder != null) binder.CardSelected += OnCardSelected;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (addButton == null) return;
            addButton.onClick.RemoveListener(OnAddClicked);
            foreach (Button b in listButtons) b.onClick.RemoveAllListeners();
            if (binder != null) binder.CardSelected -= OnCardSelected;
            base.OnDisable();
        }

        protected override void OnPhaseChanged(GamePhaseId phase)
        {
            if (phase == GamePhaseId.Home) message = "";
            base.OnPhaseChanged(phase);
        }

        void OnCardSelected(CardData _)
        {
            message = "";
            MarkDirty();
        }

        int Capacity => Context.Upgrades.ShowcaseSlots(Context.Session.Upgrades);

        void OnAddClicked()
        {
            CardData card = binder != null ? binder.SelectedCard : null;
            if (card == null || Context == null) return;
            // 보관함이 있으면 바로 빈 칸에 꽂히며 날아감 (DEC-036, 소리 · 진동은 보관함이), 없으면 데이터만
            CardCase box = CardCase.Instance;
            ShowcaseResult result = box != null ? box.Place(card) : ShowcaseRules.TryAdd(Context.Session, card.id, Capacity);
            switch (result)
            {
                case ShowcaseResult.Ok:
                    message = $"{card.displayName}을(를) 모니터 위 보관함에 세웠어요!";
                    if (box == null) Sfx.Play(SoundId.PileLand, transform.position);
                    break;
                case ShowcaseResult.Full: message = "진열장이 가득 찼어요 (슬라임마켓에서 늘릴 수 있어요)"; break;
                default: message = "진열할 카드가 없어요 (판매 등록 중인 카드는 빼고)"; break;
            }
            Refresh();
        }

        void OnListClicked(int index)
        {
            GameSession s = Context != null ? Context.Session : null;
            if (s == null || index >= s.Showcase.Count) return;
            string id = s.Showcase[index];
            if (ShowcaseRules.Remove(s, id))
            {
                CardData card = Context.Cards.Get(id);
                message = $"{(card != null ? card.displayName : id)} 진열 내림 (바인더로)";
            }
            Refresh();
        }

        protected override void Redraw()
        {
            if (Context == null || Context.Session == null) return;
            GameSession s = Context.Session;
            CardData card = binder != null ? binder.SelectedCard : null;
            int count = card != null ? s.Collection.CountOf(card.id) : 0;

            selectedLabel.text = card == null ? NoSelection
                : $"{card.displayName}\n보유 {count}장 · 진열 {ShowcaseRules.CountOf(s, card.id)}장";
            addButton.interactable = card != null && count > 0;

            int capacity = Capacity;
            statusLabel.text = $"진열 칸 {s.Showcase.Count} / {capacity}  ·  진열 카드는 팔리지 않아요";
            messageLabel.text = message;

            for (int i = 0; i < listButtons.Length; i++)
            {
                bool has = i < s.Showcase.Count;
                listButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                CardData shown = Context.Cards.Get(s.Showcase[i]);
                listButtons[i].GetComponentInChildren<TMP_Text>().text = (shown != null ? shown.displayName : s.Showcase[i]) + " (내리기)";
            }
        }
    }
}

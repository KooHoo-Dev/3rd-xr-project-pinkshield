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
    /// 슬라임마켓: 방 업그레이드 상점 화면 (집_업그레이드_설계 3장 E2). 컴퓨터 모니터의 두 번째 쪽(HomeComputer, 11-20 J1), 분야마다 상품 카드 1장
    /// (이름 · 단계 점 · 다음 효과 · 가격 띠 버튼 · 상태 리본, 11-20 F8). 사면 그날 밤 택배 상자로 오고(DEC-040 U5), 쓴 돈은 최종 점수의 가구 값(Q2).
    /// 실수로 사지 않게 한 번 누르면 "한 번 더 누르면 사요", ConfirmSeconds 안에 한 번 더 누르면 삽니다(판매 패널 모두 팔기와 같은 방식).
    /// 규칙은 순수 C# UpgradeRules. 씬을 다시 빌드하지 않도록 HomeUpgradeInstaller가 런타임에 만듭니다.
    /// </summary>
    public sealed class MarketScreen : HomePanel
    {
        const float ConfirmSeconds = 4f;
        const float MinConfirmGap = 0.3f; // 레이 클릭이 두 번 들어와도 바로 사지지 않게

        // 화면 크기 (1px = 1mm)
        const float Width = 1000f, Height = 860f;

        UpgradeKind[] kinds;
        TMP_Text[] rowLabels;
        Button[] buyButtons;
        Image[] cardBacks;   // 상품 카드 바탕 (상태에 따라 색)
        TMP_Text[] ribbons;  // 카드 위 상태 리본 (배송 중 · 도착 · 완료 · 돈 부족)
        static readonly Color CardColor = new Color(0.38f, 0.3f, 0.56f, 1f);
        static readonly Color OrderedColor = new Color(0.25f, 0.4f, 0.62f, 1f);
        static readonly Color ArrivedColor = new Color(0.62f, 0.32f, 0.5f, 1f);
        static readonly Color DoneColor = new Color(0.27f, 0.45f, 0.33f, 1f);
        static readonly Color PoorColor = new Color(0.3f, 0.28f, 0.34f, 1f);
        // 밝은 SlimeOS 창 (UI 개편 샘플 8번): 상품 칸 = 크림 + 잉크 테, 상태는 색 대신 리본 문구 위주
        // (11-20 눈 편한 색: 밝은 크림 칸 → 어두운 보라 칸, 상태는 칸 색을 조금만 다르게 + 리본 문구)
        static readonly Color LightCard = UiTheme.OsCard;
        static readonly Color LightOrdered = new Color(0.22f, 0.29f, 0.33f);
        static readonly Color LightArrived = new Color(0.33f, 0.22f, 0.33f);
        static readonly Color LightDone = new Color(0.2f, 0.19f, 0.27f);
        static readonly Color LightPoor = new Color(0.21f, 0.2f, 0.28f);
        bool lightCards;
        TMP_Text statusLabel, messageLabel;
        string message = "";
        int armed = -1;            // 확인을 기다리는 줄
        float armedUntil = -1f;

        /// <summary>parent 로컬 position에 화면을 만듭니다(집 페이즈 이벤트를 받도록 늘 켜진 Home 루트 아래). 화면 앞면은 yaw 방향을 봅니다.</summary>
        public static MarketScreen Create(Transform parent, Vector3 localPosition, float yaw, UpgradeCatalog catalog, TMP_FontAsset font)
        {
            RectTransform rt = WorldUiKit.CreateCanvas("SlimeMarket", parent, Width, Height, out GameObject content);
            rt.gameObject.SetActive(false);
            rt.localPosition = localPosition;
            rt.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f); // UI 앞면은 캔버스 -Z 쪽에서 보임

            Transform c = content.transform;
            WorldUiKit.AddPanel(c, 12f);
            WorldUiKit.AddText(c, "Title", "슬라임마켓  <size=70%>방 꾸미기 · 사면 오늘 밤 택배로 와요</size>",
                new Vector2(0.03f, 0.9f), new Vector2(0.97f, 0.975f), 34f, UiTheme.Gold, font);

            // 상품 카드 격자 (11-20 F8: 글자 줄 목록이 허접해 보임 → 쇼핑앱처럼 4열 카드 + 가격 띠 버튼 + 상태 리본)
            int n = catalog.tracks != null ? catalog.tracks.Length : 0;
            var kinds = new UpgradeKind[n];
            var labels = new TMP_Text[n];
            var buttons = new Button[n];
            var backs = new Image[n];
            var ribbons = new TMP_Text[n];
            const int cols = 4;
            const float top = 0.885f, bottom = 0.17f, left = 0.03f, right = 0.97f, gap = 0.012f;
            int rows = Mathf.Max(1, (n + cols - 1) / cols);
            float cardW = (right - left - gap * (cols - 1)) / cols, cardH = (top - bottom - gap * (rows - 1)) / rows;
            for (int i = 0; i < n; i++)
            {
                kinds[i] = catalog.tracks[i].kind;
                int row = i / cols, col = i % cols;
                float x0 = left + col * (cardW + gap), y1 = top - row * (cardH + gap);
                backs[i] = WorldUiKit.AddImage(c, $"Card_{kinds[i]}", new Vector2(x0, y1 - cardH), new Vector2(x0 + cardW, y1), CardColor);
                backs[i].raycastTarget = false;
                Transform card = backs[i].transform;
                ribbons[i] = WorldUiKit.AddText(card, "Ribbon", "", new Vector2(0f, 0.86f), new Vector2(1f, 1f), 17f, UiTheme.Gold, font);
                labels[i] = WorldUiKit.AddText(card, "Body", "", new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.86f), 21f, UiTheme.Text, font);
                labels[i].alignment = TextAlignmentOptions.Top;
                labels[i].enableAutoSizing = true;
                labels[i].fontSizeMin = 15f;
                labels[i].fontSizeMax = 21f;
                buttons[i] = WorldUiKit.AddButton(card, "사기", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.25f), font);
                buttons[i].GetComponentInChildren<TMP_Text>().fontSize = 22f;
            }
            TMP_Text status = WorldUiKit.AddText(c, "Status", "", new Vector2(0.03f, 0.09f), new Vector2(0.97f, 0.155f), 22f, UiTheme.SubText, font);
            TMP_Text msg = WorldUiKit.AddText(c, "Message", "", new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f), 24f, UiTheme.Gold, font);

            var screen = rt.gameObject.AddComponent<MarketScreen>();
            screen.content = content;
            screen.kinds = kinds;
            screen.rowLabels = labels;
            screen.buyButtons = buttons;
            screen.cardBacks = backs;
            screen.ribbons = ribbons;
            screen.statusLabel = status;
            screen.messageLabel = msg;
            rt.gameObject.SetActive(true);
            return screen;
        }

        /// <summary>밝은 창(SlimeOS)에 놓였을 때 부름: 상품 칸을 크림 + 잉크 테로, 글자 색을 밝은 바탕용으로</summary>
        public void UseLightCards()
        {
            if (lightCards || !Application.isPlaying) return;
            lightCards = true;
            foreach (Image back in cardBacks)
            {
                back.sprite = UiSprites.Rounded();
                back.type = Image.Type.Sliced;
                back.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
                Image rim = WorldUiKit.AddImage(back.transform, "Rim", Vector2.zero, Vector2.one, UiTheme.OsCardLine);
                rim.raycastTarget = false;
                rim.sprite = UiSprites.Frame(3);
                rim.type = Image.Type.Sliced;
                rim.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
                rim.transform.SetAsFirstSibling();
            }
            Refresh();
        }

        protected override void OnEnable()
        {
            if (buyButtons == null) return; // Create가 설정을 넣기 전
            for (int i = 0; i < buyButtons.Length; i++)
            {
                int index = i;
                buyButtons[i].onClick.AddListener(() => OnBuyClicked(index));
            }
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (buyButtons == null) return;
            foreach (Button b in buyButtons) b.onClick.RemoveAllListeners();
            base.OnDisable();
        }

        protected override void OnPhaseChanged(GamePhaseId phase)
        {
            if (phase == GamePhaseId.Home) { message = ""; armed = -1; }
            base.OnPhaseChanged(phase);
        }

        // 확인 시간이 지나면 "한 번 더" 안내를 지움
        void Update()
        {
            if (armed < 0 || Time.unscaledTime <= armedUntil) return;
            armed = -1;
            message = "";
            Refresh();
        }

        void OnBuyClicked(int index)
        {
            if (Context == null || Context.Session == null || index >= kinds.Length) return;
            GameSession s = Context.Session;
            UpgradeRules rules = Context.Upgrades;
            UpgradeKind kind = kinds[index];
            string name = rules.Catalog.Get(kind)?.displayName ?? kind.ToString();
            float now = Time.unscaledTime;

            BuyResult check = rules.Check(s, kind);
            if (check != BuyResult.Ok)
            {
                armed = -1;
                message = Reason(check, name, rules.NextLevel(s.Upgrades, kind));
                Sfx.Play(SoundId.UiDeny, transform.position);
            }
            else if (armed != index || now > armedUntil)
            {
                armed = index;
                armedUntil = now + ConfirmSeconds;
                message = $"{name}  {MoneyText.Format(rules.NextLevel(s.Upgrades, kind).price)} — 한 번 더 누르면 사요";
            }
            else if (now >= armedUntil - ConfirmSeconds + MinConfirmGap)
            {
                armed = -1;
                if (rules.TryBuy(s, kind, out int price) == BuyResult.Ok)
                {
                    message = $"{name} 주문 완료 {MoneyText.Signed(-price)} · 곧 택배가 와요 (효과는 내일부터)"; // 그날 밤 택배 (DEC-040 U5)
                    Sfx.Play(SoundId.PosPay, transform.position);
                }
            }
            Refresh();
        }

        static string Reason(BuyResult r, string name, UpgradeCatalog.Level next)
        {
            switch (r)
            {
                case BuyResult.Pending: return $"{name}: 주문한 택배가 아직 안 열렸어요";
                case BuyResult.MaxLevel: return $"{name}: 이미 최고 단계예요";
                case BuyResult.NotEnoughMoney: return $"{name}: 돈이 모자라요 ({MoneyText.Format(next != null ? next.price : 0)} 필요)";
                default: return "살 수 없는 물건이에요";
            }
        }

        protected override void Redraw()
        {
            if (Context == null || Context.Session == null) return;
            GameSession s = Context.Session;
            UpgradeRules rules = Context.Upgrades;
            bool armedValid = armed >= 0 && Time.unscaledTime <= armedUntil;

            for (int i = 0; i < kinds.Length; i++)
            {
                UpgradeKind kind = kinds[i];
                UpgradeCatalog.Track track = rules.Catalog.Get(kind);
                int level = s.Upgrades.LevelOf(kind), max = rules.MaxLevel(kind);
                UpgradeStatus status = s.Upgrades.StatusOf(kind);
                UpgradeCatalog.Level next = rules.NextLevel(s.Upgrades, kind);
                bool decor = UpgradeRules.IsDecor(kind); // 꾸미기는 단계 없이 "있음 / 없음"
                string trackName = track != null ? track.displayName : kind.ToString();
                // 상품 카드: 이름 · 단계 점(●●○) · 다음 효과 / 위 리본 = 상태 / 바탕색 = 상태 · 살 수 있는지 (F8)
                string dotColor = lightCards ? "#E2BE6A" : "#FFD27F", effectColor = lightCards ? "#B9B0CC" : "#E2DBF5"; // SlimeOS 창이면 눈 편한 색
                string dots = decor ? "" : $"\n<size=85%><color={dotColor}>" + new string('●', level) + new string('○', Mathf.Max(0, max - level)) + "</color></size>";
                string effect = next != null ? next.effect : decor ? "방에 있어요" : "모두 업그레이드했어요";
                rowLabels[i].text = $"<b>{trackName}</b>{dots}\n<size=80%><color={effectColor}>{effect}</color></size>";
                bool poor = next != null && s.Wallet.Money < next.price;
                ribbons[i].text = lightCards
                    ? (status == UpgradeStatus.Ordered ? "<color=#7FD6B0>배송 중</color>" :
                       status == UpgradeStatus.Arrived ? "<color=#F29BC4>택배 도착! 방 상자를 열어요</color>" :
                       next == null ? "<color=#B9B0CC>완료</color>" :
                       poor ? "<color=#9F96B5>돈이 모자라요</color>" : "")
                    : (status == UpgradeStatus.Ordered ? "<color=#9FDCFF>배송 중</color>" :
                       status == UpgradeStatus.Arrived ? "<color=#FFB3D9>택배 도착! 방 상자를 열어요</color>" :
                       next == null ? "<color=#9CE59C>완료</color>" :
                       poor ? "<color=#B9B3C8>돈이 모자라요</color>" : "");
                cardBacks[i].color = lightCards
                    ? (status == UpgradeStatus.Ordered ? LightOrdered : status == UpgradeStatus.Arrived ? LightArrived
                       : next == null ? LightDone : poor ? LightPoor : LightCard)
                    : (status == UpgradeStatus.Ordered ? OrderedColor : status == UpgradeStatus.Arrived ? ArrivedColor
                       : next == null ? DoneColor : poor ? PoorColor : CardColor);

                TMP_Text label = buyButtons[i].GetComponentInChildren<TMP_Text>();
                bool canOrder = status == UpgradeStatus.None && next != null;
                label.text = !canOrder ? (next == null ? "완료" : "주문함") : armedValid && armed == i ? "한 번 더!" : MoneyText.Format(next.price);
                buyButtons[i].interactable = canOrder;
                if (lightCards) UiSkin.StyleButton(buyButtons[i], canOrder && !poor ? UiButtonKind.Primary : UiButtonKind.Normal); // 살 수 있는 것만 분홍
            }

            statusLabel.text = $"내 자산 {MoneyText.Format(s.Wallet.Money)}   ·   가구 값 {MoneyText.Format(UpgradeRules.FurnitureValue(s.Upgrades))} (최종 점수에 그대로 들어가요)";
            messageLabel.text = message;
        }
    }
}

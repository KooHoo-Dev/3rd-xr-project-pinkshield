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
    /// 슬라임마켓: 방 업그레이드 상점 화면 (집_업그레이드_설계 3장 E2). 컴퓨터 옆 벽에 걸린 화면으로, 분야마다 한 줄
    /// (이름 · 지금 단계 · 다음 효과 · [가격 사기]). 사면 다음 날 아침 택배 상자로 오고(DEC-031 Q3), 쓴 돈은 최종 점수의 가구 값(Q2).
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
            WorldUiKit.AddText(c, "Title", "슬라임마켓  <size=70%>방 꾸미기 · 산 물건은 다음 날 택배로 와요</size>",
                new Vector2(0.03f, 0.9f), new Vector2(0.97f, 0.975f), 34f, UiTheme.Gold, font);

            int n = catalog.tracks != null ? catalog.tracks.Length : 0;
            var kinds = new UpgradeKind[n];
            var labels = new TMP_Text[n];
            var buttons = new Button[n];
            const float top = 0.885f, bottom = 0.17f;
            float rowH = (top - bottom) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                kinds[i] = catalog.tracks[i].kind;
                float y1 = top - i * rowH, y0 = y1 - rowH + 0.012f;
                labels[i] = WorldUiKit.AddText(c, $"Row_{kinds[i]}", "", new Vector2(0.04f, y0), new Vector2(0.72f, y1), 24f, UiTheme.Text, font);
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = WorldUiKit.AddButton(c, "사기", new Vector2(0.74f, y0), new Vector2(0.96f, y1), font);
                buttons[i].GetComponentInChildren<TMP_Text>().fontSize = 22f;
            }
            TMP_Text status = WorldUiKit.AddText(c, "Status", "", new Vector2(0.03f, 0.09f), new Vector2(0.97f, 0.155f), 22f, UiTheme.SubText, font);
            TMP_Text msg = WorldUiKit.AddText(c, "Message", "", new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f), 24f, UiTheme.Gold, font);

            var screen = rt.gameObject.AddComponent<MarketScreen>();
            screen.content = content;
            screen.kinds = kinds;
            screen.rowLabels = labels;
            screen.buyButtons = buttons;
            screen.statusLabel = status;
            screen.messageLabel = msg;
            rt.gameObject.SetActive(true);
            return screen;
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
                    message = $"{name} 주문 완료 {MoneyText.Signed(-price)} · 내일 아침 택배로 와요";
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
                string head = decor ? trackName : $"{trackName} <size=80%>Lv{level}/{max}</size>";
                string tail =
                    status == UpgradeStatus.Ordered ? "<color=#7FD1FF>내일 아침 도착</color>" :
                    status == UpgradeStatus.Arrived ? "<color=#7FD1FF>택배 도착! 방의 상자를 잡아 열어요</color>" :
                    next == null ? (decor ? "<color=#9CE59C>방에 있음</color>" : "<color=#9CE59C>최고 단계</color>") :
                    next.effect;
                rowLabels[i].text = $"{head}\n<size=80%><color=#CFC6EA>{tail}</color></size>";

                TMP_Text label = buyButtons[i].GetComponentInChildren<TMP_Text>();
                bool canOrder = status == UpgradeStatus.None && next != null;
                label.text = !canOrder ? "-" : armedValid && armed == i ? "한 번 더!" : $"{MoneyText.Format(next.price)} 사기";
                buyButtons[i].interactable = canOrder;
            }

            statusLabel.text = $"내 자산 {MoneyText.Format(s.Wallet.Money)}   ·   가구 값 {MoneyText.Format(UpgradeRules.FurnitureValue(s.Upgrades))} (최종 점수에 그대로 들어가요)";
            messageLabel.text = message;
        }
    }
}

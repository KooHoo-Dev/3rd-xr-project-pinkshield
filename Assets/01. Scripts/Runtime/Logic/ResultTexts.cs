namespace CardShop.Logic
{
    /// <summary>
    /// 정산 · 최종 판에 쓰는 글자 (타이틀연출_손님_설계 0-3). 화면 코드와 떼어 두어 숫자 · 부호를 테스트로 고정합니다.
    /// 오른쪽 숫자 열은 TMP &lt;pos&gt; 태그로 맞춥니다.
    /// </summary>
    public static class ResultTexts
    {
        const string Col = "<pos=68%>";

        /// <summary>영수증 점선 한 줄 (정산 합계 위)</summary>
        public const string Rule = "<color=#9C93AE>- - - - - - - - - - - - - - - - - - - -</color>";

        /// <summary>+12 / −3 / 0 (빼기는 읽기 쉬운 유니코드 마이너스)</summary>
        public static string Signed(int v) => v > 0 ? "+" + v : v < 0 ? "−" + (-v) : "0";

        public static string SettlementTitle(int day, int dayCount) => day > dayCount
            ? $"{day}일차 정산 <size=60%>(연장 {day - dayCount}일)</size>" // 무한 모드 (J4)
            : $"{day}일차 정산 <size=60%>({day}/{dayCount}일)</size>";

        /// <summary>무한 모드 결과 판 본문 (J4): 7일 공식 기록 한 줄 + 연장 기간 요약. 랭크 없음</summary>
        public static string EndlessFinal(EndlessState e, GameSession s)
        {
            if (e == null || s == null) return "";
            var sb = new System.Text.StringBuilder();
            FinalResult o = e.Official;
            if (o != null) sb.Append("<size=80%>공식 기록 (7일)").Append(Col).Append(o.Score).Append("점 · ").Append(o.Rank != null ? o.Rank.name : "-").Append("</size>\n");
            sb.Append("연장 근무").Append(Col).Append(e.ExtraDays(s.Day)).Append("일\n");
            sb.Append("그동안 번 돈").Append(Col).Append(MoneyText.Signed(e.MoneyGained(s.Wallet.Money))).Append('\n');
            sb.Append("연 팩").Append(Col).Append(e.PacksOpened(s.Stats.packsOpened)).Append("개\n");
            sb.Append("새로 모은 카드").Append(Col).Append(e.NewKinds(s.Collection.DiscoveredCount)).Append("종\n");
            sb.Append("지금 자산").Append(Col).Append(MoneyText.Format(s.Wallet.Money));
            return sb.ToString();
        }

        /// <param name="total">SettlementPhase.LastTotal (이미 지갑에 더해진 값)</param>
        /// <param name="money">더한 뒤 보유 금액</param>
        public static string Settlement(DayLedger l, EconomyRules rules, int total, int money)
        {
            if (l == null || rules == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("기본급").Append(Col).Append(MoneyText.Signed(l.basePay)).Append('\n');
            sb.Append($"청소 {l.cleanupsDone}곳");
            if (l.cleanupsMissed > 0) sb.Append($" · 못 치움 {l.cleanupsMissed}곳");
            sb.Append(Col).Append(MoneyText.Signed(rules.CleanupTotal(l))).Append('\n');
            sb.Append($"빠른 계산 {l.checkoutsFast}건");
            if (l.customersLost > 0) sb.Append($" · 떠난 손님 {l.customersLost}명");
            sb.Append(Col).Append(MoneyText.Signed(rules.CheckoutTotal(l))).Append('\n');
            sb.Append($"판매 {l.sales.Count}장").Append(Col).Append(MoneyText.Signed(rules.SalesTotal(l))).Append('\n');
            if (l.thievesCaught > 0) sb.Append($"<size=80%>도둑 {l.thievesCaught}명 잡음!</size>\n");
            if (l.freePackStolen) sb.Append("<size=80%>도둑이 오늘 무료 팩을 가져감</size>\n");
            sb.Append(Rule).Append('\n'); // 영수증 점선 (UI_개편_설계 4번 판)
            sb.Append($"<b>합계</b>{Col}<b>{MoneyText.Signed(total)}</b>\n");
            sb.Append($"<size=80%>내 자산</size>{Col}<size=80%>{MoneyText.Format(money)}</size>");
            return sb.ToString();
        }

        public static string Final(FinalResult r, string ending)
        {
            if (r == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("내 자산").Append(Col).Append(MoneyText.Format(r.Money)).Append('\n');
            sb.Append("카드 가치").Append(Col).Append(MoneyText.Format(r.CardValue)).Append('\n');
            if (r.FurnitureValue > 0) sb.Append("가구 값").Append(Col).Append(MoneyText.Format(r.FurnitureValue)).Append('\n');
            if (r.RoomPoints > 0) sb.Append($"방 꾸미기 {r.FurniturePieces}개").Append(Col).Append(Signed(r.RoomPoints)).Append('\n');
            if (r.SetPoints > 0) sb.Append($"진열장 세트 {r.ShowcaseSets.Count}개").Append(Col).Append(Signed(r.SetPoints)).Append('\n');
            sb.Append($"도감 {r.CollectedKinds}/{r.TotalKinds}종").Append(Col).Append(Signed(r.CollectionPoints)).Append('\n');
            sb.Append($"<size=80%>수퍼레어 이상 {r.RareCollected}/{r.RareTotal}종 · 연 팩 {r.PacksOpened}개</size>\n");
            if (r.BestCard != null) sb.Append($"<size=80%>최고의 카드: {r.BestCard.displayName}</size>\n");
            if (r.ShowcaseCards.Count > 0) sb.Append($"<size=80%>내 방 진열장 {r.ShowcaseCards.Count}장 (카드 가치에 포함)</size>\n");
            sb.Append($"<b>최종 점수</b>{Col}<b>{r.Score}</b>\n");
            string rank = r.Rank != null ? r.Rank.name : "-";
            sb.Append($"<size=120%><b>랭크 {rank}</b></size>");
            if (!string.IsNullOrEmpty(ending)) sb.Append($"  {ending}");
            string room = RoomTitle(r.FurnitureValue, r.ShowcaseCards.Count);
            if (room != null) sb.Append($" <size=80%>· {room}</size>");
            return sb.ToString();
        }

        // 내 방 칭호 기준 (가구 값, 달러). 설계서 표 가격 기준: 꾸미기만 ≈ $15~90, 한 분야 끝까지 ≈ $140~290, 전부 ≈ $1,000
        public const int CozyRoomMin = 1, DecoratedRoomMin = 150, RichRoomMin = 400;

        /// <summary>
        /// 엔딩 뒤에 붙는 내 방 칭호 한 마디 (집_업그레이드_설계 3장 "엔딩 문구"). 가구를 안 샀고 진열도 없으면 null.
        /// </summary>
        public static string RoomTitle(int furnitureValue, int showcaseCount)
        {
            if (furnitureValue >= RichRoomMin) return "원룸 카드 부자";
            if (furnitureValue >= DecoratedRoomMin) return "꾸민 원룸의 수집가";
            if (furnitureValue >= CozyRoomMin) return "아늑한 원룸";
            return showcaseCount > 0 ? "진열장 있는 원룸" : null;
        }
    }
}

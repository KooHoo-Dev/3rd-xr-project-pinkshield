namespace CardShop.Logic
{
    /// <summary>
    /// 무한 모드 상태 (무한모드_설계 J4). 7일 결과는 "공식 기록"으로 저장해 두고, 연장 기간에는 평가 없이 계속 놉니다(Q2).
    /// 연장 기간 합계(번 돈 · 연 팩 · 새 카드 종류)는 시작할 때의 값과 끝날 때의 값의 차이로 셉니다. 저장 기능은 없음(Q7).
    /// </summary>
    public sealed class EndlessState
    {
        /// <summary>계속하기를 눌러 연장 근무 중</summary>
        public bool Active { get; private set; }
        /// <summary>그만두기를 눌러 무한 모드 결과 판을 보는 중</summary>
        public bool Finished { get; private set; }
        /// <summary>계속하기를 누른 날 (마지막 공식 날, 보통 7)</summary>
        public int StartDay { get; private set; }
        /// <summary>7일 공식 기록</summary>
        public FinalResult Official { get; private set; }

        int startMoney, startPacks, startKinds;

        public void Start(FinalResult official, int day, int money, int packsOpened, int kinds)
        {
            Active = true;
            Finished = false;
            Official = official;
            StartDay = day;
            startMoney = money;
            startPacks = packsOpened;
            startKinds = kinds;
        }

        public void Finish() { if (Active) Finished = true; }

        /// <summary>연장한 날 수 (7일차 밤에 시작해 8일차를 마치면 1)</summary>
        public int ExtraDays(int day) => day - StartDay < 0 ? 0 : day - StartDay;
        public int MoneyGained(int money) => money - startMoney;
        public int PacksOpened(int packsOpened) => packsOpened - startPacks;
        public int NewKinds(int kinds) => kinds - startKinds;
    }
}

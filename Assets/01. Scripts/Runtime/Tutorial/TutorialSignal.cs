namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 단계의 시작 · 완료 조건이 되는 "일어난 일" 이름 (튜토리얼_설계 8-4).
    /// 값을 명시해 두어 SO에 저장된 단계가 순서 변경에 깨지지 않습니다. 중간 값은 비워 두고 새 신호는 끝에 붙입니다.
    /// </summary>
    public enum TutorialSignal
    {
        None = 0,

        // 공통
        PhaseEntered = 1,      // 페이즈가 바뀌고 화면 전환(페이드)까지 끝남
        StepTimeout = 2,       // (내부용) 시간이 다 된 단계

        // Intro · 개봉
        GiftPackGrabbed = 10,
        GiftPackOpened = 11,
        RevealFinished = 12,   // 카드 넘기기가 끝나 결과함이 뜸
        PackOpenedPaid = 13,   // 돈을 내고 연 첫 팩 이후마다
        PackOpenedFree = 14,   // 계산대 무료 팩을 엶
        PackDeniedMoney = 15,  // 돈이 모자라 개봉이 거절됨
        BoxAffordable = 16,    // 퇴근 전, 돈 ≥ 상자 가격
        BoxOpened = 17,
        ShelfPackGrabbed = 18,

        // 영업
        CheckoutStarted = 30,  // 손님이 계산대에 물건을 올림
        ItemScanned = 31,
        AllScanned = 32,
        AllBagged = 33,
        CheckoutPaid = 34,
        TrashSpawned = 35,
        TrashBinned = 36,
        MessyPackSpawned = 37,
        MessyPackFixed = 38,
        BusinessClosing = 39,  // 영업 시계가 끝남
        ThiefAppeared = 40,
        BroomGrabbed = 41,
        ThiefCaught = 42,
        ThiefEscaped = 43,
        ScannerGrabbed = 44,

        // 정산 · 퇴근 · 집
        SettlementConfirmed = 60,
        ClockedOut = 61,
        BinderBrowsed = 62,
        DisplayableCardOwned = 63, // 집에서 수퍼레어 이상 카드를 가지고 있음
        SalesRegistered = 64,
        InstantSold = 65,
        Slept = 66,

        // 청소 다시 설계 (FIX F5 · F6, 저장된 번호가 꼬이지 않게 끝에 추가)
        StainSpawned = 70,
        StainCleaned = 71,
        // 처음 하는 업무 정지 (FIX C1 · DEC-028)
        ClosingCleanupStarted = 72,
        ListedCardCheckout = 73,
        WalletViewed = 74,

        // 경제 · 가구 튜토리얼 (튜토리얼_설계 13-4, DEC-040 · 041)
        RareCardPicked = 80,         // 집: 바인더에서 수퍼레어 이상 카드를 고름
        InstantSoldZero = 81,        // 집: 노멀 낱장을 팔려다 매입가 0
        ListedSaleSettled = 82,      // 정산: 판매 등록 카드 판매 수입 > 0
        SalesSlotsFull = 83,         // 집: 판매 칸이 다 참 (바인더 최고 단계가 아닐 때)
        ListedCardsOnShelf = 84,     // 영업: 가게 진열대에 등록 카드가 있음
        MarketSurgeToday = 85,       // 영업: 오늘 1순위 강도 ≥ 급등
        ForecastStrengthVisible = 86,// 컴퓨터 2단계 이상
        DeliveryArrived = 87,        // 집: 택배 초인종
        ShowcaseSetPossible = 88,    // 집: 같은 속성 수퍼레어 이상 3장 보유
        ShowcaseSetCompleted = 89,   // 집: 진열장 세트 완성
        DecorInstalled = 90,         // 집: 꾸미기 설치
        PosterInstalled = 91,        // 집: 포스터 설치
        CozyRoomCompleted = 92,      // 집: 꾸미기 5종 완성
        OnlineStallInstalled = 93,   // 집: 컴퓨터 3단계 (슬라임마켓)
        UpgradeCreditShown = 94,     // 정산: "가구 덕분에" 줄이 있음
        EndlessFirstDay = 95,        // 영업: 무한 모드 연장 첫날
        MarketFallingToday = 96,     // 영업: 오늘 하락 속성이 있음
        RecordSurgeToday = 97,       // 영업: 오늘 강도 = 역대급
        LampInstalled = 98,          // 집: 조명 설치 (누르면 켜고 끔, T6)
        AirConInstalled = 99,        // 집: 에어컨 설치 (누르면 켜고 끔, T6)
    }
}

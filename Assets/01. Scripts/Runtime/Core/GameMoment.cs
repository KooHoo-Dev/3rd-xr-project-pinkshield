namespace CardShop.Core
{
    /// <summary>
    /// 게임 곳곳에서 알리는 "일어난 일" (GameEvents.Happened). 튜토리얼이 처음 생긴 상황을 알아채는 데 씁니다 (튜토리얼_설계 8-4).
    /// 이벤트를 하나씩 늘리지 않고 이름만 늘려, 알리는 쪽은 한 줄(RaiseHappened)만 씁니다. 값은 저장하지 않으므로 순서를 바꿔도 됩니다.
    /// </summary>
    public enum GameMoment
    {
        // 계산
        CheckoutStarted,
        ItemScanned,
        AllScanned,
        AllBagged,
        CheckoutPaid,
        // 청소
        TrashSpawned,
        TrashBinned,
        MessyPackSpawned,
        MessyPackFixed,
        // 영업 · 도둑
        BusinessClosing,
        ThiefAppeared,
        ThiefCaught,
        ThiefEscaped,
        // 개봉
        GiftPackOpened,
        PackOpenedFree,
        PackOpenedPaid,
        PackDeniedMoney,
        BoxOpened,
        RevealFinished,
        // 집
        SalesRegistered,
        InstantSold,
        BinderBrowsed,
    }
}

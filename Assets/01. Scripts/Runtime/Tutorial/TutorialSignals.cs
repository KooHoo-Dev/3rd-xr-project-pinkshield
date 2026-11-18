using CardShop.Core;

namespace CardShop.Tutorial
{
    /// <summary>게임이 알린 일(GameMoment)을 튜토리얼 신호(TutorialSignal)로 바꿉니다. 대응이 없으면 None.</summary>
    public static class TutorialSignals
    {
        public static TutorialSignal From(GameMoment m) => m switch
        {
            GameMoment.CheckoutStarted => TutorialSignal.CheckoutStarted,
            GameMoment.ItemScanned => TutorialSignal.ItemScanned,
            GameMoment.AllScanned => TutorialSignal.AllScanned,
            GameMoment.AllBagged => TutorialSignal.AllBagged,
            GameMoment.CheckoutPaid => TutorialSignal.CheckoutPaid,
            GameMoment.TrashSpawned => TutorialSignal.TrashSpawned,
            GameMoment.TrashBinned => TutorialSignal.TrashBinned,
            GameMoment.MessyPackSpawned => TutorialSignal.MessyPackSpawned,
            GameMoment.MessyPackFixed => TutorialSignal.MessyPackFixed,
            GameMoment.BusinessClosing => TutorialSignal.BusinessClosing,
            GameMoment.ThiefAppeared => TutorialSignal.ThiefAppeared,
            GameMoment.ThiefCaught => TutorialSignal.ThiefCaught,
            GameMoment.ThiefEscaped => TutorialSignal.ThiefEscaped,
            GameMoment.GiftPackOpened => TutorialSignal.GiftPackOpened,
            GameMoment.PackOpenedFree => TutorialSignal.PackOpenedFree,
            GameMoment.PackOpenedPaid => TutorialSignal.PackOpenedPaid,
            GameMoment.PackDeniedMoney => TutorialSignal.PackDeniedMoney,
            GameMoment.BoxOpened => TutorialSignal.BoxOpened,
            GameMoment.RevealFinished => TutorialSignal.RevealFinished,
            GameMoment.SalesRegistered => TutorialSignal.SalesRegistered,
            GameMoment.InstantSold => TutorialSignal.InstantSold,
            GameMoment.BinderBrowsed => TutorialSignal.BinderBrowsed,
            _ => TutorialSignal.None,
        };
    }
}

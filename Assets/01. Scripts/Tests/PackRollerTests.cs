using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 고정 시드로 대량 개봉해 기획서 5-3 ③④ 표와 비교합니다.
    /// 확률 설정을 바꾸면 이 테스트의 기대값도 기획서와 함께 갱신하세요.
    /// </summary>
    public class PackRollerTests
    {
        const int Seed = 12345;
        const int Packs = 200_000;

        PackOddsConfig odds;
        CardDatabase db;
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            odds = Make<PackOddsConfig>();
            db = Make<CardDatabase>();
            // 등급별 2장씩 가짜 카드
            for (int r = 0; r < CardEnumUtil.RarityCount; r++)
                for (int i = 0; i < 2; i++)
                {
                    var c = Make<CardData>();
                    c.id = $"T-{r}-{i}";
                    c.rarity = (CardRarity)r;
                    c.basePrice = r + 1;
                    db.cards.Add(c);
                }
            db.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        static float AtLeast(PackRoller roller, PackClue clue, CardRarity min)
        {
            int hit = 0;
            for (int i = 0; i < Packs; i++)
                if (roller.Roll(clue, false).BestRarity >= min) hit++;
            return hit * 100f / Packs;
        }

        // 기획서 5-3 ③: 한 팩에서 이 등급 이상이 1장 이상 (특수 팩 제외 값)
        [TestCase(PackClue.None, 100f, 42.6f, 10.2f, 0.7f)]
        [TestCase(PackClue.FoldedTop, 100f, 60.3f, 15.2f, 1.0f)]
        [TestCase(PackClue.FoldedTop | PackClue.FlippedBack, 100f, 76.0f, 20.1f, 1.4f)]
        public void AtLeastRarity_MatchesDesignTable(PackClue clue, float rare, float sr, float unique, float legendary)
        {
            odds.godPackChance = 0f;
            odds.legendaryPackChance = 0f;
            const float tol = 0.5f; // %p

            Assert.AreEqual(rare, AtLeast(new PackRoller(odds, db, new SystemRandomSource(Seed)), clue, CardRarity.Rare), tol, "레어 이상");
            Assert.AreEqual(sr, AtLeast(new PackRoller(odds, db, new SystemRandomSource(Seed)), clue, CardRarity.SuperRare), tol, "수퍼레어 이상");
            Assert.AreEqual(unique, AtLeast(new PackRoller(odds, db, new SystemRandomSource(Seed)), clue, CardRarity.Unique), tol, "유니크 이상");
            Assert.AreEqual(legendary, AtLeast(new PackRoller(odds, db, new SystemRandomSource(Seed)), clue, CardRarity.Legendary), 0.2f, "레전더리");
        }

        // 기획서 5-3 ④: 갓팩 0.2%, 레전더리 확정 팩 1.0%
        [Test]
        public void SpecialPackRates_MatchDesign()
        {
            var roller = new PackRoller(odds, db, new SystemRandomSource(Seed));
            int god = 0, legend = 0;
            for (int i = 0; i < Packs; i++)
            {
                PackResult r = roller.Roll(PackClue.None, true);
                if (r.Special == SpecialPackType.GodPack) god++;
                if (r.Special == SpecialPackType.LegendaryPack) legend++;
            }
            Assert.AreEqual(0.2f, god * 100f / Packs, 0.05f, "갓팩");
            Assert.AreEqual(1.0f, legend * 100f / Packs, 0.1f, "레전더리 확정 팩");
        }

        [Test]
        public void GodPack_AllUniqueOrBetter_LegendaryPack_HitIsLegendary()
        {
            var roller = new PackRoller(odds, db, new SystemRandomSource(Seed));

            roller.ForceNextSpecial(SpecialPackType.GodPack);
            foreach (CardData c in roller.Roll(PackClue.None, false).Cards)
                Assert.GreaterOrEqual((int)c.rarity, (int)CardRarity.Unique);

            roller.ForceNextSpecial(SpecialPackType.LegendaryPack);
            PackResult legend = roller.Roll(PackClue.None, false);
            Assert.AreEqual(CardRarity.Legendary, legend.Cards[legend.Cards.Length - 1].rarity);
        }

        [Test]
        public void ForcedValues_ApplyToOnePackOnly()
        {
            odds.godPackChance = 0f;
            odds.legendaryPackChance = 0f;
            var roller = new PackRoller(odds, db, new SystemRandomSource(Seed));
            roller.ForceNextHitRarity(CardRarity.Legendary);
            Assert.AreEqual(SpecialPackType.None, roller.Roll(PackClue.None, false).Special);
            roller.ForceNextSpecial(SpecialPackType.GodPack);
            roller.Roll(PackClue.None, false);
            Assert.AreEqual(SpecialPackType.None, roller.Roll(PackClue.None, false).Special, "강제는 1회만");
        }

        [Test]
        public void EveryPack_HasRareOrBetter()
        {
            var roller = new PackRoller(odds, db, new SystemRandomSource(Seed));
            for (int i = 0; i < 20_000; i++)
                Assert.GreaterOrEqual((int)roller.Roll(PackClue.None, false).BestRarity, (int)CardRarity.Rare);
        }

        [Test]
        public void BoxPack_IgnoresClue()
        {
            var roller = new PackRoller(odds, db, new SystemRandomSource(Seed));
            Assert.AreEqual(PackClue.None, roller.Roll(PackClue.FoldedTop | PackClue.FlippedBack, true).Clue);
        }

        // 기획서 5-2: 일반 75 / 단서 1종 20 / 단서 2종 5
        [Test]
        public void ShelfClueDistribution_MatchesDesign()
        {
            PackClue[] clues = ShelfGenerator.Generate(100_000, odds, new SystemRandomSource(Seed));
            int none = 0, one = 0, two = 0;
            foreach (PackClue c in clues)
            {
                int n = CardEnumUtil.ClueCount(c);
                if (n == 0) none++; else if (n == 1) one++; else two++;
            }
            Assert.AreEqual(75f, none / 1000f, 0.5f);
            Assert.AreEqual(20f, one / 1000f, 0.5f);
            Assert.AreEqual(5f, two / 1000f, 0.3f);
        }
    }

    public class PackOpeningServiceTests
    {
        EconomyConfig economy;
        PackOddsConfig odds;
        CardDatabase db;
        GameSession session;
        PackOpeningService service;

        [SetUp]
        public void SetUp()
        {
            economy = ScriptableObject.CreateInstance<EconomyConfig>();
            odds = ScriptableObject.CreateInstance<PackOddsConfig>();
            db = ScriptableObject.CreateInstance<CardDatabase>();
            for (int r = 0; r < CardEnumUtil.RarityCount; r++)
            {
                var c = ScriptableObject.CreateInstance<CardData>();
                c.id = $"T-{r}";
                c.rarity = (CardRarity)r;
                c.basePrice = (r + 1) * 10;
                db.cards.Add(c);
            }
            db.InvalidateCache();
            session = new GameSession(0);
            service = new PackOpeningService(() => session, new PackRoller(odds, db, new SystemRandomSource(1)), economy);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CardData c in db.cards) Object.DestroyImmediate(c);
            Object.DestroyImmediate(db);
            Object.DestroyImmediate(odds);
            Object.DestroyImmediate(economy);
        }

        [Test]
        public void FreePack_ThenPaid_ThenNotEnoughMoney()
        {
            session.FreePacksLeft = 1;
            session.Wallet.Add(economy.packPrice);

            Assert.IsTrue(service.TryOpenSingle(PackClue.None, false, out _, out _), "무료 팩");
            Assert.AreEqual(economy.packPrice, session.Wallet.Money, "무료 팩은 차감 없음");

            Assert.IsTrue(service.TryOpenSingle(PackClue.None, false, out _, out _), "유료 팩");
            Assert.AreEqual(0, session.Wallet.Money);

            Assert.IsFalse(service.TryOpenSingle(PackClue.None, false, out _, out OpenFailReason fail));
            Assert.AreEqual(OpenFailReason.NotEnoughMoney, fail);
            Assert.AreEqual(2, session.Stats.packsOpened);
        }

        [Test]
        public void GiftPack_IsFreeAndKeepsFreePack()
        {
            session.FreePacksLeft = 1;
            Assert.IsTrue(service.TryOpenSingle(PackClue.None, true, out _, out _));
            Assert.AreEqual(1, session.FreePacksLeft);
        }

        [Test]
        public void OpenedCards_GoToCollectionWithNewFlags()
        {
            session.FreePacksLeft = 1;
            service.TryOpenSingle(PackClue.None, false, out PackResult r, out _);
            Assert.AreEqual(odds.CardsPerPack, session.Collection.TotalCards);
            Assert.IsTrue(r.IsNew[0], "첫 카드는 항상 새 카드");
            Assert.IsNotNull(session.Stats.bestCard);
        }

        [Test]
        public void Box_CostsBoxPrice_OpensTenPacks()
        {
            session.Wallet.Add(economy.boxPrice);
            Assert.IsTrue(service.TryOpenBox(out PackResult[] packs, out _));
            Assert.AreEqual(economy.packsPerBox, packs.Length);
            Assert.AreEqual(0, session.Wallet.Money);
            Assert.AreEqual(economy.packsPerBox * odds.CardsPerPack, session.Collection.TotalCards);
        }
    }
}

using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>가구를 사는 보람 (DEC-040): 진열장 세트(U1) · 꾸미기 세트(U3) · 방 점수(U4).</summary>
    public class RoomRewardTests
    {
        readonly List<Object> made = new List<Object>();

        CardData Card(CardElement e, CardRarity r, CardKind kind = CardKind.Slime)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = $"{e}-{r}-{made.Count}"; c.kind = kind; c.element = e; c.rarity = r;
            made.Add(c);
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made) Object.DestroyImmediate(o);
            made.Clear();
        }

        [Test]
        public void ShowcaseSet_ThreeSameElementSuperRarePlus_IsOneSet()
        {
            var cards = new List<CardData>
            {
                Card(CardElement.Fire, CardRarity.SuperRare), Card(CardElement.Fire, CardRarity.Unique), Card(CardElement.Fire, CardRarity.Legendary),
                Card(CardElement.Water, CardRarity.SuperRare), Card(CardElement.Water, CardRarity.Unique),
            };
            CollectionAssert.AreEqual(new[] { CardElement.Fire }, ShowcaseSetRules.Sets(cards));
        }

        [Test]
        public void ShowcaseSet_IgnoresLowRarityAndNonSlime()
        {
            var cards = new List<CardData>
            {
                Card(CardElement.Fire, CardRarity.SuperRare), Card(CardElement.Fire, CardRarity.Rare), Card(CardElement.Fire, CardRarity.Unique),
                Card(CardElement.None, CardRarity.Unique, CardKind.Snack), Card(CardElement.None, CardRarity.Unique, CardKind.Snack),
                Card(CardElement.None, CardRarity.Unique, CardKind.Snack),
            };
            Assert.AreEqual(0, ShowcaseSetRules.SetCount(cards));
        }

        [Test]
        public void ShowcaseSet_SixOfOneElementIsTwo_CappedAtMax()
        {
            var cards = new List<CardData>();
            for (int i = 0; i < 6; i++) cards.Add(Card(CardElement.Earth, CardRarity.SuperRare));
            for (int i = 0; i < 3; i++) cards.Add(Card(CardElement.Air, CardRarity.SuperRare));
            for (int i = 0; i < 3; i++) cards.Add(Card(CardElement.Neutral, CardRarity.SuperRare));
            List<CardElement> sets = ShowcaseSetRules.Sets(cards);
            Assert.AreEqual(ShowcaseSetRules.MaxSets, sets.Count);
            Assert.AreEqual(CardElement.Earth, sets[0], "많은 속성부터");
            Assert.AreEqual(CardElement.Earth, sets[1]);
        }
    }

    public class RoomScoreTests
    {
        Fixture f;
        UpgradeCatalog catalog;
        UpgradeRules rules;
        GameSession s;

        [SetUp]
        public void SetUp()
        {
            f = new Fixture();
            catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            rules = new UpgradeRules(catalog);
            s = new GameSession(1000);
        }

        [TearDown]
        public void TearDown()
        {
            f.Destroy();
            Object.DestroyImmediate(catalog);
        }

        void BuyAndOpen(UpgradeKind kind)
        {
            Assert.AreEqual(BuyResult.Ok, rules.TryBuy(s, kind, out _), kind.ToString());
            Assert.IsTrue(s.Upgrades.Open(kind));
        }

        [Test]
        public void RoomPoints_PerFurniturePiece()
        {
            var score = new ScoreRules(f.Score, f.Rarity, f.Db);
            int before = score.Calculate(s).Score;
            BuyAndOpen(UpgradeKind.Poster);
            BuyAndOpen(UpgradeKind.Bed);
            FinalResult r = score.Calculate(s);
            Assert.AreEqual(2, r.FurniturePieces);
            Assert.AreEqual(2 * f.Score.pointsPerFurniturePiece, r.RoomPoints);
            Assert.AreEqual(before + r.RoomPoints, r.Score, "가구 값 100%는 돈과 맞바꿈, 방 점수만큼 오름");
            StringAssert.Contains("방 꾸미기 2개", ResultTexts.Final(r, ""));
        }

        [Test]
        public void ShowcaseSet_AddsPoints()
        {
            // Fixture: F-SR · F-U는 불 슬라임. 불 3장이 필요하므로 F-SR 2장 + F-U
            s.Showcase.Add("F-SR");
            s.Showcase.Add("F-SR");
            s.Showcase.Add("F-U");
            FinalResult r = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(s);
            CollectionAssert.AreEqual(new[] { CardElement.Fire }, r.ShowcaseSets);
            Assert.AreEqual(f.Score.pointsPerShowcaseSet, r.SetPoints);
            StringAssert.Contains("진열장 세트 1개", ResultTexts.Final(r, ""));
        }

        [Test]
        public void AllDecor_GivesExtraFreePack()
        {
            Assert.AreEqual(0, rules.ExtraFreePacks(s.Upgrades));
            UpgradeKind[] decor = { UpgradeKind.Poster, UpgradeKind.Rug, UpgradeKind.Lamp, UpgradeKind.Figure };
            foreach (UpgradeKind k in decor) BuyAndOpen(k);
            Assert.IsFalse(rules.AllDecorOwned(s.Upgrades), "에어컨이 아직");
            Assert.AreEqual(0, rules.ExtraFreePacks(s.Upgrades));
            BuyAndOpen(UpgradeKind.AirCon);
            Assert.IsTrue(rules.AllDecorOwned(s.Upgrades));
            Assert.AreEqual(UpgradeRules.CozyRoomFreePacks, rules.ExtraFreePacks(s.Upgrades));
        }
    }
}

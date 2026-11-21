using System.Collections.Generic;
using CardShop.Customers;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>11-22 G1 (사용자 그림): 오른쪽 칸 상자 → 팩 → 카드(맨 위), 왼쪽 칸 피규어 앞뒤로</summary>
    public class BagLayoutTests
    {
        // 실제 계산대 물건 크기에 가까운 값 (상자 20 × 3.5 × 13cm, 아크릴 피규어 10 × 13cm)
        static readonly Vector3 Box = new Vector3(0.2f, 0.035f, 0.13f);
        static readonly Vector3 Pack = new Vector3(0.07f, 0.01f, 0.1f);
        static readonly Vector3 Card = new Vector3(0.063f, 0.002f, 0.088f);
        static readonly Vector3 Figure = new Vector3(0.1f, 0.13f, 0.04f);
        const float BagWidth = 0.34f, BagDepth = 0.19f;

        static List<Vector3> Place(params Vector3[] sizes) => BagLayout.Place(sizes, BagWidth, BagDepth);

        [Test]
        public void PackGoesOnTopOfBox_InAnyOrder()
        {
            List<Vector3> spots = Place(Pack, Box);
            Assert.AreEqual(Vector3.zero, spots[1], "피규어가 없으면 상자는 바닥 가운데");
            Assert.AreEqual(Box.y, spots[0].y, 1e-5f, "팩은 상자 윗면");
        }

        [Test]
        public void CardIsOnTop_BoxPackCard_InAnyOrder()
        {
            List<Vector3> spots = Place(Card, Figure, Pack, Box);
            Assert.AreEqual(0f, spots[3].y, 1e-5f, "상자가 맨 아래");
            Assert.AreEqual(Box.y, spots[2].y, 1e-5f, "팩은 상자 위");
            Assert.Greater(spots[0].y, spots[2].y + Pack.y - 1e-5f, "카드는 팩 위");
            Assert.AreEqual(spots[3].x, spots[0].x, 1e-5f, "상자 · 팩 · 카드는 한 줄");
        }

        [Test]
        public void FigureOnLeft_StackOnRight()
        {
            List<Vector3> spots = Place(Box, Figure);
            Assert.Greater(spots[0].x, 0f, "상자 줄은 오른쪽");
            Assert.Less(spots[1].x, 0f, "피규어는 왼쪽");
            Assert.AreEqual(0f, spots[1].y, 1e-5f, "피규어는 바닥");
            Assert.LessOrEqual(spots[1].x + Figure.x / 2f, spots[0].x - Box.x / 2f + 1e-5f, "상자와 겹치지 않음");
        }

        [Test]
        public void TwoFigures_StandFrontAndBack()
        {
            List<Vector3> spots = Place(Box, Figure, Figure);
            Assert.AreEqual(spots[1].x, spots[2].x, 1e-5f, "같은 왼쪽 칸");
            Assert.AreEqual(0f, spots[2].y, 1e-5f, "위로 쌓지 않음");
            Assert.Greater(Mathf.Abs(spots[1].z - spots[2].z), Figure.z - 1e-5f, "앞뒤로 나란히");
        }

        [Test]
        public void FigureGoesOnTop_WhenNoRoomBeside()
        {
            var big = new Vector3(0.3f, 0.08f, 0.15f);
            List<Vector3> spots = Place(big, Figure);
            Assert.AreEqual(big.y, spots[1].y, 1e-5f);
        }

        [Test]
        public void PacksStackOnEachOther_WithoutBox()
        {
            List<Vector3> spots = Place(Pack, Pack);
            Assert.AreEqual(0f, spots[0].y, 1e-5f);
            Assert.Greater(spots[1].y, Pack.y - 1e-5f);
        }
    }
}

using System.Collections.Generic;
using CardShop.Customers;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>11-20 C3: 봉투 안 자리 — 상자 바닥 · 팩은 상자 위 · 피규어는 옆(자리 없으면 위)</summary>
    public class BagLayoutTests
    {
        static readonly Vector3 Box = new Vector3(0.12f, 0.08f, 0.1f);
        static readonly Vector3 Pack = new Vector3(0.07f, 0.01f, 0.11f);
        static readonly Vector3 Figure = new Vector3(0.06f, 0.1f, 0.06f);
        const float BagWidth = 0.28f;

        [Test]
        public void PackGoesOnTopOfBox_InAnyOrder()
        {
            List<Vector3> spots = BagLayout.Place(new[] { Pack, Box }, BagWidth);
            Assert.AreEqual(Vector3.zero, spots[1], "상자는 바닥 가운데");
            Assert.AreEqual(Box.y, spots[0].y, 1e-5f, "팩은 상자 윗면");
        }

        [Test]
        public void FigureStandsBesideBox_OnFloor()
        {
            List<Vector3> spots = BagLayout.Place(new[] { Box, Figure }, BagWidth);
            Assert.AreEqual(0f, spots[1].y, 1e-5f);
            Assert.Greater(Mathf.Abs(spots[1].x), Box.x / 2f, "상자와 겹치지 않게 옆");
        }

        [Test]
        public void FigureGoesOnBox_WhenNoRoomBeside()
        {
            var big = new Vector3(0.27f, 0.08f, 0.15f);
            List<Vector3> spots = BagLayout.Place(new[] { big, Figure }, BagWidth);
            Assert.AreEqual(big.y, spots[1].y, 1e-5f);
        }

        [Test]
        public void TwoFiguresWithoutBox_StandSideBySide()
        {
            List<Vector3> spots = BagLayout.Place(new[] { Figure, Figure }, BagWidth);
            Assert.AreEqual(0f, spots[0].y, 1e-5f);
            Assert.AreEqual(0f, spots[1].y, 1e-5f);
            Assert.AreNotEqual(spots[0].x, spots[1].x);
        }

        [Test]
        public void PacksStackOnEachOther_WithoutBox()
        {
            List<Vector3> spots = BagLayout.Place(new[] { Pack, Pack }, BagWidth);
            Assert.AreEqual(0f, spots[0].y, 1e-5f);
            Assert.Greater(spots[1].y, Pack.y - 1e-5f);
        }
    }
}

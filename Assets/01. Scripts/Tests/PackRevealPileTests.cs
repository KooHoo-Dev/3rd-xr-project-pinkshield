using CardShop.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>시연 피드백 B1·B2: 카드 더미 자리 계산과 "보관함에 넣기" 입력 잠금.</summary>
    public class PackRevealPileTests
    {
        const float Thickness = 0.002f, MaxAngle = 7f, MaxOffset = 0.004f;

        static Pose Base => new Pose(new Vector3(0f, 0.9f, 0.5f), CardPileLayout.FlatRotation(new Vector3(0f, -0.4f, 0.9f)));

        [Test]
        public void FlatRotation_FaceUpAndTopAwayFromPlayer()
        {
            Quaternion r = CardPileLayout.FlatRotation(new Vector3(0f, -0.4f, 0.9f)); // 기준점처럼 기울어진 방향도 수평으로
            Vector3 front = r * Vector3.back; // 카드 앞면 = 로컬 -Z
            Vector3 top = r * Vector3.up;     // 카드 윗변
            Assert.That(Vector3.Dot(front, Vector3.up), Is.GreaterThan(0.999f), "앞면이 위");
            Assert.That(Vector3.Dot(top, Vector3.forward), Is.GreaterThan(0.999f), "윗변이 플레이어 반대쪽");
        }

        [Test]
        public void Pile_RisesByCardThickness()
        {
            for (int i = 0; i < 10; i++)
            {
                Pose p = CardPileLayout.GetPose(i, Base, Thickness, MaxAngle, MaxOffset);
                Assert.AreEqual(Base.position.y + i * Thickness, p.position.y, 1e-5f, $"{i}번째 높이");
            }
        }

        [Test]
        public void Pile_JitterStaysWithinLimitsAndIsRepeatable()
        {
            bool anyTilt = false;
            for (int i = 0; i < 20; i++)
            {
                Pose p = CardPileLayout.GetPose(i, Base, Thickness, MaxAngle, MaxOffset);
                float angle = Quaternion.Angle(Base.rotation, p.rotation);
                Assert.LessOrEqual(angle, MaxAngle + 0.01f, $"{i}번째 각도");
                if (angle > 0.5f) anyTilt = true;

                Vector3 flat = p.position - Base.position;
                flat.y = 0f;
                Assert.LessOrEqual(flat.magnitude, MaxOffset * Mathf.Sqrt(2f) + 1e-5f, $"{i}번째 어긋남");

                // 앞면은 계속 위 (수직축으로만 돌림)
                Assert.That(Vector3.Dot(p.rotation * Vector3.back, Vector3.up), Is.GreaterThan(0.999f));

                Pose again = CardPileLayout.GetPose(i, Base, Thickness, MaxAngle, MaxOffset);
                Assert.AreEqual(p.position, again.position, "같은 순번은 같은 자리");
            }
            Assert.IsTrue(anyTilt, "살짝 비뚤게 쌓여야 함");
        }

        [Test]
        public void Pile_NeighboursAreNotIdentical()
        {
            Pose a = CardPileLayout.GetPose(0, Base, Thickness, MaxAngle, MaxOffset);
            Pose b = CardPileLayout.GetPose(1, Base, Thickness, MaxAngle, MaxOffset);
            Assert.Greater(Quaternion.Angle(a.rotation, b.rotation), 0.5f, "이웃 카드끼리 각도가 달라야 더미처럼 보임");
        }

        [Test]
        public void InputGate_BlocksUntilLockEnds()
        {
            var gate = new RevealInputGate();
            Assert.IsTrue(gate.IsOpen(0f), "처음엔 열림");

            gate.Lock(10f, 0.4f); // 마지막 장을 넘긴 순간
            Assert.IsFalse(gate.IsOpen(10f), "같은 눌림은 무시");
            Assert.IsFalse(gate.IsOpen(10.39f));
            Assert.IsTrue(gate.IsOpen(10.41f));
        }

        [Test]
        public void InputGate_ShorterLockDoesNotShortenLongerOne()
        {
            var gate = new RevealInputGate();
            gate.Lock(0f, 0.4f);
            gate.Lock(0.1f, 0.1f); // 결과함 등장(짧음)이 앞선 잠금을 줄이면 안 됨
            Assert.IsFalse(gate.IsOpen(0.3f));
            Assert.IsTrue(gate.IsOpen(0.4f));

            gate.Reset();
            Assert.IsTrue(gate.IsOpen(0.1f), "초기화하면 바로 열림");
        }
    }
}

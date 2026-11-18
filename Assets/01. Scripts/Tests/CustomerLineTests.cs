using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>계산대 줄 칸 순서 (CustomerLine): 떠나는 손님의 칸 유지 · 맨 앞만 계산 · 줄 당기기.</summary>
    public class CustomerLineTests
    {
        sealed class Guest { }

        [Test]
        public void Join_TakesTailSlots_FrontIsFirst()
        {
            var line = new CustomerLine<Guest>();
            Guest a = new Guest(), b = new Guest();
            Assert.AreEqual(0, line.Join(a));
            Assert.AreEqual(1, line.Join(b));
            Assert.IsTrue(line.IsFront(a));
            Assert.IsFalse(line.IsFront(b), "맨 앞이 아니면 계산 시작 안 함");
        }

        // 결제한 손님이 인사하는 동안 다음 손님이 맨 앞으로 와서 겹쳐 서지 않게
        [Test]
        public void PaidCustomer_KeepsFrontSlotUntilLeaving()
        {
            var line = new CustomerLine<Guest>();
            Guest paid = new Guest(), next = new Guest();
            line.Join(paid);
            line.Join(next);

            Assert.AreEqual(1, line.SlotOf(next), "인사하는 동안은 그대로 1번 칸");
            Assert.AreEqual(0, line.Leave(paid), "맨 앞에서 떠남 → 카운터 쪽 길로 나감");
            Assert.AreEqual(0, line.SlotOf(next));
            Assert.IsTrue(line.IsFront(next));
        }

        [Test]
        public void AngryCustomerInMiddle_LeavesAndBackMovesUp()
        {
            var line = new CustomerLine<Guest>();
            Guest front = new Guest(), angry = new Guest(), back = new Guest();
            line.Join(front);
            line.Join(angry);
            line.Join(back);

            Assert.AreEqual(1, line.Leave(angry), "중간에서 떠남 → 줄 옆 길로 나감");
            Assert.IsTrue(line.IsFront(front), "맨 앞 손님은 그대로");
            Assert.AreEqual(1, line.SlotOf(back), "뒤 손님은 한 칸 당겨짐");
        }

        [Test]
        public void JoinWhileShifting_GoesToNewTail()
        {
            var line = new CustomerLine<Guest>();
            Guest a = new Guest(), b = new Guest(), c = new Guest();
            line.Join(a);
            line.Join(b);
            line.Leave(a);
            Assert.AreEqual(1, line.Join(c), "당겨진 뒤의 끝 칸");
        }

        [Test]
        public void Leave_UnknownCustomer_ReturnsMinusOne()
        {
            var line = new CustomerLine<Guest>();
            Assert.AreEqual(-1, line.Leave(new Guest()));
            Assert.AreEqual(-1, line.SlotOf(new Guest()));
        }
    }
}

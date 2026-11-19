using CardShop.Tutorial;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class ControllerHintTests
    {
        [Test]
        public void Parse_ReadsHandAndButton_DefaultsToBothHands()
        {
            ControllerHint a = ControllerHint.Parse("오른손 검지 트리거를 누른 채 흰 선을 따라 옆으로!");
            Assert.IsTrue(a.Right && !a.Left && a.Trigger && !a.Grip);

            ControllerHint b = ControllerHint.Parse("스캐너를 그립으로 꾹 잡아!");
            Assert.IsTrue(b.Left && b.Right && b.Grip && !b.Trigger, "손이 없으면 양손");

            Assert.IsFalse(ControllerHint.Parse("깜빡이는 곳부터 치워!").Any, "버튼 이름이 없으면 빛내지 않음");
            Assert.IsFalse(ControllerHint.Parse(null).Any);
        }
    }
}

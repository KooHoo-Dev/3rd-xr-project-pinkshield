using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>알림 쪽지: 경고는 빨강, 좋은 소식은 초록, 팩 개봉 · 손님 결제처럼 잦은 것은 띄우지 않음</summary>
    public class NoticeRulesTests
    {
        [Test]
        public void Warnings()
        {
            Assert.AreEqual(NoticeKind.Warning, NoticeRules.Classify("도둑이야! 오늘 무료 팩을 들고 도망가요."));
            Assert.AreEqual(NoticeKind.Warning, NoticeRules.Classify("먼저 바코드를 찍어 주세요."));
            Assert.AreEqual(NoticeKind.Warning, NoticeRules.Classify("바닥에 얼룩이 생겼어요. 카운터 오른쪽 대걸레로 문질러 주세요!"));
        }

        [Test]
        public void GoodNews()
        {
            Assert.AreEqual(NoticeKind.Good, NoticeRules.Classify("무료 팩을 되찾았어요!"));
            Assert.AreEqual(NoticeKind.Good, NoticeRules.Classify("손님이 판매 등록한 「불꽃」을(를) 샀어요!"));
        }

        [Test]
        public void FrequentOnesHidden()
        {
            Assert.AreEqual(NoticeKind.Hidden, NoticeRules.Classify("팩 개봉 (-5)"));
            Assert.AreEqual(NoticeKind.Hidden, NoticeRules.Classify("손님이 가게 재고 2개를 사 갔어요."));
            Assert.AreEqual(NoticeKind.Hidden, NoticeRules.Classify(""));
            // 11-20: 띄우는 목록에 없는 것은 모두 숨김 (자막과 겹치던 팩 넘기기 안내 등)
            Assert.AreEqual(NoticeKind.Hidden, NoticeRules.Classify("오른손 트리거로 한 장씩 넘기세요 · 1.5초 꾹: 건너뛰기"));
            Assert.AreEqual(NoticeKind.Hidden, NoticeRules.Classify("오늘은 한가하네요! 빨리 마감할게요."));
        }
    }
}

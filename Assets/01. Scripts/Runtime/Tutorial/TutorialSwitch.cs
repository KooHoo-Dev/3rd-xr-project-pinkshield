namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 켬/끔 (튜토리얼_설계 9장 Q3: 저장하지 않음). 앱을 켤 때마다 켜져 있고, 타이틀 판 버튼으로 끌 수 있습니다.
    /// 시연장에서는 한 헤드셋을 여러 사람이 쓰므로, 기억해 두면 다음 사람이 안내를 못 받기 때문입니다.
    /// 새 게임을 시작할 때의 값을 그 판 동안 씁니다.
    /// </summary>
    public static class TutorialSwitch
    {
        public static bool Enabled { get; set; } = true;
    }
}

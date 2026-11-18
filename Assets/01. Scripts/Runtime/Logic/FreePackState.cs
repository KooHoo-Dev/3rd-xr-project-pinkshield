namespace CardShop.Logic
{
    /// <summary>오늘 무료 팩이 어디 있나.</summary>
    public enum FreePackPlace
    {
        Home,     // 계산대 위 제자리
        Carried,  // 도둑 손에 (훔쳐서 달아나는 중)
        Dropped,  // 빗자루에 맞은 도둑이 떨어뜨려 바닥에 있음
        Stolen,   // 놓침: 도둑이 들고 인도 끝까지 감 (아직 도둑 손에)
        Gone,     // 놓친 팩을 치움 (오늘은 더 이상 없음)
    }

    /// <summary>
    /// 오늘 무료 팩의 상태 (순수 C#, FreePackStateTests, 카드도둑_설계 2장). 예전에는 플래그 세 개(carrying · packDropped · stolenToday)를
    /// 맞춰 가며 썼는데, 한 곳에서만 바뀌는 상태 하나로 모았습니다. 팩을 실제로 옮기는 일은 FreePackHolder, 흐름은 ThiefDirector.
    /// 반환값이 "팩을 어디로 옮겨야 하는지"를 알려 줍니다.
    /// </summary>
    public sealed class FreePackState
    {
        /// <summary>팩을 실제로 옮겨야 하는 곳.</summary>
        public enum Move { None, Home, HomeHidden }

        public FreePackPlace Place { get; private set; } = FreePackPlace.Home;

        /// <summary>제자리에 있음 = 도둑이 올 이유가 있음 (이미 잃었거나 바닥에 있으면 그날은 더 안 옴).</summary>
        public bool AtHome => Place == FreePackPlace.Home;

        /// <summary>도둑 손에 있음 (도둑이 사라질 때 팩을 먼저 떼어 내야 함).</summary>
        public bool OnThief => Place == FreePackPlace.Carried || Place == FreePackPlace.Stolen;

        /// <summary>영업 시작: 어제 일과 상관없이 제자리.</summary>
        public void StartDay() => Place = FreePackPlace.Home;

        /// <summary>도둑이 집음. 제자리에 있을 때만 true.</summary>
        public bool Steal()
        {
            if (Place != FreePackPlace.Home) return false;
            Place = FreePackPlace.Carried;
            return true;
        }

        /// <summary>도둑이 빗자루에 맞음. 팩을 들고 있었으면 바닥에 떨어뜨리고 true (훔치기 전에 막았으면 false).</summary>
        public bool Caught()
        {
            if (Place != FreePackPlace.Carried) return false;
            Place = FreePackPlace.Dropped;
            return true;
        }

        /// <summary>도둑이 팩을 들고 인도 끝에 도착 = 도난.</summary>
        public bool Escaped()
        {
            if (Place != FreePackPlace.Carried) return false;
            Place = FreePackPlace.Stolen;
            return true;
        }

        /// <summary>떨어진 팩을 플레이어가 주움 → 제자리로. 바닥에 있을 때만 true.</summary>
        public bool PickUp()
        {
            if (Place != FreePackPlace.Dropped) return false;
            Place = FreePackPlace.Home;
            return true;
        }

        /// <summary>도둑이 밖으로 나가 사라짐: 손에 있던 팩을 떼어 내 둘 곳. 놓친 팩은 제자리로 돌리되 숨김(오늘은 없음).</summary>
        public Move ThiefRemoved()
        {
            switch (Place)
            {
                case FreePackPlace.Stolen: Place = FreePackPlace.Gone; return Move.HomeHidden;
                case FreePackPlace.Carried: Place = FreePackPlace.Home; return Move.Home;
                default: return Move.None;
            }
        }

        /// <summary>
        /// 영업이 강제로 끝남(마감 · 처음부터 · 디버그): 도둑 손의 팩은 도난으로 치지 않고 제자리,
        /// 잡고 안 주운 팩은 되찾은 것으로 침. 이미 놓친 팩은 그대로 잃음.
        /// </summary>
        public Move ForceEnd()
        {
            switch (Place)
            {
                case FreePackPlace.Carried:
                case FreePackPlace.Dropped:
                    Place = FreePackPlace.Home;
                    return Move.Home;
                case FreePackPlace.Stolen:
                    Place = FreePackPlace.Gone;
                    return Move.Home;
                default:
                    return Move.None;
            }
        }
    }
}

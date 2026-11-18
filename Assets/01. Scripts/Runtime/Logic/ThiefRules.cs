using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 카드 도둑 규칙 (카드도둑_설계 3 · 4장). 화면과 상관없는 판단만 모아 EditMode 테스트로 고정합니다.
    /// 도둑을 실제로 걷게 하고 팩 · 빗자루를 다루는 일은 ThiefDirector가 합니다.
    /// </summary>
    public static class ThiefRules
    {
        /// <summary>오늘 나올 도둑 수. firstDay 전에는 0.</summary>
        public static int CountToday(int day, int firstDay, int perDay) => day >= firstDay ? Mathf.Max(0, perDay) : 0;

        /// <summary>
        /// 도둑 등장 시각(영업 시작부터 초, 오름차순). 영업 시간 × window(비율 최소, 최대) 안에서 무작위.
        /// window가 뒤집혀 있거나 0~1 밖이면 맞춰서 씁니다.
        /// </summary>
        public static float[] Schedule(int count, float businessSeconds, Vector2 window, IRandomSource random)
        {
            if (count <= 0 || businessSeconds <= 0f) return new float[0];
            float a = Mathf.Clamp01(Mathf.Min(window.x, window.y));
            float b = Mathf.Clamp01(Mathf.Max(window.x, window.y));
            var times = new float[count];
            for (int i = 0; i < count; i++)
                times[i] = businessSeconds * (a + (float)random.NextDouble() * (b - a));
            System.Array.Sort(times);
            return times;
        }

        /// <summary>
        /// 빗자루에 맞았는가: 빗자루 머리가 minSpeed(m/s) 이상으로 움직이고, 도둑 몸(발 위치에서 키 높이까지, 반지름 radius인 기둥) 안에 있음.
        /// 톡 대기만 한 것은 치지 않습니다.
        /// </summary>
        public static bool IsHit(Vector3 broomHead, float headSpeed, Vector3 thiefFeet, float thiefHeight, float radius, float minSpeed)
        {
            if (headSpeed < minSpeed) return false;
            if (broomHead.y < thiefFeet.y - 0.05f || broomHead.y > thiefFeet.y + thiefHeight + 0.1f) return false;
            Vector2 d = new Vector2(broomHead.x - thiefFeet.x, broomHead.z - thiefFeet.z);
            return d.sqrMagnitude <= radius * radius;
        }

        /// <summary>퇴근 후 무료 팩 수: 도둑에게 무료 팩을 뺏긴 날은 0.</summary>
        public static int FreePacksToday(int perDay, bool stolen) => stolen ? 0 : Mathf.Max(0, perDay);

        /// <summary>
        /// 도둑이 도망치는 인도 끝 지점: 문 바깥 지점에서 인도를 따라 왼쪽(-X) 또는 오른쪽(+X)으로 distance만큼.
        /// z는 인도 가운데(sidewalkZ).
        /// </summary>
        public static Vector3 EscapePoint(Vector3 doorOutside, float sidewalkZ, float distance, bool left) =>
            new Vector3(doorOutside.x + (left ? -distance : distance), 0f, sidewalkZ);

        /// <summary>밖 → 훔칠 자리. 손님 입장 통로(문 → 꺾는 곳 → 통로 아래 끝)를 그대로 따름.</summary>
        public static Vector3[] ApproachPath(CustomerRoutes routes, Vector3 stealSpot) => new[]
        {
            routes.DoorOutside, routes.Door, routes.EntryTurn, routes.AisleLow, stealSpot,
        };

        /// <summary>훔칠 자리 → 인도 끝. 입장 통로를 거꾸로 달려 문을 나간 뒤 인도 가운데로 나와 옆으로.</summary>
        public static Vector3[] FleePath(CustomerRoutes routes, Vector3 escape) => new[]
        {
            routes.AisleLow, routes.EntryTurn, routes.Door, routes.DoorOutside,
            new Vector3(routes.DoorOutside.x, 0f, escape.z), escape,
        };

        /// <summary>
        /// 잡힌 뒤 빠져나가는 길. 가게 안 통로 왼쪽(진열대 · 카운터 쪽)이면 통로를 따라 문으로, 앞벽 쪽이면 곧장 문으로,
        /// 가게 밖이면 인도 가운데로 나와 farEscape(인도 끝 너머)로.
        /// </summary>
        public static Vector3[] RunOffPath(CustomerRoutes routes, Vector3 from, bool insideShop, Vector3 farEscape)
        {
            Vector3 street = new Vector3(routes.DoorOutside.x, 0f, farEscape.z);
            if (!insideShop) return new[] { new Vector3(from.x, 0f, farEscape.z), farEscape };
            if (from.z < routes.EntryTurn.z - 0.3f && from.x < routes.EntryTurn.x + 0.3f)
                return new[] { new Vector3(routes.AisleLow.x, 0f, from.z), routes.EntryTurn, routes.Door, routes.DoorOutside, street, farEscape };
            return new[] { routes.Door, routes.DoorOutside, street, farEscape };
        }
    }
}

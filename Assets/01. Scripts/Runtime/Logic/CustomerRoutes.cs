using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 손님이 걷는 길(웨이포인트 목록)을 만듭니다 (2026-11-18 기기 테스트 피드백: 손님이 줄을 서지 않고 겹쳐 섬).
    /// 손님은 점 사이를 직선으로 걸으므로, 길이 줄 선 손님 · 진열대 · 카운터를 가로지르지 않게 통로를 정해 둡니다.
    ///
    ///   입장: 밖 → 문 바깥 → 문 안 → (앞벽을 따라) 꺾는 곳 → (진열대 오른쪽 통로로) 통로 아래 끝 → 구경 자리 앞 → 구경 자리
    ///   줄 합류: 구경 자리 앞 → 통로 아래 끝 → 통로에서 줄 끝 뒤쪽 → 줄 끝 바로 뒤 → 줄 끝 (뒤에서 다가가 섬)
    ///   결제 후: 맨 앞 → 카운터 앞을 따라 오른쪽 → 문 안 → 문 바깥 → 밖
    ///   줄 중간에서 떠남: 줄 옆(통로 쪽 또는 뒤쪽)으로 비켜서 → 꺾는 곳 높이 → 문 안 → …
    ///
    /// 순수 계산이라 ShopLayoutTests가 같은 함수로 "길이 벽 · 줄 · 진열대를 지나지 않는지"를 확인합니다.
    /// 점은 씬의 Transform 위치를 넘기므로 씬에서 지점을 옮겨도 그대로 따라갑니다.
    /// </summary>
    public readonly struct CustomerRoutes
    {
        /// <summary>줄 끝에 합류할 때 줄 끝에서 이만큼 뒤(+Z)에서 다가감 (m)</summary>
        public const float ApproachBack = 0.5f;
        /// <summary>줄 중간에서 떠날 때 "곧은 줄(맨 앞과 같은 x)"로 보는 x 차이 (m)</summary>
        const float StraightLineTolerance = 0.3f;

        public readonly Vector3 Outside, DoorOutside, Door, EntryTurn, AisleLow, CounterExit;

        public CustomerRoutes(Vector3 outside, Vector3 doorOutside, Vector3 door, Vector3 entryTurn, Vector3 aisleLow, Vector3 counterExit)
        {
            Outside = outside;
            DoorOutside = doorOutside;
            Door = door;
            EntryTurn = entryTurn;
            AisleLow = aisleLow;
            CounterExit = counterExit;
        }

        float AisleX => AisleLow.x;

        static Vector3 At(float x, float z) => new Vector3(x, 0f, z);

        /// <summary>밖 → 구경 자리</summary>
        public Vector3[] EnterToBrowse(Vector3 browse) => new[]
        {
            DoorOutside, Door, EntryTurn, AisleLow, At(browse.x, AisleLow.z), browse,
        };

        /// <summary>구경 자리가 다 찼을 때: 밖 → 바로 줄 끝</summary>
        public Vector3[] EnterToQueue(Vector3 tail) => new[]
        {
            DoorOutside, Door, EntryTurn, At(AisleX, tail.z + ApproachBack), At(tail.x, tail.z + ApproachBack), tail,
        };

        /// <summary>구경 자리 → 줄 끝 (통로로 나와서 줄 끝 뒤에서 다가감)</summary>
        public Vector3[] BrowseToQueue(Vector3 browse, Vector3 tail) => new[]
        {
            At(browse.x, AisleLow.z), AisleLow, At(AisleX, tail.z + ApproachBack), At(tail.x, tail.z + ApproachBack), tail,
        };

        /// <summary>결제 자리(맨 앞) → 밖. 줄을 가로지르지 않게 카운터 앞을 따라 오른쪽으로 빠짐</summary>
        public Vector3[] LeaveFromCounter() => new[] { CounterExit, Door, DoorOutside, Outside };

        /// <summary>
        /// 줄 중간(또는 줄로 걸어오던 중)에서 떠남. 곧은 줄(맨 앞과 같은 x)에 있으면 통로 쪽으로, 꺾인 줄에 있으면 뒤로 비켜서 나감.
        /// </summary>
        public Vector3[] LeaveFromQueue(Vector3 from, Vector3 front)
        {
            Vector3 aside = Mathf.Abs(from.x - front.x) < StraightLineTolerance ? At(AisleX, from.z) : At(from.x, EntryTurn.z);
            return new[] { aside, At(aside.x, EntryTurn.z), Door, DoorOutside, Outside };
        }

        /// <summary>
        /// 줄 칸 위치. 준비된 칸보다 줄이 길면 마지막 두 칸의 간격대로 이어서 섭니다 (칸이 하나면 그 칸 뒤로 0.7m씩).
        /// </summary>
        public static Vector3 SlotPosition(Vector3[] slots, Vector3 lastForward, int index)
        {
            int last = slots.Length - 1;
            if (index <= last) return slots[index];
            Vector3 step = last > 0 ? slots[last] - slots[last - 1] : -lastForward * 0.7f;
            return slots[last] + step * (index - last);
        }
    }
}

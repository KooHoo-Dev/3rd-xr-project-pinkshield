using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// 가게 · 집 공간의 기준 좌표 (가게_집_인테리어_설계 2 · 3장). 씬 빌더와 배치 테스트가 같은 값을 씁니다.
    /// 플레이어는 원점에서 +Z(거리 쪽)를 봅니다. 카운터 x -1.11~1.11 · z 0.385~1.015, 윗면 0.9.
    /// 인테리어 프리팹을 손으로 고칠 때도 이 값(벽 · 출입구 · 손님 지점)은 지켜야 손님 동선이 벽을 뚫지 않습니다.
    /// </summary>
    public static class ShopLayout
    {
        // ---------------------------------------------------------------- 가게 건물 (D1: 껍데기만 1.2배)
        /// <summary>LowPolyInterior 벽 · 바닥 · 천장 모듈에 곱하는 배율. 가구는 1배.</summary>
        public const float ShellScale = 1.2f;
        /// <summary>벽 한 칸 폭 (2.5m × 1.2)</summary>
        public const float Module = 2.5f * ShellScale;
        public const float CeilingY = 2.5f * ShellScale;

        // 벽 중심선. 가로 3칸(9m) × 세로 2칸(6m)
        public const float MinX = -4.5f;
        public const float MaxX = 4.5f;
        public const float BackZ = -1.8f;   // 플레이어 뒤 (카운터 뒤 벽장)
        public const float FrontZ = 4.2f;   // 거리 쪽 (파노라마 창 + 출입구)
        /// <summary>벽 두께의 절반 (WallFloor2 계열 0.23m × 1.2 ÷ 2, 여유 포함)</summary>
        public const float WallHalf = 0.15f;

        /// <summary>출입구 가운데 x (앞벽 오른쪽 칸 가운데)</summary>
        public const float DoorX = MaxX - Module / 2f;
        /// <summary>출입구 구멍 폭의 절반 (실측 약 1.0m × 1.2, 손님이 지나갈 여유를 빼고 보수적으로)</summary>
        public const float DoorHalfWidth = 0.5f;

        // ---------------------------------------------------------------- 손님 동선
        /// <summary>등장 · 퇴장 지점: 출입구 오른쪽 벽 뒤, 계산대에서 보이지 않는 곳</summary>
        public static readonly Vector3 OutsidePos = new Vector3(5.6f, 0f, 5.6f);
        /// <summary>출입구 바로 바깥 인도</summary>
        public static readonly Vector3 DoorOutsidePos = new Vector3(DoorX, 0f, 5.0f);
        /// <summary>출입구 바로 안</summary>
        public static readonly Vector3 DoorInsidePos = new Vector3(DoorX, 0f, 3.5f);
        /// <summary>
        /// 판매 진열대 구매 자리 2칸 (11-19 손님 동선): 진열대 뒤(창 쪽)에서 플레이어 쪽을 보고 구경 → 손님 얼굴이 보임.
        /// 예전에는 진열대 앞 3칸(z 1.75)이라 손님이 등을 보였음.
        /// </summary>
        public static readonly Vector3[] BrowsePositions = { new Vector3(-2.3f, 0f, 3.08f), new Vector3(-1.5f, 0f, 3.08f) };
        /// <summary>구매 자리 뒤를 지나는 길 (z). 구매 자리에서 0.47m 떨어져 서 있는 손님을 스치지 않음</summary>
        public const float BrowseLaneZ = 3.55f;
        /// <summary>
        /// 구매 자리가 빌 때까지 기다리는 줄: 구매 자리 2 오른쪽에서 창을 따라 오른쪽(문 쪽)으로 섬. 0번이 맨 앞.
        /// 계산대 줄 뒤를 지나는 길(QueueBackLaneZ)과 0.46m 떨어지게 창 가까이 둠. 더 길면 마지막 두 칸 간격대로 이어짐.
        /// </summary>
        public static readonly Vector3[] BrowseWaitPositions =
        {
            new Vector3(-0.8f, 0f, 3.72f), new Vector3(-0.1f, 0f, 3.72f), new Vector3(0.6f, 0f, 3.72f), new Vector3(1.3f, 0f, 3.72f),
        };
        /// <summary>다 본 손님이 진열대 왼쪽 끝을 돌아 나오는 길 (x). 진열대 왼쪽 끝 x -2.67에서 0.6m 바깥</summary>
        public const float StandLeftX = -3.3f;
        /// <summary>
        /// 계산대 줄. 0번이 맨 앞(결제 자리). 0.7m 간격으로 뒤(+Z)로 3칸 선 뒤 오른쪽(+X)으로 꺾음 —
        /// 앞벽 쪽 입장 통로(EntryTurnPos의 z)와 진열대 쪽 통로(AisleX)를 막지 않게 (기기 테스트 피드백: 손님이 겹쳐 섬).
        /// 이보다 길면 마지막 두 칸의 간격대로 이어집니다.
        /// </summary>
        public static readonly Vector3[] QueuePositions =
        {
            new Vector3(0f, 0f, 1.4f), new Vector3(0f, 0f, 2.1f), new Vector3(0f, 0f, 2.8f),
            new Vector3(0.7f, 0f, 2.8f), new Vector3(1.4f, 0f, 2.8f),
        };
        /// <summary>계산대 줄 뒤를 지나는 길 (z): 줄 끝에 합류할 때 다가오는 길이자, 줄 중간에서 떠날 때 문으로 가는 길. 줄 맨 뒤 칸(z 2.8)에서 0.46m</summary>
        public const float QueueBackLaneZ = 2.8f + 0.46f;
        /// <summary>
        /// 입장 통로가 꺾이는 곳 (지금은 카드 도둑 길에만 씀: 문 → 계산대 줄 뒤 길 → 진열대 오른쪽 통로).
        /// 11-19 밤: 창가 구매 대기 줄(z 3.72)을 스치지 않게 z 3.6 → 줄 뒤 길(QueueBackLaneZ)로 내림
        /// </summary>
        public static readonly Vector3 EntryTurnPos = new Vector3(-0.65f, 0f, QueueBackLaneZ);
        /// <summary>진열대 오른쪽 통로의 아래 끝 (카운터 앞 · 구경 자리 앞을 지나는 낮은 통로가 시작되는 곳)</summary>
        public static readonly Vector3 AisleLowPos = new Vector3(-0.65f, 0f, 1.3f);
        /// <summary>결제를 마친 손님이 줄을 가로지르지 않고 카운터 앞을 따라 오른쪽으로 빠지는 곳</summary>
        public static readonly Vector3 CounterExitPos = new Vector3(2.2f, 0f, 1.4f);
        /// <summary>앞벽 파노라마 창 구간 (x 범위). 창으로는 바깥이 보임</summary>
        public const float PanoramaMinX = MinX;
        public const float PanoramaMaxX = MinX + 2f * Module;

        // ---------------------------------------------------------------- 가구 (게임 기능이 붙는 것)
        public const float CounterTop = 0.9f;
        public const float TableTop = 0.8f;
        public static readonly Vector3 CounterCenter = new Vector3(0f, 0f, 0.7f);
        public static readonly Vector3 CounterSize = new Vector3(2.22f, CounterTop, 0.63f);
        /// <summary>팩 테이블 (D8: 카운터 2.22m와 겹치지 않게 x -1.356 → -1.5)</summary>
        public static readonly Vector3 PackTablePos = new Vector3(-1.5f, 0f, 0.65f);
        public static readonly Vector3 PackTableSize = new Vector3(0.6f, TableTop, 0.5f);
        /// <summary>판매 진열대 (손님이 구경하는 곳)</summary>
        public static readonly Vector3 StandPos = new Vector3(-1.9f, 0f, 2.4f);
        public static readonly Vector3 StandSize = new Vector3(1.54f, 0.77f, 0.52f);

        // ---------------------------------------------------------------- 청소 (기획서 5-1, 인테리어 설계 S5. 창문 얼룩 S6은 DEC-014로 제외)
        /// <summary>쓰레기통 바닥 가운데 (카운터 오른쪽 옆, 플레이어 자리에서 한 걸음)</summary>
        public static readonly Vector3 TrashBinPos = new Vector3(1.6f, 0f, 0.55f);
        /// <summary>판매 진열대 위 팩 꽂이 칸 수 · 칸 간격 (흐트러진 팩 청소)</summary>
        public const int PackRackSlots = 4;
        public const float PackRackSpacing = 0.3f;

        // ---------------------------------------------------------------- 카드 도둑 (카드도둑_설계 5장)
        /// <summary>오늘 무료 팩 받침 바닥 가운데: 계산대 윗면 손님 쪽 왼쪽 (상자 열기 · 퇴근 버튼 x -0.98 · -0.8, z 0.5보다 손님 쪽)</summary>
        public static readonly Vector3 FreePackPos = new Vector3(-0.9f, CounterTop, 0.9f);
        /// <summary>도둑이 무료 팩을 집으려고 서는 자리 (계산대 앞, 진열대 통로 왼쪽)</summary>
        public static readonly Vector3 ThiefStealPos = new Vector3(-0.9f, 0f, 1.4f);
        /// <summary>빗자루 세워 두는 자리: 카운터 오른쪽 뒤 (문 쪽으로 나가며 집기 좋게, 쓰레기통 앞)</summary>
        public static readonly Vector3 BroomPos = new Vector3(1.35f, 0f, 0.12f);
        /// <summary>가게 앞 인도 가운데 z (도둑이 달리는 줄)</summary>
        public const float SidewalkMidZ = FrontZ + 2.2f;

        // ---------------------------------------------------------------- 타이틀 · 정산 판 (타이틀연출_손님_설계 0-2 · 0-3)
        /// <summary>타이틀에서 플레이어가 서는 곳: 가게 앞 인도, 파노라마 창과 출입구가 함께 보이는 자리. 가게(−Z)를 봄</summary>
        public static readonly Vector3 TitleSpawnPos = new Vector3(0.5f, 0f, FrontZ + 3.6f);
        public const float TitleSpawnYaw = 180f;
        /// <summary>타이틀 판 가운데 (플레이어 앞 1.4m, 눈높이보다 조금 아래). 판 앞면이 플레이어를 봄</summary>
        public static readonly Vector3 TitleBoardPos = new Vector3(0.5f, 1.45f, FrontZ + 2.2f);
        /// <summary>정산 · 최종 판 가운데: 카운터 위 손님 쪽 (플레이어 자리 원점에서 1.3m). 아래 끝이 카운터 윗면보다 위</summary>
        public static readonly Vector3 ResultBoardPos = new Vector3(0f, 1.5f, 1.3f);

        // ---------------------------------------------------------------- 집 (가게에서 30m 떨어진 같은 씬 구역)
        /// <remarks>
        /// 예전 (30, 0, 0)은 가게 앞 거리 끝(x ±27.5) 바로 옆이라 인도에서 집 상자가 보였음 (2026-11-18 기기 테스트 피드백 M1).
        /// 가게 뒤쪽 60m로 옮겨 가게 건물 뒤에 가리고, ZoneVisibility가 페이즈별로 집 · 거리를 켜고 끕니다.
        /// </remarks>
        public static readonly Vector3 HomeOrigin = new Vector3(0f, 0f, -60f);
        // 집 로컬 좌표의 벽 중심선 (2.5m 모듈 2 × 2). 책상(z 1.6)이 뒷벽에 붙도록 뒷벽을 z 2.0에 둠.
        public const float HomeMinX = -2.5f;
        public const float HomeMaxX = 2.5f;
        public const float HomeBackZ = 2.0f;
        public const float HomeFrontZ = -3.0f;
        public const float HomeCeilingY = 2.5f;
        public const float HomeWallHalf = 0.12f;
        /// <summary>집 잠자기 스위치 (앞벽 안쪽 면, 침대와 현관 사이, 높이 1.2m) — H1 "불 끄고 잠자기"</summary>
        public static readonly Vector3 HomeSleepSwitch = new Vector3(0.55f, 1.2f, HomeFrontZ + HomeWallHalf);

        // ---------------------------------------------------------------- 걸어 다닐 수 있는 곳 (PlayAreaGuard)
        /// <summary>가게 앞 인도에서 걸을 수 있는 끝 z (연석 · 가로등 줄 바로 앞. 그 너머는 도로라 바닥 콜라이더가 없음)</summary>
        public const float SidewalkMaxZ = FrontZ + 4.4f;
        /// <summary>가게 앞 인도에서 걸을 수 있는 x 범위의 절반 (인도 타일은 ±27.5까지)</summary>
        public const float SidewalkHalfX = 25f;

        /// <summary>
        /// 점이 플레이어가 걸어 다녀도 되는 곳인가: 가게 안 · 출입구 · 가게 앞 인도 · 집 안.
        /// 도로 · 골목 · 맞은편 인도는 바닥 콜라이더가 없거나 업무와 상관없는 곳이라 막습니다.
        /// </summary>
        public static bool InsideWalkArea(Vector3 p)
        {
            if (InsideShop(p)) return true;
            bool door = Mathf.Abs(p.x - DoorX) < DoorHalfWidth && p.z > FrontZ - 0.5f && p.z < FrontZ + 0.5f;
            if (door) return true;
            bool sidewalk = Mathf.Abs(p.x) < SidewalkHalfX && p.z > FrontZ + WallHalf && p.z < SidewalkMaxZ;
            if (sidewalk) return true;
            Vector3 h = p - HomeOrigin;
            return h.x > HomeMinX && h.x < HomeMaxX && h.z > HomeFrontZ && h.z < HomeBackZ;
        }

        /// <summary>점이 가게 벽 안쪽(벽 두께 제외)에 있는가</summary>
        public static bool InsideShop(Vector3 p, float margin = 0f) =>
            p.x > MinX + WallHalf + margin && p.x < MaxX - WallHalf - margin &&
            p.z > BackZ + WallHalf + margin && p.z < FrontZ - WallHalf - margin;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>다른 손님 한 명 (위치 · 지금 걷는 속도, 수평면)</summary>
    public readonly struct SteeringNeighbor
    {
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;

        public SteeringNeighbor(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// 손님끼리 걸을 때 겹치지 않게 하는 간단한 피하기 (순수 계산, 3차 기기 피드백: 손님 이동이 서로 겹쳐 어색).
    /// 웨이포인트로 곧장 가되,
    /// - 앞에 있는 사람은 옆으로 비켜 지나감 (정면이면 둘 다 자기 오른쪽으로 → 마주 오는 둘이 엇갈림)
    /// - 같은 쪽으로 걷는 사람 바로 뒤면 속도를 줄여 따라감 (밀고 들어가지 않음)
    /// - 도착 직전(arriveRadius 안)에는 피하지 않음: 줄 · 구경 칸이 붙어 있어도 제자리에 섬
    /// 뒤에 있는 사람은 신경 쓰지 않습니다(그쪽이 피함).
    /// </summary>
    public static class CustomerSteering
    {
        /// <summary>
        /// 이번 프레임에 걸을 속도(수평). 크기는 speed 이하.
        /// </summary>
        /// <param name="radius">한 사람이 차지하는 반지름 (m). 이 값의 3배 안의 사람만 봄</param>
        /// <param name="arriveRadius">목표까지 이보다 가까우면 피하지 않고 곧장 감</param>
        public static Vector3 Velocity(Vector3 position, Vector3 target, float speed, IReadOnlyList<SteeringNeighbor> others,
            float radius = 0.35f, float arriveRadius = 0.45f)
        {
            Vector3 toTarget = Flat(target - position);
            float targetDist = toTarget.magnitude;
            if (targetDist < 1e-4f) return Vector3.zero;
            Vector3 dir = toTarget / targetDist;
            if (targetDist < arriveRadius || others == null || others.Count == 0) return dir * speed;

            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            float lookAhead = radius * 3f;
            Vector3 steer = dir;
            float slow = 1f;

            for (int i = 0; i < others.Count; i++) // foreach는 IReadOnlyList 열거자를 박싱해 손님마다 매 프레임 할당이 생김
            {
                SteeringNeighbor o = others[i];
                Vector3 d = Flat(o.Position - position);
                float dist = d.magnitude;
                if (dist < 1e-4f || dist > lookAhead) continue;
                Vector3 dn = d / dist;
                float ahead = Vector3.Dot(dir, dn);
                if (ahead <= 0f) continue; // 뒤 · 옆 뒤는 무시

                // 상대가 내 목표보다 멀리 있으면 길을 막지 않음
                if (dist > targetDist + radius) continue;

                // 옆으로 비킴: 상대가 오른쪽에 있으면 왼쪽으로, 아니면(정면 포함) 오른쪽으로
                float side = Vector3.Dot(right, dn);
                Vector3 away = side > 0.05f ? -right : right;
                float strength = (1f - dist / lookAhead) * ahead;
                steer += away * (strength * 1.5f);

                // 같은 쪽으로 걷는 사람 바로 뒤: 따라감
                Vector3 ov = Flat(o.Velocity);
                if (ov.sqrMagnitude > 0.01f && Vector3.Dot(ov.normalized, dir) > 0.5f && ahead > 0.8f)
                    slow = Mathf.Min(slow, Mathf.Clamp01((dist - radius * 1.2f) / radius));
            }

            if (steer.sqrMagnitude < 1e-6f) steer = right;
            return steer.normalized * (speed * slow);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}

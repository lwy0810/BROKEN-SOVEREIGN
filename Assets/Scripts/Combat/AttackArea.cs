using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 범위 판정 공용 유틸리티 (순수 클래스). 기술·적 공격이 공유합니다.
    /// 거리·각도는 수평(XZ) 기준입니다.
    /// </summary>
    public static class AttackArea
    {
        private static readonly Collider[] _buffer = new Collider[32];

        /// <summary>
        /// 부채꼴 안에서 살아 있는 가장 가까운 대상을 찾습니다.
        /// </summary>
        public static bool TryFindClosestInCone(Vector3 origin, Vector3 forward, float radius, float angle,
            LayerMask mask, out IDamageable target, out Vector3 targetPosition)
        {
            target = null;
            targetPosition = origin;
            float closestSqr = float.MaxValue;

            int count = Physics.OverlapSphereNonAlloc(origin, radius, _buffer, mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                IDamageable candidate = _buffer[i].GetComponentInParent<IDamageable>();

                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                Vector3 position = _buffer[i].transform.position;

                if (!IsInCone(origin, forward, radius, angle, position))
                {
                    continue;
                }

                float sqr = FlatOffset(origin, position).sqrMagnitude;

                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    target = candidate;
                    targetPosition = position;
                }
            }

            return target != null;
        }

        public static bool IsInCone(Vector3 origin, Vector3 forward, float radius, float angle, Vector3 point)
        {
            Vector3 offset = FlatOffset(origin, point);

            if (offset.sqrMagnitude > radius * radius)
            {
                return false;
            }

            // 거의 겹친 대상은 방향과 관계없이 포함
            if (offset.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            forward.y = 0f;
            return Vector3.Angle(forward, offset) <= angle * 0.5f;
        }

        private static Vector3 FlatOffset(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset;
        }
    }
}

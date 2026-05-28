using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 스프레더(Spreader) Y축 호이스트 — 케이블을 감거나 풀어 상하 이동.
    /// X/Z는 TrolleyMover가 동기화하므로 여기서는 건드리지 않는다.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Spreader Hoist")]
    [DisallowMultipleComponent]
    public sealed class SpreaderHoist : MonoBehaviour, IAxisMover
    {
        [Header("승강 범위 (로컬 Y, 미터)")]
        [SerializeField] float min = 0.05f;
        [SerializeField] float max = 4f;

        // 컨테이너 적재 시 하강 한계를 올리는 양 — 스프레더가 아니라 '컨테이너 밑면'이 바닥에 닿게.
        // SpreaderGrabber가 잡을 때 설정, 놓을 때 0으로. 0이면 빈 스프레더(기존 동작).
        float floorOffset = 0f;
        public void SetFloorOffset(float v) => floorOffset = Mathf.Max(0f, v);

        public float Min => min;
        public float Max => max;
        public float Current => transform.localPosition.y;

        public void MoveTo(float value)
        {
            float clamped = Mathf.Clamp(value, min + floorOffset, max);
            var p = transform.localPosition;
            p.y = clamped;
            transform.localPosition = p;
        }

        public void MoveToNormalized(float t01)
        {
            MoveTo(Mathf.Lerp(min, max, Mathf.Clamp01(t01)));
        }

        public void Configure(float minY, float maxY)
        {
            min = minY;
            max = maxY;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform parent = transform.parent != null ? transform.parent : transform;
            Vector3 a = parent.TransformPoint(new Vector3(transform.localPosition.x, min, transform.localPosition.z));
            Vector3 b = parent.TransformPoint(new Vector3(transform.localPosition.x, max, transform.localPosition.z));
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.04f);
            Gizmos.DrawWireSphere(b, 0.04f);
        }
#endif
    }
}

using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 갠트리 주행 — 크레인 루트를 안벽 방향(Z축)으로 슬라이딩.
    /// 트롤리(X)·호이스트(Y)와 동일하게 IAxisMover로 추상화 → 상위(VR/Operator)는 어떤 축인지 모르고 일관되게 호출.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Gantry Mover")]
    [DisallowMultipleComponent]
    public sealed class GantryMover : MonoBehaviour, IAxisMover
    {
        [Header("주행 범위 (로컬 Z, 미터)")]
        [SerializeField] float min = -1f;
        [SerializeField] float max =  1f;

        public float Min => min;
        public float Max => max;
        public float Current => transform.localPosition.z;

        public void MoveTo(float value)
        {
            float clamped = Mathf.Clamp(value, min, max);
            var p = transform.localPosition;
            p.z = clamped;
            transform.localPosition = p;
        }

        public void MoveToNormalized(float t01) => MoveTo(Mathf.Lerp(min, max, Mathf.Clamp01(t01)));

        /// <summary>Builder가 한 번에 셋업할 때 사용. min/max는 절대 로컬 Z(생성 시 초기 위치 기준 ±range로 줄 것).</summary>
        public void Configure(float minZ, float maxZ)
        {
            min = minZ;
            max = maxZ;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform parent = transform.parent != null ? transform.parent : transform;
            Vector3 a = parent.TransformPoint(new Vector3(transform.localPosition.x, transform.localPosition.y, min));
            Vector3 b = parent.TransformPoint(new Vector3(transform.localPosition.x, transform.localPosition.y, max));
            Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.04f);
            Gizmos.DrawWireSphere(b, 0.04f);
        }
#endif
    }
}

using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 트롤리(Trolley)를 붐 위 레일을 따라 X축으로 슬라이딩.
    /// 스프레더는 트롤리의 X를 따라가야 하므로 호이스트 참조를 받아 동기화한다.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Trolley Mover")]
    [DisallowMultipleComponent]
    public sealed class TrolleyMover : MonoBehaviour, IAxisMover
    {
        [Header("레일 범위 (로컬 X, 미터)")]
        [SerializeField] float min = -5f;
        [SerializeField] float max =  15f;

        [Tooltip("스프레더 케이블/헤드블록 Transform — 트롤리 X를 따라간다.")]
        [SerializeField] Transform spreaderRoot;

        public float Min => min;
        public float Max => max;
        public float Current => transform.localPosition.x;

        public void MoveTo(float value)
        {
            float clamped = Mathf.Clamp(value, min, max);
            var p = transform.localPosition;
            p.x = clamped;
            transform.localPosition = p;

            if (spreaderRoot != null)
            {
                var sp = spreaderRoot.localPosition;
                sp.x = clamped;
                spreaderRoot.localPosition = sp;
            }
        }

        public void MoveToNormalized(float t01)
        {
            MoveTo(Mathf.Lerp(min, max, Mathf.Clamp01(t01)));
        }

        /// <summary>Builder가 한 번에 셋업할 때 사용.</summary>
        public void Configure(float minX, float maxX, Transform spreader)
        {
            min = minX;
            max = maxX;
            spreaderRoot = spreader;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Transform parent = transform.parent != null ? transform.parent : transform;
            Vector3 a = parent.TransformPoint(new Vector3(min, transform.localPosition.y, transform.localPosition.z));
            Vector3 b = parent.TransformPoint(new Vector3(max, transform.localPosition.y, transform.localPosition.z));
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.04f);
            Gizmos.DrawWireSphere(b, 0.04f);
        }
#endif
    }
}

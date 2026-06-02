using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 트롤리(Trolley)를 붐 위 레일을 따라 X축으로 슬라이딩.
    /// 스프레더는 트롤리의 X를 따라가야 하므로 호이스트 참조를 받아 동기화한다.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Trolley Mover")]
    [DisallowMultipleComponent]
    public sealed class TrolleyMover : AxisMoverBase
    {
        [Header("레일 범위 (로컬 X, 미터)")]
        [SerializeField] float min = -5f;
        [SerializeField] float max =  15f;

        [Tooltip("스프레더 케이블/헤드블록 Transform — 트롤리 X를 따라간다.")]
        [SerializeField] Transform spreaderRoot;

        public override float Min => min;
        public override float Max => max;

        protected override float ReadAxis() => transform.localPosition.x;
        protected override void WriteAxis(float clamped)
        {
            var p = transform.localPosition;
            p.x = clamped;
            transform.localPosition = p;
        }

        // 트롤리 X 이동 후 스프레더도 같은 X로 동기화.
        protected override void OnMoved(float clamped)
        {
            if (spreaderRoot != null)
            {
                var sp = spreaderRoot.localPosition;
                sp.x = clamped;
                spreaderRoot.localPosition = sp;
            }
        }

        /// <summary>Builder가 한 번에 셋업할 때 사용.</summary>
        public void Configure(float minX, float maxX, Transform spreader)
        {
            min = minX;
            max = maxX;
            spreaderRoot = spreader;
        }

#if UNITY_EDITOR
        protected override int GizmoAxis => 0;   // X
        protected override Color GizmoColor => new Color(0.2f, 0.9f, 1f, 0.9f);
#endif
    }
}

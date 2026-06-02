using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 스프레더에 컨테이너를 attach/detach.
    /// - Attach: 컨테이너를 attachPoint의 자식으로 만들고 Rigidbody를 kinematic으로 전환.
    /// - Detach: 부모를 풀고 Rigidbody를 동적 상태로 복원, 옵션으로 새 부모 지정.
    /// 트위스트락 동작(0.4s 가정)은 즉시 처리. 추후 애니메이션이 필요하면 이벤트로 분리.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Spreader Attach")]
    [DisallowMultipleComponent]
    public sealed class SpreaderAttach : MonoBehaviour
    {
        [Tooltip("컨테이너가 결합될 좌표 — 비워두면 자기 자신 Transform 사용.")]
        [SerializeField] Transform attachPoint;

        Transform attached;
        Rigidbody attachedBody;
        bool savedUseGravity;
        bool savedIsKinematic;

        public bool HasContainer => attached != null;
        public Transform AttachedContainer => attached;
        /// <summary>적재된 컨테이너 질량(kg). 없으면 0. Rigidbody.mass를 그대로 반환 — 라벨/HUD 표시용.</summary>
        public float AttachedMassKg => attachedBody != null ? attachedBody.mass : 0f;

        Transform Point => attachPoint != null ? attachPoint : transform;
        /// <summary>컨테이너가 매달리는 기준 Transform. 네트워크 동기화가 클라이언트에서 동일 위치에 컨테이너를 붙이는 데 사용.</summary>
        public Transform AttachAnchor => Point;

        public void Configure(Transform point)
        {
            attachPoint = point;
        }

        /// <summary>
        /// 컨테이너를 결합. 이미 잡고 있으면 무시(중복 잡기 방지).
        /// </summary>
        public bool Attach(Transform container)
        {
            if (container == null || attached != null) return false;

            attached = container;
            attachedBody = container.GetComponent<Rigidbody>();
            if (attachedBody != null)
            {
                savedUseGravity = attachedBody.useGravity;
                savedIsKinematic = attachedBody.isKinematic;
                attachedBody.useGravity = false;
                attachedBody.isKinematic = true;
            }
            container.SetParent(Point, worldPositionStays: false);
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
            return true;
        }

        /// <summary>
        /// 결합 해제. newParent를 주면 그 아래로 이동(예: 야드 슬롯), null이면 씬 루트로.
        /// </summary>
        public Transform Detach(Transform newParent = null)
        {
            if (attached == null) return null;

            attached.SetParent(newParent, worldPositionStays: true);
            if (attachedBody != null)
            {
                attachedBody.useGravity = savedUseGravity;
                attachedBody.isKinematic = savedIsKinematic;
            }
            var released = attached;
            attached = null;
            attachedBody = null;
            return released;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = HasContainer ? new Color(0.2f, 1f, 0.3f, 1f)
                                        : new Color(1f, 0.4f, 0.4f, 1f);
            Gizmos.matrix = Point.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.25f, 0.05f, 0.12f));
        }
#endif
    }
}

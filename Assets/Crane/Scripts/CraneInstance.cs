using UnityEngine;

namespace CraneProject
{
    /// <summary>
    /// 크레인 프리팹 인스턴스. PLC 좌표를 받아 트롤리·스프레더를 이동시킨다.
    /// 두 기종 공통 사용 (STS는 트롤리=X 슬라이딩, RTG는 트롤리=Z 슬라이딩).
    /// 좌표 단위는 컨테이너와 동일한 미니어처 스케일(1/24) 적용 후 로컬 좌표.
    /// </summary>
    public class CraneInstance : MonoBehaviour
    {
        public enum CraneKind { StsQuay, RtgYard }

        [Header("기종")]
        [SerializeField] CraneKind kind = CraneKind.StsQuay;

        [Header("자식 Transform 참조")]
        [Tooltip("트롤리 GameObject — STS는 X 슬라이딩, RTG는 Z 슬라이딩")]
        [SerializeField] Transform trolley;
        [Tooltip("스프레더(+헤드블록) GameObject — Y 승강. 트롤리와 X/Z는 동기화.")]
        [SerializeField] Transform spreader;

        [Header("본체 색상 변경 대상 (Body 머티리얼)")]
        [Tooltip("Body 서브메시(인덱스 0)를 가진 Renderer들. PLC 상태에 따라 색상 변경됨.")]
        [SerializeField] Renderer[] bodyRenderers;

        [Header("트롤리 가동 범위 (로컬 좌표, 미터)")]
        [SerializeField] float trolleyAxisMin;
        [SerializeField] float trolleyAxisMax;

        [Header("스프레더 가동 범위 (로컬 Y, 미터)")]
        [SerializeField] float spreaderYMin;
        [SerializeField] float spreaderYMax;

        [Header("기본 자세 (로컬 좌표)")]
        [SerializeField] Vector3 trolleyRestPos;
        [SerializeField] Vector3 spreaderRestPos;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public CraneKind Kind => kind;
        public Transform Trolley => trolley;
        public Transform Spreader => spreader;

        /// <summary>
        /// 트롤리 슬라이딩 축 위치 설정 (STS: X, RTG: Z).
        /// 값은 로컬 미니어처 좌표(미터). 범위 밖이면 클램프.
        /// </summary>
        public void SetTrolleyAxis(float value)
        {
            if (trolley == null) return;
            float clamped = Mathf.Clamp(value, trolleyAxisMin, trolleyAxisMax);
            var p = trolleyRestPos;
            if (kind == CraneKind.StsQuay) p.x = clamped;
            else                            p.z = clamped;
            trolley.localPosition = p;

            // 스프레더는 트롤리의 X/Z를 따라간다 (Y는 별도)
            if (spreader != null)
            {
                var sp = spreader.localPosition;
                if (kind == CraneKind.StsQuay) sp.x = clamped;
                else                            sp.z = clamped;
                spreader.localPosition = sp;
            }
        }

        /// <summary>
        /// 스프레더 Y(승강) 설정. 로컬 미니어처 좌표(미터).
        /// </summary>
        public void SetSpreaderY(float value)
        {
            if (spreader == null) return;
            float clamped = Mathf.Clamp(value, spreaderYMin, spreaderYMax);
            var p = spreader.localPosition;
            p.y = clamped;
            spreader.localPosition = p;
        }

        /// <summary>
        /// PLC 실측 좌표(미터)를 받아 미니어처 스케일로 변환해 적용.
        /// </summary>
        public void ApplyPlcMeters(float trolleyAxisMeters, float spreaderYMeters,
                                   float scale = ProceduralCraneMesh.DefaultMiniatureScale)
        {
            SetTrolleyAxis(trolleyAxisMeters * scale);
            SetSpreaderY(spreaderYMeters * scale);
        }

        /// <summary>
        /// 본체(Body) 색상 변경. PLC 상태(정상=녹, 주의=황, 이상=적)에 사용.
        /// </summary>
        public void SetBodyColor(Color color)
        {
            if (bodyRenderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb, 0);
                mpb.SetColor(BaseColorId, color);
                mpb.SetColor(ColorId, color);
                r.SetPropertyBlock(mpb, 0);
            }
        }

        /// <summary>
        /// 빌더에서 호출 — 가동 범위·기본 자세를 한 번에 설정.
        /// </summary>
        public void Configure(CraneKind kind,
                              Transform trolley, Transform spreader,
                              Renderer[] bodyRenderers,
                              float trolleyAxisMin, float trolleyAxisMax,
                              float spreaderYMin, float spreaderYMax,
                              Vector3 trolleyRestPos, Vector3 spreaderRestPos)
        {
            this.kind = kind;
            this.trolley = trolley;
            this.spreader = spreader;
            this.bodyRenderers = bodyRenderers;
            this.trolleyAxisMin = trolleyAxisMin;
            this.trolleyAxisMax = trolleyAxisMax;
            this.spreaderYMin = spreaderYMin;
            this.spreaderYMax = spreaderYMax;
            this.trolleyRestPos = trolleyRestPos;
            this.spreaderRestPos = spreaderRestPos;

            if (trolley != null)  trolley.localPosition  = trolleyRestPos;
            if (spreader != null) spreader.localPosition = spreaderRestPos;
        }
    }
}

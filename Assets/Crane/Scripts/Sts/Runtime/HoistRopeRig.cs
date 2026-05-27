using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// 호이스트 로프를 매 프레임 트롤리(고정 상단)↔스프레더(승강 하단) 사이로 늘이고 줄인다.
    /// 게임 오브젝트(실린더)를 직접 스케일/이동하므로 스프레더가 오르내리면 로프가 따라 신축한다.
    /// 로프는 spreaderRoot의 자식이고, 스프레더 Y는 spreaderRoot 로컬 기준이라 같은 좌표계에서 계산.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/Hoist Rope Rig")]
    [DisallowMultipleComponent]
    [ExecuteAlways]   // 에디터에서도 스프레더 따라 로프 갱신(Play 없이 확인 가능)
    public sealed class HoistRopeRig : MonoBehaviour
    {
        // ※ [SerializeField] 필수 — Play 진입(도메인 리로드) 시 Configure로 넣은 참조가
        //    직렬화되지 않으면 null로 리셋돼 로프가 안 늘어난다(그동안 안 되던 원인).
        [SerializeField] Transform spreader;       // 승강하는 스프레더
        [SerializeField] float topY;               // 로프 상단(트롤리 부착, spreaderRoot 로컬 Y)
        [SerializeField] float attachOffsetY;      // 스프레더 원점 → 로프 하단 부착점(헤드블록 상단)
        [SerializeField] Transform[] ropes;
        [SerializeField] Vector2[] anchors;        // 각 로프의 (x, z) 고정 위치
        [SerializeField] float radius;

        public void Configure(Transform spreader, float topY, float attachOffsetY,
                              Transform[] ropes, Vector2[] anchors, float radius)
        {
            this.spreader = spreader;
            this.topY = topY;
            this.attachOffsetY = attachOffsetY;
            this.ropes = ropes;
            this.anchors = anchors;
            this.radius = radius;
            // 생성 시점엔 스프레더 rest Y가 아직 안 잡혀 있어 즉시 계산하지 않음
            // (Rod가 만든 rest 길이 로프를 에디터 표시로 사용, Play에서 LateUpdate가 신축).
        }

        // 스프레더가 움직인 뒤(드라이버 update/coroutine) 갱신
        void LateUpdate() => Apply();

        void Apply()
        {
            if (spreader == null || ropes == null) return;

            float botY = spreader.localPosition.y + attachOffsetY;
            float midY = (topY + botY) * 0.5f;
            float halfLen = Mathf.Abs(topY - botY) * 0.5f;   // 실린더 높이 2 기준 → 절반

            for (int i = 0; i < ropes.Length; i++)
            {
                var r = ropes[i];
                if (r == null) continue;
                r.localRotation = Quaternion.identity;       // 수직
                r.localPosition = new Vector3(anchors[i].x, midY, anchors[i].y);
                r.localScale = new Vector3(radius * 2f, halfLen, radius * 2f);
            }
        }
    }
}

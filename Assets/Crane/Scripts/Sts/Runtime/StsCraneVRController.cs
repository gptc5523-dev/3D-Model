using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;            // InputDevices — Quest 컨트롤러 직접 읽기(OpenXR 액션 에셋 비의존)

namespace Container.Crane.Sts
{
    /// <summary>
    /// VR 컨트롤러(Quest)로 STS 크레인을 수동 조종. 입력은 UnityEngine.XR.InputDevices로 직접 읽음
    /// (코드 InputAction이 OpenXR에서 입력을 못 받는 문제 회피). 키보드 미사용 — 컨트롤러 전용.
    /// - 오른손 스틱 X → 트롤리(이동),  왼손 스틱 Y → 호이스트(상하, 위=올림)
    /// - Y 버튼(왼손) → 집기,  X 버튼(왼손) → 놓기,  B 버튼(오른손) → 걷기↔크레인 모드 토글
    /// (집기/놓기를 X·Y에 둬서 트리거/그립은 손 직접 집기[XRGrabInteractable]와 겹치지 않음)
    /// 크레인 모드일 때만 스틱이 크레인을 움직이고, 씬의 XR 로코모션은 자동으로 끈다.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/STS Crane VR Controller")]
    [RequireComponent(typeof(StsCrane))]
    [DisallowMultipleComponent]
    public sealed class StsCraneVRController : MonoBehaviour
    {
        [Header("모드")]
        [Tooltip("시작 시 바로 크레인 조종 모드 (테스트 편의). 게임 흐름에선 false 권장.")]
        [SerializeField] bool startInCraneMode = true;
        [Tooltip("크레인 모드일 때 끌 커스텀 이동 스크립트(있으면). XR 표준 로코모션은 자동 탐색됨.")]
        [SerializeField] Behaviour[] suppressWhileControlling;

        [Header("속도 (정규화 범위/초, 스틱 최대 시)")]
        [SerializeField] float trolleySpeed = 0.5f;
        [SerializeField] float hoistSpeed = 0.5f;

        [Header("입력")]
        [SerializeField, Range(0f, 0.5f)] float deadzone = 0.12f;
        [Tooltip("Console에 입력/모드 로그 출력")]
        [SerializeField] bool debugLog = true;

        StsCrane crane;
        SpreaderGrabber grabber;
        bool craneMode;
        bool prevGrab, prevRelease, prevToggle;
        readonly List<Behaviour> disabledLoco = new List<Behaviour>();

        // XRI LocomotionProvider 타입을 리플렉션으로(컴파일 의존성 제거)
        static System.Type _locoType;
        static bool _locoSearched;
        static System.Type LocomotionProviderType
        {
            get
            {
                if (_locoSearched) return _locoType;
                _locoSearched = true;
                _locoType =
                    System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider, Unity.XR.Interaction.Toolkit")
                    ?? System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.LocomotionProvider, Unity.XR.Interaction.Toolkit");
                return _locoType;
            }
        }

        void Awake()
        {
            crane = GetComponent<StsCrane>();
            grabber = GetComponent<SpreaderGrabber>();
        }

        void OnEnable()
        {
            var op = GetComponent<StsCraneOperator>();
            if (op != null) op.enabled = false;   // 수동 조종 중엔 자동 사이클 정지
            craneMode = startInCraneMode;
            ApplyMode();
            if (debugLog) Debug.Log($"[Crane] VRController 활성 — 시작 크레인모드 {craneMode}");
        }

        void OnDisable()
        {
            craneMode = false;   // 컨트롤러 끄면 로코모션 복구
            ApplyMode();
        }

        void Update()
        {
            if (crane == null) return;

            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            // 트롤리 = 오른손 스틱 X,  호이스트 = 왼손 스틱 Y
            float tx = 0f, hy = 0f;
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rs)) tx = rs.x;
            if (left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 ls)) hy = ls.y;

            // 집기 = Y(왼손 보조), 놓기 = X(왼손 주), 모드 토글 = B(오른손 보조) — 엣지 검출
            bool grabNow = Btn(left, CommonUsages.secondaryButton);    // Y
            bool releaseNow = Btn(left, CommonUsages.primaryButton);   // X
            bool toggleNow = Btn(right, CommonUsages.secondaryButton); // B

            if (toggleNow && !prevToggle) { craneMode = !craneMode; ApplyMode(); }
            prevToggle = toggleNow;

            if (grabNow && !prevGrab)
            {
                if (debugLog) Debug.Log("[Crane] Y 입력 → 집기(Grab)");
                grabber?.Grab();
            }
            prevGrab = grabNow;

            if (releaseNow && !prevRelease)
            {
                if (debugLog) Debug.Log("[Crane] X 입력 → 놓기(Release)");
                grabber?.Release();
            }
            prevRelease = releaseNow;

            if (!craneMode) return;   // 걷기 모드: 스틱(이동/승강) 무시

            IAxisMover trolley = crane.Trolley;
            if (trolley != null && Mathf.Abs(tx) > deadzone)
            {
                float range = trolley.Max - trolley.Min;
                trolley.MoveTo(trolley.Current + tx * trolleySpeed * range * Time.deltaTime);
            }

            IAxisMover hoist = crane.Spreader;
            if (hoist != null && Mathf.Abs(hy) > deadzone)
            {
                float range = hoist.Max - hoist.Min;
                hoist.MoveTo(hoist.Current + hy * hoistSpeed * range * Time.deltaTime);
            }
        }

        static bool Btn(UnityEngine.XR.InputDevice d, InputFeatureUsage<bool> usage)
            => d.isValid && d.TryGetFeatureValue(usage, out bool v) && v;

        // 크레인 모드면 씬의 모든 XR 로코모션(+수동 지정분)을 끄고, 걷기 모드면 끈 것만 복구.
        void ApplyMode()
        {
            if (craneMode)
            {
                if (suppressWhileControlling != null)
                    foreach (var b in suppressWhileControlling)
                        if (b != null && b.enabled) { b.enabled = false; disabledLoco.Add(b); }

                var locoType = LocomotionProviderType;
                if (locoType != null)
                    foreach (var o in FindObjectsByType(locoType, FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        if (o is Behaviour b && b.enabled) { b.enabled = false; disabledLoco.Add(b); }
            }
            else
            {
                foreach (var b in disabledLoco)
                    if (b != null) b.enabled = true;
                disabledLoco.Clear();
            }
        }
    }
}

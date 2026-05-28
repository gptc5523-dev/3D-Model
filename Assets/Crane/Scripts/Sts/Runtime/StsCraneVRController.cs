using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;            // InputDevices — Quest 컨트롤러 직접 읽기(OpenXR 액션 에셋 비의존)

namespace Container.Crane.Sts
{
    /// <summary>
    /// VR 컨트롤러(Quest)로 STS 크레인을 수동 조종. 입력은 UnityEngine.XR.InputDevices로 직접 읽음
    /// (코드 InputAction이 OpenXR에서 입력을 못 받는 문제 회피). 키보드 미사용 — 컨트롤러 전용.
    /// - 오른손 스틱 X → 트롤리(이동),  왼손 스틱 Y → 호이스트(상하, 위=올림),  왼손 스틱 X → 갠트리(좌우 주행, A 토글 시만)
    /// - Y 버튼(왼손) → 집기,  X 버튼(왼손) → 놓기,  A 버튼(오른손) → 갠트리 주행 모드 토글,  B 버튼(오른손) → 걷기↔크레인 모드 토글
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
        [SerializeField] float gantrySpeed = 0.3f;   // 갠트리는 크레인 전체 이동이라 좀 느리게

        [Header("입력")]
        [SerializeField, Range(0f, 0.5f)] float deadzone = 0.12f;
        [Tooltip("Console에 입력/모드 로그 출력")]
        [SerializeField] bool debugLog = true;

        StsCrane crane;
        SpreaderGrabber grabber;
        bool craneMode;
        bool gantryActive;   // A 토글: ON이면 왼스틱 X가 갠트리를 조종
        bool prevGrab, prevRelease, prevToggle, prevGantryToggle;
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

            // 트롤리 = 오른손 스틱 X,  호이스트 = 왼손 스틱 Y,  갠트리 = 왼손 스틱 X (A 토글 시만)
            float tx = 0f, hy = 0f, lx = 0f;
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rs)) tx = rs.x;
            if (left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 ls)) { hy = ls.y; lx = ls.x; }

            // 집기 = Y(왼손 보조), 놓기 = X(왼손 주), 갠트리 토글 = A(오른손 주), 크레인↔걷기 토글 = B(오른손 보조) — 엣지 검출
            bool grabNow = Btn(left, CommonUsages.secondaryButton);     // Y
            bool releaseNow = Btn(left, CommonUsages.primaryButton);    // X
            bool gantryToggleNow = Btn(right, CommonUsages.primaryButton);   // A
            bool toggleNow = Btn(right, CommonUsages.secondaryButton);  // B

            if (toggleNow && !prevToggle) { craneMode = !craneMode; ApplyMode(); }
            prevToggle = toggleNow;

            // A: 갠트리 주행 모드 토글 (걷기 모드면 자동 OFF)
            if (gantryToggleNow && !prevGantryToggle && craneMode)
            {
                gantryActive = !gantryActive;
                if (debugLog) Debug.Log($"[Crane] A 입력 → 갠트리 주행 모드 {(gantryActive ? "ON" : "OFF")} (왼스틱 X로 좌우 주행)");
                Haptic(right, 0.4f, 0.05f);
            }
            prevGantryToggle = gantryToggleNow;
            if (!craneMode && gantryActive) gantryActive = false;   // 걷기 모드 들어가면 안전상 OFF

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

            // A 모드(갠트리 주행): 크레인 전체만 움직이고 트롤리/호이스트는 잠금(섞이지 않게)
            if (gantryActive)
            {
                IAxisMover gantry = crane.Gantry;
                if (gantry != null && Mathf.Abs(lx) > deadzone)
                {
                    float range = gantry.Max - gantry.Min;
                    gantry.MoveTo(gantry.Current + lx * gantrySpeed * range * Time.deltaTime);
                }
                return;
            }

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

        // 컨트롤러 짧은 진동 — 모드 토글 피드백
        static void Haptic(UnityEngine.XR.InputDevice d, float amplitude, float seconds)
        {
            if (!d.isValid) return;
            if (d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Max(0f, seconds));
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

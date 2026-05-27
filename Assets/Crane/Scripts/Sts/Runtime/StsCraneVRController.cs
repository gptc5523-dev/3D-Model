using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;   // LocomotionProvider 베이스(이동/회전/텔레포트/등반)

namespace Container.Crane.Sts
{
    /// <summary>
    /// VR 컨트롤러(Quest)로 STS 크레인을 수동 조종. 게임패드/키보드 보조 바인딩 포함(에디터 테스트).
    /// - 오른쪽 스틱 좌우(X) → 트롤리(이동)
    /// - 왼쪽 스틱 상하(Y)  → 호이스트(스프레더 상하, 위=올림)
    /// - 트리거(양손)/Space → 집기/놓기 토글
    /// - 모드 토글 버튼(B/Y 또는 Tab) → "걷기 ↔ 크레인 조종" 전환.
    ///   크레인 모드일 때만 스틱이 크레인을 움직이고, 씬의 XR 로코모션은 자동으로 끈다.
    /// 활성 시 자동 사이클(StsCraneOperator)도 끈다.
    /// </summary>
    [AddComponentMenu("Container/STS Crane/STS Crane VR Controller")]
    [RequireComponent(typeof(StsCrane))]
    [DisallowMultipleComponent]
    public sealed class StsCraneVRController : MonoBehaviour
    {
        [Header("모드")]
        [Tooltip("시작 시 바로 크레인 조종 모드 (테스트 편의). 게임 흐름에선 false 권장.")]
        [SerializeField] bool startInCraneMode = true;
        [Tooltip("크레인 모드일 때 끌 커스텀 이동 스크립트(있으면). XR 표준 로코모션은 자동 탐색되니 비워도 됨.")]
        [SerializeField] Behaviour[] suppressWhileControlling;

        [Header("속도 (정규화 범위/초, 스틱 최대 시)")]
        [SerializeField] float trolleySpeed = 0.5f;
        [SerializeField] float hoistSpeed = 0.5f;

        [Header("입력")]
        [SerializeField, Range(0f, 0.5f)] float deadzone = 0.12f;
        [Tooltip("집기 감지 박스 반경(m)")]
        [SerializeField] float grabHalfExtents = 0.15f;

        StsCrane crane;
        InputAction trolleyAxis;   // 오른손 스틱 X — 트롤리(이동)
        InputAction hoistAxis;     // 왼손 스틱 Y — 호이스트(상하)
        InputAction grab;          // 트리거(양손) — 집기/놓기
        InputAction toggle;        // B/Y(양손) — 모드 전환
        bool craneMode;
        readonly List<Behaviour> disabledLoco = new List<Behaviour>();   // 크레인 모드 진입 시 끈 로코모션(복구용)

        void Awake()
        {
            crane = GetComponent<StsCrane>();

            // 오른쪽 컨트롤러 = 트롤리(이동, 좌우 X)
            trolleyAxis = new InputAction("CraneTrolley", InputActionType.Value, expectedControlType: "Axis");
            trolleyAxis.AddBinding("<XRController>{RightHand}/thumbstick/x");
            trolleyAxis.AddBinding("<XRController>{RightHand}/primary2DAxis/x");
            trolleyAxis.AddBinding("<Gamepad>/rightStick/x");
            trolleyAxis.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");

            // 왼쪽 컨트롤러 = 호이스트(상하 Y, 위=올림)
            hoistAxis = new InputAction("CraneHoist", InputActionType.Value, expectedControlType: "Axis");
            hoistAxis.AddBinding("<XRController>{LeftHand}/thumbstick/y");
            hoistAxis.AddBinding("<XRController>{LeftHand}/primary2DAxis/y");
            hoistAxis.AddBinding("<Gamepad>/leftStick/y");
            hoistAxis.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");

            grab = new InputAction("CraneGrab", InputActionType.Button);
            grab.AddBinding("<XRController>{RightHand}/triggerButton");
            grab.AddBinding("<XRController>{LeftHand}/triggerButton");
            grab.AddBinding("<Keyboard>/space");

            toggle = new InputAction("CraneModeToggle", InputActionType.Button);
            toggle.AddBinding("<XRController>{RightHand}/secondaryButton");
            toggle.AddBinding("<XRController>{LeftHand}/secondaryButton");
            toggle.AddBinding("<Keyboard>/tab");
        }

        void OnEnable()
        {
            var op = GetComponent<StsCraneOperator>();
            if (op != null) op.enabled = false;   // 수동 조종 중엔 자동 사이클 정지

            trolleyAxis?.Enable();
            hoistAxis?.Enable();
            grab?.Enable();
            toggle?.Enable();

            craneMode = startInCraneMode;
            ApplyMode();
        }

        void OnDisable()
        {
            trolleyAxis?.Disable();
            hoistAxis?.Disable();
            grab?.Disable();
            toggle?.Disable();

            craneMode = false;   // 컨트롤러 끄면 로코모션 복구
            ApplyMode();
        }

        void Update()
        {
            if (crane == null) return;

            if (toggle != null && toggle.WasPressedThisFrame())
            {
                craneMode = !craneMode;
                ApplyMode();
            }

            if (!craneMode) return;   // 걷기 모드: 크레인 입력 무시(로코모션이 스틱 사용)

            // 오른손 스틱 X → 트롤리
            float tx = trolleyAxis != null ? trolleyAxis.ReadValue<float>() : 0f;
            IAxisMover trolley = crane.Trolley;
            if (trolley != null && Mathf.Abs(tx) > deadzone)
            {
                float range = trolley.Max - trolley.Min;
                trolley.MoveTo(trolley.Current + tx * trolleySpeed * range * Time.deltaTime);
            }

            // 왼손 스틱 Y → 호이스트
            float hy = hoistAxis != null ? hoistAxis.ReadValue<float>() : 0f;
            IAxisMover hoist = crane.Spreader;
            if (hoist != null && Mathf.Abs(hy) > deadzone)
            {
                float range = hoist.Max - hoist.Min;
                hoist.MoveTo(hoist.Current + hy * hoistSpeed * range * Time.deltaTime);
            }

            if (grab != null && grab.WasPressedThisFrame()) ToggleGrab();
        }

        // 크레인 모드면 씬의 모든 XR 로코모션(+수동 지정분)을 끄고, 걷기 모드면 끈 것만 복구.
        void ApplyMode()
        {
            if (craneMode)
            {
                if (suppressWhileControlling != null)
                    foreach (var b in suppressWhileControlling)
                        if (b != null && b.enabled) { b.enabled = false; disabledLoco.Add(b); }

                var providers = FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None);
                foreach (var p in providers)
                    if (p != null && p.enabled) { p.enabled = false; disabledLoco.Add(p); }
            }
            else
            {
                foreach (var b in disabledLoco)
                    if (b != null) b.enabled = true;
                disabledLoco.Clear();
            }
        }

        void ToggleGrab()
        {
            var attach = crane.Attach;
            if (attach == null) return;
            if (attach.HasContainer) { attach.Detach(); return; }

            Transform p = attach.transform;
            var hits = Physics.OverlapBox(p.position, Vector3.one * grabHalfExtents, p.rotation);
            foreach (var h in hits)
            {
                var rb = h.attachedRigidbody;
                if (rb != null) { attach.Attach(rb.transform); break; }
            }
        }
    }
}

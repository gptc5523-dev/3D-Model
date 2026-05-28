using System;
using UnityEngine;

namespace Container.Crane.Sts
{
    /// <summary>
    /// XR 텔레포트(빨간 포인터)와 우스틱 턴(스냅/연속 회전)을 Awake에서 일괄 비활성화.
    ///   - TeleportationProvider/Area/Anchor → 텔레포트 기능 OFF
    ///   - SnapTurnProvider / ContinuousTurnProvider → 시점 회전 OFF
    ///   - 이름에 "Teleport"가 포함된 GameObject(ray 인터랙터 + 라인 비주얼) → 시각 빨간 포인터 OFF
    /// 걷기(좌스틱 ContinuousMoveProvider)는 건드리지 않음. 씬 어디든 하나 붙이면 됨(예: XR Origin 또는 _Managers).
    /// </summary>
    [AddComponentMenu("Container/XR/Disable Teleport and Turn")]
    [DisallowMultipleComponent]
    public sealed class XRTeleportTurnDisabler : MonoBehaviour
    {
        [Tooltip("이름에 'Teleport'를 포함한 GameObject(텔레포트 ray 인터랙터/라인 비주얼)도 비활성화")]
        [SerializeField] bool disableTeleportNamedObjects = true;
        [Tooltip("Console에 비활성화 결과 로그 출력")]
        [SerializeField] bool debugLog = true;

        // 타입 이름으로만 매칭(네임스페이스 변경/패키지 버전 차이에 견고)
        static readonly string[] BehaviourTypeNames = {
            "TeleportationProvider",
            "TeleportationArea",
            "TeleportationAnchor",
            "SnapTurnProvider",
            "SnapTurnProviderBase",
            "ContinuousTurnProvider",
            "ContinuousTurnProviderBase",
        };

        void Awake()
        {
            int provs = 0, gos = 0;

            // 1) Behaviour 타입명 매칭으로 텔레포트/턴 provider 끔
            var allBehaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in allBehaviours)
            {
                if (b == null || !b.enabled) continue;
                string n = b.GetType().Name;
                for (int i = 0; i < BehaviourTypeNames.Length; i++)
                    if (n == BehaviourTypeNames[i]) { b.enabled = false; provs++; break; }
            }

            // 2) "Teleport" 이름 GameObject 일괄 OFF — ray 인터랙터·라인 비주얼·앵커 시각 등
            if (disableTeleportNamedObjects)
            {
                var allGo = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var go in allGo)
                {
                    if (go == null || !go.activeSelf) continue;
                    if (go.name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0)
                    { go.SetActive(false); gos++; }
                }
            }

            if (debugLog)
                Debug.Log($"[XR] 텔레포트·턴 비활성화 — Provider/Area/Anchor {provs}개 OFF, " +
                          $"Teleport-named GameObject {gos}개 OFF (걷기는 유지)");
        }
    }
}

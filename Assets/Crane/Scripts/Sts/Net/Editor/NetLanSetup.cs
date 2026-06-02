using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Container.Crane.Sts.Net.EditorTools
{
    /// <summary>
    /// LAN 멀티플레이 씬 배선을 한 번에 자동 구성:
    ///   1) 플레이어 아바타 프리팹(머리+양손) 생성 → Assets/Crane/Net/PlayerAvatar.prefab
    ///   2) 씬에 NetworkManager(+UnityTransport, NetLanUI, LanDiscovery) 생성·연결
    ///   3) 씬에 CraneNetSync(+NetworkObject) 생성
    /// 메뉴: Tool ▸ LAN 멀티플레이 셋업 (5인)
    ///
    /// 손으로 NetworkObject를 붙이는 실수 위험을 없앤다. 셋업 후 씬을 저장하면 끝.
    /// </summary>
    public static class NetLanSetup
    {
        const string PrefabDir = "Assets/Crane/Net";
        const string PrefabPath = PrefabDir + "/PlayerAvatar.prefab";

        [MenuItem("Tool/LAN 멀티플레이 셋업 (5인)")]
        public static void Setup()
        {
            var avatar = CreateOrLoadAvatarPrefab();
            var nm = SetupNetworkManager(avatar);
            SetupCraneSync();

            EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
            EditorUtility.DisplayDialog("LAN 멀티플레이 셋업 완료",
                "NetworkManager / 플레이어 아바타 / CraneNetSync 구성이 끝났습니다.\n\n" +
                "씬을 저장(Ctrl+S)한 뒤, 빌드해서 같은 와이파이의 기기들에서:\n" +
                " • 조종자 = '호스트 시작'\n • 관전자 = 호스트 IP로 '참가'\n\n" +
                "테스트 후 동작을 알려주세요(현재 미검증 상태).", "확인");
        }

        // ───────── 1) 아바타 프리팹 ─────────
        // 항만 작업자(안전모+보안경) 실루엣으로 조형 — 단순 큐브 placeholder 대신 의도된 모양.
        // 매번 새로 만들어 같은 경로에 덮어쓴다(GUID 유지 → NetworkManager.PlayerPrefab 참조 보존).
        // 디자인을 바꾸려면 이 함수의 오프셋/스케일/색을 수정 후 셋업 메뉴를 다시 실행.
        static GameObject CreateOrLoadAvatarPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/Crane", "Net");

            var root = new GameObject("PlayerAvatar");
            root.AddComponent<NetworkObject>();
            var sync = root.AddComponent<PlayerAvatarSync>();

            // 머리(피부색 구) — 회전이 안전모/보안경에 전달되도록 부모로 사용. 자식 좌표는 머리 로컬 기준.
            var head = MakeVisual(PrimitiveType.Sphere, "Head", root.transform,
                Vector3.zero, new Vector3(0.19f, 0.21f, 0.19f), new Color(0.86f, 0.68f, 0.56f));

            // 안전모 돔(머리 위 약간 납작한 구) — 이 렌더러에 참가자별 색이 칠해짐(tintTarget)
            var helmet = MakeVisual(PrimitiveType.Sphere, "Helmet", head.transform,
                new Vector3(0f, 0.28f, 0f), new Vector3(1.2f, 0.85f, 1.2f), new Color(0.96f, 0.74f, 0.12f));
            // 안전모 챙(얇은 원기둥) — 이마 앞으로 살짝 튀어나옴
            MakeVisual(PrimitiveType.Cylinder, "Brim", head.transform,
                new Vector3(0f, 0.10f, 0.04f), new Vector3(1.5f, 0.05f, 1.5f), new Color(0.92f, 0.70f, 0.10f));
            // 보안경/바이저(눈 위치 어두운 띠)
            MakeVisual(PrimitiveType.Cube, "Visor", head.transform,
                new Vector3(0f, 0.0f, 0.44f), new Vector3(0.95f, 0.30f, 0.30f), new Color(0.05f, 0.05f, 0.08f));

            // 양손 — 작업 장갑 느낌의 둥근 모양(납작한 구)
            var lh = MakeVisual(PrimitiveType.Sphere, "LeftHand", root.transform,
                Vector3.zero, new Vector3(0.09f, 0.075f, 0.11f), new Color(0.22f, 0.24f, 0.28f));
            var rh = MakeVisual(PrimitiveType.Sphere, "RightHand", root.transform,
                Vector3.zero, new Vector3(0.09f, 0.075f, 0.11f), new Color(0.22f, 0.24f, 0.28f));

            // 직렬화 private 필드 연결
            var so = new SerializedObject(sync);
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.FindProperty("leftHand").objectReferenceValue = lh.transform;
            so.FindProperty("rightHand").objectReferenceValue = rh.transform;
            so.FindProperty("tintTarget").objectReferenceValue = helmet.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject MakeVisual(PrimitiveType type, string name, Transform parent,
                                     Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // 아바타는 물리 충돌 없음
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ───────── 2) NetworkManager ─────────
        static NetworkManager SetupNetworkManager(GameObject avatarPrefab)
        {
            var nm = Object.FindFirstObjectByType<NetworkManager>();
            if (nm == null)
            {
                var go = new GameObject("NetworkManager");
                nm = go.AddComponent<NetworkManager>();
            }
            var utp = nm.GetComponent<UnityTransport>();
            if (utp == null) utp = nm.gameObject.AddComponent<UnityTransport>();
            if (nm.GetComponent<NetLanUI>() == null) nm.gameObject.AddComponent<NetLanUI>();
            if (nm.GetComponent<LanDiscovery>() == null) nm.gameObject.AddComponent<LanDiscovery>();

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = avatarPrefab;
            nm.NetworkConfig.ConnectionApproval = true;   // 인원 제한(5인)용

            EditorUtility.SetDirty(nm);
            return nm;
        }

        // ───────── 3) CraneNetSync ─────────
        static void SetupCraneSync()
        {
            if (Object.FindFirstObjectByType<CraneNetSync>() != null) return;
            var go = new GameObject("CraneNetSync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<CraneNetSync>();
            EditorUtility.SetDirty(go);
        }
    }
}

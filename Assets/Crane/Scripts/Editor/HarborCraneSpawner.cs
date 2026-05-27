#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CraneProject.EditorTools
{
    /// <summary>
    /// Sketchfab Harbor Crane (CC-BY 4.0 by MisterH) glTF 모델을 씬에 자동 spawn.
    /// 절차적 메시 폐기, 외부 PBR 모델 사용. 크레디트는 Assets/CREDITS.md 참고.
    ///
    /// 사용 흐름:
    ///   1) Package Manager가 com.unity.cloud.gltfast 받기 (manifest.json 자동)
    ///   2) Assets/Crane/Imported/HarborCrane/scene.gltf를 Unity가 자동 import → root prefab 생성
    ///   3) Container/Crane/Spawn Imported Harbor Crane 실행
    /// </summary>
    public static class HarborCraneSpawner
    {
        const string ImportedGltf = "Assets/Crane/Imported/HarborCrane/scene.gltf";
        const string RootName     = "Harbor_Crane";

        // 컨테이너 비례 기준값
        // 컨테이너 실측 높이 2.591m (ISO 668 22G1), 미니어처 스케일 1/24 적용.
        const float ContainerHeightReal = 2.591f;
        const float MiniatureScale      = 1f / 24f;
        const float ContainerHeightMini = ContainerHeightReal * MiniatureScale; // ≈ 0.108m

        // 크레인 : 컨테이너 높이 비례 — 절차적 STS Crane(StsApexHeight=22m)과 동일 비례 사용.
        // 22m / 2.591m ≈ 8.5배 → 게임 내 모든 크레인이 같은 비례.
        const float CraneToContainerRatio = 8.5f;

        // 목표 높이(m) — 컨테이너 미니어처 × 8.5배 = 약 0.918m.
        const float TargetHeightM = ContainerHeightMini * CraneToContainerRatio;

        // 컨테이너 호스트 위치 기준 spawn 오프셋 — +X(오른쪽)로 컨테이너 옆에.
        // 컨테이너 길이(0.252m) 절반 + 크레인 footprint 여유.
        static readonly Vector3 SpawnOffset = new Vector3(0.5f, 0f, 0f);

        // 회전 보정 — 모델이 앞으로 누워서 스프레더가 바닥에 있음. X축 -90도로 뒤로 일으켜 세움.
        static readonly Vector3 RotationFix = new Vector3(-90f, 0f, 0f);

        [MenuItem("Container/Crane/Spawn Imported Harbor Crane")]
        public static void Spawn()
        {
            // 이전 인스턴스 정리 — 절차적/임포트 둘 다
            foreach (var legacy in new[] { "Crane_STS", "Crane_RTG", RootName })
            {
                var go = GameObject.Find(legacy);
                if (go != null) Object.DestroyImmediate(go);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedGltf);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Harbor Crane 미발견",
                    $"{ImportedGltf} 를 찾을 수 없습니다.\n\n" +
                    "확인:\n" +
                    "  1) Package Manager에서 'glTFast' 가져오기 완료 여부\n" +
                    "  2) Assets/Crane/Imported/HarborCrane/ 안에 scene.gltf 존재 여부\n" +
                    "  3) Unity가 import 완료할 때까지 잠시 대기 후 재시도",
                    "확인");
                return;
            }

            // 1단계: wrapper + prefab 1:1 스케일로 spawn → 실제 bounds 측정
            var wrapper = new GameObject(RootName);
            Vector3 anchor = FindContainerAnchor();
            wrapper.transform.position = anchor + SpawnOffset;
            wrapper.transform.localScale = Vector3.one;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            if (instance == null)
            {
                Object.DestroyImmediate(wrapper);
                Debug.LogError("[HarborCraneSpawner] 인스턴스 생성 실패");
                return;
            }
            instance.transform.localPosition = Vector3.zero;
            // 회전 보정 (Z-up → Y-up)
            instance.transform.localRotation = Quaternion.Euler(RotationFix);

            // 2단계: world bounds 측정
            var renderers = wrapper.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[HarborCraneSpawner] Renderer 없음 — 스케일 적용 불가");
                return;
            }
            Bounds rawBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) rawBounds.Encapsulate(renderers[i].bounds);
            float rawHeight = rawBounds.size.y;
            Debug.Log($"[HarborCraneSpawner] 1차 raw bounds (scale 1): " +
                      $"center={rawBounds.center}, size={rawBounds.size}");

            // 3단계: target / actual → 자동 스케일
            float autoScale = rawHeight > 0.001f ? TargetHeightM / rawHeight : 1f;
            wrapper.transform.localScale = Vector3.one * autoScale;

            // 4단계: 스케일 적용 후 bounds 재측정 → 위치 보정 (지면에 발 닿게)
            Bounds finalBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) finalBounds.Encapsulate(renderers[i].bounds);
            // 모델 바닥(min.y)이 anchor.y와 맞도록 wrapper 위치 보정
            float yShift = anchor.y - finalBounds.min.y;
            wrapper.transform.position += new Vector3(0f, yShift, 0f);

            Debug.Log($"[HarborCraneSpawner] 최종: autoScale={autoScale:F4}, " +
                      $"final bounds size={(rawBounds.size * autoScale)} " +
                      $"(목표 Y={TargetHeightM}m)");

            Selection.activeGameObject = wrapper;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.FrameSelected();
        }

        static Transform FindDescendantByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static Vector3 FindContainerAnchor()
        {
            var spawnerType = System.Type.GetType("ContainerProject.ContainerSpawner, Assembly-CSharp");
            if (spawnerType != null)
            {
                var spawner = Object.FindFirstObjectByType(spawnerType) as Component;
                if (spawner != null) return spawner.transform.position;
            }
            var named = GameObject.Find("Container_Procedural");
            if (named != null) return named.transform.position;
            return Vector3.zero;
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ContainerProject.EditorTools
{
    /// <summary>
    /// 절차적(Procedural) 컨테이너 스폰 메뉴.
    /// 기능·사이즈는 기존 SpawnContainers.Spawn20ftStd와 동일. 디자인(메시)만 ProceduralContainerMesh 사용.
    /// 프리팹/팔레트/ContainerInstance 사용하지 않음 — Std Set 패턴 그대로.
    /// </summary>
    public static class VRTestMenu
    {
        // 기존 SpawnContainers Std20과 동일 색상 풀
        static readonly Color[] PaletteColors =
        {
            new Color(0.72f, 0.19f, 0.18f),  // Red
            new Color(0.12f, 0.31f, 0.49f),  // Blue
            new Color(0.29f, 0.42f, 0.23f),  // Green
            new Color(0.85f, 0.45f, 0.15f),  // Orange
            new Color(0.40f, 0.26f, 0.18f),  // Brown
            new Color(0.28f, 0.28f, 0.30f),  // DarkGray
            new Color(0.92f, 0.92f, 0.90f),  // White
            new Color(0.85f, 0.78f, 0.58f),  // Beige
        };

        [MenuItem("Container/Spawn/Procedural (1 unit)")]
        public static void SpawnSingleProcedural()
        {
            SpawnSingle(length: ProceduralContainerMesh.Length20ft, suffix: "");
        }

        [MenuItem("Container/Spawn/Procedural 40ft (1 unit)")]
        public static void SpawnSingleProcedural40ft()
        {
            SpawnSingle(length: ProceduralContainerMesh.Length40ft, suffix: "40ft");
        }

        static void SpawnSingle(float length, string suffix)
        {
            // 기존 컨테이너 모두 삭제 (Std Set 동일 패턴)
            var existing = Object.FindObjectsByType<CubeReset>(FindObjectsSortMode.None);
            foreach (var c in existing) Undo.DestroyObjectImmediate(c.gameObject);

            string name = string.IsNullOrEmpty(suffix) ? "Container_Procedural" : "Container_Procedural_" + suffix;
            var go = BuildOne(PaletteColors[Random.Range(0, PaletteColors.Length)], name, length);
            var reset = go.GetComponent<CubeReset>();
            if (reset != null) reset.SetHorizontalOffset(0f);

            Undo.RegisterCreatedObjectUndo(go, "Spawn Procedural");
            Selection.activeGameObject = go;

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.FrameSelected();

            Debug.Log($"[VRTestMenu] Procedural 컨테이너 1개 스폰 (length={length}m, mesh bounds: {go.GetComponent<MeshFilter>().sharedMesh.bounds.size})");
        }

        [MenuItem("Container/Spawn/Procedural (2x2)")]
        public static void SpawnProcedural2x2()
        {
            var existing = Object.FindObjectsByType<CubeReset>(FindObjectsSortMode.None);
            foreach (var c in existing) Undo.DestroyObjectImmediate(c.gameObject);

            // 미니어처 크기 (1/24): 폭(Z) 0.102m, 높이(Y) 0.108m
            const float gap = 0.005f;
            const float containerWidth  = 2.438f / 24f;
            const float containerHeight = 2.591f / 24f;
            float halfH = (containerWidth + gap) * 0.5f;
            float stackV = containerHeight + gap;

            // (h, v) = (horizontalOffset, verticalOffset) — 좌하/우하/좌상/우상
            var positions = new (float h, float v)[]
            {
                (-halfH, 0f),
                (+halfH, 0f),
                (-halfH, stackV),
                (+halfH, stackV),
            };
            Color[] colors = { PaletteColors[0], PaletteColors[1], PaletteColors[2], PaletteColors[3] };
            string[] names = { "Container_Procedural_BL", "Container_Procedural_BR", "Container_Procedural_TL", "Container_Procedural_TR" };

            GameObject last = null;
            for (int i = 0; i < 4; i++)
            {
                var go = BuildOne(colors[i], names[i]);
                var reset = go.GetComponent<CubeReset>();
                if (reset != null)
                {
                    reset.SetHorizontalOffset(positions[i].h);
                    reset.SetVerticalOffset(positions[i].v);
                }
                // 4개가 동시에 (0,0,0)에서 시작 → 콜라이더 겹쳐서 서로 튕기는 문제 방지.
                // 스폰 직후엔 kinematic 고정. 사용자가 그랩 후 놓으면 CubeReset 이 자동으로 풀어줌.
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                Undo.RegisterCreatedObjectUndo(go, "Spawn Procedural 2x2");
                last = go;
            }
            if (last != null)
            {
                Selection.activeGameObject = last;
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) sv.FrameSelected();
            }
            Debug.Log("[VRTestMenu] Procedural 컨테이너 4개 (2x2) 스폰 — kinematic 으로 고정. 그랩 후 놓으면 물리 활성화.");
        }

        // ───────────────────────────── 단일 컨테이너 빌더 ─────────────────────────────
        static GameObject BuildOne(Color bodyColor, string name, float length = -1f)
        {
            // 1. 메시 (매번 새로 — 미니어처 스케일 1/24, 중심 피봇, X=긴 방향)
            //    length 인자 양수면 BuildSized 로 임의 사이즈, 아니면 기본 20ft Build
            Mesh mesh = length > 0f
                ? ProceduralContainerMesh.BuildSized(length, ProceduralContainerMesh.StdWidth, ProceduralContainerMesh.HeightStd, name + "_Mesh")
                : ProceduralContainerMesh.Build(name + "_Mesh");

            // 2. 셰이더
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 3. 머티리얼 인스턴스 4개 (서브메시 순서: 0=Body, 1=Door, 2=Frame, 3=Castings)
            var matBody = MakeMat(litShader, bodyColor, metallic: 0.10f, smoothness: 0.40f, suffix: "_Body");
            var matDoor = MakeMat(litShader, MulColor(bodyColor, 0.80f), metallic: 0.10f, smoothness: 0.40f, suffix: "_Door");
            var matFrame = MakeMat(litShader, new Color(0.18f, 0.18f, 0.20f), metallic: 0.55f, smoothness: 0.45f, suffix: "_Frame");
            var matCastings = MakeMat(litShader, new Color(0.10f, 0.10f, 0.11f), metallic: 0.40f, smoothness: 0.25f, suffix: "_Castings");

            // 4. GameObject + 메시
            var root = new GameObject(name);
            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { matBody, matDoor, matFrame, matCastings };

            // 5. 콜라이더 (메시 bounds 기반 — 스케일에 자동 동기)
            var box = root.AddComponent<BoxCollider>();
            var bounds = mesh.bounds;
            box.center = bounds.center;
            box.size = bounds.size;

            // 6. VR 인터랙션 (기존 SpawnContainers Std Set 패턴 동일)
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = true;
            // useDynamicAttach: 손이 닿은 지점이 그립 포인트가 되도록 (피봇 스냅 방지)
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            root.AddComponent<CubeReset>();

            return root;
        }

        static Material MakeMat(Shader shader, Color c, float metallic, float smoothness, string suffix)
        {
            var mat = new Material(shader) { name = "ProcMat" + suffix };
            if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))       mat.SetColor("_Color", c);
            if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness"))  mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        static Color MulColor(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;

namespace ContainerProject.EditorTools
{
    /// <summary>
    /// 현재 ProceduralContainerMesh 코드 + 머티리얼 4종 + Box Collider 를 묶어 FBX 로 export.
    /// 머티리얼은 ContainerEditorBuilder.BuildAll 으로 미리 생성돼 있어야 함 (없으면 경고).
    /// </summary>
    public static class ContainerFbxExporter
    {
        const string MatDir     = "Assets/Container/Materials";
        const string MatBody    = MatDir + "/Container_Body.mat";
        const string MatDoor    = MatDir + "/Container_Door.mat";
        const string MatFrame   = MatDir + "/Container_Frame.mat";
        const string MatCasting = MatDir + "/Container_Castings.mat";

        // [MenuItem("Container/Export FBX")]   // 메뉴 숨김(사용자 요청) — 복구하려면 주석 해제
        public static void ExportFbx()
        {
            string path = EditorUtility.SaveFilePanel(
                "Container FBX 저장",
                Application.dataPath,
                "Container_20ft.fbx",
                "fbx");
            if (string.IsNullOrEmpty(path)) return;

            // 현재 ProceduralContainerMesh 상수/로직 그대로 빌드
            Mesh mesh = ProceduralContainerMesh.Build();

            // 머티리얼 로드 (서브메시 0=Body, 1=Door, 2=Frame, 3=Castings)
            var materials = new Material[]
            {
                AssetDatabase.LoadAssetAtPath<Material>(MatBody),
                AssetDatabase.LoadAssetAtPath<Material>(MatDoor),
                AssetDatabase.LoadAssetAtPath<Material>(MatFrame),
                AssetDatabase.LoadAssetAtPath<Material>(MatCasting),
            };
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    Debug.LogWarning($"[ContainerFbxExporter] 머티리얼 슬롯 {i} 누락. 'Container/Build/Procedural Prefab' 메뉴 한 번 실행해서 머티리얼 먼저 생성 부탁드립니다.");
            }

            var go = new GameObject("Container_20ft");
            try
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = materials;

                var col = go.AddComponent<BoxCollider>();
                col.center = mesh.bounds.center;
                col.size   = mesh.bounds.size;

                ModelExporter.ExportObject(path, go);

                Debug.Log($"[ContainerFbxExporter] FBX 저장 완료: {path}");
                EditorUtility.DisplayDialog(
                    "FBX Export 완료",
                    $"파일: {path}\n" +
                    $"메시: {mesh.vertexCount} verts / 서브메시 {mesh.subMeshCount}\n" +
                    $"머티리얼: Body/Door/Frame/Castings",
                    "OK");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mesh);
            }
        }

        // [MenuItem("Container/Export All FBX")]   // 메뉴 숨김(사용자 요청) — 복구하려면 주석 해제
        public static void ExportAllFbx()
        {
            string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            int exported = 0;

            // ─── 1. SpawnContainers 의 5종 (Std20/Std40/HC40/Reefer20/Reefer40) ───
            var stdConfigs = new (SpawnContainers.ContainerType type, string fileName, Color color)[]
            {
                (SpawnContainers.ContainerType.Std20,    "Container_Std20",    new Color(0.72f, 0.19f, 0.18f)),
                (SpawnContainers.ContainerType.Std40,    "Container_Std40",    new Color(0.85f, 0.45f, 0.15f)),
                (SpawnContainers.ContainerType.HC40,     "Container_HC40",     new Color(0.85f, 0.45f, 0.15f)),
                (SpawnContainers.ContainerType.Reefer20, "Container_Reefer20", new Color(0.93f, 0.93f, 0.91f)),
                (SpawnContainers.ContainerType.Reefer40, "Container_Reefer40", new Color(0.93f, 0.93f, 0.91f)),
            };
            foreach (var cfg in stdConfigs)
            {
                var spec = SpawnContainers.MakeSpec(cfg.type, cfg.color, cfg.fileName);
                var go = SpawnContainers.BuildContainer(spec, litShader);
                string path = System.IO.Path.Combine(desktop, cfg.fileName + ".fbx");
                try
                {
                    ModelExporter.ExportObject(path, go);
                    Debug.Log($"[ExportAllFbx] {cfg.fileName} → {path}");
                    exported++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ExportAllFbx] {cfg.fileName} 실패: {e.Message}");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            // ─── 2. Procedural (메인 컨테이너) ───
            {
                Mesh mesh = ProceduralContainerMesh.Build("Container_Procedural");
                var materials = new Material[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>(MatBody),
                    AssetDatabase.LoadAssetAtPath<Material>(MatDoor),
                    AssetDatabase.LoadAssetAtPath<Material>(MatFrame),
                    AssetDatabase.LoadAssetAtPath<Material>(MatCasting),
                };
                var go = new GameObject("Container_Procedural");
                try
                {
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = materials;
                    var col = go.AddComponent<BoxCollider>();
                    col.center = mesh.bounds.center;
                    col.size   = mesh.bounds.size;

                    string path = System.IO.Path.Combine(desktop, "Container_Procedural.fbx");
                    ModelExporter.ExportObject(path, go);
                    Debug.Log($"[ExportAllFbx] Container_Procedural → {path}");
                    exported++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ExportAllFbx] Container_Procedural 실패: {e.Message}");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(mesh);
                }
            }

            EditorUtility.DisplayDialog(
                "Container Export All 완료",
                $"{exported}개 FBX 저장됨\n위치: {desktop}",
                "OK");
        }
    }
}
#endif

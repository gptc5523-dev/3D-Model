using UnityEngine;

public static class SpawnContainers
{
    public enum ContainerType { Std20, Std40, HC40, Reefer20, Reefer40 }

    const float CornerSize = 0.014f;

    public struct ContainerSpec
    {
        public ContainerType type;
        public float length;
        public float width;
        public float height;
        public string name;
        public Color color;
        public bool isReefer;
        public bool isHC;
    }

    // Spawn 메뉴는 모두 제거됨. 메인 컨테이너는 Container/Spawn/Procedural (1 unit) 하나만.
    // 아래 BuildContainer/MakeSpec 등은 Container/Export All FBX 에서 Std/HC/Reefer 5종 빌드용으로 유지.

    public static ContainerSpec MakeSpec(ContainerType type, Color color, string name)
    {
        var spec = new ContainerSpec
        {
            type = type,
            width = 0.10f,
            color = color,
            name = name,
            isReefer = type == ContainerType.Reefer20 || type == ContainerType.Reefer40,
            isHC = type == ContainerType.HC40
        };
        switch (type)
        {
            case ContainerType.Std20:    spec.length = 0.25f; spec.height = 0.106f; break;
            case ContainerType.Std40:    spec.length = 0.50f; spec.height = 0.106f; break;
            case ContainerType.HC40:     spec.length = 0.50f; spec.height = 0.119f; break;
            case ContainerType.Reefer20: spec.length = 0.25f; spec.height = 0.106f; break;
            case ContainerType.Reefer40: spec.length = 0.50f; spec.height = 0.106f; break;
        }
        return spec;
    }

    public static GameObject BuildContainer(ContainerSpec spec, Shader shader)
    {
        var root = new GameObject(spec.name);
        float hx = spec.length * 0.5f;
        float hy = spec.height * 0.5f;
        float hz = spec.width * 0.5f;

        var body = MakeChild(root.transform, "Body", PrimitiveType.Cube);
        body.transform.localScale = new Vector3(spec.length, spec.height, spec.width);
        ApplyMaterial(body, spec.color, shader, 0.55f, 0.35f);

        Color ribColor = MulColor(spec.color, 0.85f);
        int ribCount = Mathf.RoundToInt(9f * spec.length / 0.25f);
        float ribSpacing = spec.length / (ribCount + 1);
        for (int i = 1; i <= ribCount; i++)
        {
            float x = -hx + ribSpacing * i;
            AddBox(root.transform, "Rib_F_" + i,
                new Vector3(0.004f, spec.height * 0.85f, 0.003f),
                new Vector3(x, 0f, hz + 0.0015f),
                ribColor, shader, 0.5f, 0.3f);
            AddBox(root.transform, "Rib_B_" + i,
                new Vector3(0.004f, spec.height * 0.85f, 0.003f),
                new Vector3(x, 0f, -hz - 0.0015f),
                ribColor, shader, 0.5f, 0.3f);
        }

        Color trimColor = MulColor(spec.color, 0.55f);
        float t = 0.006f;
        AddBox(root.transform, "Trim_T_F", new Vector3(spec.length + t*2, t, t), new Vector3(0,  hy + t*0.5f,  hz + t*0.5f), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_T_B", new Vector3(spec.length + t*2, t, t), new Vector3(0,  hy + t*0.5f, -hz - t*0.5f), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_T_L", new Vector3(t, t, spec.width),        new Vector3(-hx - t*0.5f,  hy + t*0.5f, 0), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_T_R", new Vector3(t, t, spec.width),        new Vector3( hx + t*0.5f,  hy + t*0.5f, 0), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_B_F", new Vector3(spec.length + t*2, t, t), new Vector3(0, -hy - t*0.5f,  hz + t*0.5f), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_B_B", new Vector3(spec.length + t*2, t, t), new Vector3(0, -hy - t*0.5f, -hz - t*0.5f), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_B_L", new Vector3(t, t, spec.width),        new Vector3(-hx - t*0.5f, -hy - t*0.5f, 0), trimColor, shader, 0.6f, 0.25f);
        AddBox(root.transform, "Trim_B_R", new Vector3(t, t, spec.width),        new Vector3( hx + t*0.5f, -hy - t*0.5f, 0), trimColor, shader, 0.6f, 0.25f);

        Color cornerColor = new Color(0.13f, 0.13f, 0.13f);
        Vector3[] cornerPos = {
            new Vector3(-hx, -hy, -hz), new Vector3( hx, -hy, -hz),
            new Vector3(-hx, -hy,  hz), new Vector3( hx, -hy,  hz),
            new Vector3(-hx,  hy, -hz), new Vector3( hx,  hy, -hz),
            new Vector3(-hx,  hy,  hz), new Vector3( hx,  hy,  hz),
        };
        for (int i = 0; i < cornerPos.Length; i++)
        {
            var corner = MakeChild(root.transform, "Corner_" + i, PrimitiveType.Cube);
            corner.transform.localScale = Vector3.one * CornerSize;
            corner.transform.localPosition = cornerPos[i];
            ApplyMaterial(corner, cornerColor, shader, 0.75f, 0.2f);
        }

        Color slotColor = new Color(0.05f, 0.05f, 0.05f);
        float halfCorner = CornerSize * 0.5f;
        float slotEps = 0.0008f;
        float slotPatch = CornerSize * 0.55f;
        float slotThin = 0.0012f;
        for (int i = 0; i < cornerPos.Length; i++)
        {
            Vector3 c = cornerPos[i];
            float sx = Mathf.Sign(c.x);
            float sy = Mathf.Sign(c.y);
            float sz = Mathf.Sign(c.z);

            AddBox(root.transform, "CornerSlotX_" + i,
                new Vector3(slotThin, slotPatch * 0.55f, slotPatch),
                new Vector3(c.x + sx * (halfCorner + slotEps), c.y, c.z),
                slotColor, shader, 0.3f, 0.1f);
            AddBox(root.transform, "CornerSlotY_" + i,
                new Vector3(slotPatch, slotThin, slotPatch * 0.55f),
                new Vector3(c.x, c.y + sy * (halfCorner + slotEps), c.z),
                slotColor, shader, 0.3f, 0.1f);
            AddBox(root.transform, "CornerSlotZ_" + i,
                new Vector3(slotPatch, slotPatch * 0.55f, slotThin),
                new Vector3(c.x, c.y, c.z + sz * (halfCorner + slotEps)),
                slotColor, shader, 0.3f, 0.1f);
        }

        Color doorColor = MulColor(spec.color, 0.72f);
        float doorThick = 0.008f;
        float doorH = spec.height * 0.85f;
        float doorTotalW = spec.width * 0.88f;
        float doorGap = 0.0015f;
        float doorW = (doorTotalW - doorGap) * 0.5f;
        float doorCenterZ = (doorW + doorGap) * 0.5f;
        float doorX = hx + 0.004f;

        AddBox(root.transform, "Door_L",
            new Vector3(doorThick, doorH, doorW),
            new Vector3(doorX, 0f, -doorCenterZ),
            doorColor, shader, 0.55f, 0.3f);
        AddBox(root.transform, "Door_R",
            new Vector3(doorThick, doorH, doorW),
            new Vector3(doorX, 0f, doorCenterZ),
            doorColor, shader, 0.55f, 0.3f);

        Color hingeColor = new Color(0.16f, 0.16f, 0.16f);
        float[] hingeYs = { doorH * 0.42f, doorH * 0.16f, -doorH * 0.16f, -doorH * 0.42f };
        for (int d = -1; d <= 1; d += 2)
        {
            float hingeZ = d * (doorTotalW * 0.5f);
            string suffix = d > 0 ? "R" : "L";
            for (int hi = 0; hi < hingeYs.Length; hi++)
            {
                AddBox(root.transform, "Hinge_" + suffix + "_" + hi,
                    new Vector3(0.012f, 0.010f, 0.005f),
                    new Vector3(hx + 0.006f, hingeYs[hi], hingeZ),
                    hingeColor, shader, 0.85f, 0.45f);
            }
        }

        Color hwColor = new Color(0.22f, 0.22f, 0.22f);
        float[] barZ = {
            -doorCenterZ - doorW * 0.32f,
            -doorCenterZ + doorW * 0.32f,
             doorCenterZ - doorW * 0.32f,
             doorCenterZ + doorW * 0.32f
        };
        for (int b = 0; b < barZ.Length; b++)
        {
            float z = barZ[b];

            var bar = MakeChild(root.transform, "DoorBar_" + b, PrimitiveType.Cylinder);
            bar.transform.localScale = new Vector3(0.005f, spec.height * 0.42f, 0.005f);
            bar.transform.localPosition = new Vector3(hx + 0.009f, 0f, z);
            ApplyMaterial(bar, hwColor, shader, 0.85f, 0.45f);

            AddBox(root.transform, "DoorHandle_" + b,
                new Vector3(0.004f, 0.024f, 0.007f),
                new Vector3(hx + 0.013f, 0f, z),
                hwColor, shader, 0.85f, 0.45f);

            AddBox(root.transform, "HandleGuard_" + b,
                new Vector3(0.005f, 0.032f, 0.003f),
                new Vector3(hx + 0.011f, 0f, z + 0.0055f),
                hwColor, shader, 0.85f, 0.45f);

            AddBox(root.transform, "DoorLockTop_" + b,
                new Vector3(0.006f, 0.006f, 0.008f),
                new Vector3(hx + 0.009f,  spec.height * 0.42f * 0.55f, z),
                hwColor, shader, 0.85f, 0.45f);
            AddBox(root.transform, "DoorLockBot_" + b,
                new Vector3(0.006f, 0.006f, 0.008f),
                new Vector3(hx + 0.009f, -spec.height * 0.42f * 0.55f, z),
                hwColor, shader, 0.85f, 0.45f);
        }

        Color plateColor = new Color(0.92f, 0.92f, 0.88f);
        Color cscColor = new Color(0.16f, 0.16f, 0.16f);
        AddBox(root.transform, "IDPlate",
            new Vector3(0.0012f, 0.014f, 0.028f),
            new Vector3(doorX + doorThick * 0.5f + 0.0008f, doorH * 0.32f, doorCenterZ),
            plateColor, shader, 0.1f, 0.05f);
        AddBox(root.transform, "IDPlate_Text",
            new Vector3(0.0006f, 0.005f, 0.022f),
            new Vector3(doorX + doorThick * 0.5f + 0.0018f, doorH * 0.32f, doorCenterZ),
            cscColor, shader, 0.1f, 0.1f);
        AddBox(root.transform, "CSCPlate",
            new Vector3(0.0012f, 0.010f, 0.014f),
            new Vector3(doorX + doorThick * 0.5f + 0.0008f, doorH * 0.10f, doorCenterZ - 0.012f),
            cscColor, shader, 0.2f, 0.1f);

        if (spec.isHC)
        {
            Color stripeColor = new Color(0.95f, 0.78f, 0.10f);
            AddBox(root.transform, "HCStripe_F",
                new Vector3(spec.length * 0.92f, 0.005f, 0.0018f),
                new Vector3(0f, hy - 0.012f, hz + 0.0009f),
                stripeColor, shader, 0.3f, 0.3f);
            AddBox(root.transform, "HCStripe_B",
                new Vector3(spec.length * 0.92f, 0.005f, 0.0018f),
                new Vector3(0f, hy - 0.012f, -hz - 0.0009f),
                stripeColor, shader, 0.3f, 0.3f);
        }

        if (spec.isReefer)
        {
            BuildReeferUnit(root.transform, spec, shader);
        }

        return root;
    }

    static void BuildReeferUnit(Transform parent, ContainerSpec spec, Shader shader)
    {
        float hx = spec.length * 0.5f;
        float hy = spec.height * 0.5f;
        float unitDepth = 0.04f;
        float unitX = -hx - unitDepth * 0.5f;

        Color unitColor = new Color(0.78f, 0.78f, 0.80f);
        Color darkAccent = new Color(0.20f, 0.20f, 0.22f);
        Color ledRed = new Color(0.95f, 0.20f, 0.20f);
        Color ledGreen = new Color(0.20f, 0.95f, 0.30f);
        Color ledYellow = new Color(0.95f, 0.80f, 0.20f);
        Color boltColor = new Color(0.35f, 0.35f, 0.35f);

        AddBox(parent, "ReeferUnit_Body",
            new Vector3(unitDepth, spec.height * 0.92f, spec.width * 0.92f),
            new Vector3(unitX, 0f, 0f),
            unitColor, shader, 0.6f, 0.4f);

        float frontX = -hx - unitDepth - 0.0005f;
        float grilleW = spec.width * 0.62f;
        float grilleSpacing = 0.008f;
        for (int i = 0; i < 5; i++)
        {
            float y = (i - 2) * grilleSpacing - 0.004f;
            AddBox(parent, "ReeferGrille_" + i,
                new Vector3(0.0015f, 0.003f, grilleW),
                new Vector3(frontX, y, 0f),
                darkAccent, shader, 0.5f, 0.3f);
        }

        AddBox(parent, "ReeferLCD",
            new Vector3(0.0015f, 0.012f, 0.018f),
            new Vector3(frontX, hy * 0.55f, -spec.width * 0.18f),
            darkAccent, shader, 0.2f, 0.6f);
        AddBox(parent, "ReeferLCD_Text",
            new Vector3(0.0008f, 0.005f, 0.012f),
            new Vector3(frontX - 0.001f, hy * 0.55f, -spec.width * 0.18f),
            ledGreen, shader, 0.1f, 0.7f);

        float ledY = hy * 0.55f;
        float ledZ0 = spec.width * 0.15f;
        AddBox(parent, "ReeferLED_R",
            new Vector3(0.0008f, 0.003f, 0.003f),
            new Vector3(frontX - 0.0005f, ledY, ledZ0),
            ledRed, shader, 0.1f, 0.8f);
        AddBox(parent, "ReeferLED_G",
            new Vector3(0.0008f, 0.003f, 0.003f),
            new Vector3(frontX - 0.0005f, ledY, ledZ0 + 0.005f),
            ledGreen, shader, 0.1f, 0.8f);
        AddBox(parent, "ReeferLED_Y",
            new Vector3(0.0008f, 0.003f, 0.003f),
            new Vector3(frontX - 0.0005f, ledY, ledZ0 + 0.010f),
            ledYellow, shader, 0.1f, 0.8f);

        var cable = MakeChild(parent, "ReeferCable", PrimitiveType.Cylinder);
        cable.transform.localScale = new Vector3(0.003f, 0.018f, 0.003f);
        cable.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        cable.transform.localPosition = new Vector3(frontX - 0.009f, -hy * 0.55f, spec.width * 0.30f);
        ApplyMaterial(cable, darkAccent, shader, 0.3f, 0.4f);

        AddBox(parent, "ReeferPlug",
            new Vector3(0.006f, 0.008f, 0.008f),
            new Vector3(frontX - 0.020f, -hy * 0.55f, spec.width * 0.30f),
            darkAccent, shader, 0.4f, 0.5f);

        AddBox(parent, "ReeferHandle",
            new Vector3(0.025f, 0.004f, 0.005f),
            new Vector3(frontX - 0.005f, hy * 0.85f, 0f),
            darkAccent, shader, 0.7f, 0.4f);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int yi = 0; yi < 3; yi++)
            {
                float by = (yi - 1) * spec.height * 0.30f;
                AddBox(parent, "ReeferBolt_" + (side > 0 ? "R" : "L") + "_" + yi,
                    new Vector3(0.0015f, 0.0025f, 0.0025f),
                    new Vector3(frontX, by, side * spec.width * 0.40f),
                    boltColor, shader, 0.7f, 0.3f);
            }
        }
    }

    static GameObject AddBox(Transform parent, string name, Vector3 scale, Vector3 pos, Color color, Shader shader, float metallic, float smoothness)
    {
        var go = MakeChild(parent, name, PrimitiveType.Cube);
        go.transform.localScale = scale;
        go.transform.localPosition = pos;
        ApplyMaterial(go, color, shader, metallic, smoothness);
        return go;
    }

    static GameObject MakeChild(Transform parent, string name, PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void ApplyMaterial(GameObject go, Color color, Shader shader, float metallic, float smoothness)
    {
        var mr = go.GetComponent<MeshRenderer>();
        var mat = new Material(shader) { name = go.name + "_Mat" };
        bool urp = shader.name.Contains("Universal Render Pipeline");
        if (urp) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        mat.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        if (!urp) mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        mr.sharedMaterial = mat;
    }

    static Color MulColor(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
}

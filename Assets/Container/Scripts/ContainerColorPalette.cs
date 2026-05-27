using UnityEngine;

namespace ContainerProject
{
    /// <summary>
    /// 컨테이너 색상 풀. 실제 항만에서 자주 보이는 선사 컬러 기반.
    /// 머티리얼을 직접 생성하지 않고 색상만 정의 → 런타임에 MaterialPropertyBlock으로 적용.
    /// </summary>
    [CreateAssetMenu(fileName = "ContainerColorPalette", menuName = "Container/Color Palette")]
    public class ContainerColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct ColorEntry
        {
            public string name;
            [ColorUsage(showAlpha: false)] public Color color;
        }

        [SerializeField]
        ColorEntry[] colors = new ColorEntry[]
        {
            new ColorEntry { name = "White",          color = new Color(0.92f, 0.92f, 0.90f) },
            new ColorEntry { name = "Maersk Navy",    color = new Color(0.06f, 0.18f, 0.42f) },
            new ColorEntry { name = "Maersk Sky",     color = new Color(0.26f, 0.69f, 0.83f) },
            new ColorEntry { name = "MSC Beige",      color = new Color(0.85f, 0.78f, 0.58f) },
            new ColorEntry { name = "CMA CGM Red",    color = new Color(0.75f, 0.10f, 0.13f) },
            new ColorEntry { name = "Hapag Orange",   color = new Color(0.95f, 0.45f, 0.10f) },
            new ColorEntry { name = "Evergreen",      color = new Color(0.13f, 0.45f, 0.30f) },
            new ColorEntry { name = "ONE Magenta",    color = new Color(0.91f, 0.12f, 0.39f) },
            new ColorEntry { name = "COSCO Red",      color = new Color(0.83f, 0.18f, 0.18f) },
            new ColorEntry { name = "HMM Orange",     color = new Color(1.00f, 0.62f, 0.20f) },
            new ColorEntry { name = "Rust Brown",     color = new Color(0.45f, 0.30f, 0.22f) },
            new ColorEntry { name = "Steel Gray",     color = new Color(0.55f, 0.55f, 0.58f) }
        };

        public int Count => colors == null ? 0 : colors.Length;
        public ColorEntry Get(int index) => colors[index];

        public ColorEntry Random(System.Random rng = null)
        {
            rng ??= new System.Random();
            return colors[rng.Next(colors.Length)];
        }
    }
}

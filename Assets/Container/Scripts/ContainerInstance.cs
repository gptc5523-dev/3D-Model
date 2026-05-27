using TMPro;
using UnityEngine;

namespace ContainerProject
{
    /// <summary>
    /// 컨테이너 프리팹 인스턴스. 스폰 시점에 번호·색상을 적용한다.
    /// 메시는 자식 오브젝트에 들어가 있어야 하며, 번호 라벨은 TMP 텍스트로 구성한다.
    /// </summary>
    public class ContainerInstance : MonoBehaviour
    {
        [System.Serializable]
        public struct BodyRendererSlot
        {
            public Renderer renderer;
            [Tooltip("색상을 적용할 머티리얼 슬롯 인덱스. -1이면 renderer 전체에 적용.")]
            public int materialIndex;
        }

        [Header("외형 참조")]
        [Tooltip("본체 색상을 적용할 Renderer 슬롯들. 단일 MeshRenderer + 4 서브메시 구조에서는 slot=0(Body), slot=1(Door)만 등록.")]
        [SerializeField] BodyRendererSlot[] bodyRenderers;
        [Tooltip("컨테이너 측면/도어에 부착될 번호 텍스트들")]
        [SerializeField] TMP_Text[] idLabels;

        [Header("런타임 정보 (읽기 전용)")]
        [SerializeField] string containerId;
        [SerializeField] string displayId;
        [SerializeField] string colorName;

        public string ContainerId => containerId;
        public string DisplayId => displayId;
        public string ColorName => colorName;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color"); // Built-in 폴백

        public void ApplyRandom(ContainerColorPalette palette, System.Random rng = null)
        {
            rng ??= new System.Random();

            containerId = ContainerIdGenerator.Generate(rng);
            displayId = ContainerIdGenerator.FormatForDisplay(containerId);
            ApplyIdToLabels(displayId);

            if (palette != null && palette.Count > 0)
            {
                var entry = palette.Random(rng);
                colorName = entry.name;
                ApplyColor(entry.color);
            }
        }

        public void Apply(string id, Color color, string colorLabel = null)
        {
            containerId = id;
            displayId = ContainerIdGenerator.FormatForDisplay(id);
            colorName = colorLabel;
            ApplyIdToLabels(displayId);
            ApplyColor(color);
        }

        void ApplyIdToLabels(string text)
        {
            if (idLabels == null) return;
            foreach (var label in idLabels)
            {
                if (label != null) label.text = text;
            }
        }

        void ApplyColor(Color color)
        {
            if (bodyRenderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var slot in bodyRenderers)
            {
                if (slot.renderer == null) continue;
                if (slot.materialIndex < 0)
                {
                    slot.renderer.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, color);
                    mpb.SetColor(ColorId, color);
                    slot.renderer.SetPropertyBlock(mpb);
                }
                else
                {
                    slot.renderer.GetPropertyBlock(mpb, slot.materialIndex);
                    mpb.SetColor(BaseColorId, color);
                    mpb.SetColor(ColorId, color);
                    slot.renderer.SetPropertyBlock(mpb, slot.materialIndex);
                }
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.LoadingSpinner
{
    public class UILoadingSpinner : MonoBehaviour
    {
        private static readonly int COLOR01_SHADER_PROP = Shader.PropertyToID("_color01");
        private static readonly int FILL_HEAD_SHADER_PROP = Shader.PropertyToID("_fillHead");
        private static readonly int FILL_TAIL_SHADER_PROP = Shader.PropertyToID("_fillTail");

        public Image img;
        public Color color;
        public float head;
        public float tail;

        private void Awake()
        {
            InitializeMaterial();
        }

        private void Update()
        {
            UpdateValues();
        }

        private void InitializeMaterial()
        {
            // Create per spinner material instance
            img.material = new Material(img.material);
        }

        private void UpdateValues()
        {
            // Animate the material instance
            Material rendered = img.materialForRendering;
            if (!rendered) return;

            rendered.SetColor(COLOR01_SHADER_PROP, color);
            rendered.SetFloat(FILL_HEAD_SHADER_PROP, head);
            rendered.SetFloat(FILL_TAIL_SHADER_PROP, tail);
        }
    }
}

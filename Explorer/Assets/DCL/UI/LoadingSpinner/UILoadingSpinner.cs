using System;
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

        private Material material;

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
            material = img.maskable ? new Material(img.materialForRendering) : new Material(img.material);

            img.material = material;
        }

        private void UpdateValues()
        {
            if (!material) return;

            material.SetColor(COLOR01_SHADER_PROP, color);
            material.SetFloat(FILL_HEAD_SHADER_PROP, head);
            material.SetFloat(FILL_TAIL_SHADER_PROP, tail);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.LoadingSpinner
{
    public class UILoadingSpinner : MonoBehaviour
    {
        public Image img;
        public Color color;
        public float head;
        public float tail;

        private Material material;

        private void Start()
        {
            GetMaterial();
        }

        private void GetMaterial()
        {
            if (img.maskable) { material = img.materialForRendering; }
            else
            {
                material = new Material(img.material);
                img.material = material;
            }
        }

        private void Update()
        {
            SetValues();
        }

        public void SetValues()
        {
            if (material)
            {
                material.SetColor("_color01", color);
                material.SetFloat("_fillHead", head);
                material.SetFloat("_fillTail", tail);
            }
            else
            {
                GetMaterial();
                SetValues();
            }
        }
    }
}

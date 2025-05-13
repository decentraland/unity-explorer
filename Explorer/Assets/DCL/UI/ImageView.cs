using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ImageView : MonoBehaviour
    {
        [field: SerializeField]
        internal GameObject LoadingObject { get; private set; }

        [field: SerializeField]
        internal Image Image { get; private set; }

        public bool IsLoading
        {
            get => LoadingObject.activeSelf;
            set => LoadingObject.SetActive(value);
        }

        public bool ImageEnabled
        {
            set => Image.enabled = value;
        }

        public float Alpha
        {
            set
            {
                Color color = Image.color;
                Image.color = new Color(color.r, color.g, color.b, value);
            }
        }

        public void SetImage(Sprite sprite)
        {
            Image.enabled = true;
            Image.sprite = sprite;
            LoadingObject.SetActive(false);
        }

        public void SetColor(Color color) =>
            Image.color = color;

        public async UniTask FadeInAsync(float duration, CancellationToken ct)
        {
            Color color = Image.color;
            float t = 0f;
            float from = color.a;

            while (t < duration)
            {
                ct.ThrowIfCancellationRequested();
                Alpha = Mathf.Lerp(from, 1f, t / duration);
                t += Time.deltaTime;
                await UniTask.NextFrame(ct);
            }

            Alpha = 1f;
        }
    }
}

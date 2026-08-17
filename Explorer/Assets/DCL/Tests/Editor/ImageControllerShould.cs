using DCL.UI;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Tests.Editor
{
    public class ImageControllerShould
    {
        private GameObject go = null!;

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();

            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void KillColorTweenOnStopLoading()
        {
            go = new GameObject(nameof(ImageControllerShould), typeof(Image));
            var view = go.AddComponent<ImageView>();
            var image = go.GetComponent<Image>();

            view.Image = image;

            var controller = new ImageController(view, null!);

            image.color = new Color(0f, 0f, 0f, 0f);
            image.DOColor(Color.white, 10f);
            Assert.IsTrue(DOTween.IsTweening(image));

            controller.StopLoading();

            Assert.IsFalse(DOTween.IsTweening(image));
            Assert.AreEqual(1f, image.color.a, 0.001f);
        }
    }
}

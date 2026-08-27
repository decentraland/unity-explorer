using DCL.SyntheticInput.UiSimulation;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.SyntheticInput.Tests
{
    public class UiOcclusionShould
    {
        private readonly List<RaycastResult> raycastResults = new ();
        private readonly List<GameObject> createdObjects = new ();

        [TearDown]
        public void TearDown()
        {
            raycastResults.Clear();

            foreach (GameObject go in createdObjects)
                Object.DestroyImmediate(go);

            createdObjects.Clear();
        }

        private GameObject Create(string name, Transform? parent = null)
        {
            var go = new GameObject(name);

            if (parent != null)
                go.transform.SetParent(parent);
            else
                createdObjects.Add(go);

            return go;
        }

        private void SetTopHit(GameObject hit) =>
            raycastResults.Add(new RaycastResult { gameObject = hit });

        [Test]
        public void FailWhenNothingRaycastsAtThePoint()
        {
            GameObject target = Create("target");

            Assert.That(UiOcclusion.IsTopHitFor(target, raycastResults, out GameObject? blocker), Is.False);
            Assert.That(blocker, Is.Null);
        }

        [Test]
        public void PassWhenTheTargetItselfIsTheTopHit()
        {
            GameObject target = Create("target");
            SetTopHit(target);

            Assert.That(UiOcclusion.IsTopHitFor(target, raycastResults, out _), Is.True);
        }

        [Test]
        public void PassWhenTheTopHitIsInsideTheTarget()
        {
            GameObject target = Create("target");
            GameObject label = Create("label", target.transform);
            SetTopHit(label);

            Assert.That(UiOcclusion.IsTopHitFor(target, raycastResults, out _), Is.True);
        }

        [Test]
        public void PassWhenTheTopHitResolvesItsClickToTheTarget()
        {
            GameObject target = Create("target");
            target.AddComponent<Image>();
            Button button = target.AddComponent<Button>();
            Assert.That(button, Is.Not.Null);

            GameObject raycastableChild = Create("graphic", target.transform);
            raycastableChild.AddComponent<Image>();
            SetTopHit(raycastableChild);

            Assert.That(UiOcclusion.IsTopHitFor(target, raycastResults, out _), Is.True);
        }

        [Test]
        public void FailWithTheBlockerWhenAnotherElementCoversTheTarget()
        {
            GameObject target = Create("target");
            GameObject cover = Create("modal-cover");
            SetTopHit(cover);

            Assert.That(UiOcclusion.IsTopHitFor(target, raycastResults, out GameObject? blocker), Is.False);
            Assert.That(blocker, Is.EqualTo(cover));
        }
    }
}

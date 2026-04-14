using DCL.ECSComponents;
using DCL.SDKComponents.Tween;
using NUnit.Framework;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenSDKComponentHelperShould
    {
        [Test]
        public void ResolveMoveRotateScale_WhenAllFieldsProvided_UsesProvidedValues()
        {
            var go = new GameObject();
            go.transform.localPosition = new Vector3(1, 2, 3);
            go.transform.localRotation = Quaternion.Euler(10, 20, 30);
            go.transform.localScale = new Vector3(4, 5, 6);

            var moveRotateScale = new MoveRotateScale
            {
                PositionStart = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 0 },
                PositionEnd = new Decentraland.Common.Vector3 { X = 10, Y = 0, Z = 0 },
                RotationStart = new Decentraland.Common.Quaternion { X = 0, Y = 0, Z = 0, W = 1 },
                RotationEnd = new Decentraland.Common.Quaternion { X = 0, Y = 0.7071f, Z = 0, W = 0.7071f },
                ScaleStart = new Decentraland.Common.Vector3 { X = 1, Y = 1, Z = 1 },
                ScaleEnd = new Decentraland.Common.Vector3 { X = 2, Y = 2, Z = 2 }
            };

            TweenSDKComponentHelper.ResolveMoveRotateScale(moveRotateScale, go.transform, out ResolvedMoveRotateScale resolved);

            Assert.AreEqual(new Vector3(0, 0, 0), resolved.PositionStart);
            Assert.AreEqual(new Vector3(10, 0, 0), resolved.PositionEnd);
            Assert.AreEqual(new Vector3(1, 1, 1), resolved.ScaleStart);
            Assert.AreEqual(new Vector3(2, 2, 2), resolved.ScaleEnd);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResolveMoveRotateScale_WhenScaleOmitted_FillsScaleFromTransform()
        {
            var go = new GameObject();
            go.transform.localPosition = new Vector3(5, 5, 5);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(2, 3, 4);

            var moveRotateScale = new MoveRotateScale
            {
                PositionStart = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 0 },
                PositionEnd = new Decentraland.Common.Vector3 { X = 1, Y = 1, Z = 1 }
                // RotationStart, RotationEnd, ScaleStart, ScaleEnd left null
            };

            TweenSDKComponentHelper.ResolveMoveRotateScale(moveRotateScale, go.transform, out ResolvedMoveRotateScale resolved);

            Assert.AreEqual(new Vector3(0, 0, 0), resolved.PositionStart);
            Assert.AreEqual(new Vector3(1, 1, 1), resolved.PositionEnd);
            Assert.AreEqual(go.transform.localRotation, resolved.RotationStart);
            Assert.AreEqual(go.transform.localRotation, resolved.RotationEnd);
            Assert.AreEqual(new Vector3(2, 3, 4), resolved.ScaleStart, "Omitted scale should come from current transform");
            Assert.AreEqual(new Vector3(2, 3, 4), resolved.ScaleEnd, "Omitted scale should come from current transform");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResolveMoveRotateScale_WhenPositionOmitted_FillsPositionFromTransform()
        {
            var go = new GameObject();
            go.transform.localPosition = new Vector3(7, 8, 9);
            go.transform.localRotation = Quaternion.Euler(0, 90, 0);
            go.transform.localScale = new Vector3(1, 1, 1);

            var moveRotateScale = new MoveRotateScale
            {
                RotationStart = new Decentraland.Common.Quaternion { X = 0, Y = 0, Z = 0, W = 1 },
                RotationEnd = new Decentraland.Common.Quaternion { X = 0, Y = 0.7071f, Z = 0, W = 0.7071f }
                // PositionStart, PositionEnd, ScaleStart, ScaleEnd left null
            };

            TweenSDKComponentHelper.ResolveMoveRotateScale(moveRotateScale, go.transform, out ResolvedMoveRotateScale resolved);

            Assert.AreEqual(new Vector3(7, 8, 9), resolved.PositionStart);
            Assert.AreEqual(new Vector3(7, 8, 9), resolved.PositionEnd);
            Assert.AreEqual(new Vector3(1, 1, 1), resolved.ScaleStart);
            Assert.AreEqual(new Vector3(1, 1, 1), resolved.ScaleEnd);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GetEase_ReturnsLinear_ForEfLinear()
        {
            var ease = TweenSDKComponentHelper.GetEase(EasingFunction.EfLinear);
            Assert.AreEqual(DG.Tweening.Ease.Linear, ease);
        }

        [Test]
        public void GetEase_ReturnsDefaultLinear_ForUnknownEasing()
        {
            var ease = TweenSDKComponentHelper.GetEase((EasingFunction)(-1));
            Assert.AreEqual(DG.Tweening.Ease.Linear, ease);
        }
    }
}

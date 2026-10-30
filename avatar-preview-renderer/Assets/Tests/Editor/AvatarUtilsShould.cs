using System.Collections.Generic;
using System.Text.RegularExpressions;
using Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests
{
    public class AvatarUtilsShould
    {
        private GameObject _avatarHips;
        private GameObject _otherHips;
        private GameObject _copy;
        private SkinnedMeshRenderer _renderer;
        private Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _avatarHips = new GameObject("Avatar_Hips");
            _otherHips = new GameObject("Avatar_Hips");

            // A copy of a worn item, built inactive and still skinned to the avatar's own bone.
            _copy = new GameObject("copy");
            _copy.SetActive(false);

            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(_copy.transform);
            _renderer = mesh.AddComponent<SkinnedMeshRenderer>();

            // A renderer keeps its bones only with a mesh skinned to them: one triangle on one bone.
            var weight = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            _mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
                boneWeights = new[] { weight, weight, weight },
                bindposes = new[] { Matrix4x4.identity },
            };
            _renderer.sharedMesh = _mesh;
            _renderer.bones = new[] { _avatarHips.transform };
            _renderer.rootBone = _avatarHips.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_copy);
            Object.DestroyImmediate(_avatarHips);
            Object.DestroyImmediate(_otherHips);
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void BindAnInactiveCopyToTheGivenSkeleton()
        {
            // Arrange: the setup gives the copy its own materials, which the Editor reports as a leak.
            LogAssert.Expect(LogType.Error, new Regex("Instantiating material"));

            // Act
            AvatarUtils.SetupWearable(_copy, Colors(), new List<Renderer>(), _otherHips.transform,
                new[] { _otherHips.transform }, includeInactive: true);

            // Assert
            Assert.AreSame(_otherHips.transform, _renderer.rootBone);
            Assert.AreSame(_otherHips.transform, _renderer.bones[0]);
        }

        private static AvatarColors Colors() => new(Color.white, Color.black, Color.grey);
    }
}

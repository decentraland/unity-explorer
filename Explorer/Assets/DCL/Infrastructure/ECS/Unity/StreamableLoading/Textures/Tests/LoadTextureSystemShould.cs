using DCL.Profiles;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class GetTextureIntentionShould
    {
        [Test]
        public void NotBeEqualWhenSameUserIdButDifferentFaceSnapshotUrl()
        {
            var intentionA = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test", faceSnapshotUrl: "https://cdn.example.com/face_v1.png");
            var intentionB = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test", faceSnapshotUrl: "https://cdn.example.com/face_v2.png");

            Assert.IsFalse(intentionA.Equals(intentionB));
            Assert.AreNotEqual(intentionA.GetHashCode(), intentionB.GetHashCode());
        }

        [Test]
        public void BeEqualWhenSameUserIdWithoutFaceSnapshotUrl()
        {
            var intentionA = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test");
            var intentionB = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test");

            Assert.IsTrue(intentionA.Equals(intentionB));
            Assert.AreEqual(intentionA.GetHashCode(), intentionB.GetHashCode());
        }

        [Test]
        public void BeEqualWhenSameUserIdAndSameFaceSnapshotUrl()
        {
            var intentionA = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test", faceSnapshotUrl: "https://cdn.example.com/face.png");
            var intentionB = new GetTextureIntention("user1", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "Test", faceSnapshotUrl: "https://cdn.example.com/face.png");

            Assert.IsTrue(intentionA.Equals(intentionB));
            Assert.AreEqual(intentionA.GetHashCode(), intentionB.GetHashCode());
        }
    }

    [TestFixture]
    public class LoadTextureSystemShould : LoadSystemBaseShould<LoadTextureSystem, TextureData, GetTextureIntention>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Images/non_existing.png"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetTextureIntention CreateSuccessIntention() =>
            new (successPath, string.Empty, TextureWrapMode.MirrorOnce, FilterMode.Trilinear, TextureType.Albedo, reportSource: "Test");

        protected override GetTextureIntention CreateNotFoundIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(failPath) };

        protected override GetTextureIntention CreateWrongTypeIntention() =>
            new () { CommonArguments = new CommonLoadingArguments(wrongTypePath) };

        protected override LoadTextureSystem CreateSystem() =>
            new (world, cache, TestWebRequestController.INSTANCE, IDiskCache<TextureData>.Null.INSTANCE,
                Substitute.For<IProfileRepository>());

        protected override void AssertSuccess(TextureData data)
        {
            Texture2D asset = data.EnsureTexture2D();

            Assert.AreEqual(TextureWrapMode.MirrorOnce, asset.wrapMode);
            Assert.AreEqual(FilterMode.Trilinear, asset.filterMode);
        }
    }
}

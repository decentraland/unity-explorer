using Arch.Core;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.GLTF;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class GetSceneEmoteFromLocalSceneIntentionShould
    {
        private World world = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void MaskFullBody_RequestsLegacyClips()
        {
            GetGLTFIntention gltfIntention = CreatePromiseAndGetGLTFIntention(AvatarEmoteMask.AemFullBody);

            Assert.IsFalse(gltfIntention.MecanimAnimationClips,
                "FullBody scene emotes must load as Legacy animations to avoid GLTFast SetCurve failures in builds.");
        }

        [Test]
        public void MaskUpperBody_RequestsMecanimClips()
        {
            GetGLTFIntention gltfIntention = CreatePromiseAndGetGLTFIntention(AvatarEmoteMask.AemUpperBody);

            Assert.IsTrue(gltfIntention.MecanimAnimationClips,
                "Masked scene emotes must load as Mecanim clips because AnimatorOverrideController + layer masks are Mecanim-only.");
        }

        [Test]
        public void EqualityDistinguishesMask()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();

            var fullBody = new GetSceneEmoteFromLocalSceneIntention(sceneData, "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemFullBody);
            var upperBody = new GetSceneEmoteFromLocalSceneIntention(sceneData, "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemUpperBody);

            Assert.IsFalse(fullBody.Equals(upperBody),
                "Intentions with identical hash/path/bodyShape but different masks must not be equal to prevent promise-cache cross-contamination.");
        }

        private GetGLTFIntention CreatePromiseAndGetGLTFIntention(AvatarEmoteMask mask)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            IEmote emote = Substitute.For<IEmote>();

            var intention = new GetSceneEmoteFromLocalSceneIntention(sceneData, "emote.glb", "hash", BodyShape.MALE, loop: false, mask);

            intention.CreateAndAddPromiseToWorld(world, PartitionComponent.TOP_PRIORITY, null, emote);

            var query = new QueryDescription().WithAll<GltfPromise>();
            GltfPromise? capturedPromise = null;

            world.Query(query, (ref GltfPromise p) => capturedPromise = p);

            Assert.IsNotNull(capturedPromise, "CreateAndAddPromiseToWorld should create an entity carrying a GltfPromise.");

            return capturedPromise!.Value.LoadingIntention;
        }
    }
}

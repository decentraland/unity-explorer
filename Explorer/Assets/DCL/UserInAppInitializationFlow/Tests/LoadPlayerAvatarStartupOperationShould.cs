using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class LoadPlayerAvatarStartupOperationShould
    {
        private World world = null!;
        private ILoadingStatus loadingStatus = null!;
        private SelfProfile selfProfile = null!;
        private ObjectProxy<AvatarBase> avatarBaseProxy = null!;
        private GameObject avatarGameObject = null!;
        private CancellationTokenSource cts = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(0.5f);

            selfProfile = Substitute.For<SelfProfile>();

            avatarGameObject = new GameObject("AvatarBase");
            AvatarBase avatarBase = avatarGameObject.AddComponent<AvatarBase>();
            avatarBaseProxy = new ObjectProxy<AvatarBase>();
            avatarBaseProxy.SetObject(avatarBase);

            // Pre-cancel so UniTask.WaitWhile exits immediately (already-cancelled token fast path)
            cts = new CancellationTokenSource();
            cts.Cancel();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatarGameObject);
            world.Dispose();
            cts.Dispose();
        }

        [Test]
        public void AddsProfileToPlayerEntityWhenNotPresent()
        {
            var profile = Profile.NewRandomProfile("0x8a5b1234567890abcdef1234567890abcdef1234");
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromOk(profile)));

            Entity playerEntity = world.Create();
            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);
            operation.ExecuteAsync(MakeParams(playerEntity), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(world.Has<Profile>(playerEntity));
            Profile onEntity = world.Get<Profile>(playerEntity);
            Assert.AreNotSame(profile, onEntity, "the entity owns a copy, not the model's instance");
            Assert.IsTrue(onEntity.IsSameProfile(profile));
        }

        [Test]
        public void SetsProfileOnPlayerEntityWhenAlreadyPresent()
        {
            var oldProfile = Profile.NewRandomProfile("0x1a2b1234567890abcdef1234567890abcdef1234");
            var newProfile = Profile.NewRandomProfile("0x3c4d1234567890abcdef1234567890abcdef1234");
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromOk(newProfile)));

            oldProfile.ClearLinks();
            Entity playerEntity = world.Create();
            world.Add(playerEntity, oldProfile);

            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);
            operation.ExecuteAsync(MakeParams(playerEntity), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(world.Get<Profile>(playerEntity).IsSameProfile(newProfile));
            Assert.IsNull(oldProfile.Links, "the replaced instance is disposed");
        }

        [Test]
        public void ReportProfileNotFoundWithoutAddingProfileWhenTheProfileIsNotDeployed()
        {
            // Arrange
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromError(ProfileReadError.NotFound)));

            Entity playerEntity = world.Create();
            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);

            // Act
            EnumResult<TaskError> result = operation.ExecuteAsync(MakeParams(playerEntity), cts.Token).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(result.Success, "A missing own profile must fail the operation instead of continuing with a null profile");
            Assert.AreEqual(TaskError.MessageError, result.Error!.Value.State);
            Assert.IsInstanceOf<ProfileNotFoundException>(result.Error.Value.Exception, "The flow recognizes the missing profile by its exception to show the explicit popup");
            Assert.IsFalse(world.Has<Profile>(playerEntity), "A null Profile component would make every profile system throw each frame");
        }

        [Test]
        public void ReportATimeoutWhenTheProfileFetchFails()
        {
            // Arrange
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromError(ProfileReadError.FetchFailed)));

            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);

            // Act
            EnumResult<TaskError> result = operation.ExecuteAsync(MakeParams(world.Create()), cts.Token).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(TaskError.Timeout, result.Error!.Value.State);
            Assert.IsInstanceOf<ProfileFetchFailedException>(result.Error.Value.Exception, "The flow keeps a failed own-profile step over the LiveKit result by its exception");
        }

        [Test]
        public void ReportCancellationWhenTheProfileReadIsCancelled()
        {
            // Arrange
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(ProfileReadResult.FromError(ProfileReadError.Cancelled)));

            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);

            // Act
            EnumResult<TaskError> result = operation.ExecuteAsync(MakeParams(world.Create()), cts.Token).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(TaskError.Cancelled, result.Error!.Value.State);
        }

        private IStartupOperation.Params MakeParams(Entity playerEntity)
        {
            var flowParams = new UserInAppInitializationFlowParameters(
                showAuthentication: false,
                showLoading: false,
                loadSource: IUserInAppInitializationFlow.LoadSource.StartUp,
                world: world,
                playerEntity: playerEntity);

            return new IStartupOperation.Params(AsyncLoadProcessReport.Create(cts.Token), flowParams);
        }
    }
}

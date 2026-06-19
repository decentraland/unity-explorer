using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.Utilities;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class LoadPlayerAvatarStartupOperationShould
    {
        private World world;
        private ILoadingStatus loadingStatus;
        private ISelfProfile selfProfile;
        private ObjectProxy<AvatarBase> avatarBaseProxy;
        private GameObject avatarGameObject;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(0.5f);

            selfProfile = Substitute.For<ISelfProfile>();

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
            var profile = new Profile();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<Profile?>(profile));

            Entity playerEntity = world.Create();
            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);
            operation.ExecuteAsync(MakeParams(playerEntity), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(world.Has<Profile>(playerEntity));
            Assert.AreSame(profile, world.Get<Profile>(playerEntity));
        }

        [Test]
        public void SetsProfileOnPlayerEntityWhenAlreadyPresent()
        {
            var oldProfile = new Profile();
            var newProfile = new Profile();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult<Profile?>(newProfile));

            Entity playerEntity = world.Create();
            world.Add(playerEntity, oldProfile);

            var operation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, avatarBaseProxy);
            operation.ExecuteAsync(MakeParams(playerEntity), cts.Token).GetAwaiter().GetResult();

            Assert.AreSame(newProfile, world.Get<Profile>(playerEntity));
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

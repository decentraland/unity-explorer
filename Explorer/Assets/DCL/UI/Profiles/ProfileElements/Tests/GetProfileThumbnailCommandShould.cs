using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.UI.ProfileElements.Tests
{
    public class GetProfileThumbnailCommandShould
    {
        private const string USER_ID = "0x0000000000000000000000000000000000000001";

        private ISpriteCache spriteCache = null!;
        private GetProfileThumbnailCommand command = null!;
        private ReactiveProperty<ProfileThumbnailViewModel> property = null!;
        private Sprite fallback = null!;

        [SetUp]
        public void SetUp()
        {
            spriteCache = Substitute.For<ISpriteCache>();

            var profileRepository = new ProfileRepositoryWrapper(Substitute.For<IProfileRepository>(), Substitute.For<IProfileCache>(), spriteCache, Substitute.For<IWeb3IdentityCache>());
            command = new GetProfileThumbnailCommand(profileRepository);

            property = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());
            fallback = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fallback.texture);
            Object.DestroyImmediate(fallback);
        }

        [Test]
        public async Task ShowTheFallbackWithoutAskingTheCacheWhenThereIsNoFaceSnapshotUrl()
        {
            // Act
            await command.ExecuteAsync(property, fallback, USER_ID, string.Empty, CancellationToken.None);

            // Assert
            Assert.That(property.Value.ThumbnailState, Is.EqualTo(ProfileThumbnailViewModel.State.Fallback));
            Assert.That(property.Value.Sprite, Is.SameAs(fallback));
            spriteCache.DidNotReceiveWithAnyArgs().GetCachedSprite(null!);
            _ = spriteCache.DidNotReceiveWithAnyArgs().GetSpriteAsync(null!, false, null, CancellationToken.None);
        }

        [Test]
        public async Task ReportAnErrorWhenThereIsNoFaceSnapshotUrlNorFallback()
        {
            // Act
            await command.ExecuteAsync(property, null, USER_ID, string.Empty, CancellationToken.None);

            // Assert
            Assert.That(property.Value.ThumbnailState, Is.EqualTo(ProfileThumbnailViewModel.State.Error));
        }
    }
}

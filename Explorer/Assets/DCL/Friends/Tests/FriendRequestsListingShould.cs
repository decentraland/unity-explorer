using DCL.Profiles;
using DCL.Profiles.Self;
using NUnit.Framework;

namespace DCL.Friends.Tests
{
    public class FriendRequestsListingShould
    {
        [Test]
        public void SkipTheServerWhileTheOwnProfileIsNotDeployed()
        {
            // Act
            bool hasNoDeployedProfile = RPCFriendsService.HasNoDeployedProfile(ProfileReadResult.FromError(ProfileReadError.NotFound));

            // Assert
            Assert.That(hasNoDeployedProfile, Is.True);
        }

        [Test]
        public void AskTheServerWhenTheOwnProfileIsKnown()
        {
            // Act
            bool hasNoDeployedProfile = RPCFriendsService.HasNoDeployedProfile(ProfileReadResult.FromOk(Profile.NewRandomProfile()));

            // Assert
            Assert.That(hasNoDeployedProfile, Is.False);
        }

        [Test]
        public void NotMistakeAFailedReadForAMissingProfile()
        {
            // Act
            bool hasNoDeployedProfile = RPCFriendsService.HasNoDeployedProfile(ProfileReadResult.FromError(ProfileReadError.FetchFailed));

            // Assert
            Assert.That(hasNoDeployedProfile, Is.False);
        }
    }
}

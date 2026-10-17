using Decentraland.SocialService;
using Decentraland.SocialService.V2;
using NUnit.Framework;

namespace DCL.Friends.Tests
{
    public class FriendRequestsPageShould
    {
        [Test]
        public void NotRequireTheOwnProfileWhenThePageHasNoRequests()
        {
            // Arrange
            var response = new PaginatedFriendshipRequestsResponse { Requests = new FriendshipRequests() };

            // Act
            bool requiresOwnProfile = RPCFriendsService.RequiresOwnProfile(response);

            // Assert
            Assert.That(requiresOwnProfile, Is.False);
        }

        [Test]
        public void RequireTheOwnProfileWhenThePageHasRequests()
        {
            // Arrange
            var response = new PaginatedFriendshipRequestsResponse
            {
                Requests = new FriendshipRequests { Requests = { new FriendshipRequestResponse { Id = "req-1" } } },
            };

            // Act
            bool requiresOwnProfile = RPCFriendsService.RequiresOwnProfile(response);

            // Assert
            Assert.That(requiresOwnProfile, Is.True);
        }

        [Test]
        public void NotRequireTheOwnProfileWhenTheServerFailed()
        {
            // Arrange
            var response = new PaginatedFriendshipRequestsResponse { InternalServerError = new InternalServerError { Message = "boom" } };

            // Act
            bool requiresOwnProfile = RPCFriendsService.RequiresOwnProfile(response);

            // Assert
            Assert.That(requiresOwnProfile, Is.False);
        }
    }
}

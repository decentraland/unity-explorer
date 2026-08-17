using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Friends.Tests
{
    /// <summary>
    ///     The Accept-path call sites (<c>PassportController</c>, <c>CommunityPlayerEntryContextMenu</c>,
    ///     <c>GenericUserProfileContextMenuController</c>) await <c>AcceptFriendshipAsync</c> inside a
    ///     fire-and-forget <c>UniTaskVoid</c>: a rejection that escapes the await is never observed and stops
    ///     the follow-up statement that re-syncs the interaction state. <c>SuppressToResultAsync</c> is what
    ///     turns that rejection into a reported, observed result carrying the server's reason.
    /// </summary>
    public class FriendsAcceptPathRejectionSuppressionShould
    {
        private const string FRIEND_ID = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";
        private const string SERVER_MESSAGE = "The friendship action is not valid for the current friendship status";

        [Test]
        public async Task ObserveTypedRejectionAsAnErrorResultInsteadOfPropagatingIt()
        {
            using var reportScope = new MockedReportScope();

            var friendsService = Substitute.For<IFriendsService>();

            friendsService.AcceptFriendshipAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                          .Returns(UniTask.FromException(new FriendshipActionRejectedException(SERVER_MESSAGE)));

            EnumResult<TaskError> result = await friendsService.AcceptFriendshipAsync(FRIEND_ID, CancellationToken.None)
                                                               .SuppressToResultAsync(ReportCategory.FRIENDS);

            Assert.That(result.Success, Is.False,
                "the rejection must surface as an observed error result, not be treated as success");

            Assert.That(result.Error!.Value.Message, Is.EqualTo(SERVER_MESSAGE),
                "the server's rejection reason must reach the observed result, not be swallowed");
        }
    }
}

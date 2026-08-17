using Decentraland.SocialService;
using Decentraland.SocialService.V2;
using NUnit.Framework;
using System;

namespace DCL.Friends.Tests
{
    /// <summary>
    ///     A server-side friendship state-machine rejection carries an explanatory <c>Message</c> that has to
    ///     survive into the thrown exception, so the Accept/Reject/Cancel call sites can surface it instead of
    ///     the bare oneof case name.
    /// </summary>
    public class RPCFriendsServiceUnwrapUpsertFriendshipResponseShould
    {
        private const string INVALID_ACTION_MESSAGE = "The friendship action is not valid for the current friendship status";
        private const string INTERNAL_ERROR_MESSAGE = "profile not found";

        [Test]
        public void PassThroughAcceptedResponse()
        {
            var accepted = new UpsertFriendshipResponse.Types.Accepted();
            var response = new UpsertFriendshipResponse { Accepted = accepted };

            UpsertFriendshipResponse.Types.Accepted result = RPCFriendsService.UnwrapUpsertFriendshipResponse(response);

            Assert.That(result, Is.SameAs(accepted));
        }

        [Test]
        public void ThrowTypedRejectionWithServerMessagePreservedOnInvalidFriendshipAction()
        {
            var response = new UpsertFriendshipResponse
            {
                InvalidFriendshipAction = new InvalidFriendshipAction { Message = INVALID_ACTION_MESSAGE },
            };

            var thrown = Assert.Throws<FriendshipActionRejectedException>(() => RPCFriendsService.UnwrapUpsertFriendshipResponse(response));

            Assert.That(thrown.Message, Is.EqualTo(INVALID_ACTION_MESSAGE),
                "the server's explanatory message is the whole point of the typed rejection; a generic exception discards it");
        }

        [Test]
        public void FallBackToTheResponseCaseWhenTheRejectionCarriesNoMessage()
        {
            var response = new UpsertFriendshipResponse { InvalidFriendshipAction = new InvalidFriendshipAction() };

            var thrown = Assert.Throws<FriendshipActionRejectedException>(() => RPCFriendsService.UnwrapUpsertFriendshipResponse(response));

            Assert.That(thrown.Message, Is.EqualTo(nameof(UpsertFriendshipResponse.ResponseOneofCase.InvalidFriendshipAction)),
                "proto3 string fields default to empty, never null, so an unset server message must still name the rejection");
        }

        [Test]
        public void IncludeServerMessageOnInternalServerError()
        {
            var response = new UpsertFriendshipResponse
            {
                InternalServerError = new InternalServerError { Message = INTERNAL_ERROR_MESSAGE },
            };

            var thrown = Assert.Throws<Exception>(() => RPCFriendsService.UnwrapUpsertFriendshipResponse(response));

            Assert.That(thrown.Message, Does.Contain(INTERNAL_ERROR_MESSAGE));
        }

        [Test]
        public void KeepGenericMessageOnUnhandledResponseCase()
        {
            var response = new UpsertFriendshipResponse();

            var thrown = Assert.Throws<Exception>(() => RPCFriendsService.UnwrapUpsertFriendshipResponse(response));

            Assert.That(thrown.Message, Is.EqualTo($"Cannot update friendship {response.ResponseCase}"));
        }
    }
}

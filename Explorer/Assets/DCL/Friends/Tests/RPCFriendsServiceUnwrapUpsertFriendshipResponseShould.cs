using Decentraland.SocialService;
using Decentraland.SocialService.V2;
using NUnit.Framework;
using System;
using System.Reflection;

namespace DCL.Friends.Tests
{
    /// <summary>
    ///     Regression coverage for unity-explorer#9618 — a server-side friendship-state-machine rejection
    ///     (<c>InvalidFriendshipAction</c>, social-service-ea PR #427, "fix: enforce friendship state machine
    ///     in upsertFriendship") reaching Sentry as a bare <see cref="Exception" /> with the server's
    ///     explanatory message discarded. At the pin, <c>RPCFriendsService.UpdateFriendshipAsync</c> collapses
    ///     every non-<c>Accepted</c> <see cref="UpsertFriendshipResponse" /> case into
    ///     <c>throw new Exception($"Cannot update friendship {response.ResponseCase}")</c>
    ///     (<c>RPCFriendsService.cs:590</c>).
    ///     <para>
    ///     The fix extracts the oneof-unwrap into <c>internal static
    ///     RPCFriendsService.UnwrapUpsertFriendshipResponse(UpsertFriendshipResponse)</c> and adds a typed,
    ///     public <c>FriendshipActionRejectedException</c> (namespace <c>DCL.Friends</c>) that carries
    ///     the server's <c>Message</c>. Both are new production API introduced by the fix, so this file looks
    ///     them up via reflection instead of referencing them directly: on the pre-fix tree neither the method
    ///     nor the type exists, and a direct reference would fail to *compile* — which, because this file lives
    ///     in the shared <c>DCL.EditMode.Tests</c> assembly (via <c>DCL.Friends.Tests.asmref</c>), would break
    ///     every other EditMode test in the project, not just these. Reflection turns that into an explicit,
    ///     readable "seam missing" assertion failure on the pre-fix tree instead (same reflection-shim idiom as
    ///     <c>DeepLinkBridgePublishRoundTripShould</c>, ue9520).
    ///     </para>
    /// </summary>
    public class RPCFriendsServiceUnwrapUpsertFriendshipResponseShould
    {
        private const string UNWRAP_METHOD_NAME = "UnwrapUpsertFriendshipResponse";
        private const string REJECTED_EXCEPTION_TYPE_NAME = "FriendshipActionRejectedException";

        private const string INVALID_ACTION_MESSAGE = "The friendship action is not valid for the current friendship status";
        private const string INTERNAL_ERROR_MESSAGE = "profile not found";

        // Null on a pre-fix tree (the method does not exist yet) — every [Test] asserts on this first so the
        // failure is an explicit, readable assertion rather than a build error blocking the whole shared
        // DCL.EditMode.Tests assembly.
        private static readonly MethodInfo? UNWRAP_METHOD =
            typeof(RPCFriendsService).GetMethod(UNWRAP_METHOD_NAME, BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void PassThroughAcceptedResponse()
        {
            Assert.That(UNWRAP_METHOD, Is.Not.Null, SeamMissingMessage());

            var accepted = new UpsertFriendshipResponse.Types.Accepted();
            var response = new UpsertFriendshipResponse { Accepted = accepted };

            var result = (UpsertFriendshipResponse.Types.Accepted) Invoke(response);

            Assert.That(result, Is.SameAs(accepted), "the Accepted case must pass through unchanged, exactly as the pre-fix switch did");
        }

        [Test]
        public void ThrowTypedRejectionWithServerMessagePreservedOnInvalidFriendshipAction()
        {
            Assert.That(UNWRAP_METHOD, Is.Not.Null, SeamMissingMessage());

            var response = new UpsertFriendshipResponse
            {
                InvalidFriendshipAction = new InvalidFriendshipAction { Message = INVALID_ACTION_MESSAGE },
            };

            Exception? thrown = InvokeExpectingThrow(response);

            Assert.That(thrown, Is.Not.Null, "InvalidFriendshipAction must throw, not return a value");

            Assert.That(thrown!.GetType().Name, Is.EqualTo(REJECTED_EXCEPTION_TYPE_NAME),
                "the pin's bare 'throw new Exception($\"Cannot update friendship {response.ResponseCase}\")' loses the " +
                $"server reason and the response-case type; the fix must throw the typed {REJECTED_EXCEPTION_TYPE_NAME} " +
                $"instead — got {thrown.GetType().FullName}");

            Assert.That(thrown.Message, Does.Contain(INVALID_ACTION_MESSAGE),
                "the server's explanatory Message (InvalidFriendshipAction.Message) must survive into the exception — " +
                "the pin's generic exception discards it entirely, logging only the literal string " +
                "'Cannot update friendship InvalidFriendshipAction' with no server payload");
        }

        [Test]
        public void IncludeServerMessageOnInternalServerError()
        {
            Assert.That(UNWRAP_METHOD, Is.Not.Null, SeamMissingMessage());

            var response = new UpsertFriendshipResponse
            {
                InternalServerError = new InternalServerError { Message = INTERNAL_ERROR_MESSAGE },
            };

            Exception? thrown = InvokeExpectingThrow(response);

            Assert.That(thrown, Is.Not.Null);

            Assert.That(thrown!.GetType().Name, Is.Not.EqualTo(REJECTED_EXCEPTION_TYPE_NAME),
                "InternalServerError is not a friendship-action rejection; it must stay a generic Exception, not the " +
                "typed FriendshipActionRejectedException");

            Assert.That(thrown.Message, Does.Contain(INTERNAL_ERROR_MESSAGE),
                "the fix folds the server's InternalServerError.Message into the exception text — the pin's message " +
                "is just the bare oneof case name with no server detail");
        }

        [Test]
        public void KeepGenericMessageOnUnhandledResponseCase()
        {
            Assert.That(UNWRAP_METHOD, Is.Not.Null, SeamMissingMessage());

            var response = new UpsertFriendshipResponse(); // ResponseCase == None — no oneof field set

            Exception? thrown = InvokeExpectingThrow(response);

            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown!.Message, Is.EqualTo($"Cannot update friendship {response.ResponseCase}"),
                "the default/unmatched-case message must stay byte-identical to the pre-fix behavior");
        }

        private static object Invoke(UpsertFriendshipResponse response)
        {
            try
            {
                return UNWRAP_METHOD!.Invoke(null, new object[] { response })!;
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException!;
            }
        }

        private static Exception? InvokeExpectingThrow(UpsertFriendshipResponse response)
        {
            try
            {
                Invoke(response);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private static string SeamMissingMessage() =>
            $"seam missing: RPCFriendsService.{UNWRAP_METHOD_NAME} does not exist on this tree — the client-side fix " +
            "for unity-explorer#9618 (a server InvalidFriendshipAction rejection surfacing as a generic " +
            "System.Exception with the server's Message discarded) is absent.";
    }
}

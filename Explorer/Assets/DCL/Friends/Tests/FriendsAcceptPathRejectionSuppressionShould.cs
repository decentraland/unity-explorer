using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Friends.Tests
{
    /// <summary>
    ///     Regression coverage for unity-explorer#9618's three "bare await" Accept-path call sites —
    ///     <c>GenericUserProfileContextMenuController.cs:387</c>, <c>CommunityPlayerEntryContextMenu.cs:276</c>,
    ///     <c>PassportController.cs:1157</c> (all three: <c>AcceptFriendRequestThenChangeInteractionStatusAsync</c>,
    ///     a local <c>async UniTaskVoid</c> function invoked via <c>.Forget()</c>).
    ///     <para>
    ///     Pre-fix, all three run <c>await friendService.AcceptFriendshipAsync(userId, ct);</c> with no
    ///     exception handling. On a server rejection the exception is never observed (fire-and-forget
    ///     <c>UniTaskVoid</c>), the statement after the await never runs (nothing at the two context-menu
    ///     sites; <c>ShowFriendshipInteraction()</c> at the Passport site, which is what leaves the passport's
    ///     Accept button stuck and invites the poisoned retry the report describes), and the failure is
    ///     invisible except as an unobserved-task-exception.
    ///     </para>
    ///     <para>
    ///     The fix appends <c>.SuppressToResultAsync(ReportCategory.FRIENDS)</c> at all three sites — the same
    ///     idiom every other Accept/Cancel/Reject/Delete call site in the codebase already uses. This test
    ///     exercises that exact idiom in isolation: a mocked <see cref="IFriendsService" /> whose
    ///     <c>AcceptFriendshipAsync</c> throws the fix's new <c>FriendshipActionRejectedException</c>, piped
    ///     through the real, unchanged <c>DCL.Utilities.Extensions.UniTaskExtensions.SuppressToResultAsync</c>.
    ///     It deliberately does NOT instantiate <c>PassportController</c> / <c>GenericUserProfileContextMenuController</c>
    ///     / <c>CommunityPlayerEntryContextMenu</c> directly: none of the three has any existing test coverage
    ///     in this tree, and each pulls in 10+ additional dependencies (IMVCManager, voice chat orchestrator,
    ///     analytics, ScriptableObject-backed context-menu settings, ChatEventBus, ...) with no established
    ///     mocking harness — standing one up from scratch was judged out of scope for a minimal regression test,
    ///     and, without a Unity Editor available in this environment to compile/run and catch constructor NPEs
    ///     before delivery, unverifiable. What is asserted here — "no exception escapes the suppressed await"
    ///     and "the statement after it always runs" — is the entire mechanism the fix relies on at all three
    ///     sites; the sites themselves differ only in what that follow-up statement does.
    ///     </para>
    ///     <para>
    ///     <c>FriendshipActionRejectedException</c> is new production API added by the fix, so it is looked up
    ///     by name via <c>Assembly.GetType</c> instead of referenced directly: on the pre-fix tree the type does
    ///     not exist yet, and a direct reference would fail to *compile* the shared <c>DCL.EditMode.Tests</c>
    ///     assembly (via <c>DCL.Friends.Tests.asmref</c>), breaking every other EditMode test in the project.
    ///     The lookup instead turns that into an explicit "type missing" assertion failure on the pre-fix tree
    ///     (same reflection-shim idiom as <c>RPCFriendsServiceUnwrapUpsertFriendshipResponseShould</c> in this
    ///     folder, and <c>DeepLinkBridgePublishRoundTripShould</c>, ue9520).
    ///     </para>
    /// </summary>
    public class FriendsAcceptPathRejectionSuppressionShould
    {
        private const string REJECTED_EXCEPTION_TYPE_NAME = "FriendshipActionRejectedException";
        private const string FRIEND_ID = "0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10";
        private const string SERVER_MESSAGE = "The friendship action is not valid for the current friendship status";

        // Null on a pre-fix tree (the type does not exist yet) — see class doc.
        private static readonly Type? REJECTED_EXCEPTION_TYPE =
            typeof(RPCFriendsService).Assembly.GetType("DCL.Friends." + REJECTED_EXCEPTION_TYPE_NAME);

        private ReportHubLogger originalReportHubLogger = null!;

        [SetUp]
        public void SetUp()
        {
            // SuppressToResultAsync reports the observed exception via ReportHub.LogException (that IS part of
            // the fix's behavior — "observe + attribute", not silent swallowing). The default logger forwards
            // to Debug.unityLogger.LogException, which the Unity Test Framework treats as a test failure unless
            // explicitly expected. Swap in a no-op logger for the duration of this test so the assertions below
            // are the only thing that can fail it, and restore the real one afterwards.
            originalReportHubLogger = ReportHub.Instance;
            ReportHub.Initialize(new ReportHubLogger(new List<IReportHandler>()));
        }

        [TearDown]
        public void TearDown() =>
            ReportHub.Initialize(originalReportHubLogger);

        [Test]
        public async Task NotPropagateTypedRejectionAndRunTheFollowUpStatementUnconditionally()
        {
            Assert.That(REJECTED_EXCEPTION_TYPE, Is.Not.Null,
                $"type missing: DCL.Friends.{REJECTED_EXCEPTION_TYPE_NAME} does not exist on this tree — the " +
                "client-side fix for unity-explorer#9618's Accept-path bare-await sites is absent.");

            var rejection = (Exception) Activator.CreateInstance(REJECTED_EXCEPTION_TYPE!, SERVER_MESSAGE)!;

            var friendsService = Substitute.For<IFriendsService>();
            friendsService.AcceptFriendshipAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                          .Returns(UniTask.FromException(rejection));

            var followUpRan = false;
            Exception? escaped = null;
            EnumResult<TaskError> result = default;

            try
            {
                // The exact idiom the fix installs at all three Accept-path sites.
                result = await friendsService.AcceptFriendshipAsync(FRIEND_ID, CancellationToken.None)
                                              .SuppressToResultAsync(ReportCategory.FRIENDS);

                // Mirrors the statement that follows the await at each site — nothing at the two context-menu
                // sites, PassportController's ShowFriendshipInteraction() at the Passport site.
                followUpRan = true;
            }
            catch (Exception e)
            {
                escaped = e;
            }

            Assert.That(escaped, Is.Null,
                "no exception may escape the suppressed accept call — this is exactly the unobserved-exception " +
                "variant the fix removes (pre-fix: bare await in a fire-and-forget UniTaskVoid)");

            Assert.That(followUpRan, Is.True,
                "the statement after the awaited accept call must run regardless of outcome — pre-fix, a thrown " +
                "exception stops execution before it, which is why PassportController's stuck 'Accept' button " +
                "never re-syncs (ShowFriendshipInteraction() is never reached)");

            Assert.That(result.Success, Is.False,
                "the rejection must be an observed error result, not silently treated as success");

            Assert.That(result.Error!.Value.Message, Does.Contain(SERVER_MESSAGE),
                "the server's rejection reason must reach the observed Result — SuppressToResultAsync attributes " +
                "the exception (reports it, carries its Message), it does not swallow it");
        }
    }
}

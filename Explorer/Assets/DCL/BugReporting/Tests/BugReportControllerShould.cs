using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.BugReporting.UI;
using DCL.Input;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utility.Types;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.BugReporting.Tests
{
    // View-less tests: the submit pipeline is exercised without ever instantiating a view.
    public class BugReportControllerShould
    {
        private const int ISSUE_TYPE_INDEX = 2;
        private const string DESCRIPTION = "The avatar falls through the floor.";

        private BugReportService bugReportService = null!;
        private ISelfProfile selfProfile = null!;
        private World world = null!;
        private BugReportController controller = null!;

        private BugReportInput captured;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            captured = default;
            bugReportService = Substitute.For<BugReportService>(null, null);

            bugReportService.SubmitAsync(Arg.Do<BugReportInput>(input => captured = input), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(Result<string>.SuccessResult("ticket-1")));

            selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(null));

            world = World.Create();
            controller = new BugReportController(() => null!, bugReportService, selfProfile, Substitute.For<IInputBlock>(), world, world.Create());
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            World.Destroy(world);
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [TestCase(-1, DESCRIPTION, true, false)]
        [TestCase(0, "", true, false)]
        [TestCase(0, "   ", true, false)]
        [TestCase(0, DESCRIPTION, false, false)]
        [TestCase(0, DESCRIPTION, true, true)]
        public void GateSubmissionOnIssueTypeDescriptionAndLogsConsent(int issueTypeIndex, string description, bool shareLogs, bool expected) =>
            Assert.AreEqual(expected, BugReportController.CanSubmit(issueTypeIndex, description, shareLogs));

        [Test]
        public void ResolveThePrefilledIssueTypeToItsDropdownIndex()
        {
            Assert.AreEqual(Array.IndexOf(BugReportIssueTypes.ALL, BugReportIssueTypes.PERFORMANCE), BugReportController.IssueTypeIndexOf(BugReportIssueTypes.PERFORMANCE));
            Assert.AreEqual(-1, BugReportController.IssueTypeIndexOf(null));
            Assert.AreEqual(-1, BugReportController.IssueTypeIndexOf(new BugReportIssueType("Unknown", "no-such-option")));
        }

        [Test]
        public async Task SendDraftValuesToService()
        {
            // Arrange
            byte[] firstBytes = { 1, 2, 3 };
            byte[] secondBytes = { 4, 5, 6 };
            BugReportImage[] images = { new (firstBytes, "image/png", null!), new (secondBytes, "image/jpeg", null!) };
            var draft = new BugReportDraft(ISSUE_TYPE_INDEX, $"  {DESCRIPTION}  ", images);

            // Act
            Result<string> result = await controller.SubmitDraftAsync(draft, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("ticket-1", result.Value);
            Assert.AreEqual(BugReportIssueTypes.ALL[ISSUE_TYPE_INDEX].OptionId, captured.IssueType.OptionId);
            Assert.AreEqual(DESCRIPTION, captured.Description);
            Assert.AreEqual(2, captured.Images!.Count);
            Assert.AreSame(firstBytes, captured.Images[0].Bytes);
            Assert.AreEqual("image/png", captured.Images[0].ContentType);
            Assert.AreSame(secondBytes, captured.Images[1].Bytes);
            Assert.AreEqual("image/jpeg", captured.Images[1].ContentType);
            Assert.IsNull(captured.UserName);
            Assert.IsNull(captured.Coordinates);
        }

        [Test]
        public async Task IncludeSessionContextInTheInput()
        {
            // Arrange
            IBugReportSessionContext sessionContext = Substitute.For<IBugReportSessionContext>();
            sessionContext.MeetsMinimumSpecs.Returns(false);
            sessionContext.SceneSdkVersion.Returns("7.5.6");
            sessionContext.LauncherVersion.Returns("1.4.2");

            using var contextController = new BugReportController(
                () => null!, bugReportService, selfProfile, Substitute.For<IInputBlock>(), world, world.Create(), sessionContext: sessionContext);

            // Act
            await contextController.SubmitDraftAsync(Draft(), CancellationToken.None);

            // Assert
            Assert.IsFalse(captured.MeetsMinimumSpecs);
            Assert.AreEqual("7.5.6", captured.SceneSdkVersion);
            Assert.AreEqual("1.4.2", captured.LauncherVersion);
        }

        [Test]
        public async Task IncludeProfileNameWhenAvailable()
        {
            // Arrange
            var profile = new Profile(UserId.New("0x1").Unwrap(), "Tester", new Avatar());
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(profile));

            // Act
            await controller.SubmitDraftAsync(Draft(), CancellationToken.None);

            // Assert
            Assert.AreEqual(profile.DisplayName, captured.UserName);
        }

        [Test]
        public async Task ProceedWithoutUserNameWhenProfileLookupFails()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(_ => throw new Exception("profile backend down"));

            // Act
            Result<string> result = await controller.SubmitDraftAsync(Draft(), CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNull(captured.UserName);
        }

        [Test]
        public async Task ReturnCancelledWithoutCallingServiceWhenCancelled()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Result<string> result = await controller.SubmitDraftAsync(Draft(), cts.Token);

            // Assert
            Assert.IsFalse(result.Success);
            await bugReportService.DidNotReceive().SubmitAsync(Arg.Any<BugReportInput>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ForwardServiceFailureAsResult()
        {
            // Arrange
            bugReportService.SubmitAsync(Arg.Any<BugReportInput>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(Result<string>.ErrorResult("proxy rejected the ticket")));

            // Act
            Result<string> result = await controller.SubmitDraftAsync(Draft(), CancellationToken.None);

            // Assert
            Assert.IsFalse(result.Success);
        }

        private static BugReportDraft Draft() =>
            new (ISSUE_TYPE_INDEX, DESCRIPTION, Array.Empty<BugReportImage>());
    }
}

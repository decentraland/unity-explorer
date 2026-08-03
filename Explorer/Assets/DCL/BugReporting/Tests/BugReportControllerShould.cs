using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.BugReporting.UI;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utility.Types;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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
            captured = default;
            bugReportService = Substitute.For<BugReportService>(null, null);

            bugReportService.SubmitAsync(Arg.Do<BugReportInput>(input => captured = input), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(Result<string>.SuccessResult("ticket-1")));

            selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(null));

            world = World.Create();
            controller = new BugReportController(() => null!, bugReportService, selfProfile, world, world.Create());
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            World.Destroy(world);
        }

        [TestCase(-1, DESCRIPTION, false)]
        [TestCase(0, "", false)]
        [TestCase(0, "   ", false)]
        [TestCase(0, DESCRIPTION, true)]
        public void GateSubmissionOnIssueTypeAndDescription(int issueTypeIndex, string description, bool expected) =>
            Assert.AreEqual(expected, BugReportController.CanSubmit(issueTypeIndex, description));

        [Test]
        public async Task SendDraftValuesToService()
        {
            // Arrange
            byte[] imageBytes = { 1, 2, 3 };
            var image = new BugReportImage(imageBytes, "image/png", null!);
            var draft = new BugReportDraft(ISSUE_TYPE_INDEX, $"  {DESCRIPTION}  ", image, shareLogs: false);

            // Act
            Result<string> result = await controller.SubmitDraftAsync(draft, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("ticket-1", result.Value);
            Assert.AreEqual(BugReportIssueTypes.ALL[ISSUE_TYPE_INDEX].OptionId, captured.IssueType.OptionId);
            Assert.AreEqual(DESCRIPTION, captured.Description);
            Assert.AreEqual(imageBytes, captured.Image);
            Assert.AreEqual("image/png", captured.ImageContentType);
            Assert.IsFalse(captured.ShareLogs);
            Assert.IsNull(captured.UserName);
            Assert.IsNull(captured.Coordinates);
        }

        [Test]
        public async Task IncludeProfileNameWhenAvailable()
        {
            // Arrange
            var profile = new Profile("0x1", "Tester", new Avatar());
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
            new (ISSUE_TYPE_INDEX, DESCRIPTION, null, shareLogs: true);
    }
}

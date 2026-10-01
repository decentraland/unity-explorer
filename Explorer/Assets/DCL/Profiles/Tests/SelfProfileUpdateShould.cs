using DCL.Profiles.Self;
using ECS.TestSuite;
using NUnit.Framework;
using System;

namespace DCL.Profiles.Tests
{
    public class SelfProfileUpdateShould
    {
        private static readonly UserId ALICE = UserId.New("0xAlice").Unwrap();
        private static readonly UserId BOB = UserId.New("0xBob").Unwrap();

        private static readonly ProfileFailure TRANSIENT_FAILURE = new (FailureKind.Transient, new TimeoutException("catalyst timed out"));
        private static readonly Exception DEPLOY_ERROR = new InvalidOperationException("deploy rejected");
        private static readonly RequestId READ = new (7);
        private static readonly RequestId DEPLOY = new (8);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void StartFetchingWhenIdentityAppears()
        {
            // Arrange
            SelfProfileModel model = SelfProfileModel.NoIdentity();

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromIdentityChanged(ALICE));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Address, Is.EqualTo(ALICE));
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Unknown));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Fetching));
            AssertFetch(cmd, ALICE);
        }

        [Test]
        public void ResetLocalStateAndRefetchWhenIdentitySwitches()
        {
            // Arrange
            SelfProfileModel model = Known(NewProfile(3));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromIdentityChanged(BOB));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Address, Is.EqualTo(BOB));
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Unknown));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Fetching));

            SelfProfileCmd[] batch = AssertBatch(cmd, 2);
            Assert.That(batch[0].GetKind(), Is.EqualTo(SelfProfileCmd.Kind.ResetLocalState));
            AssertFetch(batch[1], BOB);
        }

        [Test]
        public void IgnoreIdentityChangedForTheCurrentAddress()
        {
            // Arrange
            SelfProfileModel model = Known(NewProfile(3));

            // Act & Assert
            AssertUnchanged(model, SelfProfileMsg.FromIdentityChanged(ALICE));
        }

        [Test]
        public void ForgetEverythingAndResetLocalStateWhenIdentityIsCleared()
        {
            // Arrange
            SelfProfileModel model = Known(NewProfile(3));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.IdentityCleared());

            // Assert
            Assert.That(next.Session.GetKind(), Is.EqualTo(SelfProfileSession.Kind.NoIdentity));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.ResetLocalState));
        }

        [Test]
        public void IgnoreIdentityClearedWithoutIdentity()
        {
            // Act & Assert
            AssertUnchanged(SelfProfileModel.NoIdentity(), SelfProfileMsg.IdentityCleared());
        }

        [Test]
        public void PublishTheFetchedProfile()
        {
            // Arrange
            Profile fetched = NewProfile(1);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(Fetching(), SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(ALICE, fetched)));

            // Assert
            Identified identified = AssertIdentified(next);
            AssertKnown(identified.Knowledge, fetched);
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            AssertPublish(cmd, fetched);
        }

        [Test]
        public void RecordAMissingProfileWithoutPublishing()
        {
            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(Fetching(), SelfProfileMsg.FromFetchNotFound(ALICE));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Missing));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void RecordAFailedFetchWithoutRetrying()
        {
            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(Fetching(), SelfProfileMsg.FromFetchFailed(new FetchFailed(ALICE, TRANSIENT_FAILURE)));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Knowledge.IsFailed(out ProfileFailure failure), Is.True);
            Assert.That(failure.Kind, Is.EqualTo(FailureKind.Transient));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void KeepTheKnownProfileWhenAFetchFails()
        {
            // Arrange
            Profile trusted = NewProfile(3);
            SelfProfileModel model = Model(ProfileKnowledge.FromKnown(trusted), ProfileActivity.Fetching());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromFetchFailed(new FetchFailed(ALICE, TRANSIENT_FAILURE)));

            // Assert
            Identified identified = AssertIdentified(next);
            AssertKnown(identified.Knowledge, trusted);
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void DropFetchResultsForAnotherAddress()
        {
            // Arrange
            SelfProfileModel model = Fetching();

            // Act & Assert
            AssertUnchanged(model, SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(BOB, NewProfile(1))));
            AssertUnchanged(model, SelfProfileMsg.FromFetchNotFound(BOB));
            AssertUnchanged(model, SelfProfileMsg.FromFetchFailed(new FetchFailed(BOB, TRANSIENT_FAILURE)));
        }

        [Test]
        public void DropFetchResultsWhenNoFetchIsInFlight()
        {
            // Arrange
            SelfProfileModel model = Known(NewProfile(3));

            // Act & Assert
            AssertUnchanged(model, SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(ALICE, NewProfile(1))));
            AssertUnchanged(model, SelfProfileMsg.FromFetchNotFound(ALICE));
            AssertUnchanged(model, SelfProfileMsg.FromFetchFailed(new FetchFailed(ALICE, TRANSIENT_FAILURE)));
        }

        [Test]
        public void DropFetchResultsWithoutIdentity()
        {
            // Act & Assert
            AssertUnchanged(SelfProfileModel.NoIdentity(), SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(ALICE, NewProfile(1))));
        }

        [Test]
        public void PublishBeforeDeployingWhenTheProfileIsKnown()
        {
            // Arrange
            Profile trusted = NewProfile(3);
            Profile edited = NewProfile(4);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(Known(trusted), SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Assert
            Identified identified = AssertIdentified(next);
            AssertKnown(identified.Knowledge, edited);
            Deploying deploying = AssertDeploying(identified.Activity, edited);
            AssertKnown(deploying.Before, trusted);

            SelfProfileCmd[] batch = AssertBatch(cmd, 2);
            AssertPublish(batch[0], edited);
            AssertDeploy(batch[1], edited);
        }

        [Test]
        public void DeployWithoutPublishingWhenTheProfileIsMissing()
        {
            // Act & Assert
            AssertDeploysWithoutPublishing(ProfileKnowledge.Missing());
        }

        [Test]
        public void DeployWithoutPublishingWhenTheLastFetchFailed()
        {
            // Act & Assert
            AssertDeploysWithoutPublishing(ProfileKnowledge.FromFailed(TRANSIENT_FAILURE));
        }

        [Test]
        public void SupersedeAFetchInFlightWithAnEdit()
        {
            // Arrange
            Profile edited = NewProfile(1);

            // Act
            (SelfProfileModel afterEdit, SelfProfileCmd cmd) = SelfProfileModel.Update(Fetching(), SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Assert
            Identified identified = AssertIdentified(afterEdit);
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Unknown));
            AssertDeploying(identified.Activity, edited);
            AssertDeploy(cmd, edited);

            AssertUnchanged(afterEdit, SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(ALICE, NewProfile(0))));
        }

        [Test]
        public void SupersedeADeployInFlightAndKeepTheOriginalRevertPoint()
        {
            // Arrange
            Profile trusted = NewProfile(3);
            Profile firstEdit = NewProfile(4);
            Profile secondEdit = NewProfile(5);
            (SelfProfileModel afterFirst, SelfProfileCmd _) = SelfProfileModel.Update(Known(trusted), SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, firstEdit)));

            // Act
            (SelfProfileModel afterSecond, SelfProfileCmd cmd) = SelfProfileModel.Update(afterFirst, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, secondEdit)));

            // Assert
            Identified identified = AssertIdentified(afterSecond);
            AssertKnown(identified.Knowledge, secondEdit);
            Deploying deploying = AssertDeploying(identified.Activity, secondEdit);
            AssertKnown(deploying.Before, trusted);

            SelfProfileCmd[] batch = AssertBatch(cmd, 2);
            AssertPublish(batch[0], secondEdit);
            AssertDeploy(batch[1], secondEdit);
        }

        [Test]
        public void AnswerADeployWithNoIdentityWithoutIdentity()
        {
            // Arrange
            SelfProfileModel model = SelfProfileModel.NoIdentity();

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, NewProfile(1))));

            // Assert
            Assert.That(next.Session.GetKind(), Is.EqualTo(SelfProfileSession.Kind.NoIdentity));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertDeployError(next, DEPLOY, ProfileDeployError.NoIdentity);
        }

        [Test]
        public void AnswerADeployWithNothingChangedWhenTheEditIsIdentical()
        {
            // Arrange
            SelfProfileModel model = Known(NewProfile(3));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, NewProfile(3))));

            // Assert
            Assert.That(next.Session, Is.EqualTo(model.Session));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertDeployError(next, DEPLOY, ProfileDeployError.NothingChanged);
        }

        [Test]
        public void AnswerDeployRequestsWhenTheDeploySucceeds()
        {
            // Arrange
            Profile sent = NewProfile(4);
            Profile saved = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.FromKnown(NewProfile(3)), DEPLOY);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(ALICE, sent, saved)));

            // Assert
            Assert.That(AssertIdentified(next).Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            AssertPublish(cmd, saved);
            Assert.That(AssertDeployResult(next, DEPLOY).IsOk(out Profile actual), Is.True);
            Assert.That(actual, Is.SameAs(saved));
        }

        [Test]
        public void AnswerDeployRequestsWithDeployFailedWhenTheDeployFails()
        {
            // Arrange
            Profile sent = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.Missing(), DEPLOY);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployFailed(new DeployFailed(ALICE, sent, DEPLOY_ERROR)));

            // Assert
            Assert.That(AssertIdentified(next).Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Missing));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertDeployError(next, DEPLOY, ProfileDeployError.DeployFailed);
        }

        [Test]
        public void CarryDeployRequestsOverToTheSupersedingDeploy()
        {
            // Arrange
            var later = new RequestId(9);
            SelfProfileModel model = Deploying(NewProfile(4), ProfileKnowledge.FromKnown(NewProfile(3)), DEPLOY);

            // Act
            (SelfProfileModel next, _) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(later, NewProfile(5))));

            // Assert
            Deploying deploying = AssertDeploying(AssertIdentified(next).Activity, out _);
            Assert.That(deploying.Requests.Count, Is.EqualTo(2));
            Assert.That(deploying.Requests[0], Is.EqualTo(DEPLOY));
            Assert.That(deploying.Requests[1], Is.EqualTo(later));
        }

        [Test]
        public void AnswerDeployRequestsWithNoIdentityWhenIdentityIsCleared()
        {
            // Arrange
            SelfProfileModel model = Deploying(NewProfile(4), ProfileKnowledge.FromKnown(NewProfile(3)), DEPLOY);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.IdentityCleared());

            // Assert
            Assert.That(next.Session.GetKind(), Is.EqualTo(SelfProfileSession.Kind.NoIdentity));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.ResetLocalState));
            AssertDeployError(next, DEPLOY, ProfileDeployError.NoIdentity);
        }

        [Test]
        public void KeepTheDeployWhenItsRequestIsClosed()
        {
            // Arrange
            Profile sent = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.FromKnown(NewProfile(3)), DEPLOY);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromRequestClosed(DEPLOY));

            // Assert
            Deploying deploying = AssertDeploying(AssertIdentified(next).Activity, sent);
            Assert.That(deploying.Requests.Count, Is.EqualTo(0));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void PublishTheSavedProfileWhenADeploySucceeds()
        {
            // Arrange
            Profile sent = NewProfile(4);
            Profile saved = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.FromKnown(NewProfile(3)));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(ALICE, sent, saved)));

            // Assert
            Identified identified = AssertIdentified(next);
            AssertKnown(identified.Knowledge, saved);
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            AssertPublish(cmd, saved);
        }

        [Test]
        public void RevertAndRepublishWhenAnOptimisticDeployFails()
        {
            // Arrange
            Profile trusted = NewProfile(3);
            Profile sent = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.FromKnown(trusted));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployFailed(new DeployFailed(ALICE, sent, DEPLOY_ERROR)));

            // Assert
            Identified identified = AssertIdentified(next);
            AssertKnown(identified.Knowledge, trusted);
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            AssertPublish(cmd, trusted);
        }

        [Test]
        public void RevertSilentlyWhenANonOptimisticDeployFails()
        {
            // Arrange
            Profile sent = NewProfile(1);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.Missing());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployFailed(new DeployFailed(ALICE, sent, DEPLOY_ERROR)));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Missing));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Idle));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void DropResultsOfASupersededDeploy()
        {
            // Arrange
            Profile superseded = NewProfile(4);
            Profile pending = NewProfile(5);
            SelfProfileModel model = Deploying(pending, ProfileKnowledge.FromKnown(NewProfile(3)));

            // Act & Assert
            AssertUnchanged(model, SelfProfileMsg.FromDeployFailed(new DeployFailed(ALICE, superseded, DEPLOY_ERROR)));
            AssertUnchanged(model, SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(ALICE, superseded, NewProfile(4))));
        }

        [Test]
        public void DropDeployResultsForAnotherAddress()
        {
            // Arrange
            Profile sent = NewProfile(4);
            SelfProfileModel model = Deploying(sent, ProfileKnowledge.FromKnown(NewProfile(3)));

            // Act & Assert
            AssertUnchanged(model, SelfProfileMsg.FromDeployFailed(new DeployFailed(BOB, sent, DEPLOY_ERROR)));
            AssertUnchanged(model, SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(BOB, sent, NewProfile(4))));
        }

        [Test]
        public void DropDeployResultsWhenNoDeployIsInFlight()
        {
            // Arrange
            Profile sent = NewProfile(4);

            // Act & Assert
            AssertUnchanged(Known(NewProfile(3)), SelfProfileMsg.FromDeployFailed(new DeployFailed(ALICE, sent, DEPLOY_ERROR)));
            AssertUnchanged(SelfProfileModel.NoIdentity(), SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(ALICE, sent, NewProfile(4))));
        }

        private static void AssertDeploysWithoutPublishing(ProfileKnowledge before)
        {
            // Arrange
            Profile edited = NewProfile(1);
            SelfProfileModel model = Model(before, ProfileActivity.Idle());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(before.GetKind()));
            Deploying deploying = AssertDeploying(identified.Activity, edited);
            Assert.That(deploying.Before.GetKind(), Is.EqualTo(before.GetKind()));
            AssertDeploy(cmd, edited);
        }

        [Test]
        public void AnswerAReadAtOnceWhenTheProfileIsKnown()
        {
            // Arrange
            Profile known = NewProfile(3);
            SelfProfileModel model = Known(known);

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromProfileReadRequested(READ));

            // Assert
            Assert.That(next.Session, Is.EqualTo(model.Session));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            Assert.That(AssertReadResult(next, READ).IsOk(out Profile actual), Is.True);
            Assert.That(actual, Is.SameAs(known));
        }

        [Test]
        public void AnswerAReadWithNotFoundWhenTheProfileIsMissing()
        {
            // Arrange
            SelfProfileModel model = Model(ProfileKnowledge.Missing(), ProfileActivity.Idle());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromProfileReadRequested(READ));

            // Assert
            Assert.That(next.Session, Is.EqualTo(model.Session));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertReadError(next, READ, ProfileReadError.NotFound);
        }

        [Test]
        public void AnswerAReadWithNoIdentityWithoutIdentity()
        {
            // Arrange
            SelfProfileModel model = SelfProfileModel.NoIdentity();

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromProfileReadRequested(READ));

            // Assert
            Assert.That(next.Session.GetKind(), Is.EqualTo(SelfProfileSession.Kind.NoIdentity));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertReadError(next, READ, ProfileReadError.NoIdentity);
        }

        [Test]
        public void QueueAReadWhileAFetchIsInFlight()
        {
            // Arrange
            SelfProfileModel model = Fetching();

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromProfileReadRequested(READ));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.PendingReads.Count, Is.EqualTo(1));
            Assert.That(identified.PendingReads[0], Is.EqualTo(READ));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Fetching));
            Assert.That(next.ReadResults.Count, Is.EqualTo(0));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void RefetchForAReadAfterAFailedFetch()
        {
            // Arrange
            SelfProfileModel model = Model(ProfileKnowledge.FromFailed(TRANSIENT_FAILURE), ProfileActivity.Idle());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromProfileReadRequested(READ));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Knowledge.GetKind(), Is.EqualTo(ProfileKnowledge.Kind.Failed));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Fetching));
            Assert.That(identified.PendingReads.Count, Is.EqualTo(1));
            AssertFetch(cmd, ALICE);
        }

        [Test]
        public void AnswerQueuedReadsWhenTheFetchSucceeds()
        {
            // Arrange
            Profile fetched = NewProfile(3);
            SelfProfileModel model = WithPendingRead(Fetching());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromFetchSucceeded(new FetchSucceeded(ALICE, fetched)));

            // Assert
            Assert.That(AssertIdentified(next).PendingReads.Count, Is.EqualTo(0));
            AssertPublish(cmd, fetched);
            Assert.That(AssertReadResult(next, READ).IsOk(out Profile actual), Is.True);
            Assert.That(actual, Is.SameAs(fetched));
        }

        [Test]
        public void AnswerQueuedReadsWithFetchFailedWhenTheFetchFails()
        {
            // Arrange
            SelfProfileModel model = WithPendingRead(Fetching());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromFetchFailed(new FetchFailed(ALICE, TRANSIENT_FAILURE)));

            // Assert
            Assert.That(AssertIdentified(next).PendingReads.Count, Is.EqualTo(0));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
            AssertReadError(next, READ, ProfileReadError.FetchFailed);
        }

        [Test]
        public void AnswerQueuedReadsWithNoIdentityWhenIdentityIsCleared()
        {
            // Arrange
            SelfProfileModel model = WithPendingRead(Fetching());

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.IdentityCleared());

            // Assert
            Assert.That(next.Session.GetKind(), Is.EqualTo(SelfProfileSession.Kind.NoIdentity));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.ResetLocalState));
            AssertReadError(next, READ, ProfileReadError.NoIdentity);
        }

        [Test]
        public void CarryQueuedReadsOverWhenIdentitySwitches()
        {
            // Arrange
            SelfProfileModel model = WithPendingRead(Fetching());

            // Act
            (SelfProfileModel next, _) = SelfProfileModel.Update(model, SelfProfileMsg.FromIdentityChanged(BOB));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.Address, Is.EqualTo(BOB));
            Assert.That(identified.PendingReads.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnswerQueuedReadsWhenASupersedingDeploySucceeds()
        {
            // Arrange
            Profile edited = NewProfile(1);
            Profile saved = NewProfile(2);
            (SelfProfileModel deploying, _) = SelfProfileModel.Update(WithPendingRead(Fetching()), SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(deploying, SelfProfileMsg.FromDeploySucceeded(new DeploySucceeded(ALICE, edited, saved)));

            // Assert
            Assert.That(AssertIdentified(next).PendingReads.Count, Is.EqualTo(0));
            AssertPublish(cmd, saved);
            Assert.That(AssertReadResult(next, READ).IsOk(out Profile actual), Is.True);
            Assert.That(actual, Is.SameAs(saved));
        }

        [Test]
        public void DropAReadResultWhenTheRequestIsClosed()
        {
            // Arrange
            (SelfProfileModel answered, _) = SelfProfileModel.Update(Known(NewProfile(3)), SelfProfileMsg.FromProfileReadRequested(READ));

            // Act
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(answered, SelfProfileMsg.FromRequestClosed(READ));

            // Assert
            Assert.That(next.ReadResults.Count, Is.EqualTo(0));
            Assert.That(next.Session, Is.EqualTo(answered.Session));
            Assert.That(cmd.GetKind(), Is.EqualTo(SelfProfileCmd.Kind.None));
        }

        [Test]
        public void DropAQueuedReadWhenTheRequestIsClosed()
        {
            // Arrange
            SelfProfileModel model = WithPendingRead(Fetching());

            // Act
            (SelfProfileModel next, _) = SelfProfileModel.Update(model, SelfProfileMsg.FromRequestClosed(READ));

            // Assert
            Identified identified = AssertIdentified(next);
            Assert.That(identified.PendingReads.Count, Is.EqualTo(0));
            Assert.That(identified.Activity.GetKind(), Is.EqualTo(ProfileActivity.Kind.Fetching));
        }

        [Test]
        public void DeployTheEditAsTheNextVersionOfTheKnownProfile()
        {
            // Arrange
            Profile edited = NewProfile(3);
            edited.Description = "edited";
            SelfProfileModel model = Known(NewProfile(3));

            // Act
            (_, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Assert
            SelfProfileCmd[] batch = AssertBatch(cmd, 2);
            Assert.That(AssertDeploy(batch[1], edited).Version, Is.EqualTo(4));
        }

        [Test]
        public void DeployTheEditAsItsOwnNextVersionWhenNothingIsKnown()
        {
            // Arrange
            Profile edited = NewProfile(0);
            SelfProfileModel model = Model(ProfileKnowledge.Missing(), ProfileActivity.Idle());

            // Act
            (_, SelfProfileCmd cmd) = SelfProfileModel.Update(model, SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(DEPLOY, edited)));

            // Assert
            Assert.That(AssertDeploy(cmd, edited).Version, Is.EqualTo(1));
        }

        private static Profile NewProfile(int version) =>
            new (ALICE, "alice", new Avatar()) { Version = version };

        private static SelfProfileModel WithPendingRead(in SelfProfileModel model)
        {
            Identified identified = AssertIdentified(model);
            return SelfProfileModel.FromIdentified(identified.WithPendingReads(identified.PendingReads.Add(READ)));
        }

        private static SelfProfileModel Model(ProfileKnowledge knowledge, ProfileActivity activity) =>
            SelfProfileModel.FromIdentified(new Identified(ALICE, knowledge, activity));

        private static SelfProfileModel Fetching() =>
            Model(ProfileKnowledge.Unknown(), ProfileActivity.Fetching());

        private static SelfProfileModel Known(Profile profile) =>
            Model(ProfileKnowledge.FromKnown(profile), ProfileActivity.Idle());

        /// <summary>A deploy in flight, published optimistically when the knowledge before it was known.</summary>
        private static SelfProfileModel Deploying(Profile pending, ProfileKnowledge before) =>
            Deploying(pending, before, default);

        private static SelfProfileModel Deploying(Profile pending, ProfileKnowledge before, RequestIds requests)
        {
            ProfileKnowledge knowledge = before.IsKnown(out _) ? ProfileKnowledge.FromKnown(pending) : before;
            return Model(knowledge, ProfileActivity.FromDeploying(new Deploying(pending, before, requests)));
        }

        private static SelfProfileModel Deploying(Profile pending, ProfileKnowledge before, RequestId request) =>
            Deploying(pending, before, default(RequestIds).Add(request));

        private static void AssertUnchanged(in SelfProfileModel model, in SelfProfileMsg msg)
        {
            (SelfProfileModel next, SelfProfileCmd cmd) = SelfProfileModel.Update(model, msg);

            Assert.That(next, Is.EqualTo(model), $"{msg} should not change the model");
            Assert.That(cmd.IsIgnore(out string? reason), Is.True, $"{msg} should be ignored, got {cmd}");
            Assert.That(reason, Is.Not.Empty);
        }

        private static Identified AssertIdentified(in SelfProfileModel model)
        {
            Assert.That(model.IsIdentified(out Identified identified), Is.True, $"expected Identified, got {model}");
            return identified;
        }

        private static void AssertKnown(in ProfileKnowledge knowledge, Profile expected)
        {
            Assert.That(knowledge.IsKnown(out Profile? actual), Is.True, $"expected Known, got {knowledge}");
            Assert.That(actual, Is.SameAs(expected));
        }

        private static Deploying AssertDeploying(in ProfileActivity activity, Profile pending)
        {
            Deploying deploying = AssertDeploying(activity, out _);
            Assert.That(deploying.Pending, Is.SameAs(pending));
            return deploying;
        }

        private static Deploying AssertDeploying(in ProfileActivity activity, out Profile pending)
        {
            Assert.That(activity.IsDeploying(out Deploying deploying), Is.True, $"expected Deploying, got {activity}");
            pending = deploying.Pending;
            return deploying;
        }

        private static void AssertFetch(in SelfProfileCmd cmd, UserId address)
        {
            Assert.That(cmd.IsFetch(out UserId? actual), Is.True, $"expected Fetch, got {cmd}");
            Assert.That(actual, Is.EqualTo(address));
        }

        private static void AssertPublish(in SelfProfileCmd cmd, Profile expected)
        {
            Assert.That(cmd.IsPublish(out Profile? actual), Is.True, $"expected Publish, got {cmd}");
            Assert.That(actual, Is.SameAs(expected));
        }

        private static ProfileReadResult AssertReadResult(in SelfProfileModel model, RequestId id)
        {
            Assert.That(model.ReadResults.TryGet(id, out ProfileReadResult result), Is.True, $"expected a read result for {id}, got {model}");
            return result;
        }

        private static void AssertReadError(in SelfProfileModel model, RequestId id, ProfileReadError expected)
        {
            Assert.That(AssertReadResult(model, id).IsError(out ProfileReadError actual), Is.True, $"expected a read error for {id}, got {model}");
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static ProfileDeployResult AssertDeployResult(in SelfProfileModel model, RequestId id)
        {
            Assert.That(model.DeployResults.TryGet(id, out ProfileDeployResult result), Is.True, $"expected a deploy result for {id}, got {model}");
            return result;
        }

        private static void AssertDeployError(in SelfProfileModel model, RequestId id, ProfileDeployError expected)
        {
            Assert.That(AssertDeployResult(model, id).IsError(out ProfileDeployError actual), Is.True, $"expected a deploy error for {id}, got {model}");
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static DeployCmd AssertDeploy(in SelfProfileCmd cmd, Profile expected)
        {
            Assert.That(cmd.IsDeploy(out DeployCmd deploy), Is.True, $"expected Deploy, got {cmd}");
            Assert.That(deploy.Address, Is.EqualTo(ALICE));
            Assert.That(deploy.Profile, Is.SameAs(expected));
            return deploy;
        }

        private static SelfProfileCmd[] AssertBatch(in SelfProfileCmd cmd, int count)
        {
            Assert.That(cmd.IsBatch(out SelfProfileCmd[]? batch), Is.True, $"expected Batch, got {cmd}");
            Assert.That(batch, Has.Length.EqualTo(count));
            return batch!;
        }
    }
}

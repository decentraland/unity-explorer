using DCL.RealmNavigation;
using DCL.Utilities;
using NUnit.Framework;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the slimmed <see cref="NearbyVoiceChatManager"/> as a pure state-orchestration adapter:
    /// translates external triggers (Community/Private call status, world loading) into
    /// Suppress/Resume on the state model.
    /// </summary>
    public class NearbyVoiceChatManagerShould
    {
        private NearbyVoiceChatStateModel stateModel = null!;
        private ReactiveProperty<VoiceChatStatus> callStatus = null!;
        private FakeLoadingStatus loadingStatus = null!;

        [SetUp]
        public void SetUp()
        {
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            callStatus = new ReactiveProperty<VoiceChatStatus>(VoiceChatStatus.DISCONNECTED);
            loadingStatus = new FakeLoadingStatus(LoadingStatus.LoadingStage.Completed);
        }

        [TearDown]
        public void TearDown()
        {
            stateModel.Dispose();
        }

        [Test]
        public void NotSuppressLoadingWhenAlreadyCompletedAtConstruction()
        {
            // Act
            using var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void SuppressLoadingWhenStageIsNotCompletedAtConstruction()
        {
            // Arrange
            loadingStatus = new FakeLoadingStatus(LoadingStatus.LoadingStage.Init);

            // Act
            using var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
            Assert.That(stateModel.ActiveSuppression.Value, Is.EqualTo(SuppressionReason.LOADING));
        }

        [Test]
        public void ResumeLoadingWhenStageReachesCompleted()
        {
            // Arrange
            loadingStatus = new FakeLoadingStatus(LoadingStatus.LoadingStage.Init);
            using var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);

            // Act
            loadingStatus.CurrentStage.Value = LoadingStatus.LoadingStage.Completed;

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void SuppressOnCallInCall()
        {
            // Arrange
            using var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);

            // Act
            callStatus.Value = VoiceChatStatus.VOICE_CHAT_IN_CALL;

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
            Assert.That(stateModel.ActiveSuppression.Value, Is.EqualTo(SuppressionReason.CALL));
        }

        [Test]
        public void ResumeOnCallDisconnected()
        {
            // Arrange
            using var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);
            callStatus.Value = VoiceChatStatus.VOICE_CHAT_IN_CALL;

            // Act
            callStatus.Value = VoiceChatStatus.DISCONNECTED;

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        [Test]
        public void UnsubscribeAfterDispose()
        {
            // Arrange
            var manager = new NearbyVoiceChatManager(stateModel, callStatus, loadingStatus);
            manager.Dispose();

            // Act — should not trigger further state changes
            callStatus.Value = VoiceChatStatus.VOICE_CHAT_IN_CALL;
            loadingStatus.CurrentStage.Value = LoadingStatus.LoadingStage.Init;

            // Assert
            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
        }

        private class FakeLoadingStatus : ILoadingStatus
        {
            public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage { get; }
            public ReactiveProperty<string> AssetState { get; } = new (string.Empty);

            public FakeLoadingStatus(LoadingStatus.LoadingStage stage)
            {
                CurrentStage = new ReactiveProperty<LoadingStatus.LoadingStage>(stage);
            }

            public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad) { }

            public float SetCurrentStage(LoadingStatus.LoadingStage stage)
            {
                CurrentStage.Value = stage;
                return 0f;
            }

            public bool IsLoadingScreenOn() =>
                CurrentStage.Value != LoadingStatus.LoadingStage.Completed;
        }
    }
}

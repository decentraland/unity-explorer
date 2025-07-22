using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatController : IDisposable
    {
        private readonly CommunityVoiceChatTitlebarView view;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IVoiceChatOrchestratorUIEvents voiceChatOrchestratorUIEvents;
        private readonly IVoiceChatOrchestratorState voiceChatOrchestratorState;
        private readonly IObjectPool<PlayerEntryView> playerEntriesPool;
        private readonly List<PlayerEntryView> usedPlayerEntries = new ();
        private readonly CommunityVoiceChatSearchController communityVoiceChatSearchController;

        private bool isPanelCollapsed;
        private IDisposable? voiceChatTypeSubscription;

        public CommunityVoiceChatController(
            CommunityVoiceChatTitlebarView view,
            PlayerEntryView playerEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.voiceChatOrchestratorUIEvents = voiceChatOrchestrator;
            this.voiceChatOrchestratorState = voiceChatOrchestrator;
            communityVoiceChatSearchController = new CommunityVoiceChatSearchController(view.CommunityVoiceChatSearchView);

            this.view.CollapseButtonClicked += OnCollapsedButtonClicked;
            this.view.PromoteToSpeaker += OnPromoteToSpeaker;
            this.view.DemoteSpeaker += OnDemoteSpeaker;
            this.view.Kick += OnKickUser;
            this.view.Ban += OnBanUser;

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet:entry => entry.gameObject.SetActive(true),
                actionOnRelease:entry => entry.gameObject.SetActive(false));

            voiceChatTypeSubscription = voiceChatOrchestratorState.CurrentVoiceChatType.Subscribe(OnVoiceChatTypeChanged);

            OnVoiceChatTypeChanged(voiceChatOrchestratorState.CurrentVoiceChatType.Value);

            //Temporary fix, this will be moved to the Show function to set expanded as default state
            voiceChatOrchestratorUIEvents.ChangePanelSize(VoiceChatPanelSize.EXPANDED);

            AddSpeaker();
            AddSpeaker();
            AddSpeaker();
            AddListener();
            AddListener();
        }

        private void OnPromoteToSpeaker(VoiceChatMember member)
        {
        }

        private void OnDemoteSpeaker(VoiceChatMember member)
        {
        }

        private void OnKickUser(VoiceChatMember member)
        {

        }

        private void OnBanUser(VoiceChatMember member)
        {
        }

        private void OnVoiceChatTypeChanged(VoiceChatType voiceChatType)
        {
            switch (voiceChatType)
            {
                case VoiceChatType.PRIVATE:
                    Hide();
                    break;
                case VoiceChatType.COMMUNITY:
                case VoiceChatType.NONE:
                default:
                    Show();
                    break;
            }
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    view.Show();
                    break;
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                    view.Hide();
                    break;
            }
        }

        private void Show()
        {
            view.gameObject.SetActive(true);
        }

        private void Hide()
        {
            view.gameObject.SetActive(false);
        }

        private void OnCollapsedButtonClicked()
        {
            isPanelCollapsed = !isPanelCollapsed;
            voiceChatOrchestratorUIEvents.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.DEFAULT : VoiceChatPanelSize.EXPANDED);
            view.SetCollapsedButtonState(isPanelCollapsed);
        }

        private void AddSpeaker()
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry();
            entryView.transform.parent = view.CommunityVoiceChatInCallView.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private void AddListener()
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry();
            entryView.transform.parent = view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private PlayerEntryView GetAndConfigurePlayerEntry()
        {
            playerEntriesPool.Get(out PlayerEntryView entryView);
            usedPlayerEntries.Add(entryView);
            //entryView.profileView.SetupAsync(new Web3Address(""), profileRepositoryWrapper, CancellationToken.None).Forget();
            view.SubscribeContextMenu(entryView);
            return entryView;
        }

        private void ClearPool()
        {
            foreach (PlayerEntryView playerEntry in usedPlayerEntries)
                playerEntriesPool.Release(playerEntry);

            usedPlayerEntries.Clear();
        }

        public void Dispose()
        {
            view.CollapseButtonClicked -= OnCollapsedButtonClicked;
            view.PromoteToSpeaker -= OnPromoteToSpeaker;
            view.DemoteSpeaker -= OnDemoteSpeaker;
            view.Kick -= OnKickUser;
            view.Ban -= OnBanUser;

            voiceChatTypeSubscription?.Dispose();
            communityVoiceChatSearchController?.Dispose();
            ClearPool();
        }
    }
}

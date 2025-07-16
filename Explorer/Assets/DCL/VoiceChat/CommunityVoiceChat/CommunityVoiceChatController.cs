using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatController : IDisposable
    {
        private readonly CommunityVoiceChatTitlebarView view;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IVoiceChatUIEvents voiceChatOrchestratorUIEvents;
        private readonly IObjectPool<PlayerEntryView> playerEntriesPool;
        private readonly List<PlayerEntryView> usedPlayerEntries = new ();
        private readonly CommunityVoiceChatSearchController communityVoiceChatSearchController;

        private bool isPanelCollapsed;

        public CommunityVoiceChatController(
            CommunityVoiceChatTitlebarView view,
            PlayerEntryView playerEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatUIEvents voiceChatOrchestratorUIEvents)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.voiceChatOrchestratorUIEvents = voiceChatOrchestratorUIEvents;
            communityVoiceChatSearchController = new CommunityVoiceChatSearchController(view.CommunityVoiceChatSearchView);

            this.view.CollapseButtonClicked += OnCollapsedButtonClicked;

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet:entry => entry.gameObject.SetActive(true),
                actionOnRelease:entry => entry.gameObject.SetActive(false));

            //Temporary fix, this will be moved to the Show function to set expanded as default state
            voiceChatOrchestratorUIEvents.ChangePanelSize(VoiceChatPanelSize.EXPANDED);
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
            entryView.transform.parent = view.SpeakersParent;
        }

        private void AddListener()
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry();
            entryView.transform.parent = view.CommunityVoiceChatSearchView.ListenersParent;
        }

        private PlayerEntryView GetAndConfigurePlayerEntry()
        {
            playerEntriesPool.Get(out PlayerEntryView entryView);
            usedPlayerEntries.Add(entryView);
            entryView.profileView.SetupAsync(new Web3Address(""), profileRepositoryWrapper, CancellationToken.None).Forget();
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
            communityVoiceChatSearchController?.Dispose();
            ClearPool();
        }
    }
}

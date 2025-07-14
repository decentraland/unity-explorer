using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatController
    {
        private readonly CommunityVoiceChatTitlebarView view;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IObjectPool<PlayerEntryView> playerEntriesPool;
        private readonly List<PlayerEntryView> usedPlayerEntries = new ();

        public CommunityVoiceChatController(
            CommunityVoiceChatTitlebarView view,
            PlayerEntryView playerEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet:entry => entry.gameObject.SetActive(true),
                actionOnRelease:entry => entry.gameObject.SetActive(false));
        }

        private void AddSpeaker()
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry();
            entryView.transform.parent = view.SpeakersParent;
        }

        private void AddListener()
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry();
            entryView.transform.parent = view.ListenersParent;
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
    }
}

using System;
using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchPresenter : IDisposable
    {
        private static readonly Vector2 GRID_CELL_SIZE_AS_MODERATOR = new (90, 100);
        private static readonly Vector2 GRID_CELL_SIZE_NORMAL = new (90, 74);

        private readonly CommunityVoiceChatSearchView view;

        public CommunityVoiceChatSearchPresenter(CommunityVoiceChatSearchView view)
        {
            this.view = view;
            view.RequestToSpeakSection.gameObject.SetActive(false);
        }

        public void RefreshCounters(int listenersCount, int requestToSpeakCount)
        {
            view.ListenersCounter.text = $"({listenersCount})";
            view.RequestToSpeakCounter.text = $"({requestToSpeakCount})";
            view.RequestToSpeakSection.gameObject.SetActive(view.RequestToSpeakParent.transform.childCount >= 1);
        }

        public void SetGridCellSizes(bool isUserModerator)
        {
            view.RequestToSpeakGridLayout.cellSize = isUserModerator ? GRID_CELL_SIZE_AS_MODERATOR : GRID_CELL_SIZE_NORMAL;
        }

        public void Dispose()
        {
        }
    }
}

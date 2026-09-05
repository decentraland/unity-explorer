using SuperScrollView;
using System;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public class FriendPanelSectionView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; } = null!;
        [field: SerializeField] public SectionLoadingView LoadingObject { get; private set; } = null!;
        [field: SerializeField] public GameObject EmptyState { get; private set; } = null!;

        // TODO Replace events with direct invocations: the lifecycle of the view is fully under our control
        public event Action Enable;
        public event Action Disable;

        public void SetActive(bool isActive) =>
            gameObject.SetActive(isActive);

        private void OnEnable() =>
            Enable?.Invoke();

        private void OnDisable() =>
            Disable?.Invoke();

        public void SetLoadingState(bool isLoading)
        {
            if (isLoading)
                LoadingObject.Show();
            else
                LoadingObject.Hide();
        }

        public void SetEmptyState(bool isEmpty) =>
            EmptyState.SetActive(isEmpty);

        public void SetScrollViewState(bool active) =>
            LoopList?.gameObject.SetActive(active);
    }
}

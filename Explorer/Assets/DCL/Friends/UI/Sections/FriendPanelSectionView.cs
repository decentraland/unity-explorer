using SuperScrollView;
using System;
using UnityEngine;

namespace DCL.Friends.UI.Sections
{
    public class FriendPanelSectionView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; }
        [field: SerializeField] public GameObject LoadingObject { get; private set; }
        [field: SerializeField] public GameObject EmptyState { get; private set; }

        public event Action Enable;
        public event Action Disable;

        public void SetActive(bool isActive) =>
            gameObject.SetActive(isActive);

        private void OnEnable() =>
            Enable?.Invoke();

        private void OnDisable() =>
            Disable?.Invoke();

        public void SetLoadingState(bool isLoading) =>
            LoadingObject.SetActive(isLoading);

        public void SetEmptyState(bool isEmpty)
        {
            EmptyState.SetActive(isEmpty);
            LoopList.gameObject.SetActive(!isEmpty);
        }
    }
}

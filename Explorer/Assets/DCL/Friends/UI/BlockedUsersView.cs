using SuperScrollView;
using System;
using UnityEngine;

namespace DCL.Friends.UI
{
    public class BlockedUsersView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; }
        [field: SerializeField] public GameObject EmptyState { get; private set; }

        public event Action Enable;
        public event Action Disable;

        public void SetActive(bool isActive) =>
            gameObject.SetActive(isActive);

        private void OnEnable() =>
            Enable?.Invoke();

        private void OnDisable() =>
            Disable?.Invoke();
    }
}

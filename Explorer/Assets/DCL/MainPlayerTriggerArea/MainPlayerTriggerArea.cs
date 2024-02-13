using System;
using UnityEngine;

namespace DCL.MainPlayerTriggerArea
{
    public class MainPlayerTriggerArea : MonoBehaviour
    {
        public BoxCollider boxCollider;

        public void OnTriggerEnter(Collider other)
        {
            OnEnteredTrigger?.Invoke();
        }

        public void OnTriggerExit(Collider other)
        {
            OnExitedTrigger?.Invoke();
        }

        public event Action OnEnteredTrigger;
        public event Action OnExitedTrigger;
    }
}

using System;
using UnityEngine;

namespace DCL.MainPlayerTriggerArea
{
    public class MainPlayerTriggerArea : MonoBehaviour
    {
        public BoxCollider boxCollider;

        public Action OnEnteredTrigger;
        public Action OnExitedTrigger;

        public void OnTriggerEnter(Collider other)
        {
            OnEnteredTrigger?.Invoke();
        }

        public void OnTriggerExit(Collider other)
        {
            OnExitedTrigger?.Invoke();
        }

        public void ClearEvents()
        {
            OnEnteredTrigger = null;
            OnExitedTrigger = null;
        }
    }
}

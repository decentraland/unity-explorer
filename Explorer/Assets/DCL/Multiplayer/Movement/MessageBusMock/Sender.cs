using System.Collections;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Sender: MonoBehaviour
    {
        [SerializeField] private MessageBus messageBus;
        [SerializeField] private int maxSendAmount;

        public bool startSending;

        private void Update()
        {
            // if (startSending)
            // {
            //     StartCoroutine(PackageSentLoop());
            // }

            if (startSending && !isSending)
                StartCoroutine(PackageSentLoop());
            else if (!startSending && isSending)
            {
                StopAllCoroutines();
                isSending = false;
            }
        }

        private bool isSending;
        private IEnumerator PackageSentLoop()
        {
            isSending = true;
            var amount = 0;

            while (amount < maxSendAmount)
            {
                amount++;

                messageBus.Send(UnityEngine.Time.unscaledTime, transform.position);
                yield return new WaitForSeconds(messageBus.PackageSentRate);
            }

            isSending = false;
        }
    }
}

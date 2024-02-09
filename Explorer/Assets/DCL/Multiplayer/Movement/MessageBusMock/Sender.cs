using System.Collections;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Sender : MonoBehaviour
    {
        [SerializeField] private MessageBus messageBus;

        private IEnumerator Start()
        {
            while (true)
            {
                messageBus.Send(UnityEngine.Time.unscaledTime, transform.position);
                yield return new WaitForSeconds(messageBus.PackageSentRate);
            }
        }
    }
}

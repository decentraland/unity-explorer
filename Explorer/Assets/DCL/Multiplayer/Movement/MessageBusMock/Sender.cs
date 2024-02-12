using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Sender : MonoBehaviour
    {
        public int packageLost;
        public bool saveData;

        [SerializeField] private MessageBus messageBus;

        private SnapshotRecorder recorder;
        private CharacterController characterController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (saveData)
            {
                recorder = new SnapshotRecorder(characterController, messageBus);
                StartCoroutine(recorder.StartRecording());
            }

            StartCoroutine(StartSendPackages());
        }

        private void Update()
        {
            // Simulate package lost
            if (UnityEngine.Input.GetKeyUp(KeyCode.Space))
                packageLost++;
        }

        private void OnDestroy()
        {
            if (saveData)
                recorder.SaveRecordingToFile();
        }

        private IEnumerator StartSendPackages()
        {
            yield return new WaitForSeconds(messageBus.InitialLag);

            while (true)
            {
                if (packageLost > 0)
                    packageLost--;
                else
                    messageBus.Send(UnityEngine.Time.unscaledTime, characterController.transform.position, characterController.velocity);


                yield return new WaitForSeconds(messageBus.PackageSentRate + (Random.Range(0, messageBus.Jitter) * messageBus.PackageSentRate));
            }
        }
    }
}

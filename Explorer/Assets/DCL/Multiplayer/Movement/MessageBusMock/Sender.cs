using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Sender : MonoBehaviour
    {
        private const int HISTORY_SIZE = 3; // Adjust size for smoothing

        private readonly List<Vector3> velocityHistory = new ();
        public int packageLost;
        public bool saveData;

        [SerializeField] private MessageBus messageBus;
        [SerializeField] private GameObject lostText;
        [SerializeField] private GameObject pointMark;

        [SerializeField] private bool avarageAcceleration;

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
            if (Input.GetKeyUp(KeyCode.RightShift))
            {
                packageLost++;
                lostText.SetActive(false);
                lostText.SetActive(true);
            }
        }

        private void FixedUpdate()
        {
            velocityHistory.Add(characterController.velocity);

            if (velocityHistory.Count >= HISTORY_SIZE)
                velocityHistory.RemoveAt(0); // Keep the queue size constant
        }

        private void OnDestroy()
        {
            if (saveData)
                recorder.SaveRecordingToFile();
        }

        private IEnumerator StartSendPackages()
        {
            while (true)
            {
                if (packageLost > 0)
                    packageLost--;
                else if (!Input.GetKey(KeyCode.Space))
                {
                    messageBus.Send(Time.unscaledTime, characterController.transform.position, characterController.velocity, CalculateAverageAcceleration());
                    PutMark();
                }

                yield return new WaitForSeconds(messageBus.PackageSentRate);
            }
        }

        private void PutMark()
        {
            GameObject mark = Instantiate(pointMark);
            mark.transform.position = characterController.transform.position + (Vector3.up * 0.1f);
            mark.SetActive(true);
        }

        private Vector3 CalculateAverageAcceleration()
        {
            if (!avarageAcceleration)
                return velocityHistory.Count != 0 ? (velocityHistory[^1] - characterController.velocity) / Time.fixedDeltaTime : Vector3.zero;

            Vector3 acceleration = Vector3.zero;

            if (velocityHistory.Count > 1) // Ensure there are at least two velocities to compare
            {
                // Starting at 1 because we are looking at pairs, and there's no pair for the first element alone
                for (var i = 1; i < velocityHistory.Count; i++)
                {
                    Vector3 v1 = velocityHistory[^(i + 1)]; // Earlier velocity
                    Vector3 v2 = velocityHistory[^i]; // Later velocity

                    acceleration += (v2 - v1) / Time.fixedDeltaTime;
                }

                acceleration /= velocityHistory.Count - 1; // Correct division for averaging
            }

            return acceleration;
        }
    }
}

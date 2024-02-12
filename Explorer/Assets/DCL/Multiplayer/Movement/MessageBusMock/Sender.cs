using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class Sender : MonoBehaviour
    {
        public int packageLost;

        [SerializeField] private MessageBus messageBus;

        private SnapshotRecorder recorder;
        private void Start()
        {
            recorder = new SnapshotRecorder(transform, messageBus);

            StartCoroutine(recorder.StartRecording());
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
                    messageBus.Send(UnityEngine.Time.unscaledTime, transform.position);

                yield return new WaitForSeconds(messageBus.PackageSentRate);
            }
        }
    }

    [Serializable]
    public class SnapshotRecorder
    {
        private const float JITTER = 0.1f;

        private SerializableList<Snapshot> snapshots;

        private readonly Transform target;
        private readonly MessageBus messageBus;

        public SnapshotRecorder(Transform target, MessageBus messageBus)
        {
            this.target = target;
            this.messageBus = messageBus;

            snapshots = new SerializableList<Snapshot> { list = new List<Snapshot>() };
        }

        public IEnumerator StartRecording()
        {
            while (true)
            {
                var snapshot = new Snapshot
                {
                    timestamp = UnityEngine.Time.unscaledTime,
                    position = target.position,
                    controls = new Controls
                    {
                        forward = UnityEngine.Input.GetKey(KeyCode.W),
                        backward = UnityEngine.Input.GetKey(KeyCode.S),
                        left = UnityEngine.Input.GetKey(KeyCode.A),
                        right = UnityEngine.Input.GetKey(KeyCode.D),
                    },
                };

                // Debug.Log(JsonUtility.ToJson(snapshot));

                snapshots.list.Add(snapshot);
                yield return new WaitForSeconds(messageBus.PackageSentRate + (Random.Range(0, JITTER) * messageBus.PackageSentRate));
            }
        }

        public void SaveRecordingToFile()
        {
            string jsonData = JsonUtility.ToJson(snapshots);
            string filePath = Path.Combine(Application.streamingAssetsPath, "simpleFlatMove_2_11_v2.json");

            if (!string.IsNullOrEmpty(jsonData))
            {
                File.WriteAllText(filePath, jsonData);
                Debug.Log($"Data saved to {filePath}");
            }
        }

        [Serializable]
        public class SerializableList<T>
        {
            public List<T> list;
        }

        [Serializable]
        public class Snapshot
        {
            public float timestamp;
            public Vector3 position;
            public Controls controls;
        }

        [Serializable]
        public class Controls
        {
            public bool forward;
            public bool backward;
            public bool left;
            public bool right;
        }
    }
}

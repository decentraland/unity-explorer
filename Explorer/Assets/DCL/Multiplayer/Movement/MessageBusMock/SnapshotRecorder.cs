using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    [Serializable]
    public class SnapshotRecorder
    {

        private SerializableList<Snapshot> snapshots;

        private readonly CharacterController target;
        private readonly MessageBus messageBus;

        public SnapshotRecorder(CharacterController character, MessageBus messageBus)
        {
            this.target = character;
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
                    position = target.transform.position,
                    velocity = target.velocity,
                    // controls = new Controls
                    // {
                    //     forward = UnityEngine.Input.GetKey(KeyCode.W),
                    //     backward = UnityEngine.Input.GetKey(KeyCode.S),
                    //     left = UnityEngine.Input.GetKey(KeyCode.A),
                    //     right = UnityEngine.Input.GetKey(KeyCode.D),
                    // },
                };

                // Debug.Log(JsonUtility.ToJson(snapshot));

                snapshots.list.Add(snapshot);
                yield return new WaitForSeconds(messageBus.PackageSentRate + (Random.Range(0, messageBus.PackagesJitter) * messageBus.PackageSentRate));
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
            public Vector3 velocity;
            // public Controls controls;
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

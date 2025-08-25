using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace DCL.Nametags
{
    [RequireComponent(typeof(UIDocument))]
    public class NametagStressTest : MonoBehaviour
    {
        [Header("Stress Test Configuration")]
        [SerializeField] private int nametagCount = 1000;
        [SerializeField] private float movementSpeed = 100f;
        [SerializeField] private float screenBoundary = 100f;
        [SerializeField] private bool enableMovement = true;

        private VisualElement rootElement;
        private readonly List<NametagData> nametagDataList = new ();

        private float lastUpdateTime;

        private readonly string[] sampleUsernames =
        {
            "PlayerOne", "TestUser", "GamerTag", "Explorer", "Wanderer",
            "Builder", "Creator", "Adventurer", "Traveler", "Visitor"
        };

        private readonly Color[] sampleColors =
        {
            Color.white, Color.cyan, Color.yellow, Color.green,
            Color.magenta, new (1f, 0.5f, 0f), Color.red,
        };

        private struct NametagData
        {
            public NametagElement Element;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public bool IsVisible;
        }

        private void Awake() =>
            rootElement = GetComponent<UIDocument>().rootVisualElement;

        private void Update()
        {
            if (!enableMovement || nametagDataList.Count == 0)
                return;

            for (int i = 0; i < nametagDataList.Count; i++)
                UpdateNametagPosition(i);
        }

        private void UpdateNametagPosition(int index)
        {
            var data = nametagDataList[index];

            // Update position
            data.Position += data.Velocity * (Time.deltaTime * movementSpeed);

            // Screen wrapping/bouncing
            var screenSize = new Vector2(Screen.width, Screen.height);

            if (data.Position.x <= -screenBoundary || data.Position.x >= screenSize.x + screenBoundary)
                data.Velocity.x = -data.Velocity.x;

            if (data.Position.y <= -screenBoundary || data.Position.y >= screenSize.y + screenBoundary)
                data.Velocity.y = -data.Velocity.y;

            data.Position.x = Mathf.Clamp(data.Position.x, -screenBoundary, screenSize.x + screenBoundary);
            data.Position.y = Mathf.Clamp(data.Position.y, -screenBoundary, screenSize.y + screenBoundary);

            // Apply transform efficiently
            var elementTransform = data.Element.transform;
            elementTransform.position = new Vector3(data.Position.x, data.Position.y, 0);

            // Update scale based on position (simulate distance)
            float distanceFromCenter = Vector2.Distance(data.Position, screenSize * 0.5f);
            float normalizedDistance = distanceFromCenter / (screenSize.magnitude * 0.5f);
            data.Scale = Mathf.Lerp(1.2f, 0.6f, normalizedDistance);
            elementTransform.scale = Vector3.one * data.Scale;

            nametagDataList[index] = data;
        }

        [ContextMenu("Spawn Nametags")]
        public void SpawnNametags()
        {
            int spawnCount = nametagCount;
            ClearNametags();

            var screenSize = new Vector2(Screen.width, Screen.height);

            for (int i = 0; i < spawnCount; i++)
            {
                var nametagElement = new NametagElement();

                // Set random data
                string username = sampleUsernames[Random.Range(0, sampleUsernames.Length)];
                Color userColor = sampleColors[Random.Range(0, sampleColors.Length)];
                string walletId = Random.Range(0x1000, 0xFFFF).ToString("X4");
                bool verified = Random.Range(0f, 1f) < 0.3f;
                bool official = Random.Range(0f, 1f) < 0.1f;

                nametagElement.SetData(username, userColor, walletId, verified, official);

                // Occasionally show messages
                if (Random.Range(0f, 1f) < 0.4f)
                {
                    string[] messages = { "Hello world!", "Nice to meet you", "How are you?", "Great game!", "See you later" };

                    nametagElement.DisplayMessage(
                        messages[Random.Range(0, messages.Length)],
                        Random.Range(0f, 1f) < 0.2f, // mention
                        Random.Range(0f, 1f) < 0.1f, // private
                        false, // own message
                        "", "", Color.white, false, ""
                    );
                }

                var nametagData = new NametagData
                {
                    Element = nametagElement,
                    Position = new Vector2(
                        Random.Range(0, screenSize.x),
                        Random.Range(0, screenSize.y)
                    ),
                    Velocity = new Vector2(
                        Random.Range(-1f, 1f),
                        Random.Range(-1f, 1f)
                    ).normalized,
                    Scale = 1f,
                    IsVisible = true
                };

                nametagDataList.Add(nametagData);
                rootElement.Add(nametagElement);

                // Set initial position
                nametagElement.transform.position = new Vector3(nametagData.Position.x, nametagData.Position.y, 0);
            }

            Debug.Log($"Spawned {spawnCount} nametags. Total in scene: {nametagDataList.Count}");
        }

        [ContextMenu("Clear Nametags")]
        public void ClearNametags()
        {
            foreach (var data in nametagDataList)
            {
                if (data.Element != null && data.Element.parent != null)
                    rootElement.Remove(data.Element);
            }

            nametagDataList.Clear();

            Debug.Log("Cleared all nametags");
        }

        [ContextMenu("Toggle Movement")]
        public void ToggleMovement()
        {
            enableMovement = !enableMovement;
            Debug.Log($"Movement {(enableMovement ? "enabled" : "disabled")}");
        }

        [ContextMenu("Say HI")]
        public void SayHi()
        {
            foreach (var data in nametagDataList)
            {
                if (data.Element is { parent: not null })
                {
                    data.Element.DisplayMessage("HELLO!", false, false, false, string.Empty, string.Empty, Color.black, false, string.Empty);
                }
            }
        }

        private void OnDestroy() =>
            ClearNametags();
    }
}

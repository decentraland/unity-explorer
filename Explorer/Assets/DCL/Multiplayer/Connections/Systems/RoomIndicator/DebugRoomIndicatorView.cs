using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.RoomIndicator;
using DCL.Multiplayer.Profiles.Entities;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Systems
{
    public class DebugRoomIndicatorView : MonoBehaviour
    {
        [Serializable]
        internal struct Coloring
        {
            public RoomSource source;
            public string indicator;
            public Color color;
        }

        [SerializeField] private Coloring[] colors;
        [SerializeField] private SpriteRenderer background = null!;
        [SerializeField] private TMP_Text text = null!;

        private RectTransform rectTransform = null!;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void SetRooms(RoomSource source)
        {
            Color color = Color.white;
            var indicator = "N/A";

            foreach (Coloring coloring in colors)
            {
                if (source == coloring.source)
                {
                    color = coloring.color;
                    indicator = coloring.indicator;
                    break;
                }
            }

            background.color = color;
            text.text = indicator;
        }

        public void UpdateTransparency(float alpha)
        {
            text.alpha = alpha;
            background.color = new Color(background.color.r, background.color.g, background.color.b, alpha);
        }

        public void Attach(SpriteRenderer nameTagBackground)
        {
            // For island attach it to the right of the name tag
            // For gatekeeper attach it to the left of the name tag

            rectTransform.pivot = Vector2.right; // to the left

            var nameTagTransform = (RectTransform)nameTagBackground.transform;
            rectTransform.SetParent(nameTagTransform, false);
            rectTransform.ResetLocalTRS();

            Vector2 localShift = nameTagBackground.size / 2f;

            rectTransform.anchoredPosition = new Vector2(-localShift.x, 0);
        }
    }
}

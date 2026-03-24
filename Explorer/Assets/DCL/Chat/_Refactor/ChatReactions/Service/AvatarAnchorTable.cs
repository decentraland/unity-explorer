using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Maps avatar wallet IDs to live world-space positions.
    /// Each slot is addressable by a <c>byte</c> index that particles store
    /// to track which avatar they belong to.
    /// </summary>
    public sealed class AvatarAnchorTable
    {
        public const byte ANCHOR_NONE = ChatReactionsParticle.ANCHOR_NONE;
        public const string LOCAL_PLAYER_ID = "__local_player__";

        private const int MAX_ANCHORS = ANCHOR_NONE; // 0..254 usable, 255 = none

        // Slot data — parallel arrays indexed by anchor byte.
        private readonly string?[] walletIds = new string?[MAX_ANCHORS];
        private readonly Vector3[] positions = new Vector3[MAX_ANCHORS];
        private readonly bool[] active = new bool[MAX_ANCHORS];
        private readonly bool[] visible = new bool[MAX_ANCHORS];
        private int highWaterMark;

        /// <summary>
        /// Returns an existing slot for <paramref name="walletId"/>, or allocates
        /// the first inactive slot. Falls back to overwriting the oldest active slot
        /// only when all slots are occupied.
        /// </summary>
        public byte Allocate(string walletId, Vector3 initialPosition)
        {
            byte existing = FindExistingAnchor(walletId);
            if (existing != ANCHOR_NONE)
            {
                positions[existing] = initialPosition;
                return existing;
            }

            int free = FindFirstInactiveSlot();
            if (free >= 0)
                return OccupySlot(free, walletId, initialPosition);

            return OverwriteOldestSlot(walletId, initialPosition);
        }

        /// <summary>
        /// Refreshes all active anchor positions from the avatar position provider.
        /// Deactivates anchors whose avatar has left the scene.
        /// Only scans up to the high-water mark for efficiency.
        /// </summary>
        public void Refresh(IAvatarReactionPosition? avatarPosition)
        {
            if (avatarPosition == null) return;

            Profiler.BeginSample("ChatReactions.AnchorTable.Refresh");

            for (int i = 0; i < highWaterMark; i++)
            {
                if (!active[i]) continue;

                Vector3? pos = ResolveAvatarPosition(avatarPosition, i);

                if (pos.HasValue)
                    positions[i] = pos.Value;
                else
                    DeactivateSlot(i);
            }

            Profiler.EndSample();
        }

        public Vector3 GetPosition(byte index) =>
            positions[index];

        public bool IsActive(byte index) =>
            index < MAX_ANCHORS && active[index];

        public bool IsVisible(byte index) =>
            index < MAX_ANCHORS && visible[index];

        public int CountVisible()
        {
            int count = 0;

            for (int i = 0; i < highWaterMark; i++)
            {
                if (visible[i])
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Updates visibility for all active anchors using a single viewport check per anchor.
        /// Call once per frame before culling. Local player anchor is always visible.
        /// </summary>
        public void UpdateVisibility(Camera cam, float maxDistanceSqr)
        {
            if (cam == null)
            {
                for (int i = 0; i < highWaterMark; i++)
                    visible[i] = active[i];

                return;
            }

            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < highWaterMark; i++)
            {
                if (!active[i])
                {
                    visible[i] = false;
                    continue;
                }

                if (walletIds[i] == LOCAL_PLAYER_ID)
                {
                    visible[i] = true;
                    continue;
                }

                visible[i] = IsOnScreen(cam, camPos, positions[i], maxDistanceSqr);
            }
        }

        internal static bool IsOnScreen(Camera cam, Vector3 camPos, Vector3 worldPos, float maxDistSqr)
        {
            float dx = worldPos.x - camPos.x;
            float dy = worldPos.y - camPos.y;
            float dz = worldPos.z - camPos.z;

            if (dx * dx + dy * dy + dz * dz > maxDistSqr)
                return false;

            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }

        private byte FindExistingAnchor(string walletId)
        {
            for (int i = 0; i < highWaterMark; i++)
            {
                if (active[i] && walletIds[i] == walletId)
                    return (byte)i;
            }

            return ANCHOR_NONE;
        }

        private int FindFirstInactiveSlot()
        {
            for (int i = 0; i < MAX_ANCHORS; i++)
            {
                if (!active[i])
                    return i;
            }

            return -1;
        }

        private byte OverwriteOldestSlot(string walletId, Vector3 position)
        {
            int slot = highWaterMark % MAX_ANCHORS;
            return OccupySlot(slot, walletId, position);
        }

        private byte OccupySlot(int slot, string walletId, Vector3 position)
        {
            walletIds[slot] = walletId;
            positions[slot] = position;
            active[slot] = true;

            if (slot >= highWaterMark)
                highWaterMark = slot + 1;

            return (byte)slot;
        }

        // ── Refresh helpers ─────────────────────────────────────────

        private Vector3? ResolveAvatarPosition(IAvatarReactionPosition avatarPosition, int slotIndex) =>
            walletIds[slotIndex] == LOCAL_PLAYER_ID
                ? avatarPosition.GetLocalPlayerHeadPosition()
                : avatarPosition.GetHeadPosition(walletIds[slotIndex]!);

        private void DeactivateSlot(int slotIndex)
        {
            active[slotIndex] = false;
            walletIds[slotIndex] = null;
        }
    }
}

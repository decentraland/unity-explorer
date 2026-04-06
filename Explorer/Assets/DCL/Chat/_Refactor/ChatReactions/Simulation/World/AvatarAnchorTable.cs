using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
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
        private const string DEBUG_WALLET_PREFIX = "__debug_nearby_";

        /// <summary>Number of usable anchor slots (0..254). Slot 255 is reserved as ANCHOR_NONE sentinel.</summary>
        public const int MAX_ANCHORS = ANCHOR_NONE; // 0..254 usable, 255 = none

        // Slot data — parallel arrays indexed by anchor byte.
        private readonly string?[] walletIds = new string?[MAX_ANCHORS];
        private readonly Vector3[] positions = new Vector3[MAX_ANCHORS];
        private readonly bool[] active = new bool[MAX_ANCHORS];
        private readonly bool[] visible = new bool[MAX_ANCHORS];

        // O(1) wallet → slot lookup, kept in sync with the parallel arrays.
        private readonly Dictionary<string, byte> walletToSlot = new ();

        // Upper bound for iteration loops. Shrinks when trailing slots are deactivated.
        private int slotScanLimit;

        public int ActiveSlotCount => walletToSlot.Count;
        public int SlotScanLimit => slotScanLimit;
        public int SlotCapacity => MAX_ANCHORS;

        /// <summary>
        /// Returns an existing slot for <paramref name="walletId"/>, or allocates
        /// the first inactive slot. Falls back to force-evicting an occupied slot
        /// only when all slots are exhausted (unreachable with the current 200-avatar cap).
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

            return ForceEvictSlot(walletId, initialPosition);
        }

        /// <summary>
        /// Refreshes all active anchor positions from the avatar position provider.
        /// Deactivates anchors whose avatar has left the scene.
        /// Only scans up to the slot scan limit for efficiency.
        /// </summary>
        public void Refresh(IAvatarReactionPosition? avatarPosition)
        {
            if (avatarPosition == null) return;

            Profiler.BeginSample("ChatReactions.AnchorTable.Refresh");

            for (int i = 0; i < slotScanLimit; i++)
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

            for (int i = 0; i < slotScanLimit; i++)
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
            Profiler.BeginSample("ChatReactions.World.AnchorVisibility");

            if (cam == null)
            {
                for (int i = 0; i < slotScanLimit; i++)
                    visible[i] = active[i];

                Profiler.EndSample();
                return;
            }

            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < slotScanLimit; i++)
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

            Profiler.EndSample();
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

        /// <summary>
        /// Returns the alive count for a given wallet from the provided per-anchor counts.
        /// Returns -1 if the wallet has no active anchor (useful for debug display).
        /// </summary>
        public int FindAliveForWallet(string walletId, int[] alivePerAnchor)
        {
            if (!walletToSlot.TryGetValue(walletId, out byte slot))
                return -1;

            return alivePerAnchor[slot];
        }

        private byte FindExistingAnchor(string walletId) =>
            walletToSlot.TryGetValue(walletId, out byte slot) ? slot : ANCHOR_NONE;

        private int FindFirstInactiveSlot()
        {
            for (int i = 0; i < MAX_ANCHORS; i++)
            {
                if (!active[i])
                    return i;
            }

            return -1;
        }

        // NOTE: Currently evicts the slot at (slotScanLimit % MAX_ANCHORS), which is arbitrary.
        // With the current 200-avatar scene cap and 255 usable slots, this path is unreachable.
        // If the avatar cap ever exceeds MAX_ANCHORS, consider adding LRU eviction:
        //   - Track lastUsedFrame[] per slot, updated on Allocate and Refresh
        //   - Evict the slot with the smallest lastUsedFrame value (true LRU)
        private byte ForceEvictSlot(string walletId, Vector3 position)
        {
            int slot = slotScanLimit % MAX_ANCHORS;
            return OccupySlot(slot, walletId, position);
        }

        private byte OccupySlot(int slot, string walletId, Vector3 position)
        {
            // If overwriting an occupied slot (eviction), remove the old mapping first.
            if (active[slot] && walletIds[slot] != null)
                walletToSlot.Remove(walletIds[slot]);

            walletIds[slot] = walletId;
            positions[slot] = position;
            active[slot] = true;
            walletToSlot[walletId] = (byte)slot;

            if (slot >= slotScanLimit)
                slotScanLimit = slot + 1;

            return (byte)slot;
        }

        // ── Refresh helpers ─────────────────────────────────────────

        private Vector3? ResolveAvatarPosition(IAvatarReactionPosition avatarPosition, int slotIndex)
        {
            string? walletId = walletIds[slotIndex];

            if (walletId == LOCAL_PLAYER_ID)
                return avatarPosition.GetLocalPlayerHeadPosition();

            // Debug nearby anchors keep their last-known position from Allocate().
            // They don't exist in the avatar system, so GetHeadPosition would return null
            // and cause the anchor to be deactivated every frame.
            if (walletId != null && walletId.StartsWith(DEBUG_WALLET_PREFIX))
                return positions[slotIndex];

            return avatarPosition.GetHeadPosition(walletId!);
        }

        private void DeactivateSlot(int slotIndex)
        {
            if (walletIds[slotIndex] != null)
                walletToSlot.Remove(walletIds[slotIndex]);

            active[slotIndex] = false;
            walletIds[slotIndex] = null;

            ShrinkScanLimit();
        }

        private void ShrinkScanLimit()
        {
            while (slotScanLimit > 0 && !active[slotScanLimit - 1])
                slotScanLimit--;
        }
    }
}

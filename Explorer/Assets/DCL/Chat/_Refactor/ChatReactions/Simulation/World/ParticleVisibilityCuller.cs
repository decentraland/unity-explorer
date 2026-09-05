using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// Produces a dense index list of particles visible to the camera.
    /// Anchored particles use the pre-computed per-anchor visibility from <see cref="AvatarAnchorTable"/>.
    /// Unanchored particles fall back to a per-particle viewport check.
    /// </summary>
    public sealed class ParticleVisibilityCuller
    {
        private readonly int[] visibleIndices;

        public int[] VisibleIndices => visibleIndices;

        public ParticleVisibilityCuller(int capacity)
        {
            visibleIndices = new int[capacity];
        }

        /// <summary>
        /// Returns the number of visible particles. Results are in <see cref="VisibleIndices"/>.
        /// </summary>
        public int Cull(ChatReactionsParticle[] buffer, int count,
            Camera cam, AvatarAnchorTable anchors, float maxDistanceSqr)
        {
            Profiler.BeginSample("ChatReactions.World.Cull");

            if (cam == null)
            {
                for (int i = 0; i < count; i++)
                    visibleIndices[i] = i;

                Profiler.EndSample();
                return count;
            }

            Vector3 camPos = cam.transform.position;
            int visibleCount = 0;

            for (int i = 0; i < count; i++)
            {
                ref readonly var p = ref buffer[i];

                if (p.anchorIndex != ChatReactionsParticle.ANCHOR_NONE)
                {
                    if (!anchors.IsVisible(p.anchorIndex))
                        continue;
                }
                else
                {
                    if (!AvatarAnchorTable.IsOnScreen(cam, camPos, p.pos, maxDistanceSqr))
                        continue;
                }

                visibleIndices[visibleCount++] = i;
            }

            Profiler.EndSample();
            return visibleCount;
        }
    }
}

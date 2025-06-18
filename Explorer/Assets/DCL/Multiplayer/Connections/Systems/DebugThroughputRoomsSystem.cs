using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Nametags;
using DCL.Profiling;
using ECS.Abstract;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class DebugThroughputRoomsSystem : BaseUnityLoopSystem
    {
        private readonly BufferBinding island;
        private readonly BufferBinding scene;
        private readonly bool buffersInitialized;
        private readonly IRoomHub roomHub;
        private readonly float stepSeconds;
        private readonly DebugWidgetVisibilityBinding infoVisibilityBinding;

        private readonly List<(string name, string value)> mutableListScene;
        private readonly ElementBinding<IReadOnlyList<(string name, string value)>> livekitListScene;

        private readonly List<(string name, string value)> mutableListIsland;
        private readonly ElementBinding<IReadOnlyList<(string name, string value)>> livekitListIsland;

        private float current;

        public DebugThroughputRoomsSystem(
            World world,
            IRoomHub roomHub,
            IDebugContainerBuilder debugBuilder,
            ThroughputBufferBunch islandBufferBunch,
            ThroughputBufferBunch sceneBufferBunch,
            float stepSeconds = 1f //one second by default
        ) : base(world)
        {
            this.roomHub = roomHub;
            this.stepSeconds = stepSeconds;
            DebugWidgetBuilder? infoWidget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ROOM_THROUGHPUT);

            if (infoWidget == null)
            {
                buffersInitialized = false;
                return;
            }

            infoVisibilityBinding = new DebugWidgetVisibilityBinding(true);
            infoWidget.SetVisibilityBinding(infoVisibilityBinding);

            this.island = BufferBinding.CreateAndAttach(infoWidget, islandBufferBunch, "Island");
            this.scene = BufferBinding.CreateAndAttach(infoWidget, sceneBufferBunch, "Scene");

            mutableListScene = new List<(string name, string value)>();
            livekitListScene = new ElementBinding<IReadOnlyList<(string name, string value)>>(mutableListScene);
            infoWidget.AddList("Livekit Scene", livekitListScene);

            mutableListIsland = new List<(string name, string value)>();
            livekitListIsland = new ElementBinding<IReadOnlyList<(string name, string value)>>(mutableListIsland);
            infoWidget.AddList("Livekit Island", livekitListIsland);

            buffersInitialized = true;
        }

        protected override void Update(float t)
        {
            if (buffersInitialized == false)
                return;

#if ENABLE_PROFILER
            if (UnityEngine.Profiling.Profiler.enabled && UnityEngine.Profiling.Profiler.IsCategoryEnabled(NetworkProfilerCounters.CATEGORY))
            {
                (ulong incoming, ulong outgoing) = island.Collect();

                NetworkProfilerCounters.LIVEKIT_ISLAND_RECEIVED.Value = incoming;
                NetworkProfilerCounters.LIVEKIT_ISLAND_SEND.Value = outgoing;

                (incoming, outgoing) = scene.Collect();

                NetworkProfilerCounters.LIVEKIT_SCENE_RECEIVED.Value = incoming;
                NetworkProfilerCounters.LIVEKIT_SCENE_SEND.Value = outgoing;
            }
#endif

            current += t;

            if (current >= stepSeconds)
            {
                current = 0;

                if (infoVisibilityBinding.IsConnectedAndExpanded)
                {
                    island.CollectAndDraw();
                    scene.CollectAndDraw();

                    CollectAndDisplay(roomHub.SceneRoom().Room().Participants, mutableListScene, livekitListScene);
                    CollectAndDisplay(roomHub.IslandRoom().Participants, mutableListIsland, livekitListIsland);
                }
                else
                {
                    island.Reset();
                    scene.Reset();
                }
            }
        }

        private static void CollectAndDisplay(IParticipantsHub participantsHub, List<(string name, string value)> mutableList, ElementBinding<IReadOnlyList<(string name, string value)>> binding)
        {
            var participants = participantsHub.RemoteParticipantIdentities();
            mutableList.Clear();

            foreach (string sid in participants)
            {
                var participant = participantsHub.RemoteParticipant(sid);
                if (participant == null) continue;
                mutableList.Add((sid, participant.ConnectionQuality.ToString()));
            }

            binding.SetAndUpdate(mutableList);
        }

        private readonly struct BufferBinding
        {
            private readonly ThroughputBufferBunch bufferBunch;
            private readonly ElementBinding<ulong> incoming;
            private readonly ElementBinding<ulong> outgoing;

            private BufferBinding(ThroughputBufferBunch bufferBunch, ElementBinding<ulong> incoming, ElementBinding<ulong> outgoing)
            {
                this.bufferBunch = bufferBunch;
                this.incoming = incoming;
                this.outgoing = outgoing;
            }

            public static BufferBinding CreateAndAttach(DebugWidgetBuilder widgetBuilder, ThroughputBufferBunch bunch, string name)
            {
                var incoming = new ElementBinding<ulong>(0);
                var outgoing = new ElementBinding<ulong>(0);

                widgetBuilder.AddGroup(
                    name,
                    (new DebugConstLabelDef("Incoming"), new DebugLongMarkerDef(incoming, DebugLongMarkerDef.Unit.Bytes)),
                    (new DebugConstLabelDef("Outgoing"), new DebugLongMarkerDef(incoming, DebugLongMarkerDef.Unit.Bytes))
                );

                return new BufferBinding(bunch, incoming, outgoing);
            }

            public void CollectAndDraw()
            {
                CollectAndDraw(bufferBunch.Incoming, incoming);
                CollectAndDraw(bufferBunch.Outgoing, outgoing);
            }

            public (ulong incoming, ulong outgoing) Collect()
            {
                ulong incomingBytes = bufferBunch.Incoming.CurrentAmount();
                ulong outgoingBytes = bufferBunch.Outgoing.CurrentAmount();

                return (incomingBytes, outgoingBytes);
            }

            public void Reset()
            {
                bufferBunch.Incoming.Clear();
                bufferBunch.Outgoing.Clear();
            }

            private static void CollectAndDraw(IThroughputBuffer buffer, ElementBinding<ulong> binding)
            {
                ulong incomingBytes = buffer.CurrentAmount();
                buffer.Clear();
                binding.Value = incomingBytes;
            }
        }
    }
}

using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Nametags;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class DebugThroughputRoomsSystem : BaseUnityLoopSystem
    {
        private readonly BufferBinding island;
        private readonly BufferBinding scene;
        private readonly float stepSeconds;
        private float current;

        public DebugThroughputRoomsSystem(
            World world,
            IDebugContainerBuilder debugBuilder,
            ThroughputBufferBunch islandBufferBunch,
            ThroughputBufferBunch sceneBufferBunch,
            float stepSeconds = 1f //one second by default
        ) : base(world)
        {
            this.stepSeconds = stepSeconds;
            DebugWidgetBuilder? infoWidget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ROOM_THROUGHPUT);

            if (infoWidget == null)
                return;

            this.island = BufferBinding.CreateAndAttach(infoWidget, islandBufferBunch, "island");
            this.scene = BufferBinding.CreateAndAttach(infoWidget, sceneBufferBunch, "scene");
        }

        protected override void Update(float t)
        {
            current += t;

            if (current >= stepSeconds)
            {
                current = 0;
                island.CollectAndDraw();
                scene.CollectAndDraw();
            }
        }

        private readonly struct BufferBinding
        {
            private readonly ThroughputBufferBunch bufferBunch;
            private readonly ElementBinding<string> incoming;
            private readonly ElementBinding<string> outgoing;

            private BufferBinding(ThroughputBufferBunch bufferBunch, ElementBinding<string> incoming, ElementBinding<string> outgoing)
            {
                this.bufferBunch = bufferBunch;
                this.incoming = incoming;
                this.outgoing = outgoing;
            }

            public static BufferBinding CreateAndAttach(DebugWidgetBuilder widgetBuilder, ThroughputBufferBunch bunch, string name)
            {
                var incoming = new ElementBinding<string>(string.Empty);
                var outgoing = new ElementBinding<string>(string.Empty);
                widgetBuilder.AddControl(new DebugConstLabelDef($"{name} - incoming"), new DebugTextFieldDef(incoming));
                widgetBuilder.AddControl(new DebugConstLabelDef($"{name} - outgoing"), new DebugTextFieldDef(outgoing));
                return new BufferBinding(bunch, incoming, outgoing);
            }

            public void CollectAndDraw()
            {
                CollectAndDraw(bufferBunch.Incoming, incoming);
                CollectAndDraw(bufferBunch.Outgoing, outgoing);
            }

            private static void CollectAndDraw(IThroughputBuffer buffer, ElementBinding<string> binding)
            {
                ulong incomingBytes = buffer.CurrentAmount();
                buffer.Clear();
                binding.Value = Utility.ByteSize.ToReadableString(incomingBytes);
            }
        }
    }
}

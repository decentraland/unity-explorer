using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using SceneRunner.Debugging.Hub;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class WorldInfoPlugin : IDCLGlobalPlugin
    {
        private readonly IWorldInfoHub worldInfoHub;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IChatHistory chatHistory;

        public WorldInfoPlugin(IWorldInfoHub worldInfoHub, IDebugContainerBuilder debugContainerBuilder, IChatHistory chatHistory)
        {
            this.worldInfoHub = worldInfoHub;
            this.debugContainerBuilder = debugContainerBuilder;
            this.chatHistory = chatHistory;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            var poseBinding = new ElementBinding<Vector2Int>(Vector2Int.zero);
            var entityIdBinding = new ElementBinding<int>(0);

            debugContainerBuilder
               .AddWidget("World Info")
               .AddControl(
                    new DebugConstLabelDef("Scene Coordinates"),
                    new DebugVector2IntFieldDef(poseBinding)
                )
               .AddControl(
                    new DebugConstLabelDef("Entity Id"),
                    new DebugIntFieldDef(entityIdBinding)
                )
               .AddControl(
                    new DebugConstLabelDef("Show"),
                    new DebugButtonDef("Print to Chat", OnClick)
                );

            void OnClick()
            {
                var world = worldInfoHub.WorldInfo(poseBinding.Value);

                if (world == null)
                {
                    chatHistory.AddMessage(ChatMessage.NewFromSystem($"World not found: {poseBinding.Value}"));
                    return;
                }

                var message = ChatMessage.NewFromSystem(world.EntityComponentsInfo(entityIdBinding.Value));
                chatHistory.AddMessage(message);
            }

            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            //ignore
        }

        public void Dispose()
        {
            //ignore
        }
    }
}

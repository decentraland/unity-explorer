using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using Utility.Arch;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Hides or shows the local player's own avatar, via the same HiddenPlayerComponent the client
    ///     already uses for blocked/banned users (AvatarShapeVisibilitySystem hides any avatar carrying
    ///     it, every frame, regardless of camera mode or distance). Useful for aerial/free-camera
    ///     screenshots where the avatar would otherwise appear under the camera.
    /// </summary>
    public class SetAvatarHiddenTool : McpTool
    {
        private readonly World world;
        private readonly Entity playerEntity;

        public override string Name => "set_avatar_hidden";

        public override string Description =>
            "Hide or show the local player's own avatar model. Useful before taking aerial/top-down "
            + "screenshots so the avatar doesn't appear in frame.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Boolean("hidden", "True to hide the avatar, false to show it again. Default true.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public SetAvatarHiddenTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool hidden = arguments.GetBool("hidden", true);

            ref HiddenPlayerComponent attached = ref world.TryGetRef<HiddenPlayerComponent>(playerEntity, out bool isAttached);

            if (hidden)
            {
                if (!isAttached)
                    world.Add(playerEntity, new HiddenPlayerComponent { Reason = HiddenPlayerComponent.HiddenReason.MapCapture });
                else
                    attached.Reason |= HiddenPlayerComponent.HiddenReason.MapCapture;
            }
            else if (isAttached)
            {
                attached.Reason &= ~HiddenPlayerComponent.HiddenReason.MapCapture;

                if (attached.Reason == 0)
                    world.TryRemove<HiddenPlayerComponent>(playerEntity);
            }

            return UniTask.FromResult(McpToolResult.Json(new JObject { ["hidden"] = hidden }));
        }
    }
}

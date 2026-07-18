using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Plays or stops an avatar emote through <see cref="IGlobalWorldActions" />, the same intent path
    ///     a scene uses, so agents can verify emote-driven behaviour without touching the avatar directly.
    /// </summary>
    public class TriggerEmoteTool : IMcpTool
    {
        private readonly IGlobalWorldActions globalWorldActions;

        public string Name => "trigger_emote";

        public string Description =>
            "Play an avatar emote by URN (e.g. a base emote like 'wave', 'dance', 'clap'), or stop the current one with stop: true.";

        public JObject InputSchema =>
            McpJsonSchema.Object()
                          .String("urn", "Emote URN or base emote id (wave, dance, clap...).")
                          .Boolean("loop", "Loop the emote until stopped. Default false.")
                          .Boolean("stop", "Stop the currently playing emote instead of triggering one.")
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public TriggerEmoteTool(IGlobalWorldActions globalWorldActions)
        {
            this.globalWorldActions = globalWorldActions;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool stop = arguments.GetBool("stop", false);
            string urn = arguments.GetString("urn", string.Empty);

            if (!stop && string.IsNullOrEmpty(urn))
                return McpToolResult.Error("urn is required (or pass stop: true).");

            await UniTask.SwitchToMainThread(ct);

            if (stop)
            {
                globalWorldActions.StopEmote();
                return McpToolResult.Text("Emote stopped.");
            }

            bool loop = arguments.GetBool("loop", false);
            globalWorldActions.TriggerEmote(urn, loop, AvatarEmoteMask.AemFullBody);

            return McpToolResult.Text($"Emote '{urn}' triggered{(loop ? " (looping)" : string.Empty)}.");
        }
    }
}

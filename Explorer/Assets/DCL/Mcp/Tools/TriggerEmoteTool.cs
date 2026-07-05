using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Mcp.Protocol;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.Mcp.Tools
{
    public class TriggerEmoteTool : IMcpTool
    {
        private readonly IGlobalWorldActions globalWorldActions;

        public string Name => "trigger_emote";

        public string Description =>
            "Play an avatar emote by URN (e.g. a base emote like 'wave', 'dance', 'clap'), or stop the current one with stop: true.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""urn"": { ""type"": ""string"", ""description"": ""Emote URN or base emote id (wave, dance, clap...)."" },
                    ""loop"": { ""type"": ""boolean"", ""description"": ""Loop the emote until stopped. Default false."" },
                    ""stop"": { ""type"": ""boolean"", ""description"": ""Stop the currently playing emote instead of triggering one."" }
                }
            }";

        internal TriggerEmoteTool(IGlobalWorldActions globalWorldActions)
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

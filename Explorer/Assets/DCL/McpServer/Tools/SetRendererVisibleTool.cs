using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Toggles renderers whose hierarchy path contains a substring. Diagnostic tool for
    ///     visually bisecting which renderer owns a given region of the frame.
    /// </summary>
    public class SetRendererVisibleTool : McpTool
    {
        public override string Name => "set_renderer_visible";

        public override string Description =>
            "Enable or disable every Renderer whose transform hierarchy path contains the given substring (case-insensitive). Returns how many were toggled. Diagnostic: bisect which renderer owns a screen region.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("pathContains", "Substring matched against the full transform path.", isRequired: true)
                  .Boolean("visible", "true to enable, false to disable.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string? needle = arguments["pathContains"]?.ToString();
            bool visible = arguments["visible"]?.ToObject<bool>() ?? true;

            if (string.IsNullOrEmpty(needle))
                return McpToolResult.Error("pathContains is required.");

            await UniTask.SwitchToMainThread(cancellationToken: ct);

            var count = 0;

            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (FullPath(r.transform).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                r.enabled = visible;
                count++;
            }

            return McpToolResult.Text($"toggled {count} renderers matching '{needle}' to visible={visible}");
        }

        private static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);

            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, '/').Insert(0, t.name);
            }

            return sb.ToString();
        }
    }
}

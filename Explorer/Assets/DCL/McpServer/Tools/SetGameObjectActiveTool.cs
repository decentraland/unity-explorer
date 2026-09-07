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
    ///     Activates or deactivates every GameObject whose hierarchy path contains a substring.
    ///     Diagnostic sibling of set_renderer_visible that also reaches systems driving indirect
    ///     draws (instancing managers) which have no Renderer to toggle.
    /// </summary>
    public class SetGameObjectActiveTool : McpTool
    {
        public override string Name => "set_gameobject_active";

        public override string Description =>
            "SetActive on every GameObject whose transform hierarchy path contains the given substring (case-insensitive). Matches inactive objects too. Returns how many were toggled.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("pathContains", "Substring matched against the full transform path.", isRequired: true)
                  .Boolean("active", "true to activate, false to deactivate.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string? needle = arguments["pathContains"]?.ToString();
            bool active = arguments["active"]?.ToObject<bool>() ?? true;

            if (string.IsNullOrEmpty(needle))
                return McpToolResult.Error("pathContains is required.");

            await UniTask.SwitchToMainThread(cancellationToken: ct);

            var count = 0;
            var names = new StringBuilder();

            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (FullPath(t).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                t.gameObject.SetActive(active);
                count++;

                if (count <= 8)
                    names.Append(t.name).Append("; ");
            }

            return McpToolResult.Text($"toggled {count} objects matching '{needle}' to active={active}: {names}");
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

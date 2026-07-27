#if MCP_TEST_AUTOMATION
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Invokes a public static method that opted in with <see cref="McpCallableAttribute" />, by type, assembly
    ///     and arguments: the escape hatch for reaching a purpose-built test hook the client exposes.
    ///     <para>
    ///         The opt-in is what bounds the tool. Resolution without it is a sweep of every loaded assembly, which
    ///         reaches the stored identity, the filesystem, process control and every other static in the process —
    ///         an unmarked method is therefore refused whatever its type or assembly. The two build gates sit on top:
    ///         the <c>MCP_TEST_AUTOMATION</c> define removes this file from release builds, and inside the builds that
    ///         keep it the tool still needs <c>--mcp-reflection</c>. All three matter — see the security model in
    ///         <c>docs/mcp-automation.md</c> before relaxing any of them.
    ///     </para>
    /// </summary>
    public class CallStaticMethodTool : McpTool
    {
        public override string Name => "call_static_method";

        public override string Description =>
            "Invoke a public static method inside the running client and return its value — the escape hatch for "
            + "reaching a purpose-built test hook the client exposes. Only a method the client marked [McpCallable] "
            + "resolves; every other method is refused, so this reaches the exposed hooks and nothing else. Name the "
            + "type by its full, namespace-qualified name, optionally narrowed by assembly, and pass parameters "
            + "as a JSON array matched by position; strings, booleans, numbers and enums convert. The return value is "
            + "JSON for primitives and its string form otherwise. Prefer a dedicated tool where one exists — "
            + "get_scene_state already reports scene readiness.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("type", "Full, namespace-qualified name of the type declaring the hook — its namespace, not its folder path, e.g. Some.Namespace.TestHooks.", isRequired: true)
                  .String("method", "Public static method to invoke. It must carry [McpCallable]; nothing else resolves.", isRequired: true)
                  .String("assembly", "Assembly name to narrow the type search, e.g. SceneRunner.Scene.")
                  .AnyArray("parameters", "Arguments matched by position. Omit for a method that takes none.");

        // The effect is whatever the marked hook does, so nothing may be assumed about it.
        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: true, idempotent: false);

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string typeName = arguments.GetString("type", string.Empty);
            string methodName = arguments.GetString("method", string.Empty);
            string assemblyName = arguments.GetString("assembly", string.Empty);

            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
                return UniTask.FromResult(McpToolResult.Error("Provide type and method."));

            var parameters = arguments["parameters"] as JArray ?? new JArray();

            if (!TryResolveType(typeName, assemblyName, out Type type))
                return UniTask.FromResult(McpToolResult.Error($"No loaded type named '{typeName}'{(assemblyName.Length > 0 ? $" in assembly '{assemblyName}'" : string.Empty)}. Use the full name including its namespace."));

            if (!TryResolveMethod(type, methodName, parameters.Count, out MethodInfo method, out string methodError))
                return UniTask.FromResult(McpToolResult.Error(methodError));

            if (!TryBind(method, parameters, out object?[] bound, out string bindError))
                return UniTask.FromResult(McpToolResult.Error(bindError));

            object? returned;

            try { returned = method.Invoke(null, bound); }
            catch (TargetInvocationException e) { return UniTask.FromResult(McpToolResult.Error($"{type.Name}.{methodName} threw {e.InnerException?.GetType().Name}: {e.InnerException?.Message}")); }

            var result = new JObject
            {
                ["type"] = type.FullName,
                ["method"] = method.Name,
                ["returnType"] = method.ReturnType.Name,
                ["value"] = ComponentProperty.ToToken(returned),
            };

            return UniTask.FromResult(McpToolResult.Json(result));
        }

        /// <summary>
        ///     Finds a loaded type by its full, namespace-qualified name, optionally restricted to one assembly. Only
        ///     the full name is accepted: a short-name sweep would have to materialize every type of every loaded
        ///     assembly, and callers already spell the namespace out. The sweep stays unrestricted because reaching a
        ///     type buys nothing on its own — <see cref="TryResolveMethod" /> is where the opt-in gate sits.
        /// </summary>
        private static bool TryResolveType(string typeName, string assemblyName, out Type type)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName.Length > 0 && !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                Type? found = assembly.GetType(typeName, false, true);

                if (found != null)
                {
                    type = found;
                    return true;
                }
            }

            type = null!;
            return false;
        }

        /// <summary>
        ///     Picks the overload to invoke among the methods the type opted in with
        ///     <see cref="McpCallableAttribute" />. A method without the attribute is skipped before its name is even
        ///     compared, so a caller learns nothing about it and nothing outside the opted-in surface is invocable.
        /// </summary>
        private static bool TryResolveMethod(Type type, string methodName, int parameterCount, out MethodInfo method, out string error)
        {
            var rejectedOverloads = new StringBuilder();

            foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (!candidate.IsDefined(typeof(McpCallableAttribute), false))
                    continue;

                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    continue;

                if (!candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == parameterCount)
                {
                    method = candidate;
                    error = string.Empty;
                    return true;
                }

                if (rejectedOverloads.Length > 0)
                    rejectedOverloads.Append(", ");

                rejectedOverloads.Append(candidate.GetParameters().Length);
            }

            method = null!;

            // One answer for "no such method" and for "exists but not marked": which of the two it is says something
            // about the client that a caller reaching for an unexposed method has no business learning.
            error = rejectedOverloads.Length == 0
                ? $"'{type.FullName}' exposes no method named '{methodName}' to MCP. call_static_method invokes only public static methods marked [McpCallable]; mark the test hook you need to reach."
                : $"'{type.FullName}.{methodName}' has no [McpCallable] overload taking {parameterCount} parameter(s); the marked overloads take: {rejectedOverloads}.";

            return false;
        }

        private static bool TryBind(MethodInfo method, JArray parameters, out object?[] bound, out string error)
        {
            ParameterInfo[] expected = method.GetParameters();
            bound = new object?[expected.Length];

            for (var i = 0; i < expected.Length; i++)
            {
                if (ComponentProperty.TryConvert(parameters[i], expected[i].ParameterType, out bound[i], out error))
                    continue;

                error = $"parameter {i} ('{expected[i].Name}'): {error}";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
#endif

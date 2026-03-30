#if !UNITY_WEBGL || (UNITY_EDITOR && !EDITOR_DEBUG_WEBGL)
namespace SceneRuntime
{
    /// <summary>
    /// Adapts ClearScript's <see cref="Microsoft.ClearScript.V8.V8Script"/> to <see cref="ICompiledScript"/>,
    /// acting as a thin container that holds a pre-compiled script for repeated evaluation without re-parsing.
    /// </summary>
    public class V8CompiledScriptAdapter : ICompiledScript
    {
        public V8CompiledScriptAdapter(Microsoft.ClearScript.V8.V8Script v8Script)
        {
            this.V8Script = v8Script;
        }

        public Microsoft.ClearScript.V8.V8Script V8Script { get; }
    }
}
#endif

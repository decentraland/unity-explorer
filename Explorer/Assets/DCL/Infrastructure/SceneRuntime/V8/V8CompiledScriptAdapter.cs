namespace SceneRuntime
{
    public class V8CompiledScriptAdapter : ICompiledScript
    {
        public V8CompiledScriptAdapter(Microsoft.ClearScript.V8.V8Script v8Script)
        {
            this.V8Script = v8Script;
        }

        public Microsoft.ClearScript.V8.V8Script V8Script { get; }
    }
}

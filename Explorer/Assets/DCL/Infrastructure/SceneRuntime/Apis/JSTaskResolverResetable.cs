using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.SplitProxy;
using System;

namespace SceneRuntime.Apis
{
    public class JSTaskResolverResetable : IV8HostObject
    {
        private AutoResetUniTaskCompletionSource source;

        private readonly InvokeHostObject completed;
        private readonly InvokeHostObject reject;

        public JSTaskResolverResetable()
        {
            completed = Completed;
            reject = Reject;
        }

        public UniTask Task => source.Task;

        public void Reset()
        {
            source = AutoResetUniTaskCompletionSource.Create();
        }

        private void Completed(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            Completed();
        }

        private void Completed()
        {
            source.TrySetResult();
        }

        private void Reject(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            string message = args[0].GetString();
            Reject(message);
        }

        private void Reject(string message)
        {
            source.TrySetException(new ScriptEngineException(message));
        }

        void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(Completed)))
                value.SetHostObject(completed);
            else if (name.Equals(nameof(Reject)))
                value.SetHostObject(reject);
            else
                throw new NotImplementedException(
                    $"Named property {name.ToString()} is not implemented");
        }
    }
}

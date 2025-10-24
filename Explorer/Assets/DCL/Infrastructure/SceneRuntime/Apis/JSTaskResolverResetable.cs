using Cysharp.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.FastProxy;

namespace SceneRuntime.Apis
{
    public sealed class JSTaskResolverResetable : V8FastHostObject<JSTaskResolverResetable>
    {
        private AutoResetUniTaskCompletionSource source;

        static JSTaskResolverResetable()
        {
            Configure(static configuration =>
            {
                configuration.AddMethodGetter(nameof(Completed),
                    static (JSTaskResolverResetable self, in V8FastArgs args, in V8FastResult result) =>
                        self.Completed());

                configuration.AddMethodGetter(nameof(Reject),
                    static (JSTaskResolverResetable self, in V8FastArgs args, in V8FastResult result) =>
                        self.Reject(args.GetString(0)));
            });
        }

        public UniTask Task => source.Task;

        public void Reset()
        {
            source = AutoResetUniTaskCompletionSource.Create();
        }

        private void Completed()
        {
            source.TrySetResult();
        }

        private void Reject(string message)
        {
            source.TrySetException(new ScriptEngineException(message));
        }
    }
}

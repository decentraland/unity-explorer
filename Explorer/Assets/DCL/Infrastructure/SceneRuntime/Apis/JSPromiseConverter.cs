using Cysharp.Threading.Tasks;
#if !UNITY_WEBGL
using Microsoft.ClearScript.JavaScript;
#endif

namespace SceneRuntime.Apis
{
    public static class JSPromiseConverter
    {
        public static object ToPromise<T>(UniTask<T> uniTask, IJavaScriptEngine engine)
        {
#if UNITY_WEBGL
            return engine.CreatePromise(uniTask);
#else
            return uniTask.AsTask().ToPromise()!;
#endif
        }

        public static object ToPromise(UniTask uniTask, IJavaScriptEngine engine)
        {
#if UNITY_WEBGL
            return engine.CreatePromise(uniTask);
#else
            return uniTask.AsTask().ToPromise()!;
#endif
        }
    }
}

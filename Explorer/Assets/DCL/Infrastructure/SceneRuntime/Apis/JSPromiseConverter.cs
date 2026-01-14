using System;
using System.Threading.Tasks;
#if !UNITY_WEBGL
using Microsoft.ClearScript.JavaScript;
#endif

namespace SceneRuntime.Apis
{
    public static class JSPromiseConverter
    {
        public static object ToPromise<T>(Task<T> task, IJavaScriptEngine engine)
        {
#if UNITY_WEBGL
            return engine.CreatePromiseFromTask(task);
#else
            return task.ToPromise()!;
#endif
        }

        public static object ToPromise(Task task, IJavaScriptEngine engine)
        {
#if UNITY_WEBGL
            return engine.CreatePromiseFromTask(task);
#else
            return task.ToPromise()!;
#endif
        }
    }
}

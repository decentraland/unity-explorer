using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;

namespace SceneRuntime.Apis
{
    public static class Extensions
    {
        public static object ToPromise<T>(this UniTask<T> uniTask) =>
            uniTask.AsTask()!.ToPromise()!;
    }
}

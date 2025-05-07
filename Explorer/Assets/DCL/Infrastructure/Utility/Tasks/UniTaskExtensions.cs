using Cysharp.Threading.Tasks;

namespace Utility.Tasks
{
    public static class UniTaskExtensions
    {
        public static UniTask<T> AsUniTaskResult<T>(this T value) =>
            new (value);
    }
}

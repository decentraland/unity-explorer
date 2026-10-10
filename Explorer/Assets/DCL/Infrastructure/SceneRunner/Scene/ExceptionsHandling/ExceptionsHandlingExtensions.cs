using Cysharp.Threading.Tasks;

namespace SceneRunner.Scene.ExceptionsHandling
{
    public static class ExceptionsHandlingExtensions
    {
        public static UniTask<T> ReportAndRethrowException<T>(this UniTask<T> uniTask, IJavaScriptApiExceptionsHandler exceptionsHandler) =>
            exceptionsHandler.ReportAndRethrowExceptionAsync(uniTask);

        public static UniTask ReportAndRethrowException(this UniTask uniTask, IJavaScriptApiExceptionsHandler exceptionsHandler) =>
            exceptionsHandler.ReportAndRethrowExceptionAsync(uniTask);
    }
}

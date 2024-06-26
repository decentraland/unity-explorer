using Cysharp.Threading.Tasks;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     Enriches API exceptions with additional data and report them before propagating a promise further via ClearScript
    ///     as it will loose the original stack trace
    /// </summary>
    public interface IJavaScriptApiExceptionsHandler
    {
        /// <summary>
        ///     Reports exception and rethrow as a part of the async process
        /// </summary>
        UniTask<T> ReportAndRethrowException<T>(UniTask<T> task);

        /// <summary>
        ///     <inheritdoc cref="ReportAndRethrowException{T}" />
        /// </summary>
        UniTask ReportAndRethrowException(UniTask task);

        void ReportApiException(Exception e);
    }
}

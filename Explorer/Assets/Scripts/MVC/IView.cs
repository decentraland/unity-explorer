using Cysharp.Threading.Tasks;
using System.Threading;

namespace MVC
{
    public interface IView
    {
        void SetDrawOrder(CanvasOrdering order);

        /// <summary>
        ///     Show the view without connection to data, data should be set before
        /// </summary>
        UniTask ShowAsync(CancellationToken ct);

        UniTask HideAsync(CancellationToken ct, bool isInstant = false);

        void SetCanvasActive(bool isActive);
    }
}

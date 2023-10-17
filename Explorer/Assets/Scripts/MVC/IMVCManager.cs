using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MVC
{
    /// <summary>
    ///     An entry point to show the view
    /// </summary>
    public interface IMVCManager
    {
        /// <summary>
        ///     Called externally to schedule a view opening
        /// </summary>
        /// <param name="command"></param>
        /// <typeparam name="TView"></typeparam>
        /// <typeparam name="TInputData"></typeparam>
        UniTask Show<TView, TInputData>(ShowCommand<TView, TInputData> command) where TView: MonoBehaviour, IView;
    }
}

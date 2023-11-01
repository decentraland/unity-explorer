using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public abstract class ControllerBase<TView> : ControllerBase<TView, ControllerNoData> where TView: MonoBehaviour, IView
    {
        protected ControllerBase(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public static ShowCommand<TView, ControllerNoData> IssueCommand() =>
            new (default(ControllerNoData));
    }

    /// <summary>
    ///     Base for the main controller (not sub-ordinate)
    /// </summary>
    public abstract class ControllerBase<TView, TInputData> : IController<TView, TInputData> where TView: MonoBehaviour, IView
    {
        public delegate TView ViewFactoryMethod();

        private readonly ViewFactoryMethod viewFactory;

        public static ViewFactoryMethod CreateLazily(TView prefab, Transform root) =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);

        public static ViewFactoryMethod Preallocate(TView prefab, Transform root, out TView instance)
        {
            TView instance2 = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
            instance = instance2;
            instance2.Hide(CancellationToken.None, true).Forget();
            return () => instance2;
        }

        /// <summary>
        ///     This way we maintain one-to-one known relation between TView and TInput
        /// </summary>
        /// <returns></returns>
        public static ShowCommand<TView, TInputData> IssueCommand(TInputData inputData) =>
            new (inputData);

        protected ControllerBase(ViewFactoryMethod viewFactory)
        {
            this.viewFactory = viewFactory;
        }

        protected TView viewInstance { get; private set; }

        protected TInputData inputData { get; private set; }

        public abstract CanvasOrdering.SortingLayer SortLayers { get; }

        public async UniTask LaunchViewLifeCycle(CanvasOrdering ordering, TInputData inputData, CancellationToken ct)
        {
            // make sure instance is provided (it can be instantiated lazily)
            if (viewInstance == null)
            {
                viewInstance = viewFactory();
                OnViewInstantiated();
            }
            this.inputData = inputData;

            viewInstance.SetDrawOrder(ordering);
            OnBeforeViewShow();

            await viewInstance.Show(ct);
            OnViewShow();

            await WaitForCloseIntent(ct);
        }

        async UniTask IController.HideView(CancellationToken ct)
        {
            OnViewClose();
            await viewInstance.Hide(ct);
        }

        /// <summary>
        ///     Called once when the view is instantiated
        /// </summary>
        protected virtual void OnViewInstantiated() { }

        /// <summary>
        ///     View is focused when the obscuring view disappears
        /// </summary>
        public virtual void OnFocus() { }

        /// <summary>
        ///     View is blurred when gets obscured by another view in the same stack
        /// </summary>
        public virtual void OnBlur() { }

        protected virtual void OnBeforeViewShow() { }

        protected virtual void OnViewShow() { }

        protected virtual void OnViewClose() { }

        protected abstract UniTask WaitForCloseIntent(CancellationToken ct);
    }
}

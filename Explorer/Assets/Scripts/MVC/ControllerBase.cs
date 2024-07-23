using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MVC
{
    public abstract class ControllerBase<TView> : ControllerBase<TView, ControllerNoData> where TView: IView
    {
        protected ControllerBase(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public static ShowCommand<TView, ControllerNoData> IssueCommand() =>
            new (default(ControllerNoData));
    }

    /// <summary>
    ///     Base for the main controller (not sub-ordinate)
    /// </summary>
    public abstract class ControllerBase<TView, TInputData> : IDisposable, IController<TView, TInputData> where TView: IView
    {
        public delegate TView ViewFactoryMethod();

        private readonly ViewFactoryMethod viewFactory;

        public static ViewFactoryMethod CreateLazily<TViewMono>(TViewMono prefab, Transform? root) where TViewMono: MonoBehaviour, TView =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root!)!;

        public static ViewFactoryMethod Preallocate<TViewMono>(TViewMono prefab, Transform root, out TViewMono instance) where TViewMono: MonoBehaviour, TView
        {
            TViewMono instance2 = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
            instance = instance2;
            instance2.HideAsync(CancellationToken.None, true).Forget();
            return () => instance2;
        }

        /// <summary>
        ///     This way we maintain one-to-one known relation between TView and TInput
        /// </summary>
        /// <returns></returns>
        public static ShowCommand<TView, TInputData> IssueCommand(TInputData inputData) =>
            new (inputData);

        private List<IMVCControllerModule> modules;

        protected ControllerBase(ViewFactoryMethod viewFactory)
        {
            this.viewFactory = viewFactory;
            State = ControllerState.ViewHidden;
        }

        protected TView viewInstance { get; private set; }

        protected TInputData inputData { get; private set; }

        public ControllerState State { get; private set; }

        public abstract CanvasOrdering.SortingLayer Layer { get; }

        /// <summary>
        ///     Add a module to the controller
        /// </summary>
        protected TModule AddModule<TModule>(TModule module) where TModule: class, IMVCControllerModule
        {
            modules ??= new List<IMVCControllerModule>();
            modules.Add(module);
            return module;
        }

        public async UniTask LaunchViewLifeCycleAsync(CanvasOrdering ordering, TInputData data, CancellationToken ct)
        {
            // make sure instance is provided (it can be instantiated lazily)
            if (viewInstance == null)
            {
                viewInstance = viewFactory();
                OnViewInstantiated();
            }

            this.inputData = data;

            viewInstance.SetDrawOrder(ordering);
            OnBeforeViewShow();

            await viewInstance.ShowAsync(ct);

            State = ControllerState.ViewFocused;

            OnViewShow();

            for (var i = 0; i < modules?.Count; i++)
                modules[i].OnViewShow();

            await WaitForCloseIntentAsync(ct);
        }

        public async UniTask HideViewAsync(CancellationToken ct)
        {
            State = ControllerState.ViewHidden;

            for (var i = 0; i < modules?.Count; i++)
                modules[i].OnViewHide();

            OnViewClose();
            await viewInstance.HideAsync(ct);
        }

        public void SetViewCanvasActive(bool isActive)
        {
            if (viewInstance != null)
                viewInstance.SetCanvasActive(isActive);
        }

        /// <summary>
        ///     Called once when the view is instantiated
        /// </summary>
        protected virtual void OnViewInstantiated() { }

        /// <summary>
        ///     View is focused when the obscuring view disappears
        /// </summary>
        public void Focus()
        {
            State = ControllerState.ViewFocused;

            for (var i = 0; i < modules?.Count; i++)
                modules[i].OnFocus();

            OnFocus();
        }

        /// <summary>
        ///     View is blurred when gets obscured by another view in the same stack
        /// </summary>
        public void Blur()
        {
            State = ControllerState.ViewBlurred;

            for (var i = 0; i < modules?.Count; i++)
                modules[i].OnBlur();

            OnBlur();
        }

        protected virtual void OnFocus() { }

        protected virtual void OnBlur() { }

        protected virtual void OnBeforeViewShow() { }

        protected virtual void OnViewShow() { }

        protected virtual void OnViewClose() { }

        protected abstract UniTask WaitForCloseIntentAsync(CancellationToken ct);

        public virtual void Dispose() { }
    }
}

using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MVC
{
    public abstract class MVCCheetSheet
    {
        private IMVCManager mvcManager;

        /// <summary>
        ///     It's happening in Plugin
        /// </summary>
        public void Registration()
        {
            var mvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), null);

            // prefabs are taken from Addressables in Plugin
            ExampleView prefab = new GameObject("bla-bla").AddComponent<ExampleView>();

            // Parent will be set once from the scene and propagated to the plugin
            // Do we need multiple parents?
            mvcManager.RegisterController(new ExampleController(ExampleController.Preallocate(prefab, null, out _)));

            this.mvcManager = mvcManager;
        }

        /// <summary>
        ///     Called from any side that can open views
        /// </summary>
        public void Consumer()
        {
            mvcManager.ShowAsync(ExampleController.IssueCommand(new ExampleParam("TEST"))).Forget();
        }

        public readonly struct ExampleParam
        {
            public readonly string UserId;

            public ExampleParam(string userId)
            {
                UserId = userId;
            }
        }

        public class ExampleController : ControllerBase<ExampleView, ExampleParam>
        {
            public ExampleController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

            public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

            protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
                viewInstance.CloseButton.OnClickAsync(ct);

            protected override void OnBeforeViewShow()
            {
                // Set some data based on Input
                viewInstance.Text.SetText(inputData.UserId);
            }
        }

        public class ExampleView : ViewBase, IView
        {
            [field: SerializeField]
            public Button CloseButton { get; private set; }

            [field: SerializeField]
            public TMP_Text Text { get; private set; }
        }

        public struct ExampleViewDataComponent
        {
            public string Value;
        }

        public class ExampleView2 : ViewBase, IView
        {
            [field: SerializeField]
            public TMP_Text Text { get; private set; }
        }
    }
}

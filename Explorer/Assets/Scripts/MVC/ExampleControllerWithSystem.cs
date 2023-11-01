using Arch.System;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MVC
{
    public partial class ExampleControllerWithSystem : ControllerBase<MVCCheetSheet.ExampleView2>
    {
        private readonly ExampleControllerSystem system;

        public ExampleControllerWithSystem(ViewFactoryMethod viewFactory, ExampleControllerSystem system) : base(viewFactory)
        {
            this.system = system;

            // Can't pass query method to the constructor as there is a circular connection with system
            system.SetQueryMethod(QueryDataQuery);
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Fullscreen;

        [Query]
        private void QueryData(in MVCCheetSheet.ExampleViewDataComponent component)
        {
            viewInstance.Text.text = component.Value;
        }

        protected override void OnViewShow()
        {
            system.InvokeQuery();

            system.Activate();
        }

        protected override void OnViewClose()
        {
            system.Deactivate();
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}

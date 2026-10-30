using Arch.System;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MVC
{
    public partial class ExampleControllerWithSystem : ControllerBase<MVCCheetSheet.ExampleView2>
    {
        private readonly BridgeSystemBinding<ExampleControllerSystem> binding;

        public ExampleControllerWithSystem(ViewFactoryMethod viewFactory, ExampleControllerSystem system) : base(viewFactory)
        {
            binding = new BridgeSystemBinding<ExampleControllerSystem>(this, QueryDataQuery, system);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        [Query]
        private void QueryData(in MVCCheetSheet.ExampleViewDataComponent component)
        {
            viewInstance.Text.text = component.Value;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}

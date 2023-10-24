using Cysharp.Threading.Tasks;
using MVC;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TestTopController : ControllerBase<TestTopView, MVCCheetSheet.ExampleParam>
{
    public TestTopController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Top;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

}

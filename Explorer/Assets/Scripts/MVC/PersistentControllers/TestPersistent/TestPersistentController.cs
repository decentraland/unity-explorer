using Cysharp.Threading.Tasks;
using MVC;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TestPersistentController : ControllerBase<TestPersistentView, MVCCheetSheet.ExampleParam>
{
    public TestPersistentController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Persistent;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

}

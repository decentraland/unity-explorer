using Cysharp.Threading.Tasks;
using MVC;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TestPopupController : ControllerBase<TestPopupView, MVCCheetSheet.ExampleParam>
{
    public TestPopupController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Popup;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

}

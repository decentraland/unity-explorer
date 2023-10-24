using Cysharp.Threading.Tasks;
using MVC;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TestPopup2Controller : ControllerBase<TestPopup2View, MVCCheetSheet.ExampleParam>
{
    public TestPopup2Controller(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Popup;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

}

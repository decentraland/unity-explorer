using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers
{
    public interface IMapRendererMarker
    {
        Vector2Int ParcelCoords { get; }
        Vector2 Pivot { get; }

        void ToggleSelection(bool isSelected);

        void SetIsSelected(bool isSelected) { }

        UniTaskVoid AnimateSelectionAsync(CancellationToken ct) =>
            new ();

        UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct) =>
            new ();
    }
}

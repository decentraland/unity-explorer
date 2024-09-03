using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Loading
{
    public interface IAttachmentLambdaResponse<out TResponseElement>
    {
        IEnumerable<TResponseElement> CountedElements();

        static IEnumerable<TResponseElement> DefaultCountedElements(int totalAmount, IReadOnlyList<TResponseElement> elements)
        {
            if (elements.Count != totalAmount)
                ReportHub.LogError(
                    ReportCategory.WEARABLE,
                    $"The amount of elements in the response is different than the total amount! total: {totalAmount}, elements: {elements.Count}"
                );

            int required = Mathf.Min(elements.Count, totalAmount);

            for (var i = 0; i < required; i++)
                yield return elements[i]!;
        }
    }

    public interface ILambdaResponseElement<out TElementDTO>
    {
        TElementDTO Entity { get; }

        IReadOnlyList<ElementIndividualDataDto> IndividualData { get; }
    }
}

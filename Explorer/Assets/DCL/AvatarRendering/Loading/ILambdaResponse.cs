using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    public interface ILambdaResponse<out TResponseElement>
    {
        IReadOnlyList<TResponseElement> Elements { get; }
        int TotalAmount { get; }
    }

    public interface ILambdaResponseElement<out TElementDTO>
    {
        TElementDTO Entity { get; }

        IReadOnlyList<ElementIndividualDataDto> IndividualData { get; }
    }
}

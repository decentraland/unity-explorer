using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    public interface IAttachmentLambdaResponse<out TResponseElement>
    {
        /// <summary>
        ///     Page of fetched elements
        /// </summary>
        IReadOnlyList<TResponseElement> Page { get; }

        /// <summary>
        ///     Total amount of elements that can be fetched
        /// </summary>
        int TotalAmount { get; }
    }

    public interface ILambdaResponseElement<out TElementDTO>
    {
        TElementDTO Entity { get; }

        IReadOnlyList<ElementIndividualDataDto> IndividualData { get; }
    }
}

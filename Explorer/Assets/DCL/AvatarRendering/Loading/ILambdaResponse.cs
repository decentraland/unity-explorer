using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    public interface IAttachmentLambdaResponse<out TResponseElement>
    {
        /// <summary>
        /// Requested page of available elements
        /// </summary>
        IReadOnlyList<TResponseElement> Page { get; }

        /// <summary>
        /// Total amount of available elements
        /// </summary>
        int TotalAmount { get; }
    }

    public interface ILambdaResponseElement<out TElementDTO>
    {
        TElementDTO Entity { get; }

        IReadOnlyList<ElementIndividualDataDto> IndividualData { get; }
    }
}

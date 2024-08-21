using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    public struct LambdaResponseWrap<TElement> : ILambdaResponse<ILambdaResponseElement<TElement>>
    {
        public IReadOnlyList<ILambdaResponseElement<TElement>> Elements { get; }

        public int TotalAmount { get; }

        public LambdaResponseWrap(IReadOnlyList<ILambdaResponseElement<TElement>> elements, int totalAmount)
        {
            Elements = elements;
            TotalAmount = totalAmount;
        }
    }
}

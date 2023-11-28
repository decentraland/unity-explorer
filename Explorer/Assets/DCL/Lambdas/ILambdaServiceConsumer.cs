using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCLServices.Lambdas
{
    public interface ILambdaServiceConsumer<TResponse, in TAdditionalData> where TResponse: PaginatedResponse
    {
        UniTask<(TResponse response, bool success)> CreateRequestAsync(string endPoint, int pageSize, int pageNumber, TAdditionalData additionalData, CancellationToken cancellationToken = default);
    }
}

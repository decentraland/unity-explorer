using Cysharp.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Utility.Types;

namespace DCL.WebRequests.SafetyNets
{
    public class SequenceSafetyNet : ISafetyNet
    {
        private readonly ISafetyNet[] safetyNets;

        public SequenceSafetyNet(params ISafetyNet[] safetyNets)
        {
            this.safetyNets = safetyNets;
        }

        public async UniTask<Result> ExecuteAsync(HttpMethod method, string url)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Error on safety nets:");

            foreach (ISafetyNet safetyNet in safetyNets)
            {
                var result = await safetyNet.ExecuteAsync(method, url);

                if (result.Success)
                    return result;

                sb.AppendLine(result.ErrorMessage ?? "Unknown error");
            }

            return Result.ErrorResult(sb.ToString());
        }
    }
}

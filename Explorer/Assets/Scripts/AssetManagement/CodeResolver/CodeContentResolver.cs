using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace AssetManagement.CodeResolver
{
    public class CodeContentResolver
    {
        private readonly IReadOnlyDictionary<AssetSource, ICodeContentProvider> providers;

        public CodeContentResolver()
        {
            providers = new Dictionary<AssetSource, ICodeContentProvider>
            {
                { AssetSource.WEB, new WebCodeContentProvider() },
            };
        }

        public UniTask<string> GetCodeContent(string contentUrl) =>
            providers[AssetSource.WEB].GetCodeAsync(contentUrl);
    }
}

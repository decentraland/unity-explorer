using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic.RealmUrl.Names
{
    public interface IRealmNamesMap
    {
        UniTask<string> UrlFromNameAsync(string name, CancellationToken token);
    }
}

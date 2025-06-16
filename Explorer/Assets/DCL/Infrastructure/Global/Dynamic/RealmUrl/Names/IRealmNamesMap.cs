using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Global.Dynamic.RealmUrl.Names
{
    public interface IRealmNamesMap
    {
        UniTask<Uri> UrlFromNameAsync(string name, CancellationToken token);
    }
}

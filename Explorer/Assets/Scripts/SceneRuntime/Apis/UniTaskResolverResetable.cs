using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using System;

public class UniTaskResolverResetable
{
    private AutoResetUniTaskCompletionSource _source;

    public void Reset()
    {
        _source = AutoResetUniTaskCompletionSource.Create();
    }

    public UniTask Task => _source.Task;

    [UsedImplicitly]
    public void Completed()
    {
        _source.TrySetResult();
    }

    [UsedImplicitly]
    public void Reject(string message)
    {
        _source.TrySetException(new Exception(message));
    }
}

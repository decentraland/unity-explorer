using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class DetachedUniTaskFlowTests
    {
        // Minimal UniTask surface: the analyzer matches on the
        // Cysharp.Threading.Tasks.UniTaskVoid metadata name plus method names
        // (Forget, SuppressToResultAsync, ...), so task-like stubs with async
        // method builders are enough to exercise every path.
        private const string UNITASK_STUB = @"
using System;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncUniTaskVoidMethodBuilder))]
    public struct UniTaskVoid
    {
        public void Forget() { }
    }

    [AsyncMethodBuilder(typeof(AsyncUniTaskMethodBuilder))]
    public struct UniTask
    {
        public static UniTask CompletedTask => default;
        public Awaiter GetAwaiter() => default;

        public struct Awaiter : ICriticalNotifyCompletion
        {
            public bool IsCompleted => true;
            public void GetResult() { }
            public void OnCompleted(Action continuation) { }
            public void UnsafeOnCompleted(Action continuation) { }
        }
    }

    public struct AsyncUniTaskVoidMethodBuilder
    {
        public static AsyncUniTaskVoidMethodBuilder Create() => default;
        public UniTaskVoid Task => default;
        public void SetResult() { }
        public void SetException(Exception exception) { }
        public void SetStateMachine(IAsyncStateMachine stateMachine) { }
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine { }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine { }
    }

    public struct AsyncUniTaskMethodBuilder
    {
        public static AsyncUniTaskMethodBuilder Create() => default;
        public UniTask Task => default;
        public void SetResult() { }
        public void SetException(Exception exception) { }
        public void SetStateMachine(IAsyncStateMachine stateMachine) { }
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine { }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine { }
    }

    public static class UniTaskExtensions
    {
        public static void Forget(this UniTask task) { }
        public static UniTask SuppressCancellationThrow(this UniTask task) => task;
        public static UniTask SuppressToResultAsync(this UniTask task) => task;
    }
}
";

        private static Task VerifyAsync(string members, string? extraFile = null)
        {
            var test = new CSharpAnalyzerTest<DetachedUniTaskFlowAnalyzer, DefaultVerifier>
            {
                TestCode = UNITASK_STUB + @"
public class DetachedFlowScenarios
{
" + members + @"
}",
            };

            if (extraFile != null)
                test.TestState.Sources.Add(extraFile);

            return test.RunAsync();
        }

        [Test]
        public Task ReportsUnguardedUniTaskVoidMethod() =>
            VerifyAsync(@"
    private async UniTaskVoid {|DCLA002:RunAsync|}()
    {
        await UniTask.CompletedTask;
    }
");

        [Test]
        public Task ReportsUnguardedUniTaskVoidLocalFunction() =>
            VerifyAsync(@"
    public void Trigger()
    {
        async UniTaskVoid {|DCLA002:PumpAsync|}() => await UniTask.CompletedTask;
        PumpAsync();
    }
");

        [Test]
        public Task ReportsUnguardedUniTaskVoidLambda() =>
            VerifyAsync(@"
    public void Trigger()
    {
        Func<UniTaskVoid> detached = {|DCLA002:async|} () => await UniTask.CompletedTask;
        detached();
    }
");

        [Test]
        public Task ReportsForgetOnUnguardedSameFileMethod() =>
            VerifyAsync(@"
    public void Trigger()
    {
        LoadAsync().{|DCLA002:Forget|}();
    }

    private async UniTask LoadAsync()
    {
        await UniTask.CompletedTask;
    }
");

        [Test]
        public Task ReportsWhenOnlyCancellationIsCaught() =>
            VerifyAsync(@"
    private async UniTaskVoid {|DCLA002:RunAsync|}()
    {
        try { await UniTask.CompletedTask; }
        catch (OperationCanceledException) { }
    }
");

        [Test]
        public Task CleanWhenBodyGuardedByCatchException() =>
            VerifyAsync(@"
    private async UniTaskVoid RunAsync()
    {
        try { await UniTask.CompletedTask; }
        catch (Exception) { }
    }
");

        [Test]
        public Task CleanWhenWholeBodyInsideTryWithGeneralCatch() =>
            VerifyAsync(@"
    private async UniTaskVoid RunAsync()
    {
        try
        {
            await UniTask.CompletedTask;
            await UniTask.CompletedTask;
        }
        catch { }
    }
");

        [Test]
        public Task CleanForgetOnGuardedSameFileMethod() =>
            VerifyAsync(@"
    public void Trigger()
    {
        LoadAsync().Forget();
    }

    private async UniTask LoadAsync()
    {
        try { await UniTask.CompletedTask; }
        catch (Exception) { }
    }
");

        [Test]
        public Task CleanForgetOnMethodFromAnotherFile() =>
            VerifyAsync(@"
    public void Trigger()
    {
        RemoteLoader.RunAsync().Forget();
    }
", extraFile: @"
using Cysharp.Threading.Tasks;

public static class RemoteLoader
{
    public static async UniTask RunAsync() => await UniTask.CompletedTask;
}
");

        [Test]
        public Task CleanForAwaitedUnguardedUniTask() =>
            VerifyAsync(@"
    public async UniTask OuterAsync() => await LoadAsync();

    private async UniTask LoadAsync() => await UniTask.CompletedTask;
");

        [Test]
        public Task CleanWhenSuppressToResultAsyncChainGuards() =>
            VerifyAsync(@"
    private async UniTaskVoid RunAsync() => await LoadAsync().SuppressToResultAsync();

    private async UniTask LoadAsync() => await UniTask.CompletedTask;
");

        [Test]
        public Task ReportsUniTaskVoidFlowOnlyAtDeclarationNotAtForgetCallsite() =>
            VerifyAsync(@"
    public void Trigger()
    {
        RunAsync().Forget();
    }

    private async UniTaskVoid {|DCLA002:RunAsync|}()
    {
        await UniTask.CompletedTask;
    }
");

        [Test]
        public Task CleanWhenForgetTakesExceptionHandler() =>
            VerifyAsync(@"
    public void Trigger()
    {
        LoadAsync().Forget(e => { });
    }

    private async UniTask LoadAsync()
    {
        await UniTask.CompletedTask;
    }
", extraFile: @"
using System;

namespace Cysharp.Threading.Tasks
{
    public static class UniTaskForgetWithHandlerExtensions
    {
        public static void Forget(this UniTask task, Action<Exception> exceptionHandler) { }
    }
}
");

        [Test]
        public Task ReportsWhenOnlySuppressCancellationThrowGuards() =>
            VerifyAsync(@"
    private async UniTaskVoid {|DCLA002:RunAsync|}()
    {
        await LoadAsync().SuppressCancellationThrow();
    }

    private async UniTask LoadAsync() => await UniTask.CompletedTask;
");

        [Test]
        public Task CleanWhenForgetIsUnrelatedDomainMethod()
        {
            var test = new CSharpAnalyzerTest<DetachedUniTaskFlowAnalyzer, DefaultVerifier>
            {
                TestCode = UNITASK_STUB + @"
public class TrackedEntry
{
    public void Forget() { }
}

public class DetachedFlowScenarios
{
    public void Trigger()
    {
        CreateEntry().Forget();
    }

    private TrackedEntry CreateEntry() => new TrackedEntry();
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsWhenGuardOnlyInsideNestedLambda() =>
            VerifyAsync(@"
    private async UniTaskVoid {|DCLA002:RunAsync|}()
    {
        Action guardedElsewhere = () =>
        {
            try { } catch (Exception) { }
        };

        guardedElsewhere();
        await UniTask.CompletedTask;
    }
");

        [Test]
        public Task ReportsForgetOnUnguardedSameFileExtensionMethodCalledInReducedForm()
        {
            var test = new CSharpAnalyzerTest<DetachedUniTaskFlowAnalyzer, DefaultVerifier>
            {
                TestCode = UNITASK_STUB + @"
public static class ScenarioExtensions
{
    public static async UniTask LoadValueAsync(this int value) => await UniTask.CompletedTask;
}

public class DetachedFlowScenarios
{
    public void Trigger()
    {
        42.LoadValueAsync().{|DCLA002:Forget|}();
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsForgetOnUnguardedSameFilePartialMethod()
        {
            var test = new CSharpAnalyzerTest<DetachedUniTaskFlowAnalyzer, DefaultVerifier>
            {
                TestCode = UNITASK_STUB + @"
public partial class DetachedFlowScenarios
{
    public void Trigger()
    {
        LoadAsync().{|DCLA002:Forget|}();
    }

    private partial UniTask LoadAsync();
}

public partial class DetachedFlowScenarios
{
    private async partial UniTask LoadAsync() => await UniTask.CompletedTask;
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsForgetOnUnguardedSameFileLocalFunction() =>
            VerifyAsync(@"
    public void Trigger()
    {
        LoadAsync().{|DCLA002:Forget|}();

        async UniTask LoadAsync() => await UniTask.CompletedTask;
    }
");
    }
}

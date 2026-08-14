using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class PooledRentalLeakTests
    {
        private const string POOL_STUB = @"
using System.Collections.Generic;
using UnityEngine.Pool;

namespace UnityEngine.Pool
{
    public interface IObjectPool<T> where T : class
    {
        T Get();
        PooledObject<T> Get(out T v);
        void Release(T element);
    }

    public struct PooledObject<T> : System.IDisposable where T : class
    {
        public void Dispose() { }
    }

    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        public T Get() => null;
        public PooledObject<T> Get(out T v) { v = null; return default; }
        public void Release(T element) { }
    }

    public static class ListPool<T>
    {
        public static List<T> Get() => new List<T>();
        public static void Release(List<T> toRelease) { }
    }

    public static class HashSetPool<T>
    {
        public static HashSet<T> Get() => new HashSet<T>();
        public static void Release(HashSet<T> toRelease) { }
    }

    public static class DictionaryPool<TKey, TValue>
    {
        public static Dictionary<TKey, TValue> Get() => new Dictionary<TKey, TValue>();
        public static void Release(Dictionary<TKey, TValue> toRelease) { }
    }
}

public class Payload : System.IDisposable
{
    public int Value;
    public void Dispose() { }
}

public class CustomPool : IObjectPool<Payload>
{
    public Payload Get() => null;
    public PooledObject<Payload> Get(out Payload v) { v = null; return default; }
    public void Release(Payload element) { }
}

public class Cache
{
    public Payload Get() => null;
    public void Release(Payload element) { }
}
";

        private static Task VerifyAsync(string members, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<PooledRentalLeakAnalyzer, DefaultVerifier>
            {
                TestCode = POOL_STUB + @"
public class SomeService
{
    private readonly ObjectPool<Payload> pool = new ObjectPool<Payload>();
    private readonly IObjectPool<Payload> poolInterface = new ObjectPool<Payload>();
    private readonly CustomPool customPool = new CustomPool();
    private readonly Cache cache = new Cache();
    private List<int> stored;

" + members + @"
}",
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        [Test]
        public Task CleanWhenRentalAssignedInsideArgument() =>
            VerifyAsync(@"
    private void Attach(object target) { }

    public void Process()
    {
        Payload transform;
        Attach(transform = pool.Get());
        transform.Value = 1;
    }
");

        [Test]
        public Task ReportsStaticListPoolRentalNeverReleased() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers = {|DCLA004:ListPool<int>.Get()|};
        numbers.Add(1);
    }
");

        [Test]
        public Task ReportsInstanceObjectPoolRentalNeverReleased() =>
            VerifyAsync(@"
    public void Update()
    {
        Payload payload = {|DCLA004:pool.Get()|};
        payload.Value = 1;
    }
");

        [Test]
        public Task ReportsRentalFromInterfaceTypedPool() =>
            VerifyAsync(@"
    public void Update()
    {
        Payload payload = {|DCLA004:poolInterface.Get()|};
        payload.Value = 1;
    }
");

        [Test]
        public Task ReportsRentalFromCustomIObjectPoolImplementation() =>
            VerifyAsync(@"
    public void Update()
    {
        Payload payload = {|DCLA004:customPool.Get()|};
    }
");

        [Test]
        public Task ReportsHashSetAndDictionaryPoolRentals() =>
            VerifyAsync(@"
    public void Update()
    {
        HashSet<int> set = {|DCLA004:HashSetPool<int>.Get()|};
        set.Add(1);
        Dictionary<int, int> map = {|DCLA004:DictionaryPool<int, int>.Get()|};
        map[1] = 2;
    }
");

        [Test]
        public Task ReportsAssignmentFormRentalWithNullSuppression() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers;
        numbers = {|DCLA004:ListPool<int>.Get()|}!;
        if (numbers != null)
            numbers.Add(1);
    }
");

        [Test]
        public Task CleanWhenSequentiallyReleased() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers = ListPool<int>.Get();
        numbers.Add(1);
        ListPool<int>.Release(numbers);
    }
");

        [Test]
        public Task CleanForTryFinallyRental() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers = ListPool<int>.Get();
        try { numbers.Add(1); }
        finally { ListPool<int>.Release(numbers); }
    }
");

        [Test]
        public Task CleanWhenInstancePoolReleasesViaReceiver() =>
            VerifyAsync(@"
    public void Update()
    {
        Payload payload = pool.Get();
        payload.Value = 1;
        pool.Release(payload);
    }
");

        [Test]
        public Task CleanWhenRentedValueIsReturned() =>
            VerifyAsync(@"
    public List<int> Rent()
    {
        List<int> numbers = ListPool<int>.Get();
        numbers.Add(1);
        return numbers;
    }
");

        [Test]
        public Task CleanWhenStoredInFieldOrPassedToAnotherMethod() =>
            VerifyAsync(@"
    public void Update()
    {
        stored = ListPool<int>.Get();
        List<int> numbers = ListPool<int>.Get();
        Consume(numbers);
    }

    private void Consume(List<int> numbers) { }
");

        [Test]
        public Task CleanWhenCapturedByLambda() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers = ListPool<int>.Get();
        System.Action release = () => ListPool<int>.Release(numbers);
        release();
    }
");

        [Test]
        public Task CleanForUsingScopedRentals() =>
            VerifyAsync(@"
    public void Update()
    {
        using Payload payload = pool.Get();
        payload.Value = 1;

        using (pool.Get(out Payload other))
        {
            other.Value = 2;
        }
    }
");

        [Test]
        public Task CleanForGetOnNonPoolType() =>
            VerifyAsync(@"
    public void Update()
    {
        Payload payload = cache.Get();
        payload.Value = 1;
    }
");

        [Test]
        public Task CleanWhenPooledObjectSelfReleasesViaMemberInvocation() =>
            VerifyAsync(@"
    public void Update()
    {
        // mirrors GliderPropView.PlayOneShotDetached: the pooled object's own method
        // (OneShotAudioSource.Play -> Invoke(ReturnToPool) -> pool.Release(this))
        // returns it to the pool, so the rental does not leak
        Payload payload = pool.Get();
        payload.Dispose();
    }
");

        [Test]
        public Task ReportsRentalWhenGetResolvesToInheritedCollectionPoolBase()
        {
            // mirrors real UnityEngine.Pool: static pools inherit Get/Release from
            // CollectionPool<TCollection, TItem>, so the resolved method's containing
            // type is CollectionPool`2, not ListPool`1
            var test = new CSharpAnalyzerTest<PooledRentalLeakAnalyzer, DefaultVerifier>
            {
                TestCode = @"
using System.Collections.Generic;
using UnityEngine.Pool;

namespace UnityEngine.Pool
{
    public interface IObjectPool<T> where T : class
    {
        T Get();
        void Release(T element);
    }

    public class CollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
    {
        public static TCollection Get() => new TCollection();
        public static void Release(TCollection toRelease) { }
    }

    public class ListPool<T> : CollectionPool<List<T>, T> { }
}

public class SomeService
{
    public void Update()
    {
        List<int> numbers = {|DCLA004:ListPool<int>.Get()|};
        numbers.Add(1);
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsRentalIteratedWithDeconstructionForeach()
        {
            // deconstruction foreach is ForEachVariableStatementSyntax, not
            // ForEachStatementSyntax - iterating stays provably local either way
            var test = new CSharpAnalyzerTest<PooledRentalLeakAnalyzer, DefaultVerifier>
            {
                TestCode = @"
using System.Collections.Generic;
using UnityEngine.Pool;

namespace UnityEngine.Pool
{
    public interface IObjectPool<T> where T : class
    {
        T Get();
        void Release(T element);
    }

    public static class ListPool<T>
    {
        public static List<T> Get() => new List<T>();
        public static void Release(List<T> toRelease) { }
    }
}

public class Pair
{
    public void Deconstruct(out int key, out int value) { key = 0; value = 0; }
}

public class SomeService
{
    public void Update()
    {
        List<Pair> pairs = {|DCLA004:ListPool<Pair>.Get()|};

        foreach ((int key, int value) in pairs)
        {
            int sum = key + value;
        }
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsRentalLeakedEntirelyInsideLambda() =>
            VerifyAsync(@"
    public void Update()
    {
        System.Action leak = () =>
        {
            List<int> numbers = {|DCLA004:ListPool<int>.Get()|};
            numbers.Add(1);
        };

        leak();
    }
");

        [Test]
        public Task ReportsRentalLeakedInsideLocalFunction() =>
            VerifyAsync(@"
    public void Update()
    {
        Leak();

        void Leak()
        {
            List<int> numbers = {|DCLA004:ListPool<int>.Get()|};
            numbers.Add(1);
        }
    }
");

        [Test]
        public Task CleanWhenReleasedThroughIsPatternAlias() =>
            VerifyAsync(@"
    public void Update()
    {
        List<int> numbers = ListPool<int>.Get();

        if (numbers is { } alias)
            ListPool<int>.Release(alias);
    }
");
    }
}

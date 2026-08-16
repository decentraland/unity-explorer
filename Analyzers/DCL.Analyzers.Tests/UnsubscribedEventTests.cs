using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class UnsubscribedEventTests
    {
        private const string STUB = @"
using System;

public class Publisher : IDisposable
{
    public event Action Changed;
    public static event Action GlobalChanged;
    public Publisher Inner => this;
    public void Dispose() { }
}
";

        private static Task VerifyAsync(string typeDeclaration, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<UnsubscribedEventAnalyzer, DefaultVerifier>
            {
                TestCode = STUB + typeDeclaration,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        [Test]
        public Task ReportsFieldEventSubscriptionNeverUnsubscribed() =>
            VerifyAsync(@"
public class Service : IDisposable
{
    private readonly Publisher publisher = new Publisher();

    public Service()
    {
        {|DCLA006:publisher.Changed += OnChanged|};
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenUnsubscribedInDispose() =>
            VerifyAsync(@"
public class Service : IDisposable
{
    private readonly Publisher publisher = new Publisher();

    public Service()
    {
        publisher.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose()
    {
        publisher.Changed -= OnChanged;
    }
}");

        [Test]
        public Task CleanWhenTypeHasNoTeardownMethod() =>
            VerifyAsync(@"
public class CompositionRoot
{
    private readonly Publisher publisher = new Publisher();

    public CompositionRoot()
    {
        publisher.Changed += OnChanged;
    }

    private void OnChanged() { }
}");

        [Test]
        public Task CleanWhenSubscribingToOwnEvent() =>
            VerifyAsync(@"
public class SelfWiring : IDisposable
{
    public event Action Loaded;

    public SelfWiring()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenReceiverIsLocal() =>
            VerifyAsync(@"
public class DialogFlow : IDisposable
{
    public void Open()
    {
        var publisher = new Publisher();
        publisher.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenReceiverIsUnstoredParameter() =>
            VerifyAsync(@"
public class PooledItemWiring : IDisposable
{
    public PooledItemWiring(Publisher transient)
    {
        transient.Changed += OnChanged;
    }

    public void WireOnce(Publisher item)
    {
        item.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task ReportsWhenCtorParameterIsStoredToField() =>
            VerifyAsync(@"
public class InjectedDep : IDisposable
{
    private readonly Publisher publisher;

    public InjectedDep(Publisher publisher)
    {
        this.publisher = publisher;
        {|DCLA006:publisher.Changed += OnChanged|};
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenSelfCreatedPublisherIsDisposedInTeardown() =>
            VerifyAsync(@"
public class OwnedBus : IDisposable
{
    private readonly Publisher bus;

    public OwnedBus()
    {
        bus = new Publisher();
        bus.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose()
    {
        bus.Dispose();
    }
}");

        [Test]
        public Task CleanWhenInitializerCreatedPublisherIsDisposedInTeardown() =>
            VerifyAsync(@"
public class OwnedBusViaInitializer : IDisposable
{
    private readonly Publisher bus = new Publisher();

    public OwnedBusViaInitializer()
    {
        bus.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose()
    {
        bus.Dispose();
    }
}");

        [Test]
        public Task ReportsWhenSelfCreatedPublisherIsIgnoredByTeardown() =>
            VerifyAsync(@"
public class LeakyOwner : IDisposable
{
    private readonly Publisher bus;
    private readonly Publisher other = new Publisher();

    public LeakyOwner()
    {
        bus = new Publisher();
        {|DCLA006:bus.Changed += OnChanged|};
    }

    private void OnChanged() { }

    public void Dispose()
    {
        other.Dispose();
    }
}");

        [Test]
        public Task ReportsStaticEventSubscriptionNeverUnsubscribed() =>
            VerifyAsync(@"
public class Listener : IDisposable
{
    public Listener()
    {
        {|DCLA006:Publisher.GlobalChanged += OnChanged|};
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenSelfRemovingHandler() =>
            VerifyAsync(@"
public class OneShot : IDisposable
{
    private readonly Publisher publisher = new Publisher();

    public OneShot()
    {
        Action handler = null;
        handler = () => publisher.Changed -= handler;
        publisher.Changed += handler;
    }

    public void Dispose() { }
}");

        [Test]
        public Task ReportsChainedReceiverRootedAtField() =>
            VerifyAsync(@"
public class ChainService : IDisposable
{
    private readonly Publisher publisher = new Publisher();

    public ChainService()
    {
        {|DCLA006:publisher.Inner.Changed += OnChanged|};
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenChainedReceiverRootedAtParameter() =>
            VerifyAsync(@"
public class ChainWiring : IDisposable
{
    public void Wire(Publisher item)
    {
        item.Inner.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose() { }
}");

        [Test]
        public Task ReportsSubscriptionInTypeWithOnDestroyTeardown() =>
            VerifyAsync(@"
public class Behaviour
{
    private readonly Publisher publisher = new Publisher();

    public void Awake()
    {
        {|DCLA006:publisher.Changed += OnChanged|};
    }

    private void OnChanged() { }

    private void OnDestroy() { }
}");
    }
}

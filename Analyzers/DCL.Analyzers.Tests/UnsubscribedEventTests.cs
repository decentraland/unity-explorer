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

namespace UnityEngine.Events
{
    public abstract class UnityEventBase
    {
        public void RemoveAllListeners() { }
    }

    public class UnityEvent : UnityEventBase
    {
        public void AddListener(Action call) { }
        public void RemoveListener(Action call) { }
    }
}

public class Publisher
{
    public event Action Changed;
    public static event Action GlobalChanged;
    public Publisher Inner => this;
}

public class ClickSource
{
    public UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent();
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
        public Task CleanWhenReceiverIsParameter() =>
            VerifyAsync(@"
public class PooledItemWiring : IDisposable
{
    public void WireOnce(Publisher item)
    {
        item.Changed += OnChanged;
    }

    private void OnChanged() { }

    public void Dispose() { }
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
        public Task ReportsAddListenerNeverRemoved() =>
            VerifyAsync(@"
public class View : IDisposable
{
    private readonly ClickSource closeButton = new ClickSource();

    public View()
    {
        {|DCLA006:closeButton.onClick.AddListener(OnClose)|};
    }

    private void OnClose() { }

    public void Dispose() { }
}");

        [Test]
        public Task CleanWhenRemoveAllListenersPresent() =>
            VerifyAsync(@"
public class View : IDisposable
{
    private readonly ClickSource closeButton = new ClickSource();

    public View()
    {
        closeButton.onClick.AddListener(OnClose);
    }

    private void OnClose() { }

    public void Dispose()
    {
        closeButton.onClick.RemoveAllListeners();
    }
}");

        [Test]
        public Task CleanAddListenerOnParameterReceiver() =>
            VerifyAsync(@"
public class ItemWiring : IDisposable
{
    public void WireOnce(ClickSource item)
    {
        item.onClick.AddListener(OnClick);
    }

    private void OnClick() { }

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

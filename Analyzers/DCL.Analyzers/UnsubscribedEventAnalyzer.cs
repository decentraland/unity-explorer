using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace DCL.Analyzers
{
    /// <summary>
    ///     DCLA006: a type that owns a teardown method (Dispose/DisposeAsync/OnDestroy/OnDisable)
    ///     subscribes with += to an event it does not own - an event reached through a field,
    ///     property, chain, or a static event - and no -= for that event appears anywhere in the
    ///     type (review rule: every acquire has a symmetric release; the same applies to
    ///     UnityEvent AddListener without any RemoveListener/RemoveAllListeners in the type).
    ///     Deliberately conservative: types without a teardown method are never analyzed
    ///     (app-lifetime wiring is legitimate there); subscribing to the type's OWN event stays
    ///     silent (subscriber and publisher die together); receivers rooted at a local or a
    ///     parameter stay silent (locally-owned objects and the sanctioned wire-once pattern for
    ///     pooled items); any -= of the event anywhere in the type - including a self-removing
    ///     handler - silences every += of that event; and one RemoveListener/RemoveAllListeners
    ///     silences every AddListener in the type, since listener pairs cannot be matched
    ///     per-receiver without whole-program analysis.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnsubscribedEventAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA006";

        private const string UNITY_EVENT_BASE_METADATA_NAME = "UnityEngine.Events.UnityEventBase";

        private static readonly ImmutableHashSet<string> TEARDOWN_METHOD_NAMES = ImmutableHashSet.Create(
            "Dispose",
            "DisposeAsync",
            "OnDestroy",
            "OnDisable");

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Event subscription without matching unsubscription",
            "'{0}' subscribes to '{1}' but never unsubscribes - pair the += with a -= in '{2}' (Subscribe->Unsubscribe, +=->-=, AddListener->RemoveListener)",
            "Correctness",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A type owning a teardown method (Dispose/DisposeAsync/OnDestroy/OnDisable) subscribes to an " +
                         "event it does not own and never unsubscribes anywhere in the type. The publisher outlives the " +
                         "subscriber, so the subscription keeps the dead subscriber reachable and its handler firing. " +
                         "Every acquire needs its symmetric, traceable release (review rule, unity-explorer PRs #9063 " +
                         "#9059; CLAUDE.md § Component Clean-up Patterns).");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolStartAction(static symbolStartContext =>
            {
                var type = (INamedTypeSymbol)symbolStartContext.Symbol;

                if (!HasTeardownMethod(type)) return;

                var subscriptions = new ConcurrentBag<(IEventSymbol evt, Location location)>();
                var unsubscribedEvents = new ConcurrentBag<IEventSymbol>();
                var listenerAdds = new ConcurrentBag<(IMethodSymbol method, Location location)>();
                int listenerRemovals = 0;

                symbolStartContext.RegisterOperationAction(operationContext =>
                {
                    var assignment = (IEventAssignmentOperation)operationContext.Operation;

                    if (assignment.EventReference is not IEventReferenceOperation eventReference) return;

                    if (!assignment.Adds)
                    {
                        unsubscribedEvents.Add(eventReference.Event.OriginalDefinition);
                        return;
                    }

                    // the type's own (or inherited) event: subscriber and publisher share a
                    // lifetime, so the subscription cannot outlive its target
                    if (eventReference.Instance is IInstanceReferenceOperation) return;

                    if (!eventReference.Event.IsStatic && ReceiverRootIsLocalOrParameter(eventReference.Instance)) return;

                    subscriptions.Add((eventReference.Event.OriginalDefinition, assignment.Syntax.GetLocation()));
                }, OperationKind.EventAssignment);

                symbolStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    IMethodSymbol method = invocation.TargetMethod;

                    if (!IsUnityEventMethod(method)) return;

                    switch (method.Name)
                    {
                        case "AddListener" when !ReceiverRootIsLocalOrParameter(invocation.Instance):
                            listenerAdds.Add((method, invocation.Syntax.GetLocation()));
                            break;

                        case "RemoveListener":
                        case "RemoveAllListeners":
                            Interlocked.Exchange(ref listenerRemovals, 1);
                            break;
                    }
                }, OperationKind.Invocation);

                symbolStartContext.RegisterSymbolEndAction(symbolEndContext =>
                {
                    var removed = new HashSet<IEventSymbol>(unsubscribedEvents, SymbolEqualityComparer.Default);

                    foreach ((IEventSymbol evt, Location location) in subscriptions)
                    {
                        if (removed.Contains(evt)) continue;

                        symbolEndContext.ReportDiagnostic(Diagnostic.Create(
                            RULE, location, type.Name, evt.Name, TeardownMethodName(type)));
                    }

                    if (listenerRemovals != 0) return;

                    foreach ((IMethodSymbol method, Location location) in listenerAdds)
                    {
                        symbolEndContext.ReportDiagnostic(Diagnostic.Create(
                            RULE, location, type.Name, method.ContainingType.Name + "." + method.Name, TeardownMethodName(type)));
                    }
                });
            }, SymbolKind.NamedType);
        }

        private static bool HasTeardownMethod(INamedTypeSymbol type) =>
            type.GetMembers().OfType<IMethodSymbol>().Any(static method => TEARDOWN_METHOD_NAMES.Contains(method.Name));

        private static string TeardownMethodName(INamedTypeSymbol type) =>
            type.GetMembers().OfType<IMethodSymbol>().First(static method => TEARDOWN_METHOD_NAMES.Contains(method.Name)).Name;

        /// <summary>
        ///     Walks the receiver chain (fields, properties, invocation results, conversions) to
        ///     its root. A local or parameter root means the subscription target is locally owned
        ///     or of unknowable ownership - both stay silent.
        /// </summary>
        private static bool ReceiverRootIsLocalOrParameter(IOperation? instance)
        {
            IOperation? current = instance;

            while (true)
            {
                switch (current)
                {
                    case IMemberReferenceOperation memberReference:
                        current = memberReference.Instance;
                        break;

                    case IInvocationOperation invocation:
                        current = invocation.Instance;
                        break;

                    case IConversionOperation conversion:
                        current = conversion.Operand;
                        break;

                    case IConditionalAccessInstanceOperation:
                    case ILocalReferenceOperation:
                    case IParameterReferenceOperation:
                        return true;

                    default:
                        return false;
                }
            }
        }

        private static bool IsUnityEventMethod(IMethodSymbol method)
        {
            for (INamedTypeSymbol? current = method.ContainingType; current != null; current = current.BaseType)
            {
                if (FullMetadataName(current.OriginalDefinition) == UNITY_EVENT_BASE_METADATA_NAME)
                    return true;
            }

            return false;
        }

        private static string FullMetadataName(INamedTypeSymbol type)
        {
            string name = type.MetadataName;

            for (INamespaceSymbol? ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
                name = ns.Name + "." + name;

            return name;
        }
    }
}

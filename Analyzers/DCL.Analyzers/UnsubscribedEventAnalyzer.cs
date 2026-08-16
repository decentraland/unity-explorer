using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DCL.Analyzers
{
    /// <summary>
    ///     DCLA006: a type that owns a teardown method (Dispose/DisposeAsync/OnDestroy/OnDisable)
    ///     subscribes with += to a C# event it does not own - an event reached through a field,
    ///     property chain, or a static event - and no -= for that event appears anywhere in the
    ///     type (review rule: every acquire has a symmetric release).
    ///     Calibrated against the live corpus (2026-08-16): UnityEvent AddListener is deliberately
    ///     NOT covered - wiring a view's own serialized child components is the standard idiom and
    ///     drowned the rule in false positives. Silencers, all corpus-derived: types without a
    ///     teardown method are never analyzed (app-lifetime wiring is legitimate there); the
    ///     type's OWN events stay silent (subscriber and publisher die together); receivers rooted
    ///     at a local stay silent (locally-owned objects); receivers rooted at a parameter stay
    ///     silent UNLESS a constructor stores that parameter into a field (a retained dependency
    ///     must be torn down); a receiver field that the type itself assigns from an object
    ///     creation AND references in a teardown method stays silent (create-and-dispose pairing -
    ///     the publisher provably dies with the subscriber); and any -= of the event anywhere in
    ///     the type - including a self-removing handler - silences every += of that event.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnsubscribedEventAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DCLA006";

        private static readonly ImmutableHashSet<string> TEARDOWN_METHOD_NAMES = ImmutableHashSet.Create(
            "Dispose",
            "DisposeAsync",
            "OnDestroy",
            "OnDisable");

        private static readonly DiagnosticDescriptor RULE = new (
            DiagnosticId,
            "Event subscription without matching unsubscription",
            "'{0}' subscribes to '{1}' but never unsubscribes - pair the += with a -= in '{2}' (Subscribe->Unsubscribe, +=->-=)",
            "Correctness",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A type owning a teardown method (Dispose/DisposeAsync/OnDestroy/OnDisable) subscribes to an " +
                         "event it does not own and never unsubscribes anywhere in the type. The publisher outlives the " +
                         "subscriber, so the subscription keeps the dead subscriber reachable and its handler firing. " +
                         "Every acquire needs its symmetric, traceable release (review rule, unity-explorer PRs #9063 " +
                         "#9059; CLAUDE.md § Component Clean-up Patterns).");

        private enum RootKind { Field, Parameter, LocalOrConditional, Other }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolStartAction(static symbolStartContext =>
            {
                var type = (INamedTypeSymbol)symbolStartContext.Symbol;

                if (!HasTeardownMethod(type)) return;

                var subscriptions = new ConcurrentBag<(IEventSymbol evt, IFieldSymbol? rootField, IParameterSymbol? rootParameter, Location location)>();
                var unsubscribedEvents = new ConcurrentBag<IEventSymbol>();
                var selfCreatedFields = new ConcurrentBag<IFieldSymbol>();
                var teardownTouchedFields = new ConcurrentBag<IFieldSymbol>();
                var ctorStoredParameters = new ConcurrentBag<IParameterSymbol>();

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

                    IEventSymbol evt = eventReference.Event.OriginalDefinition;
                    Location location = assignment.Syntax.GetLocation();

                    if (eventReference.Event.IsStatic)
                    {
                        subscriptions.Add((evt, null, null, location));
                        return;
                    }

                    (RootKind kind, IFieldSymbol? field, IParameterSymbol? parameter) = ReceiverRoot(eventReference.Instance);

                    switch (kind)
                    {
                        case RootKind.LocalOrConditional:
                            return;

                        case RootKind.Parameter:
                            subscriptions.Add((evt, null, parameter, location));
                            return;

                        default:
                            subscriptions.Add((evt, field, null, location));
                            return;
                    }
                }, OperationKind.EventAssignment);

                symbolStartContext.RegisterOperationAction(operationContext =>
                {
                    var initializer = (IFieldInitializerOperation)operationContext.Operation;

                    if (Unwrap(initializer.Value) is not IObjectCreationOperation) return;

                    foreach (IFieldSymbol field in initializer.InitializedFields)
                        selfCreatedFields.Add(field);
                }, OperationKind.FieldInitializer);

                symbolStartContext.RegisterOperationAction(operationContext =>
                {
                    var assignment = (ISimpleAssignmentOperation)operationContext.Operation;

                    if (assignment.Target is not IFieldReferenceOperation { Instance: IInstanceReferenceOperation or null } fieldTarget) return;

                    switch (Unwrap(assignment.Value))
                    {
                        case IObjectCreationOperation:
                            selfCreatedFields.Add(fieldTarget.Field);
                            break;

                        case IParameterReferenceOperation parameterValue
                            when operationContext.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor }:
                            ctorStoredParameters.Add(parameterValue.Parameter);
                            break;
                    }
                }, OperationKind.SimpleAssignment);

                symbolStartContext.RegisterOperationAction(operationContext =>
                {
                    if (operationContext.ContainingSymbol is IMethodSymbol method && TEARDOWN_METHOD_NAMES.Contains(method.Name))
                        teardownTouchedFields.Add(((IFieldReferenceOperation)operationContext.Operation).Field);
                }, OperationKind.FieldReference);

                symbolStartContext.RegisterSymbolEndAction(symbolEndContext =>
                {
                    var removed = new HashSet<IEventSymbol>(unsubscribedEvents, SymbolEqualityComparer.Default);
                    var selfCreated = new HashSet<IFieldSymbol>(selfCreatedFields, SymbolEqualityComparer.Default);
                    var teardownTouched = new HashSet<IFieldSymbol>(teardownTouchedFields, SymbolEqualityComparer.Default);
                    var stored = new HashSet<IParameterSymbol>(ctorStoredParameters, SymbolEqualityComparer.Default);

                    foreach ((IEventSymbol evt, IFieldSymbol? rootField, IParameterSymbol? rootParameter, Location location) in subscriptions)
                    {
                        if (removed.Contains(evt)) continue;

                        // parameter-rooted: only a dependency the constructor RETAINS must be torn down
                        if (rootParameter != null && !stored.Contains(rootParameter)) continue;

                        // create-and-dispose pairing: the type news up the publisher and touches it
                        // in teardown - publisher and subscriber provably die together
                        if (rootField != null && selfCreated.Contains(rootField) && teardownTouched.Contains(rootField)) continue;

                        symbolEndContext.ReportDiagnostic(Diagnostic.Create(
                            RULE, location, type.Name, evt.Name, TeardownMethodName(type)));
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
        ///     its root. The field adjacent to the root - the type's own handle on the publisher -
        ///     is what the create-and-dispose ownership check keys on.
        /// </summary>
        private static (RootKind kind, IFieldSymbol? field, IParameterSymbol? parameter) ReceiverRoot(IOperation? instance)
        {
            IOperation? current = instance;

            while (true)
            {
                switch (current)
                {
                    case IFieldReferenceOperation { Instance: IInstanceReferenceOperation or null } baseField:
                        return (RootKind.Field, baseField.Field, null);

                    case IMemberReferenceOperation memberReference:
                        current = memberReference.Instance;
                        break;

                    case IInvocationOperation invocation:
                        current = invocation.Instance;
                        break;

                    case IConversionOperation conversion:
                        current = conversion.Operand;
                        break;

                    case ILocalReferenceOperation:
                    case IConditionalAccessInstanceOperation:
                        return (RootKind.LocalOrConditional, null, null);

                    case IParameterReferenceOperation parameterReference:
                        return (RootKind.Parameter, null, parameterReference.Parameter);

                    default:
                        return (RootKind.Other, null, null);
                }
            }
        }

        private static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
                operation = conversion.Operand;

            return operation;
        }
    }
}

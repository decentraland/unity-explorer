using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System.Linq;

namespace DCL.RealmNavigation.LoadingOperation
{
    public static class LoadingOperationDebugWidgetExtensions
    {
        public static void AddDebugControl<TParams>(this SequentialLoadingOperation<TParams> operation, DebugWidgetBuilder? builder, string label)
            where TParams: ILoadingOperationParams
        {
            if (builder == null)
                return;

            var hint = new DebugHintDef(label);

            // Add dropdown

            const string NONE = "None";

            var choices = operation.Operations.Select(o => o.GetType().Name)
                                   .Prepend(NONE)
                                   .ToList();

            var binding = new IndexedElementBinding(choices, NONE, evt
                => operation.InterruptOnOp = operation.Operations.ElementAtOrDefault(evt.index - 1));

            var labelBinding = new ElementBinding<string>("None");
            operation.CurrentOp.OnUpdate += op => labelBinding.Value = op?.GetType().Name ?? "None";

            builder.AddControl(new DebugConstLabelDef("Current Op:"), new DebugSetOnlyLabelDef(labelBinding), hint);
            builder.AddControl(new DebugDropdownDef(binding, "Interrupt On"), null);
        }
    }
}

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

            builder.AddControl(new DebugConstLabelDef($"Interrupt {label}:"), null);

            // Add dropdown

            const string NONE = "None";

            var choices = operation.Operations.Select(o => o.GetType().Name)
                                   .Prepend(NONE)
                                   .ToList();

            var binding = new IndexedElementBinding(choices, NONE, evt
                => operation.InterruptOnOp = operation.Operations.ElementAtOrDefault(evt.index));

            builder.AddControl(new DebugDropdownDef(choices, binding, "Select"), null);
        }
    }
}

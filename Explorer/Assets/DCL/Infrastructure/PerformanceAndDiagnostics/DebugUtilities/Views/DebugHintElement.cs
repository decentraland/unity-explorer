using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugHintElement : DebugElementBase<DebugHintElement, DebugHintDef>
    {
        internal const string INFO_STYLE = "debug-hint--info";
        internal const string WARNING_STYLE = "debug-hint--warning";
        internal const string ERROR_STYLE = "debug-hint--error";

        internal const string POS_BEFORE_STYLE = "debug-hint--before";
        internal const string POS_AFTER_STYLE = "debug-hint--after";

        protected override void ConnectBindings()
        {
            Label label = this.Q<Label>();

            if (definition.Text != null)
                label.text = definition.Text;

            definition.Binding?.Connect(label);

            switch (definition.HintKind)
            {
                case DebugHintDef.Kind.Info:
                    RemoveFromClassList(WARNING_STYLE);
                    RemoveFromClassList(ERROR_STYLE);
                    AddToClassList(INFO_STYLE);
                    break;
                case DebugHintDef.Kind.Warning:
                    RemoveFromClassList(INFO_STYLE);
                    RemoveFromClassList(ERROR_STYLE);
                    AddToClassList(WARNING_STYLE);
                    break;
                case DebugHintDef.Kind.Error:
                    RemoveFromClassList(INFO_STYLE);
                    RemoveFromClassList(WARNING_STYLE);
                    AddToClassList(ERROR_STYLE);
                    break;
            }

            switch (definition.HintPosition)
            {
                case DebugHintDef.Position.After:
                    RemoveFromClassList(POS_BEFORE_STYLE);
                    AddToClassList(POS_AFTER_STYLE);
                    break;
                case DebugHintDef.Position.Before:
                    RemoveFromClassList(POS_AFTER_STYLE);
                    AddToClassList(POS_BEFORE_STYLE);
                    break;
            }
        }

        public new class UxmlFactory : UxmlFactory<DebugHintElement, UxmlTraits> { }
    }
}

using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using Utility.UIToolkit;

namespace DCL.DebugUtilities
{
    public class DebugUtilitiesContainer
    {
        public DebugContainerBuilder Builder { get; }

        private DebugUtilitiesContainer(DebugContainerBuilder builder)
        {
            Builder = builder;
        }

        public static DebugUtilitiesContainer Create(DebugViewsCatalog viewsCatalog)
        {
            return new DebugUtilitiesContainer(
                new DebugContainerBuilder(
                    () => viewsCatalog.Widget.InstantiateForElement<DebugWidget>(),
                    () => viewsCatalog.ControlContainer.InstantiateForElement<DebugControl>(),
                    new Dictionary<Type, IDebugElementFactory>
                    {
                        { typeof(DebugButtonDef), new DebugElementBase<DebugButtonElement, DebugButtonDef>.Factory(viewsCatalog.Button) },
                        { typeof(DebugConstLabelDef), new DebugElementBase<DebugConstLabelElement, DebugConstLabelDef>.Factory(viewsCatalog.ConstLabel) },
                        { typeof(DebugFloatFieldDef), new DebugElementBase<DebugFloatFieldElement, DebugFloatFieldDef>.Factory(viewsCatalog.FloatField) },
                        { typeof(DebugHintDef), new DebugElementBase<DebugHintElement, DebugHintDef>.Factory(viewsCatalog.Hint) },
                        { typeof(DebugIntFieldDef), new DebugElementBase<DebugIntFieldElement, DebugIntFieldDef>.Factory(viewsCatalog.IntField) },
                        { typeof(DebugIntSliderDef), new DebugElementBase<DebugIntSliderElement, DebugIntSliderDef>.Factory(viewsCatalog.IntSlider) },
                        { typeof(DebugFloatSliderDef), new DebugElementBase<DebugFloatSliderElement, DebugFloatSliderDef>.Factory(viewsCatalog.FloatSlider) },
                        { typeof(DebugVector2IntFieldDef), new DebugElementBase<DebugVector2IntFieldElement, DebugVector2IntFieldDef>.Factory(viewsCatalog.Vector2IntField) },
                        { typeof(DebugLongMarkerDef), new DebugElementBase<DebugLongMarkerElement, DebugLongMarkerDef>.Factory(viewsCatalog.LongMarker) },
                        { typeof(DebugSetOnlyLabelDef), new DebugElementBase<DebugSetOnlyLabelElement, DebugSetOnlyLabelDef>.Factory(viewsCatalog.SetOnlyLabel) },
                        { typeof(DebugTextFieldDef), new DebugElementBase<DebugTextFieldElement, DebugTextFieldDef>.Factory(viewsCatalog.TextField) },
                        { typeof(DebugToggleDef), new DebugElementBase<DebugToggleElement, DebugToggleDef>.Factory(viewsCatalog.Toggle) },
                        { typeof(DebugDropdownDef), new DebugElementBase<DebugDropdownElement, DebugDropdownDef>.Factory(viewsCatalog.DropdownField) },
                    }
                )
            );
        }
    }
}

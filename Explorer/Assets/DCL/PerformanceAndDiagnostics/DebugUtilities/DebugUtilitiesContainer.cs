﻿using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using Utility.UIToolkit;

namespace DCL.DebugUtilities
{
    public class DebugUtilitiesContainer
    {
        public IDebugContainerBuilder Builder { get; }

        private DebugUtilitiesContainer(IDebugContainerBuilder builder)
        {
            Builder = builder;
        }

        public static DebugUtilitiesContainer Create(DebugViewsCatalog viewsCatalog, bool isFullDebug)
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
                    },
                    isFullDebug
                        ? null
                        : new HashSet<string>
                        {
                            IDebugContainerBuilder.Categories.ROOM_INFO,
                            IDebugContainerBuilder.Categories.ROOM_SCENE,
                            IDebugContainerBuilder.Categories.ROOM_THROUGHPUT,
                            IDebugContainerBuilder.Categories.ROOM_ISLAND,
                            IDebugContainerBuilder.Categories.PERFORMANCE,
                            IDebugContainerBuilder.Categories.MEMORY,
                            IDebugContainerBuilder.Categories.REALM,
                            IDebugContainerBuilder.Categories.ANALYTICS,
                        }
                )
            );
        }
    }
}

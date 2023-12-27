﻿using DCL.DebugUtilities.UIBindings;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Contains shortcuts for common scenarios of debug elements
    /// </summary>
    public static class BuilderExtensions
    {
        public static DebugWidgetBuilder AddSingleButton(this DebugWidgetBuilder builder, string buttonName, Action onClick) =>
            builder.AddControl(new DebugButtonDef(buttonName, onClick), null);


        public static DebugWidgetBuilder AddToggleField(this DebugWidgetBuilder builder,  string toggleName, EventCallback<ChangeEvent<bool>> onToggle, bool initialState)
        {
            var label = new DebugConstLabelDef(toggleName);
            var field = new DebugToggleDef(onToggle, initialState);
            builder.AddControl(label, field);
            return builder;
        }


        public static DebugWidgetBuilder AddIntFieldWithConfirmation(this DebugWidgetBuilder builder, int defaultValue, string buttonName, Action<int> onClick)
        {
            var binding = new ElementBinding<int>(defaultValue);
            var infFieldDef = new DebugIntFieldDef(binding);

            var buttonDef = new DebugButtonDef(buttonName, () => onClick?.Invoke(binding.Value));
            builder.AddControl(infFieldDef, buttonDef);
            return builder;
        }

        public static DebugWidgetBuilder AddStringFieldWithConfirmation(this DebugWidgetBuilder builder, string defaultValue, string buttonName, Action<string> onClick)
        {
            var binding = new ElementBinding<string>(defaultValue);
            var textFieldDef = new DebugTextFieldDef(binding);

            var buttonDef = new DebugButtonDef(buttonName, () => onClick?.Invoke(binding.Value));
            builder.AddControl(textFieldDef, buttonDef);
            return builder;
        }

        public static DebugWidgetBuilder AddFloatField(this DebugWidgetBuilder builder, string labelName, ElementBinding<float> elementBinding)
        {
            var label = new DebugConstLabelDef(labelName);
            var field = new DebugFloatFieldDef(elementBinding);
            builder.AddControl(label, field);
            return builder;
        }

        public static DebugWidgetBuilder AddMarker(this DebugWidgetBuilder builder, string markerName, ElementBinding<ulong> binding, DebugLongMarkerDef.Unit unit)
        {
            var label = new DebugConstLabelDef(markerName);
            var marker = new DebugLongMarkerDef(binding, unit);
            builder.AddControl(label, marker);
            return builder;
        }

        public static DebugWidgetBuilder AddCustomMarker(this DebugWidgetBuilder builder, string markerName, ElementBinding<string> binding)
        {
            var label = new DebugConstLabelDef(markerName);
            var marker = new DebugSetOnlyLabelDef(binding);

            return builder.AddControl(label, marker);
        }
    }
}

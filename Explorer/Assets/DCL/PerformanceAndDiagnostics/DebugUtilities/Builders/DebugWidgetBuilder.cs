using DCL.DebugUtilities.UIBindings;
using DCL.DebugUtilities.Views;
using DCL.Optimization.Pools;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.DebugUtilities
{
    public class DebugWidgetBuilder
    {
        private static readonly ListObjectPool<ElementPlacement> DEF_POOL = new ();

        internal readonly string name;

        private List<ElementPlacement>? placements;
        private DebugWidgetVisibilityBinding? visibilityBinding;

        public DebugWidgetBuilder(string name)
        {
            this.name = name;
            placements = null;
        }

        /// <summary>
        ///     Add a control that contains up to 2 elements
        /// </summary>
        /// <param name="left">Left can be null if right is not null</param>
        /// <param name="right">Right can be null if left is not null</param>
        /// <param name="debugHintDef">Can be null</param>
        /// <returns></returns>
        public DebugWidgetBuilder AddControl(IDebugElementDef? left, IDebugElementDef? right, DebugHintDef? debugHintDef = null)
        {
            placements ??= DEF_POOL.Get();
            placements!.Add(new ElementPlacement(left, right, debugHintDef));
            return this;
        }

        /// <summary>
        ///     Set the control of the activity of the whole widget
        /// </summary>
        /// <returns></returns>
        public DebugWidgetBuilder SetVisibilityBinding(DebugWidgetVisibilityBinding visibilityBinding)
        {
            this.visibilityBinding = visibilityBinding;
            return this;
        }

        /// <summary>
        ///     Finalize and build
        /// </summary>
        /// <returns></returns>
        internal DebugWidget Build(
            Func<DebugWidget> widgetFactoryMethod,
            Func<DebugControl> controlFactoryMethod,
            IReadOnlyDictionary<Type, IDebugElementFactory> factories)
        {
            DebugWidget widget = widgetFactoryMethod();
            widget.Initialize(name);

            // Add every element under the widget control
            foreach (ElementPlacement def in placements)
            {
                void CreateHint(DebugHintDef hintDef)
                {
                    VisualElement hint = factories[typeof(DebugHintDef)].Create(hintDef);
                    widget.AddElement(hint);
                }

                // Create a hint if it is Before
                if (def.HintDef is { HintPosition: DebugHintDef.Position.Before })
                    CreateHint(def.HintDef);

                widget.AddElement(CreateControl(controlFactoryMethod, factories, def.Left, def.Right));

                // Create a hint if it is After
                if (def.HintDef is { HintPosition: DebugHintDef.Position.After })
                    CreateHint(def.HintDef);
            }

            // Set activity binding
            visibilityBinding?.Connect(widget);

            DEF_POOL.Release(placements);
            placements = null;
            return widget;
        }

        private DebugControl CreateControl(
            Func<DebugControl> controlFactoryMethod,
            IReadOnlyDictionary<Type, IDebugElementFactory> factories,
            IDebugElementDef left,
            IDebugElementDef right)
        {
            DebugControl debugControl = controlFactoryMethod();

            VisualElement CreateElement(IDebugElementDef def)
            {
                IDebugElementFactory factory = factories[def.GetType()];
                VisualElement element = factory.Create(def);
                return element;
            }

            if (left != null)
                debugControl.Left.Add(CreateElement(left));
            else
                debugControl.Left.SetDisplayed(false);

            if (right != null)
                debugControl.Right.Add(CreateElement(right));
            else
                debugControl.Right.SetDisplayed(false);

            return debugControl;
        }

        internal readonly struct ElementPlacement
        {
            public readonly IDebugElementDef? Left;
            public readonly IDebugElementDef? Right;
            public readonly DebugHintDef? HintDef;

            public ElementPlacement(IDebugElementDef left, IDebugElementDef right, DebugHintDef? hintDef)
            {
                Left = left;
                Right = right;
                HintDef = hintDef;
            }
        }
    }
}

using DCL.DebugUtilities.UIBindings;
using DCL.DebugUtilities.Views;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.DebugUtilities
{
    public class DebugWidgetBuilder
    {
        private static readonly ListObjectPool<Placement> DEF_POOL = new ();

        internal readonly string name;

        private List<Placement>? placements;
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
            placements!.Add(new Placement.ElementPlacement(left, right, debugHintDef));
            return this;
        }

        public DebugWidgetBuilder AddGroup(string groupName, params (IDebugElementDef? left, IDebugElementDef? right)[] elements)
        {
            placements ??= DEF_POOL.Get();
            placements!.Add(new Placement.GroupPlacement(groupName, elements.Select(e => new Placement.ElementPlacement(e.left, e.right, null)).ToList()));
            return this;
        }

        public DebugWidgetBuilder AddList(string listName, IElementBinding<IReadOnlyList<(string name, string value)>> list)
        {
            placements ??= DEF_POOL.Get();
            placements!.Add(new Placement.ListPlacement(listName, list));
            return this;
        }

        public DebugWidgetBuilder AddControlWithLabel(string label, IDebugElementDef? right, DebugHintDef? debugHintDef = null) =>
            AddControl(new DebugConstLabelDef(label), right, debugHintDef);

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
            DebugWidget widget = widgetFactoryMethod()!;
            widget.Initialize(name);

            // Add every element under the widget control
            foreach (Placement def in placements!)
            {
                void CreateHint(DebugHintDef hintDef)
                {
                    VisualElement hint = factories[typeof(DebugHintDef)].Create(hintDef);
                    widget.AddElement(hint);
                }

                switch (def)
                {
                    case Placement.ElementPlacement elementDef:

                        // Create a hint if it is Before
                        if (elementDef.HintDef is { HintPosition: DebugHintDef.Position.Before })
                            CreateHint(elementDef.HintDef);

                        widget.AddElement(CreateControl(controlFactoryMethod, factories, elementDef.Left, elementDef.Right));

                        // Create a hint if it is After
                        if (elementDef.HintDef is { HintPosition: DebugHintDef.Position.After })
                            CreateHint(elementDef.HintDef);

                        break;
                    case Placement.GroupPlacement groupDef:
                        DebugWidget innerWidget = widgetFactoryMethod()!;
                        innerWidget.Initialize(groupDef.GroupName, name + groupDef.GroupName);

                        foreach (Placement.ElementPlacement element in groupDef.Elements)
                            innerWidget.AddElement(CreateControl(controlFactoryMethod, factories, element.Left, element.Right));

                        widget.AddElement(innerWidget);

                        break;
                    case Placement.ListPlacement listDef:
                        var debugList = DebugList.NewDebugList(
                            widgetFactoryMethod,
                            controlFactoryMethod,
                            factories,
                            listDef.ListName,
                            listDef.Elements,
                            name + listDef.ListName
                        );

                        widget.AddElement(debugList.Widget);
                        break;
                }
            }

            // Set activity binding
            visibilityBinding?.Connect(widget);

            DEF_POOL.Release(placements);
            placements = null;
            return widget;
        }

        internal static DebugControl CreateControl(
            Func<DebugControl> controlFactoryMethod,
            IReadOnlyDictionary<Type, IDebugElementFactory> factories,
            IDebugElementDef? left,
            IDebugElementDef? right)
        {
            DebugControl debugControl = controlFactoryMethod()!;

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

        internal abstract record Placement
        {
            internal record ElementPlacement : Placement
            {
                public readonly IDebugElementDef? Left;
                public readonly IDebugElementDef? Right;
                public readonly DebugHintDef? HintDef;

                public ElementPlacement(IDebugElementDef? left, IDebugElementDef? right, DebugHintDef? hintDef)
                {
                    Left = left;
                    Right = right;
                    HintDef = hintDef;
                }
            }

            internal record GroupPlacement : Placement
            {
                public readonly string GroupName;
                public readonly IReadOnlyList<ElementPlacement> Elements;

                public GroupPlacement(string groupName, IReadOnlyList<ElementPlacement> elements)
                {
                    GroupName = groupName;
                    Elements = elements;
                }
            }

            internal record ListPlacement : Placement
            {
                public readonly string ListName;
                public readonly IElementBinding<IReadOnlyList<(string name, string value)>> Elements;

                public ListPlacement(string listName, IElementBinding<IReadOnlyList<(string name, string value)>> elements)
                {
                    ListName = listName;
                    Elements = elements;
                }
            }
        }
    }
}

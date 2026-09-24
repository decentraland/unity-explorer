using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby.Tests
{
    /// <summary>
    ///     Gives a rail built by hand in a test the arrows and hover area its prefab carries. Edit mode never runs the rail's
    ///     Awake, so a test that pages with the arrows awakens the rail explicitly.
    /// </summary>
    public static class TestRailArrows
    {
        public static void Attach(LobbyPagedRailView rail)
        {
            var arrowsGo = new GameObject("Arrows", typeof(RectTransform));
            arrowsGo.transform.SetParent(rail.transform);
            LobbyRailArrowsView arrows = arrowsGo.AddComponent<LobbyRailArrowsView>();
            SetField(arrows, "group", arrowsGo.AddComponent<CanvasGroup>());
            SetField(arrows, "<Previous>k__BackingField", CreateButton(arrowsGo.transform, "Previous"));
            SetField(arrows, "<Next>k__BackingField", CreateButton(arrowsGo.transform, "Next"));

            SetField(rail, "arrows", arrows);
            SetField(rail, "hoverArea", rail.gameObject.AddComponent<HoverableUiElement>());
        }

        public static LobbyRailArrowsView Awaken(LobbyPagedRailView rail)
        {
            typeof(LobbyPagedRailView).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rail, null);
            return rail.GetComponentInChildren<LobbyRailArrowsView>(true);
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform));
            buttonGo.transform.SetParent(parent);
            return buttonGo.AddComponent<Button>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }
    }
}

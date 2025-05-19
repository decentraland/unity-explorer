using Arch.Core;
using Arch.Core.Utils;
using Global.Dynamic;
using SceneRunner.Scene;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Editor
{
    public sealed class EntityBrowserWindow : EditorWindow
    {
        private float scrollY;
        private static readonly GUILayoutOption[] SINGLE_LINE_HEIGHT_LAYOUT
            = { GUILayout.Height(EditorGUIUtility.singleLineHeight) };

        // World selector
        private QueryDescription sceneFacades;
        private World selectedWorld;
        private string[] worldNames;

        // Query editor
        private QueryDescription query = new ();
        private readonly HashSet<Type> all = new ();
        private readonly HashSet<Type> any = new ();
        private readonly HashSet<Type> none = new ();
        private readonly HashSet<Type> exclusive = new ();
        private HashSet<Type> unfoldedQueryTerm;
        private Type[] registryTypes;
        private int registryTypeCount;

        // Entity list
        private readonly Dictionary<(int, int), ComponentUIState> componentUiStates = new ();
        private readonly Dictionary<int, EntityUIState> entityUiStates = new ();
        private readonly HashSet<int> existingEntities = new ();
        private static readonly Dictionary<Type, (FieldInfo, string)[]> FIELD_CACHE = new ();

        [MenuItem("Arch/View/Entities")]
        private static void ShowWindow()
        {
            var window = GetWindow<EntityBrowserWindow>("Entities");
            window.Show();
        }

        private void OnEnable()
        {
            sceneFacades = new QueryDescription().WithAll<ISceneFacade>();
        }

        private void OnGUI()
        {
            scrollY = EditorGUILayout.BeginScrollView(new Vector2(0f, scrollY)).y;

            // World selector

            World globalWorld = GlobalWorld.ECSWorldInstance;

            if (globalWorld != null)
            {
                using (ListPool<(string name, World world)>.Get(out List<(string name, World world)> worlds))
                {
                    worlds.Add(("Global", globalWorld));
                    GetSceneWorlds getSceneWorlds = new () { Worlds = worlds };

                    globalWorld.InlineQuery<GetSceneWorlds, ISceneFacade>(in sceneFacades,
                        ref getSceneWorlds);

                    if (worldNames == null || worldNames.Length != worlds.Count)
                        worldNames = new string[worlds.Count];

                    for (var i = 0; i < worldNames.Length; i++)
                        worldNames[i] = worlds[i].name;

                    EditorGUI.BeginChangeCheck();

                    int selectedWorldIndex = worlds.FindIndex(i => i.world == selectedWorld);
                    selectedWorldIndex = EditorGUILayout.Popup("World", selectedWorldIndex, worldNames);

                    if (EditorGUI.EndChangeCheck())
                        selectedWorld = worlds[selectedWorldIndex].world;
                }
            }
            else
                EditorGUILayout.Popup("World", -1, Array.Empty<string>());

            // Query editor

            GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            GUILayout.Label("Query", EditorStyles.boldLabel);

            UpdateRegistryTypeArray();

            ComponentType[] newAll = DrawQueryTerm(nameof(query.All), query.All, all);
            ComponentType[] newAny = DrawQueryTerm(nameof(query.Any), query.Any, any);
            ComponentType[] newNone = DrawQueryTerm(nameof(query.None), query.None, none);
            ComponentType[] newExclusive = DrawQueryTerm(nameof(query.Exclusive), query.Exclusive, exclusive);

            query = new QueryDescription(newAll, newAny, newNone, newExclusive);

            // Entity list

            GUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
            GUILayout.Label("Entities", EditorStyles.boldLabel);

            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.4f;

            if (selectedWorld != null && query != QueryDescription.Null)
            {
                foreach (Archetype archetype in selectedWorld.Query(in query).GetArchetypeIterator())
                    DrawEntities(archetype);

                using (ListPool<int>.Get(out var keys))
                {
                    keys.AddRange(entityUiStates.Keys);

                    foreach (int entityId in keys)
                        if (!existingEntities.Contains(entityId))
                            entityUiStates.Remove(entityId);
                }

                using (ListPool<(int, int)>.Get(out var keys))
                {
                    keys.AddRange(componentUiStates.Keys);

                    foreach ((int entityId, int componentTypeId) in keys)
                        if (!existingEntities.Contains(entityId))
                            componentUiStates.Remove((entityId, componentTypeId));
                }

                existingEntities.Clear();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawComponents(Chunk chunk, int index, int entityId,
            Span<ComponentType> componentTypes)
        {
            foreach (var componentType in componentTypes)
            {
                if (!componentUiStates.TryGetValue((entityId, componentType.Id), out var uiState))
                {
                    uiState = new ComponentUIState(componentType.Type);
                    componentUiStates.Add((entityId, componentType.Id), uiState);
                }

                object component = null;
                string niceName = uiState.NiceName;

                if (uiState.Foldout)
                {
                    component = chunk.Get(index, componentType);

                    if (component != null)
                    {
                        Type actualType = component.GetType();

                        if (actualType != componentType.Type)
                            niceName = $"{niceName} ({GetNiceName(actualType)})";
                    }
                    else
                        niceName = $"{niceName} (null)";
                }

                uiState.Foldout = EditorGUILayout.Foldout(uiState.Foldout, niceName);

                if (uiState.Foldout && component != null)
                {
                    EditorGUI.indentLevel++;
                    DrawObject(component);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawEntities(Archetype archetype)
        {
            Span<ComponentType> componentTypes = archetype.Signature.Components;

            foreach (Chunk chunk in archetype)
            {
                int chunkSize = chunk.Count;

                for (int index = 0; index < chunkSize; index++)
                {
                    int entityId = chunk.Entity(index).Id;
                    existingEntities.Add(entityId);

                    if (!entityUiStates.TryGetValue(entityId, out var uiState))
                    {
                        uiState = new EntityUIState(entityId);
                        entityUiStates.Add(entityId, uiState);
                    }

                    uiState.Foldout = EditorGUILayout.Foldout(uiState.Foldout, uiState.NiceName);

                    if (uiState.Foldout)
                    {
                        EditorGUI.indentLevel++;
                        DrawComponents(chunk, index, entityId, componentTypes);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        private static void DrawObject(object obj)
        {
            Type type = obj.GetType();

            if (!FIELD_CACHE.TryGetValue(type, out (FieldInfo, string)[] fields))
            {
                const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public
                                                                 | BindingFlags.NonPublic;

                fields = type.GetFields(FLAGS)
                             .Select(i => (i, GetNiceName(i)))
                             .ToArray();

                FIELD_CACHE.Add(type, fields);
            }

            foreach ((FieldInfo field, string niceName) in fields)
            {
                object value = field.GetValue(obj);

                if (value is ICollection collection)
                {
                    string valueStr;

                    if (collection.Count <= 8)
                        valueStr = string.Join(", ", collection
                                                    .OfType<object>()
                                                    .Select(i => i.ToString()));
                    else
                        valueStr = $"count: {collection.Count}";

                    EditorGUILayout.LabelField(niceName, valueStr);
                }
                else if (value is Object unityObj)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(niceName);

                    if (GUILayout.Button(EditorGUIUtility.ObjectContent(unityObj, unityObj.GetType()),
                            EditorStyles.label, SINGLE_LINE_HEIGHT_LAYOUT))
                    {
                        EditorGUIUtility.PingObject(unityObj);
                        Selection.activeObject = unityObj;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else if (value != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(niceName);
                    int indentLevel = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;
                    EditorGUILayout.SelectableLabel(value.ToString(), SINGLE_LINE_HEIGHT_LAYOUT);
                    EditorGUI.indentLevel = indentLevel;
                    EditorGUILayout.EndHorizontal();
                }
                else
                    EditorGUILayout.LabelField(niceName, "null", italicLabel);
            }
        }

        private ComponentType[] DrawQueryTerm(string name, ComponentType[] queryTerm, HashSet<Type> types)
        {
            EditorGUI.BeginChangeCheck();

            bool foldout = EditorGUILayout.Foldout(unfoldedQueryTerm == types,
                $"{name}: [{string.Join(", ", types.Select(GetNiceName))}]");

            if (EditorGUI.EndChangeCheck())
                unfoldedQueryTerm = foldout ? types : null;

            if (!foldout)
                return queryTerm;

            EditorGUI.BeginChangeCheck();

            bool containsAny = false;

            for (int i = 0; i < registryTypeCount; i++)
            {
                Type type = registryTypes[i];
                bool oldValue = types.Contains(type);
                bool newValue = GUILayout.Toggle(oldValue, GetNiceName(type));
                containsAny = containsAny || newValue;

                if (newValue != oldValue)
                {
                    if (newValue)
                        types.Add(type);
                    else
                        types.Remove(type);
                }
            }

            // If the registry shrinks, the types set could be non-empty even if you could have no types
            // checked.
            if (!containsAny)
                types.Clear();

            if (!EditorGUI.EndChangeCheck())
                return queryTerm;

            using (ListPool<ComponentType>.Get(out var componentTypes))
            {
                foreach (Type type in types)
                    if (ComponentRegistry.TryGet(type, out ComponentType componentType))
                        componentTypes.Add(componentType);

                if (queryTerm == null || queryTerm.Length != componentTypes.Count)
                    queryTerm = new ComponentType[componentTypes.Count];

                componentTypes.CopyTo(queryTerm);
            }

            return queryTerm;
        }

        private static string GetNiceName(FieldInfo field) =>
            field.Name[0] == '<'
                ? field.Name.Substring(1, field.Name.IndexOf('>') - 1)
                : field.Name;

        private static readonly Dictionary<Type, string> TYPE_NICE_NAME_CACHE = new ();

        private static string GetNiceName(Type type)
        {
            if (!TYPE_NICE_NAME_CACHE.TryGetValue(type, out string niceName))
            {
                niceName = type.Name;

                if (type.IsGenericType)
                {
                    IEnumerable<string> args = type.GetGenericArguments().Select(GetNiceName);
                    niceName = $"{niceName.Substring(0, niceName.IndexOf('`'))}<{string.Join(", ", args)}>";
                }

                TYPE_NICE_NAME_CACHE.Add(type, niceName);
            }

            return niceName;
        }

        private static GUIStyle _italicLabel;

        private static GUIStyle italicLabel
        {
            get
            {
                if (_italicLabel == null)
                {
                    _italicLabel = new GUIStyle(GUI.skin.label);
                    _italicLabel.fontStyle = FontStyle.Italic;
                }

                return _italicLabel;
            }
        }

        /// <summary>Sort the registry types by name and filter out any nulls.</summary>
        private void UpdateRegistryTypeArray()
        {
            int oldRegistryTypeCount = registryTypeCount;
            registryTypeCount = ComponentRegistry.Types.Length;

            if (registryTypeCount == 0)
                return;

            if (registryTypes == null || registryTypes.Length < registryTypeCount)
                registryTypes = new Type[registryTypeCount];
            else

                // If the registry shrunk, null the extra items.
                for (int i = registryTypeCount; i < oldRegistryTypeCount; i++)
                    registryTypes[i] = null;

            ComponentRegistry.Types.CopyTo(registryTypes);
            Array.Sort(registryTypes, 0, registryTypeCount, RegistryTypeComparer.INSTANCE);

            // Because ComponentRegistry.Types can return nulls, we use a custom comparer that sorts
            // them to the end of the array, then we find the last non-null item.
            int lastItem = registryTypeCount - 1;

            while (lastItem >= 0 && registryTypes[lastItem] == null)
                lastItem--;

            registryTypeCount = lastItem + 1;
        }

        private sealed class ComponentUIState
        {
            public string NiceName { get; }
            public bool Foldout { get; set; }

            public ComponentUIState(Type type)
            {
                NiceName = GetNiceName(type);
            }
        }

        private sealed class EntityUIState
        {
            public bool Foldout { get; set; }
            public string NiceName { get; }

            public EntityUIState(int entityId)
            {
                NiceName = entityId.ToString();
            }
        }

        private struct GetSceneWorlds : IForEach<ISceneFacade>
        {
            public List<(string, World)> Worlds;

            public void Update(ref ISceneFacade sceneFacade)
            {
                Worlds.Add((sceneFacade.Info.Name, sceneFacade.EcsExecutor.World));
            }
        }

        private sealed class RegistryTypeComparer : IComparer<Type>
        {
            public static readonly RegistryTypeComparer INSTANCE = new ();

            public int Compare(Type x, Type y)
            {
                if (x != null && y != null)
                    return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                else if (x == null && y == null)
                    return 0;
                else if (x == null)
                    return 1;
                else // y == null
                    return -1;
            }
        }
    }
}

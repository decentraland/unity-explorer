using DCL.Diagnostics;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diagnostics.ReportsHandling.Settings.Editor
{
    [CustomPropertyDrawer(typeof(CategorySeverityMatrix))]
    public class CategorySeverityMatrixDrawer : PropertyDrawer
    {
        private const int LOG_TYPE_WIDTH_PERCENT = 60;
        private const int CATEGORY_WIDTH_PERCENT = 100 - LOG_TYPE_WIDTH_PERCENT;
        private const int RIGHT_MARGIN = 6;
        /// <summary>
        ///     Rows - drawn as is
        /// </summary>
        private static readonly string[] CATEGORIES = typeof(ReportCategory).GetFields(BindingFlags.Static | BindingFlags.Public)
                                                                            .Where(f => f.FieldType == typeof(string))
                                                                            .Select(f => f.GetValue(null))
                                                                            .Cast<string>()
                                                                            .OrderBy(s => s)
                                                                            .ToArray();

        /// <summary>
        ///     Columns - drawn rotated
        /// </summary>
        private static readonly LogType[] SEVERITIES = { LogType.Log, LogType.Warning, LogType.Error, LogType.Exception, LogType.Assert };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty entries = property.FindPropertyRelative("entries");

            void ModifyMatrix(LogType logType, string category, bool enabled)
            {
                if (enabled)
                {
                    // insert if not found
                    if (!IsEnabled(logType, category))
                    {
                        int arraySize = entries.arraySize;
                        entries.InsertArrayElementAtIndex(arraySize);
                        SerializedProperty element = entries.GetArrayElementAtIndex(arraySize);
                        element.FindPropertyRelative(nameof(CategorySeverityMatrix.Entry.Category)).stringValue = category;
                        element.FindPropertyRelative(nameof(CategorySeverityMatrix.Entry.Severity)).enumValueIndex = (int)logType;
                    }
                }
                else
                {
                    // Remove all such entries
                    while (IsEnabledFindIndex(logType, category, out int index)) entries.DeleteArrayElementAtIndex(index);
                }

                // delete all found
                entries.serializedObject.ApplyModifiedProperties();
            }

            bool IsEnabledFindIndex(LogType logType, string category, out int index)
            {
                for (var i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    string cat = entry.FindPropertyRelative(nameof(CategorySeverityMatrix.Entry.Category)).stringValue;
                    var severity = (LogType)entry.FindPropertyRelative(nameof(CategorySeverityMatrix.Entry.Severity)).enumValueIndex;

                    if (logType == severity && category == cat)
                    {
                        index = i;
                        return true;
                    }
                }

                index = -1;
                return false;
            }

            bool IsEnabled(LogType logType, string category) =>
                IsEnabledFindIndex(logType, category, out _);

            var container = new VisualElement();

            // Add a fold out
            var foldout = new Foldout { text = property.displayName };

            foldout.Add(CreateLogTypeLabels(IsEnabled, ModifyMatrix));
            foldout.Add(CreateRows(entries, IsEnabled, ModifyMatrix));
            foldout.Add(CreateButtons(ModifyMatrix));

            container.Add(foldout);

            return container;
        }

        private static VisualElement CreateLogTypeLabels(Func<LogType, string, bool> isEnabled, Action<LogType, string, bool> modifyMatrix)
        {
            var logTypeContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0f,
                    alignSelf = Align.FlexEnd,
                    marginRight = RIGHT_MARGIN,
                    width = new Length(LOG_TYPE_WIDTH_PERCENT, LengthUnit.Percent),
                },
            };

            // add all labels
            foreach (LogType logType in SEVERITIES)
            {
                var label = new Label(logType.ToString())
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleRight,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        width = new Length(100 / (float)SEVERITIES.Length, LengthUnit.Percent),
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 4f,
                        marginBottom = 4f,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 4f,
                        paddingBottom = 4f,
                    },
                };

                // toggle the whole column
                label.RegisterCallback<ClickEvent>(_ =>
                {
                    // if any is enabled - disable
                    foreach (string category in CATEGORIES)
                    {
                        if (isEnabled(logType, category))
                        {
                            foreach (string inner in CATEGORIES)
                                modifyMatrix(logType, inner, false);

                            return;
                        }
                    }

                    foreach (string inner in CATEGORIES)
                        modifyMatrix(logType, inner, true);
                });

                logTypeContainer.Add(label);
            }

            return logTypeContainer;
        }

        private static VisualElement CreateRows(SerializedProperty entries, Func<LogType, string, bool> isEnabled, Action<LogType, string, bool> modifyMatrix)
        {
            var container = new VisualElement();

            for (var categoryIndex = 0; categoryIndex < CATEGORIES.Length; categoryIndex++)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexGrow = 1f,
                        flexDirection = FlexDirection.Row,
                    },
                };

                string category = CATEGORIES[categoryIndex];

                var label = new Label(category)
                {
                    style =
                    {
                        width = new Length(CATEGORY_WIDTH_PERCENT, LengthUnit.Percent),
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleRight,
                    },
                };

                // toggle the whole category
                label.RegisterCallback<ClickEvent>(_ =>
                {
                    // if any is enabled - disable
                    foreach (LogType logType in SEVERITIES)
                    {
                        if (isEnabled(logType, category))
                        {
                            foreach (LogType inner in SEVERITIES)
                                modifyMatrix(inner, category, false);

                            return;
                        }
                    }

                    foreach (LogType inner in SEVERITIES)
                        modifyMatrix(inner, category, true);
                });

                row.Add(label);

                var toggles = new VisualElement
                {
                    style =
                    {
                        marginRight = RIGHT_MARGIN,
                        width = new Length(LOG_TYPE_WIDTH_PERCENT, LengthUnit.Percent),
                        flexDirection = FlexDirection.Row,
                    },
                };

                foreach (LogType logType in SEVERITIES)
                {
                    var vs = new VisualElement
                    {
                        style =
                        {
                            alignItems = Align.FlexEnd,
                            width = new Length(100 / (float)SEVERITIES.Length, LengthUnit.Percent),
                            marginLeft = 0f,
                            marginRight = 0f,
                            marginTop = 0f,
                            marginBottom = 0f,
                            paddingLeft = 0f,
                            paddingRight = 0f,
                            paddingTop = 0f,
                            paddingBottom = 0f,
                        },
                    };

                    var toggle = new Toggle(string.Empty)
                    {
                        style =
                        {
                            marginLeft = 0f,
                            marginRight = 0f,
                            marginTop = 2f,
                            marginBottom = 2f,
                            paddingLeft = 0f,
                            paddingRight = 0f,
                            paddingTop = 0f,
                            paddingBottom = 0f,
                        },
                    };

                    toggle.RegisterValueChangedCallback(evt => modifyMatrix(logType, category, evt.newValue));
                    toggle.SetValueWithoutNotify(isEnabled(logType, category));
                    toggle.TrackPropertyValue(entries, _ => { toggle.SetValueWithoutNotify(isEnabled(logType, category)); });

                    vs.Add(toggle);

                    toggles.Add(vs);
                }

                row.Add(toggles);

                container.Add(row);
            }

            return container;
        }

        private static VisualElement CreateButtons(Action<LogType, string, bool> modifyMatrix)
        {
            var container = new VisualElement
            {
                style =
                {
                    marginTop = 6,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0,
                },
            };

            container.Add(new Button(() =>
            {
                foreach (LogType logType in SEVERITIES)

                foreach (string cat in CATEGORIES)
                    modifyMatrix(logType, cat, true);
            }) { text = "Select All" });

            container.Add(new Button(() =>
            {
                foreach (LogType logType in SEVERITIES)

                foreach (string cat in CATEGORIES)
                    modifyMatrix(logType, cat, false);
            }) { text = "Deselect All" });

            return container;
        }
    }
}

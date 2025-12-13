using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using Object = UnityEngine.Object;

namespace DCL.DebugUtilities
{
    public class DebugUtilitiesContainer
    {
        public UIDocument RootDocument { get; private set; }
        public IDebugContainerBuilder Builder { get; }

        private DebugUtilitiesContainer(IDebugContainerBuilder builder, UIDocument rootDocument)
        {
            Builder = builder;
            RootDocument = rootDocument;
        }

        public static DebugUtilitiesContainer Create(DebugViewsCatalogSO viewsCatalog, bool isFullDebug, bool isLocalSceneDevelopment)
        {
            ISet<string>? allowedCategories = null;

            if (!isFullDebug)
            {
                if (isLocalSceneDevelopment)
                {
                    allowedCategories = new HashSet<string>
                    {
                        IDebugContainerBuilder.Categories.CURRENT_SCENE,
                        IDebugContainerBuilder.Categories.PERFORMANCE,
                        IDebugContainerBuilder.Categories.MEMORY,
                        IDebugContainerBuilder.Categories.MEMORY_LIMITS,
                        IDebugContainerBuilder.Categories.WEB_REQUESTS,
                    };
                }
                else
                {
                    allowedCategories = new HashSet<string>
                    {
                        IDebugContainerBuilder.Categories.CURRENT_SCENE,
                        IDebugContainerBuilder.Categories.ROOM_INFO,
                        IDebugContainerBuilder.Categories.ROOM_SCENE,
                        IDebugContainerBuilder.Categories.ROOM_THROUGHPUT,
                        IDebugContainerBuilder.Categories.ROOM_ISLAND,
                        IDebugContainerBuilder.Categories.PERFORMANCE,
                        IDebugContainerBuilder.Categories.MEMORY,
                        IDebugContainerBuilder.Categories.REALM,
                        IDebugContainerBuilder.Categories.PROFILES,
                        IDebugContainerBuilder.Categories.ANALYTICS,
                        IDebugContainerBuilder.Categories.WEB_REQUESTS,
                        IDebugContainerBuilder.Categories.WEB_REQUESTS_DEBUG_METRICS,
                    };
                }
            }

            UIDocument? rootDocument = Object.Instantiate(viewsCatalog.RootDocumentPrefab);

            return new DebugUtilitiesContainer(
                new DebugContainerBuilder(
                    () => viewsCatalog.Widget.InstantiateForElement<DebugWidget>(),
                    () => viewsCatalog.ControlContainer.InstantiateForElement<DebugControl>(),
                    new Dictionary<Type, IDebugElementFactory>
                    {
                        { typeof(DebugOngoingWebRequestDef), new DebugElementBase<DebugOngoingWebRequestWidget, DebugOngoingWebRequestDef>.Factory(viewsCatalog.OngoingRequestsWidget) },
                        { typeof(AverageFpsBannerDef), new DebugElementBase<AverageFpsBannerElement, AverageFpsBannerDef>.Factory(viewsCatalog.AverageFpsBanner) },
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
                    allowedCategories
                ),
                rootDocument
            );
        }
    }
}

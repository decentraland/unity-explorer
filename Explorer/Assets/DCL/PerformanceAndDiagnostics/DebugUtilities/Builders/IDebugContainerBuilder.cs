using DCL.DebugUtilities.Views;
using DCL.Utility.Types;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Builder used by Plugins to schedule the creation of individual debug widgets
    /// </summary>
    public interface IDebugContainerBuilder
    {
        bool IsVisible { get; set; }

        DebugContainer Container { get; }

        Result<DebugWidgetBuilder> AddWidget(WidgetName name);

        IReadOnlyDictionary<string, DebugWidget> Widgets { get; }

        void BuildWithFlex(UIDocument debugRootCanvas);

        public static class Categories
        {
            public static readonly WidgetName ROOM_INFO = "Room: Info".AsWidgetName();
            public static readonly WidgetName ROOM_CHAT = "Room: Chat".AsWidgetName();
            public static readonly WidgetName ROOM_VOICE_CHAT = "Room: VoiceChat".AsWidgetName();
            public static readonly WidgetName ROOM_ISLAND = "Room: Island".AsWidgetName();
            public static readonly WidgetName ROOM_SCENE = "Room: Scene".AsWidgetName();
            public static readonly WidgetName ROOM_THROUGHPUT = "Room: Throughput".AsWidgetName();
            public static readonly WidgetName PERFORMANCE = "Performance".AsWidgetName();
            public static readonly WidgetName CRASH = "Crash".AsWidgetName();
            public static readonly WidgetName MEMORY = "Memory".AsWidgetName();
            public static readonly WidgetName CURRENT_SCENE = "Current scene".AsWidgetName();
            public static readonly WidgetName REALM = "Realm".AsWidgetName();
            public static readonly WidgetName ANALYTICS = "Analytics".AsWidgetName();
            public static readonly WidgetName GPU_INSTANCING = "GPU Instancing".AsWidgetName();
            public static readonly WidgetName MEMORY_LIMITS = "Memory Limits".AsWidgetName();
            public static readonly WidgetName WEB_REQUESTS = "Web Requests".AsWidgetName();
            public static readonly WidgetName WEB_REQUESTS_DEBUG_METRICS = "Web Requests Debug Metrics".AsWidgetName();
            public static readonly WidgetName MICROPHONE = "Microphone".AsWidgetName();
            public static readonly WidgetName WEB_REQUESTS_DELAY = "Web Requests Delay".AsWidgetName();
            public static readonly WidgetName WEB_REQUESTS_STRESS_TEST = "Web Requests Stress Test".AsWidgetName();
            public static readonly WidgetName LANDSCAPE_GPUI = "Landscape - GPUI".AsWidgetName();
            public static readonly WidgetName NAMETAGS = "Nametags".AsWidgetName();
            public static readonly WidgetName CHAT = "Chat".AsWidgetName();
            public static readonly WidgetName WEB3_AUTHENTICATION = "Web3 Authentication".AsWidgetName();
            public static readonly WidgetName WORLD_INFO = "World Info".AsWidgetName();
            public static readonly WidgetName LOCOMOTION_HANDS_IK = "locomotion: hands ik".AsWidgetName();
            public static readonly WidgetName QUALITY = "Quality".AsWidgetName();
        }
    }

    public static class DebugContainerBuilderExtensions
    {
        // TODO REMOVE BEFORE MERGE
        public static DebugWidgetBuilder? TryAddWidget(this IDebugContainerBuilder debugContainerBuilder, string name) =>
            debugContainerBuilder.TryAddWidget(name.AsWidgetName());

        public static DebugWidgetBuilder? TryAddWidget(this IDebugContainerBuilder debugContainerBuilder, WidgetName name)
        {
            var widget = debugContainerBuilder.AddWidget(name);
            return widget.Success ? widget.Value : null;
        }
    }

    // Enforces to keep widget naming organized
    public class WidgetName
    {
        public readonly string Name;

        internal WidgetName(string name)
        {
            this.Name = name;
        }

        public static implicit operator string(WidgetName widgetName) =>
            widgetName.Name;
    }

    internal static class WidgetNameExtensions
    {
        internal static WidgetName AsWidgetName(this string name) =>
            new (name);
    }
}

﻿using DCL.DebugUtilities.Views;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.Types;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Builder used by Plugins to schedule the creation of individual debug widgets
    /// </summary>
    public interface IDebugContainerBuilder
    {
        bool IsVisible { get; set; }

        DebugContainer Container { get; }

        Result<DebugWidgetBuilder> AddWidget(string name);

        IReadOnlyDictionary<string, DebugWidget> Widgets { get; }

        void BuildWithFlex(UIDocument debugRootCanvas);

        public static class Categories
        {
            public const string ROOM_INFO = "Room: Info";
            public const string ROOM_CHAT = "Room: Chat";
            public const string ROOM_ISLAND = "Room: Island";
            public const string ROOM_SCENE = "Room: Scene";
            public const string ROOM_THROUGHPUT = "Room: Throughput";
            public const string PERFORMANCE = "Performance";
            public const string CRASH = "Crash";
            public const string MEMORY = "Memory";
            public const string CURRENT_SCENE = "Current scene";
            public const string REALM = "Realm";
            public const string ANALYTICS = "Analytics";
            public const string GPU_INSTANCING = "GPU Instancing";
            public const string MEMORY_LIMITS = "Memory Limits";

        }
    }

    public static class DebugContainerBuilderExtensions
    {
        public static DebugWidgetBuilder? TryAddWidget(this IDebugContainerBuilder debugContainerBuilder, string name)
        {
            var widget = debugContainerBuilder.AddWidget(name);
            return widget.Success ? widget.Value : null;
        }
    }
}

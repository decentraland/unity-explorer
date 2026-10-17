using System;

namespace DCL.PluginSystem
{
    public class PluginNotInitializedException : Exception
    {
        public PluginNotInitializedException(Type type) : base($"Plugin {type.Name} failed to initialize") { }
    }
}

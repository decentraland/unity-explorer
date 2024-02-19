using DCL.Ipfs;
using Ipfs;
using System;
using System.Collections.Generic;

namespace SceneRunner.EmptyScene
{
    [Serializable]
    public class EmptySceneMapping
    {
        public ContentDefinition grass;
        public ContentDefinition environment;
    }

    [Serializable]
    public class EmptySceneMappings
    {
        public List<EmptySceneMapping> mappings;
    }
}

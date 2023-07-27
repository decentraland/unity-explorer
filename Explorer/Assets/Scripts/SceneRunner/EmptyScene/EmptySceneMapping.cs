using Ipfs;
using System;
using System.Collections.Generic;

namespace SceneRunner.EmptyScene
{
    [Serializable]
    public class EmptySceneMapping
    {
        public IpfsTypes.ContentDefinition grass;
        public IpfsTypes.ContentDefinition environment;
    }

    [Serializable]
    public class EmptySceneMappings
    {
        public List<EmptySceneMapping> mappings;
    }
}

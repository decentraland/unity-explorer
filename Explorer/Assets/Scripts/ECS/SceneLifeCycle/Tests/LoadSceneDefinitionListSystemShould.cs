using NUnit.Framework;

/*
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using Ipfs;
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;*/

namespace ECS.SceneLifeCycle.Tests
{


    // Can't test as Post to FileSystem is not supported
    public class LoadSceneDefinitionListSystemShould /*: LoadSystemBaseShould<LoadSceneDefinitionListSystem, SceneDefinitions, GetSceneDefinitionList>*/
    {
        /*private string successPath => $"file://{Application.dataPath + "/../TestResources/Content/ActiveEntitiesByPointer.json"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Content/non_existing"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetSceneDefinitionList CreateSuccessIntention() =>
            new (new List<IpfsTypes.SceneEntityDefinition>(), new List<Vector2Int>(), new CommonLoadingArguments(successPath));

        protected override GetSceneDefinitionList CreateNotFoundIntention() =>
            new (new List<IpfsTypes.SceneEntityDefinition>(), new List<Vector2Int>(), new CommonLoadingArguments(failPath));

        protected override GetSceneDefinitionList CreateWrongTypeIntention() =>
            new (new List<IpfsTypes.SceneEntityDefinition>(), new List<Vector2Int>(), new CommonLoadingArguments(wrongTypePath));

        protected override LoadSceneDefinitionListSystem CreateSystem() =>
            new (world, cache, new MutexSync());

        protected override void AssertSuccess(SceneDefinitions asset)
        {
            Assert.That(asset.Value.Count, Is.EqualTo(3));
        }*/
    }
}

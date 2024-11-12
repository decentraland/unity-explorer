using System.Collections;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using NUnit.Framework;
using UnityEngine;

public class AvatarTransformMatrixJobWrapperShould : MonoBehaviour
{
    private AvatarTransformMatrixJobWrapper jobWrapper;

    [SetUp]
    public void SetUp()
    {
        jobWrapper = new AvatarTransformMatrixJobWrapper();
    }

    [TearDown]
    public void TearDown()
    {
        jobWrapper.Dispose();
    }


    [Test]
    public void AddNewAvatarIncrementsAvatarIndex()
    {
        var avatarBase = new GameObject().AddComponent<AvatarBase>();
        var transformMatrixComponent = new AvatarTransformMatrixComponent
        {
            IndexInGlobalJobArray = -1, bones = new Transform[ComputeShaderConstants.BONE_COUNT]
        };

        int initialIndex = transformMatrixComponent.IndexInGlobalJobArray;

        jobWrapper.UpdateAvatar(avatarBase, ref transformMatrixComponent);

        Assert.AreNotEqual(initialIndex, transformMatrixComponent.IndexInGlobalJobArray);
    }

    [Test]
    public void ResizeArraysDoublesCapacity()
    {
        // Add avatars until we exceed the initial capacity and force a resize
        for (int i = 0; i < AvatarTransformMatrixJobWrapper.AVATAR_ARRAY_SIZE + 1; i++)
        {
            var avatarBase = new GameObject().AddComponent<AvatarBase>();
            var transformMatrixComponent = new AvatarTransformMatrixComponent
            {
                IndexInGlobalJobArray = -1, bones = new Transform[ComputeShaderConstants.BONE_COUNT]
            };

            jobWrapper.UpdateAvatar(avatarBase, ref transformMatrixComponent);
        }

        // After resizing, the internal array size should be doubled
        Assert.AreEqual(AvatarTransformMatrixJobWrapper.AVATAR_ARRAY_SIZE * 2, jobWrapper.currentAvatarAmountSupported);
    }

    [Test]
    public void ReleasedIndexesAreReused()
    {
        var avatarBase = new GameObject().AddComponent<AvatarBase>();
        var transformMatrixComponent1 = new AvatarTransformMatrixComponent
        {
            IndexInGlobalJobArray = -1, bones = new Transform[ComputeShaderConstants.BONE_COUNT]
        };

        var transformMatrixComponent2 = new AvatarTransformMatrixComponent
        {
            IndexInGlobalJobArray = -1, bones = new Transform[ComputeShaderConstants.BONE_COUNT]
        };

        // Add the first avatar
        jobWrapper.UpdateAvatar(avatarBase, ref transformMatrixComponent1);
        int firstIndex = transformMatrixComponent1.IndexInGlobalJobArray;

        // Release the first avatar
        jobWrapper.ReleaseAvatar(ref transformMatrixComponent1);

        // Add a second avatar and check if it reuses the released index
        jobWrapper.UpdateAvatar(avatarBase, ref transformMatrixComponent2);
        int secondIndex = transformMatrixComponent2.IndexInGlobalJobArray;

        Assert.AreEqual(firstIndex, secondIndex);
    }

    [Test]
    public void MatrixAndBoolArraysResize()
    {
        // Fill the wrapper to trigger a resize
        for (int i = 0; i < AvatarTransformMatrixJobWrapper.AVATAR_ARRAY_SIZE + 1; i++)
        {
            var avatarBase = new GameObject().AddComponent<AvatarBase>();
            var transformMatrixComponent = new AvatarTransformMatrixComponent
            {
                IndexInGlobalJobArray = -1, bones = new Transform[ComputeShaderConstants.BONE_COUNT]
            };

            jobWrapper.UpdateAvatar(avatarBase, ref transformMatrixComponent);
        }

        // The matrices and bools should have been resized to double their initial size
        Assert.AreEqual(AvatarTransformMatrixJobWrapper.AVATAR_ARRAY_SIZE * 2, jobWrapper.matrixFromAllAvatars.Length);
        Assert.AreEqual(AvatarTransformMatrixJobWrapper.AVATAR_ARRAY_SIZE * 2, jobWrapper.updateAvatar.Length);
    }
}
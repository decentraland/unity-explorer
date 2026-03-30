# Skeleton Loading Animation

Many UI designs require the skeleton loading animation. For this reason, a `MonoBehaviour` has been created. Its name is `SkeletonLoadingView` inside `DCL.UI`.

## Setup

Inside your prefab, you need to create two GameObjects:
1. loaded state
2. loading state

Both need to have a `CanvasGroup` component.
To keep things organized, both objects should have a common parent where `SkeletonLoadingView` can be placed.

Under `loaded state` there's the normal UI, while under `loading state` you create your skeletons.
Each animated bone must have a gradient texture as a child. The design relies on the fact that this gradient texture is `GradientLoading`, therefore its length must be double the width of its container and its X=0 position must be on the leftmost side of the parent.

The final step is to drag all the canvas groups references and the gradient (bones) to the array inside `SkeletonLoadingView`. There you can configure the fade in (of the loaded state) duration and the tween duration for the wave effect.

## Example

All communities UIs implement this loading animation. Therefore you can refer to their prefabs.

## Prefab

There's a prefab that implements this loading animation for images. It works like `Image` (with an `ImageController`) and it's called `ImageWithSkeletonAnimation`.

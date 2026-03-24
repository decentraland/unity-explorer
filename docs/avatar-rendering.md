# Avatar Rendering

> See also: [Emotes](emotes.md) | [Skeleton Loading Animation](skeleton-loading-animation.md) | [Avatar Animation for Demos](avatar-animation-for-demos.md)

This section describes how Avatars are structured in unity-explorer, and how we take advantage of a custom Avatar rendering technique for maximum performance.

## Avatar Structure

Each Avatar in unity-explorer is composed by an `AvatarBase` prefab and as many wearables as the player has equipped.

![Avatar structure overview](https://github.com/decentraland/unity-explorer/assets/1999557/36b8bb68-0387-47fe-b640-e851deb5f031)

`AvatarBase` is the base GameObject prefab that drives the animation. This GameObject consists of a bone structure and an invisible skinned mesh renderer (for documentation purposes, it has been turned on in the following image). The parent GameObject contains the Animator component, which plays the animation, moving the bones.

![AvatarBase bone structure](https://github.com/decentraland/unity-explorer/assets/1999557/b1ab472b-fbf8-4601-842c-6c96ef9ca632)

'Wearables' are GameObjects that have been instantiated using the assets downloaded from the asset bundle server. These assets are downloaded as skinned mesh renderers, but this makes them non-scalable given Unity's current SMR processing. Therefore, we have to transform them into MeshRenderers (as shown in the image below) and use a custom skinning and rendering technique to achieve maximum performance.

![Wearable MeshRenderers](https://github.com/decentraland/unity-explorer/assets/1999557/75e43017-9ed9-4561-b367-43f4fb8670d8)

## Rendering

Avatars are rendered using a combination of Unity's Job System, a Compute Shader and the `AvatarCelShading` shader.

![Rendering pipeline overview](https://github.com/decentraland/unity-explorer/assets/1999557/ee58814e-736d-4641-a966-160c0ed85133)

These systems work together to achieve the skinning and rendering of all instantiated avatars present in the Global World. Let's break it down to see more in detail how it works.

### Setup

Using our custom system, an avatar is rendered using one single custom Compute Shader. All of the wearables vertex are passed into a custom buffer, and then the skinning process is dispatched in one single command; which will output the Avatar's resultant state.

This does not mean that we are going to combine meshes or materials. Each wearable is an independent mesh renderer and material; the only thing that is shared is the buffer where the vertex info is contained. The intention to do this is to achieve maximum speed when changing wearables. Since we do not combine meshes, changing a wearable (or instantiating a new avatar) is as simple as adding new information into the shared buffer.

This process can be found in the `ComputeShaderSkinning` custom skinning strategy. There you will find how we set up the buffers and indexes, as well as how we transform the wearables from `SkinnedMeshRenderer` to `MeshRenderer`.

### Skinning

Explorer-alpha uses a custom solution of GPU skinning. The math behind skinning goes as follows:

![Skinning math diagram](https://github.com/decentraland/unity-explorer/assets/1999557/e9d151d3-4694-44f1-bdc2-374bb800be0f)

#### Unity Job System

The first step is to transform the resultant animated bones from AvatarBase into local space. This is a process that can be parallelized in the CPU taking advantage of Unity's Job System and the Burst compiler. We are dispatching the job as early as possible in the frame, using the `StartAvatarMatricesCalculationSystem` and completing it as late as possible using the `FinishAvatarMatricesCalculationSystem`. Now that we have our result, we can send it to the GPU.

#### Compute Shader

The second step is to send the information into the GPU. This is done by a `SetData` call, which puts the CPU information into the GPU. This is the only communication flow we have between the processing units since this process can be very costly if abused.

We already have the original state of the mesh renderer that had been put in during the setup stage. Therefore, now we can do the remaining work in the ComputeShader. We calculate the position, normals and tangents of every vertex using a 4-weight bone calculation. You can check how this is done in `ComputeShaderSkinning`. The results are then dropped into a Global Vertex Buffer (GVB), which has the following form:

![Global Vertex Buffer structure](https://github.com/decentraland/unity-explorer/assets/1999557/eec8101d-0bb0-47a0-8c9f-3b455e939be6)

We utilize a GVB due to constraints preventing the customization of buffer sizes for the AvatarCelShading material, as this would disrupt the SRP batching process. The GVB serves as a repository for VertexInfo pertaining to all instantiated avatars. When an avatar is removed, it is relocated from the buffer, creating available space for the next avatar. This procedure can result in buffer fragmentation, necessitating the use of the `FixedComputeBufferHandler` and `MakeVertsOutBufferDefragmentationSystem` to manage and optimize the buffer.

## Avatar Cel Shading and Draw Calls

We have now all the Vertex Info necessary to render the avatars in the GVB. Using the indexes that were set during the setup stage, the material knows where to look for the vertex, normals and tangents needed for rendering.

This shader multiplies the number of draw calls, but that is not necessarily bad.

![Draw call diagram](https://github.com/decentraland/unity-explorer/assets/1999557/534661c9-9c2d-4505-97a3-ef8633143996)

A drawcall isn't just a single action, it's actually just the final call in a series of bindings to the GPU:

- Bind vertex and index buffers
- Bind Shader
- Bind Textures
- Bind per material data
- Bind per object data

So here we have the first drawcall of a batch and then the subsequent drawcalls in that batch look like this!

If we can reduce these bindings between drawcalls we can fundamentally reduce the drawcall cost. The biggest hitter for binding is shaders, so reducing shader variants and batching meshes with matching shaders give us the greatest potential. Textures are next as they have to be formatted and type-validated. Per material and object data are often stored in constant buffers, however, it's possible to map and unmap data from constant buffers that have already been bound to a shader because the size and structure are validated to the shader, the internal data can be switched out with ease and not require any validation. Vertex and Index buffer binding is just simple validation against their structure and data layout, so these are probably the cheapest to change between drawcalls.

Our approach was to develop a new shader, reduce the variants (still in development), and also move textures into texture arrays. By using a texture array we can have one texture bind for all avatars rendered and then index into the texture array in the shader. By vastly reducing the costs of binding between drawcalls we can effectively have more of them without taking a performance hit and at the same time provide flexibility in swapping wearables, skin color, body types etc without having to do mesh merging after each change.

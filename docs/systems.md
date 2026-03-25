# Systems

## LifeCycle

## Streamables

## Prioritization

## Scene bounds checker

Bounds are described by 4 planes:

```csharp
public readonly struct SceneCircumscribedPlanes
{
    public readonly float MinX;
    public readonly float MaxX;
    public readonly float MinZ;
    public readonly float MaxZ;
}
```

It means that effectively parcels a scene consists of are circumscribed (approximated by a rectangular area).

### Rendering

The capability of culling meshes as they go out of scene bounds is provided by `Scene.shader`:

```hlsl
_PlaneClipping("PlaneClipping", Vector) = (-2147483648, 2147483648, -2147483648, 2147483648)
```

`_PlaneClipping` is set only once on initialization of `GLTFContainer` or `MeshPrimitive`.
For empty scenes planes always have the default value.


Clipping is performed on the shader side for each pass via a simple algorithm, effectively leading to zero overhead.

```hlsl
static const float3 _PlaneX = float3(1.0, 0.0, 0.0);
static const float3 _PlaneY = float3(0.0, 0.0, 1.0);

void ClipFragmentViaPlaneTests(const float3 _positionWS, const float _PlaneClippingPosX, const float _PlaneClippingNegX, const float _PlaneClippingPosZ, const float _PlaneClippingNegZ)
{
    float distanceX = dot(_positionWS, _PlaneX);
    clip(distanceX - _PlaneClippingPosX);
    clip(-distanceX + _PlaneClippingNegX);

    float distanceZ = dot(_positionWS, _PlaneY);
    clip(distanceZ - _PlaneClippingPosZ);
    clip(-distanceZ + _PlaneClippingNegZ);
}
```

### Colliders

Unlike meshes, colliders can't be clipped partially.
Therefore, if collider bounds intersect scene bounds it gets fully disabled.

```csharp
collider.enabled = sceneGeometry.CircumscribedPlanes.Intersects(collider.bounds);
```


`CheckColliderBoundsSystem` is responsible for this logic:
* Both Primitive Colliders and Colliders originated from GLTF assets are checked
* This check is performed only for assets in the `0` partition bucket (the closest ones including the scene the player is currently on)
* Execution is throttled once per fixed frame

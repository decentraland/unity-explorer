import { engine, MeshRenderer, Transform } from '@dcl/sdk/ecs'

let hoverState: number = 0

export function CircleHoverSystem(dt: number) {
  hoverState += Math.PI * dt * 0.5

  const entitiesWithBoxShapes = engine.getEntitiesWith(MeshRenderer)

  // iterate over the entities of the group
  for (const [entity] of entitiesWithBoxShapes) {
    const transform = Transform.getMutable(entity)

    // mutate the position
    transform.position.y =
      Math.cos(
        hoverState + Math.sqrt(Math.pow(transform.position.x - 8, 2) + Math.pow(transform.position.z - 8, 2)) / Math.PI
      ) *
        2 +
      2

    //entity.getComponent(Material).albedoColor.set(transform.position.x / 16, transform.position.y / 16, transform.position.z / 4);
  }
}

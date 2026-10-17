import {engine, MeshRenderer, Transform, VisibilityComponent} from '@dcl/sdk/ecs'

let hoverState: number = 0
let counter : number = 0;


export function ToggleVisibilitySystem(dt: number) {
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
  }

  counter++;
  if(counter > 60){
    const entitiesWithBoxShapes = engine.getEntitiesWith(MeshRenderer)
    for (const [entity] of entitiesWithBoxShapes) {
      const visibility = VisibilityComponent.getMutable(entity)
      visibility.visible = !visibility.visible;
    }
    counter = 0;
  }

}

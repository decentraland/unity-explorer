export * from '@dcl/sdk'

import {engine, Transform, MeshRenderer, TextShape, Billboard, VisibilityComponent} from '@dcl/sdk/ecs'
import { ToggleVisibilitySystem } from './circularSystem'

// My cube generator
function createCube(x: number, y: number, z: number) {
  // Dynamic entity because we aren't loading static entities out of this scene code
  const myEntity = engine.addEntity(true)

  Transform.create(myEntity, {
    position: { x, y, z }
  })

  MeshRenderer.setBox(myEntity)
  VisibilityComponent.create(myEntity, {visible : true})

  return myEntity
}

for (let x = 0.5; x < 16; x += 1) {
  for (let y = 0.5; y < 16; y += 1) {
    createCube(x, 0, y)
  }
}

engine.addSystem(ToggleVisibilitySystem)


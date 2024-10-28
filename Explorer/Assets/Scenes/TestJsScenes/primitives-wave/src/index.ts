export * from '@dcl/sdk'

import { engine, Transform, MeshRenderer } from '@dcl/sdk/ecs'
import { CircleHoverSystem } from './circularSystem'

// My cube generator
function createPrimitive(x: number, y: number, z: number, counter: number) {
  // Dynamic entity because we aren't loading static entities out of this scene code
  const myEntity = engine.addEntity(true)

  Transform.create(myEntity, {
    position: { x, y, z }
  })

  if(counter === 0) {
    MeshRenderer.setBox(myEntity)
  }else if(counter === 1) {
    MeshRenderer.setPlane(myEntity)
  }else if(counter === 2){
    MeshRenderer.setSphere(myEntity)
  }else{
    MeshRenderer.setCylinder(myEntity)
  }

  return myEntity
}

var counter = 0;
for (let x = 0.5; x < 16; x += 1) {
  for (let y = 0.5; y < 16; y += 1) {
    createPrimitive(x, 0, y, counter)
    counter = (counter + 1) % 4;
  }
}

engine.addSystem(CircleHoverSystem)



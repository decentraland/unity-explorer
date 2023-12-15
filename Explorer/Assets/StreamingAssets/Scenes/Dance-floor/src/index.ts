import { Animator, AudioSource, engine, Entity, GltfContainer, Material, MeshRenderer, Transform } from '@dcl/sdk/ecs'
import { Quaternion, Vector3 } from '@dcl/sdk/math'
export * from '@dcl/sdk'

import { colors, Beat, Tile } from './definitions'

function randomizeTileColor(entity: Entity) {
  const colorNum = Math.floor(Math.random() * colors.length)
  Material.setPbrMaterial(entity, {
    albedoColor: colors[colorNum],
    metallic: 0
  })
}

// Add floor Tiles
;[0, 1, 2, 3].forEach((x) => {
  ;[0, 1, 2, 3].forEach((z) => {
    const tileEntity = engine.addEntity()

    MeshRenderer.setPlane(tileEntity)

    Transform.create(tileEntity, {
      position: Vector3.create(x * 4 + 2, 0, z * 4 + 2),
      rotation: Quaternion.fromEulerDegrees(90, 0, 0),
      scale: Vector3.create(4, 4, 4)
    })

    randomizeTileColor(tileEntity)

    Tile.create(tileEntity)
  })
})

// Add Trevor NPC dancing
const trevorNPCEntity = engine.addEntity()
GltfContainer.create(trevorNPCEntity, {
  src: 'models/Trevor.glb'
})
Animator.create(trevorNPCEntity, {
  states: [
    {
      name: 'idle',
      clip: 'Armature_Idle',
      playing: true,
      loop: true
    }
  ]
})
Transform.create(trevorNPCEntity, {
  position: Vector3.create(5, 0.1, 5),
  rotation: Quaternion.fromEulerDegrees(0, -90, 0),
  scale: Vector3.create(1.5, 1.5, 1.5)
})

// Add audio
AudioSource.create(trevorNPCEntity, {
  audioClipUrl: 'sounds/Vexento.mp3',
  playing: true,
  loop: true
})

// Add beat keeper
const beatKeeperEntity = engine.addEntity()
const beatKeeperComponent = Beat.create(beatKeeperEntity, {
  interval: 0.5,
  timer: 0.5
})

// System to change tiles color
function tilesColorChangeSystem(dt: number) {
  beatKeeperComponent.timer -= dt

  if (beatKeeperComponent.timer < 0) {
    beatKeeperComponent.timer = beatKeeperComponent.interval

    for (const [tileEntity] of engine.getEntitiesWith(Tile)) {
      randomizeTileColor(tileEntity)
    }
  }
}
engine.addSystem(tilesColorChangeSystem)

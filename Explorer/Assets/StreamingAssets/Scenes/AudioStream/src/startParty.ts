import { engine, AudioStream, MeshRenderer, Transform, VideoPlayer, Material, Schemas } from '@dcl/sdk/ecs'
import { Color3, Quaternion } from '@dcl/sdk/math'

export function startParty() {
  startMusicStream()
  createVideoShapes()
}

function startMusicStream() {
  const streamEntity = engine.addEntity()
  AudioStream.create(streamEntity, {
    url: 'http://ice3.somafm.com/dronezone-128-mp3',
    playing: true,
    volume: 0.8
  })
}
function createVideoShapes() {
  // Create video stream player for textures
  const videoPlayer = engine.addEntity()
  VideoPlayer.create(videoPlayer, {
    src: 'https://player.vimeo.com/external/552481870.m3u8?s=c312c8533f97e808fccc92b0510b085c8122a875',
    playing: true,
    loop: true
  })

  // By reusing this texture we keep memory usage low
  const videoTexture = Material.Texture.Video({ videoPlayerEntity: videoPlayer })

  // Material settings
  const videoMaterial = {
    texture: videoTexture,
    roughness: 1.0,
    specularIntensity: 0,
    metallic: 0,
    emissiveTexture: videoTexture,
    emissiveIntensity: 1,
    emissiveColor: Color3.White()
  }

  // Floor
  const floor = engine.addEntity()
  MeshRenderer.setPlane(floor)
  Transform.create(floor, {
    position: { x: 8, y: 0.05, z: 8 },
    rotation: Quaternion.fromEulerDegrees(90, 0, 0),
    scale: { x: 16, y: 16, z: 16 }
  })
  Material.setPbrMaterial(floor, videoMaterial)

  // Big Cube
  const bigCube = engine.addEntity()
  MeshRenderer.setBox(bigCube)
  Transform.create(bigCube, {
    position: { x: 8, y: 5, z: 8 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 2, y: 2, z: 2 }
  })
  RotationComponent.create(bigCube, { speed: 1 })
  Material.setPbrMaterial(bigCube, videoMaterial)

  // Small Cube 1
  const smallCube1 = engine.addEntity()
  MeshRenderer.setBox(smallCube1)
  Transform.create(smallCube1, {
    position: { x: 3, y: 2, z: 3 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 1, y: 1, z: 1 }
  })
  RotationComponent.create(smallCube1, { speed: 0.4 })
  Material.setPbrMaterial(smallCube1, videoMaterial)

  // Small Cube 2
  const smallCube2 = engine.addEntity()
  MeshRenderer.setBox(smallCube2)
  Transform.create(smallCube2, {
    position: { x: 13, y: 4, z: 13 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 1, y: 1, z: 1 }
  })
  RotationComponent.create(smallCube2, { speed: 0.4 })
  Material.setPbrMaterial(smallCube2, videoMaterial)

  // Cone
  const cone = engine.addEntity()
  MeshRenderer.setCylinder(
    cone,
    0, // radius bottom
    1 // radius top
  )
  Transform.create(cone, {
    position: { x: 13, y: 8, z: 3 },
    rotation: Quaternion.fromEulerDegrees(55, 42, 38.7),
    scale: { x: 1.5, y: 1.5, z: 1.5 }
  })
  RotationComponent.create(cone, { speed: 0.2 })
  Material.setPbrMaterial(cone, videoMaterial)
}

// This component will hold all entities that rotate
const RotationComponent = engine.defineComponent('rotationComponent', {
  speed: Schemas.Number
})

// This system gets all entities with RotationComponent
// and increments their rotation
export function rotationSystem() {
  for (const [entity] of engine.getEntitiesWith(RotationComponent)) {
    const transform = Transform.getMutable(entity)
    const speed = RotationComponent.get(entity).speed
    const deltaRotation = Quaternion.fromEulerDegrees(speed, speed, speed)
    // Multiply the current rotation by the delta rotation to apply the rotation increment
    transform.rotation = Quaternion.multiply(transform.rotation, deltaRotation)
  }
}

engine.addSystem(rotationSystem)

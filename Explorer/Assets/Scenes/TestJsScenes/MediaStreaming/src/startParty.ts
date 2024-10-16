import {
  ColliderLayer,
  engine,
  InputAction,
  MeshCollider,
  AudioStream,
  MeshRenderer,
  Transform,
  VideoPlayer,
  pointerEventsSystem,
  videoEventsSystem,
  VideoState,
  TextShape,
  Material,
  Schemas
} from '@dcl/sdk/ecs'
import { Color3, Quaternion } from '@dcl/sdk/math'

export function startParty() {
  startMusicStream()
  createVideoShapes()

  // create screen for another video stream
  const screen = engine.addEntity()
  MeshRenderer.setPlane(screen)
  Transform.create(screen, { position: { x: 4, y: 1, z: 4 }, scale : { x: 2, y: 2, z: 2 }})

  // Create another video stream for it
  VideoPlayer.create(screen, {
    src: 'https://player.vimeo.com/external/878776548.m3u8?s=e6e54ac3862fe71ac3ecbdb2abbfdd7ca7daafaf&logging=false',
    playing: true,
    volume: 0.0,
    loop: false
  })
// By reusing this texture we keep memory usage low
  const videoTexture = Material.Texture.Video({ videoPlayerEntity: screen })

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
  Material.setPbrMaterial(screen, videoMaterial)
}

function startMusicStream() {
  const streamEntity = engine.addEntity()
  AudioStream.create(streamEntity, {
    url: 'http://ice3.somafm.com/dronezone-128-mp3',
    playing: true,
    volume: 0.3
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

  // Create text-shape for video events
  const sign = engine.addEntity()
  Transform.create(sign, {position: { x: 2, y: 1, z: 2 }})
  TextShape.create(sign, {text: 'Hello World'})
  const mutableText = TextShape.getMutable(sign)
  mutableText.text = 'new string'

  // register to video events
  videoEventsSystem.registerVideoEventsEntity(
    videoPlayer,
    function (videoEvent) {
      const mutableText = TextShape.getMutable(sign)
      mutableText.text =
        'video event - state: ' +
        videoEvent.state +
        '\ncurrent offset:' +
        videoEvent.currentOffset +
        '\nvideo length:' +
        videoEvent.videoLength


      switch (videoEvent.state) {
        case VideoState.VS_READY:
          mutableText.text = ('video event - video is READY')
          break
        case VideoState.VS_NONE:
          mutableText.text = ('video event - video is in NO STATE')
          break
        case VideoState.VS_ERROR:
          mutableText.text = ('video event - video ERROR')
          break
        case VideoState.VS_SEEKING:
          mutableText.text = ('video event - video is SEEKING')
          break
        case VideoState.VS_LOADING:
          mutableText.text = ('video event - video is LOADING')
          break
        case VideoState.VS_BUFFERING:
          mutableText.text = ('video event - video is BUFFERING')
          break
        case VideoState.VS_PLAYING:
          mutableText.text = ('video event - video started PLAYING')
          break
        case VideoState.VS_PAUSED:
          mutableText.text = ('video event - video is PAUSED')
          break
      }
    }
  )

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
  MeshCollider.setPlane(floor, ColliderLayer.CL_POINTER | ColliderLayer.CL_PHYSICS)
  Transform.create(floor, {
    position: { x: 8, y: 0.05, z: 8 },
    rotation: Quaternion.fromEulerDegrees(90, 0, 0),
    scale: { x: 16, y: 16, z: 16 }
  })
  Material.setPbrMaterial(floor, videoMaterial)

  pointerEventsSystem.onPointerDown(
    {
      entity: floor,
      opts: {
        button: InputAction.IA_POINTER,
        hoverText: 'Play/pause'
      }
    },
    () => {
      const vp = VideoPlayer.getMutable(videoPlayer)
      vp.playing = !vp.playing
    }
  )

  // Big Cube
  const bigCube = engine.addEntity()
  MeshRenderer.setBox(bigCube)
  MeshCollider.setBox(bigCube, ColliderLayer.CL_POINTER | ColliderLayer.CL_PHYSICS)
  Transform.create(bigCube, {
    position: { x: 8, y: 5, z: 8 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 2, y: 2, z: 2 }
  })
  RotationComponent.create(bigCube, { speed: 1 })
  Material.setPbrMaterial(bigCube, videoMaterial)

  pointerEventsSystem.onPointerDown(
    {
      entity: bigCube,
      opts: {
        button: InputAction.IA_POINTER,
        hoverText: 'Change clip'
      }
    },
    () => {
      const vp = VideoPlayer.getMutable(videoPlayer)
      vp.src =  'https://player.vimeo.com/external/878776548.m3u8?s=e6e54ac3862fe71ac3ecbdb2abbfdd7ca7daafaf&logging=false'
    }
  )

  // Small Cube 1
  const smallCube1 = engine.addEntity()
  MeshRenderer.setBox(smallCube1)
  MeshCollider.setBox(smallCube1, ColliderLayer.CL_POINTER | ColliderLayer.CL_PHYSICS)
  Transform.create(smallCube1, {
    position: { x: 3, y: 2, z: 3 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 1, y: 1, z: 1 }
  })
  RotationComponent.create(smallCube1, { speed: 0.4 })
  Material.setPbrMaterial(smallCube1, videoMaterial)

  pointerEventsSystem.onPointerDown(
    {
      entity: smallCube1,
      opts: {
        button: InputAction.IA_POINTER,
        hoverText: 'x2 playback'
      }
    },
    () => {
      const vp = VideoPlayer.getMutable(videoPlayer)
      if(vp.playbackRate)
        vp.playbackRate = 2.0*vp.playbackRate;
    }
  )

  // Small Cube 2
  const smallCube2 = engine.addEntity()
  MeshRenderer.setBox(smallCube2)
  MeshCollider.setBox(smallCube2, ColliderLayer.CL_POINTER | ColliderLayer.CL_PHYSICS)
  Transform.create(smallCube2, {
    position: { x: 13, y: 4, z: 13 },
    rotation: Quaternion.fromEulerDegrees(45, 0, 45),
    scale: { x: 1, y: 1, z: 1 }
  })
  RotationComponent.create(smallCube2, { speed: 0.4 })
  Material.setPbrMaterial(smallCube2, videoMaterial)

  pointerEventsSystem.onPointerDown(
    {
      entity: smallCube2,
      opts: {
        button: InputAction.IA_POINTER,
        hoverText: 'x2 Position'
      }
    },
    () => {
      const vp = VideoPlayer.getMutable(videoPlayer)
      if(vp.position)
        vp.position = vp.position + vp.position;
    }
  )

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

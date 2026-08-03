import {
  Billboard,
  BillboardMode,
  engine,
  Entity,
  Material,
  MeshRenderer,
  TextAlignMode,
  TextShape,
  Transform,
  VideoPlayer
} from '@dcl/sdk/ecs'
import { Color4, Vector3 } from '@dcl/sdk/math'
import { BASE_URL, Case, CASES, Expectation } from './cases.generated'

// A 6-wide grid of screens on one wall, so every case is on screen at once and
// a failure reads as a hole in the wall rather than something you have to walk
// to. The scene is 2x2 parcels; the wall sits at the far edge and the spawn
// point looks straight at it.
const COLUMNS = 6
const SCREEN_WIDTH = 4
const SCREEN_HEIGHT = 2.25
const COLUMN_PITCH = 5
const ROW_PITCH = 3.9
const WALL_Z = 4
const FIRST_ROW_Y = 2.4
const GRID_LEFT_X = 1.5

const OUTCOME_COLOR: Record<Expectation, Color4> = {
  PLAYS: Color4.create(0.16, 0.62, 0.24, 1),
  REFUSED: Color4.create(0.72, 0.16, 0.16, 1),
  BUILD_DEPENDENT: Color4.create(0.16, 0.45, 0.72, 1)
}

function label(position: Vector3, text: string, fontSize: number, color: Color4): Entity {
  const entity = engine.addEntity()
  Transform.create(entity, { position })
  TextShape.create(entity, {
    text,
    fontSize,
    textColor: color,
    textAlign: TextAlignMode.TAM_MIDDLE_CENTER,
    width: SCREEN_WIDTH * 4,
    height: 1
  })
  return entity
}

// The coloured slab behind each screen is the at-a-glance verdict: green where
// the case must play, red where the sandbox must refuse it. A red screen showing
// video is as much a failure as a green screen showing nothing.
function backdrop(position: Vector3, expected: Expectation): Entity {
  const entity = engine.addEntity()
  Transform.create(entity, {
    position,
    scale: Vector3.create(SCREEN_WIDTH + 0.35, SCREEN_HEIGHT + 0.35, 0.08)
  })
  MeshRenderer.setBox(entity)
  Material.setPbrMaterial(entity, { albedoColor: OUTCOME_COLOR[expected] })
  return entity
}

function screen(position: Vector3, url: string): Entity {
  const entity = engine.addEntity()
  Transform.create(entity, {
    position,
    scale: Vector3.create(SCREEN_WIDTH, SCREEN_HEIGHT, 1)
  })
  MeshRenderer.setPlane(entity)

  VideoPlayer.create(entity, { src: url, playing: true, loop: true, volume: 0 })
  Material.setBasicMaterial(entity, {
    texture: Material.Texture.Video({ videoPlayerEntity: entity })
  })

  return entity
}

function describe(testCase: Case): string {
  const codecs = [testCase.video, testCase.audio].filter((codec) => codec !== null).join(' + ')
  const gate = testCase.gate === null ? '' : `  gate: ${testCase.gate}`
  const editor =
    testCase.expectedEditor === testCase.expected ? '' : `  (Editor: ${testCase.expectedEditor})`
  return `${testCase.expected}${editor}\n${testCase.container ?? '-'}${codecs === '' ? '' : `  ${codecs}`}${gate}`
}

function placeCase(testCase: Case, index: number): void {
  const column = index % COLUMNS
  const row = Math.floor(index / COLUMNS)
  const x = GRID_LEFT_X + column * COLUMN_PITCH + SCREEN_WIDTH / 2
  const y = FIRST_ROW_Y + row * ROW_PITCH

  backdrop(Vector3.create(x, y, WALL_Z + 0.1), testCase.expected)
  screen(Vector3.create(x, y, WALL_Z), testCase.url)
  label(Vector3.create(x, y + SCREEN_HEIGHT / 2 + 0.45, WALL_Z - 0.05), testCase.id, 1.1, Color4.White())
  label(
    Vector3.create(x, y - SCREEN_HEIGHT / 2 - 0.6, WALL_Z - 0.05),
    describe(testCase),
    0.75,
    OUTCOME_COLOR[testCase.expected]
  )
}

function emptyState(): void {
  const entity = engine.addEntity()
  Transform.create(entity, { position: Vector3.create(16, 6, 16) })
  Billboard.create(entity, { billboardMode: BillboardMode.BM_Y })
  TextShape.create(entity, {
    text:
      'UUAV media sandbox\n\nNo cases were injected into this scene.\n' +
      'Run it through the harness:  nix run .#uuav-test',
    fontSize: 3,
    textColor: Color4.create(0.9, 0.55, 0.1, 1)
  })
}

export function main(): void {
  if (CASES.length === 0) {
    emptyState()
    return
  }

  const title = engine.addEntity()
  const rows = Math.ceil(CASES.length / COLUMNS)
  Transform.create(title, {
    position: Vector3.create(16, FIRST_ROW_Y + rows * ROW_PITCH + 0.6, WALL_Z)
  })
  TextShape.create(title, {
    text: `UUAV media sandbox - ${CASES.length} cases from ${BASE_URL}\ngreen must play, red must be refused, blue depends on the plugin's FFmpeg build`,
    fontSize: 1.6,
    textColor: Color4.White(),
    textAlign: TextAlignMode.TAM_MIDDLE_CENTER,
    width: 40,
    height: 2
  })

  CASES.forEach(placeCase)
}

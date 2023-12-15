import { engine, Schemas } from '@dcl/sdk/ecs'
import { Color3, Color4 } from '@dcl/sdk/math'

export const colors: Color4[] = [
  { a: 1, ...Color3.fromHexString('#1dccc7') },
  { a: 1, ...Color3.fromHexString('#ffce00') },
  { a: 1, ...Color3.fromHexString('#9076ff') },
  { a: 1, ...Color3.fromHexString('#fe3e3e') },
  { a: 1, ...Color3.fromHexString('#3efe94') },
  { a: 1, ...Color3.fromHexString('#3d30ec') },
  { a: 1, ...Color3.fromHexString('#6699cc') }
]

export const Tile = engine.defineComponent('Title', {})

export const Beat = engine.defineComponent('Beat', {
  interval: Schemas.Number,
  timer: Schemas.Number
})

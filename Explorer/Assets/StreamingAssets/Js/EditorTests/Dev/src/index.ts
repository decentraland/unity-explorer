//to compile: npx webpack --config webpack.config.js

import { typeSuites } from './gen/apis.d-ti'
import { createCheckers } from "ts-interface-checker"

const checkerSuite = createCheckers(typeSuites())

export default checkerSuite

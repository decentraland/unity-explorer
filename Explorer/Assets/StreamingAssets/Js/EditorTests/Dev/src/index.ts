//to compile: npx webpack --config webpack.config.js

import ICheckerStorage from './checkers/checkerStorage'
import logCheckerStorage from './checkers/logCheckerStorage'
import wrapCheckerStorage from './checkers/wrapCheckerStorage'
import { typeSuites } from './gen/apis.d-ti'
import { createCheckers } from "ts-interface-checker"

const checkerSuite = createCheckers(typeSuites())

let cachedChecker: ICheckerStorage | undefined = undefined

export function checkerStorage(log: (message: string) => void): ICheckerStorage {
    if (cachedChecker === undefined) {
        cachedChecker = logCheckerStorage(
            wrapCheckerStorage(checkerSuite),
            log
        )
    }
    return cachedChecker
}

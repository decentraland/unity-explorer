//to compile: npx webpack --config webpack.config.js

import { newCheckerRegistrationWrap } from './registrationWraps/checkerRegistrationWrap'
import { Logger } from './loggers/logger'
import { newTestsRegistrationWrap } from './registrationWraps/testsRegistrationWrap'
import { newLazy } from './lazies/lazy'

const checkerRegistrationWrap = newLazy(newCheckerRegistrationWrap)
const testsRegistrationWrap = newLazy(newTestsRegistrationWrap)

//To be called from jsSide
export function registerBundle(
    mutableBundle: any,
    logger: Logger
) {
    checkerRegistrationWrap.value().register(mutableBundle, logger)
}

//TODO call on js side
//To be called from jsSide
export function registerIntegrationTests(
    mutableBundle: any,
    logger: Logger
) {
    testsRegistrationWrap.value().register(mutableBundle, logger)
}
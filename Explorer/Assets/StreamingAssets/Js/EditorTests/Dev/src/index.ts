//to compile: npx webpack --config webpack.config.js

import { newCheckerRegistrationWrap } from './registrationWraps/checkerRegistrationWrap'
import { Logger } from './loggers/logger'
import { newTestsRegistrationWrap } from './registrationWraps/testsRegistrationWrap'
import { newLazy } from './lazies/lazy'
import { newLogsRegistrationWrap } from './registrationWraps/logsRegistrationWrap'

const checkerRegistrationWrap = newLazy(newCheckerRegistrationWrap)
const testsRegistrationWrap = newLazy(newTestsRegistrationWrap)
const logsRegistrationWrap = newLazy(newLogsRegistrationWrap)

//To be called from jsSide
export function registerBundle(
    mutableBundle: any,
    logger: Logger
): void {
    checkerRegistrationWrap.value().register(mutableBundle, logger)
}

//TODO call on js side
//To be called from jsSide
export function registerIntegrationTests(
    mutableBundle: any,
    logger: Logger
): void {
    testsRegistrationWrap.value().register(mutableBundle, logger)
}

export function registerLogs(
    mutableBundle: any,
    logger: Logger
): void {
    logsRegistrationWrap.value().register(mutableBundle, logger)
}
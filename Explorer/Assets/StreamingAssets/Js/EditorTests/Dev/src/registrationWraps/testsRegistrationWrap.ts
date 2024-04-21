import { Logger } from "../loggers/logger";
import { newRegistrationWrap, RegistrationWrap, RegistrationWrapMethod } from "./registrationWrap";

export function newTestsRegistrationWrap(): RegistrationWrap {

    const registrationWrapMethod: RegistrationWrapMethod = (
        methodKey: string,
        originMethod: (message: any) => Promise<any>,
        logger: Logger
    ) => {
        logger.warning(`Test for ${methodKey} not implemented yet!`)
        return null

        return async (message: any) => {
            const result = await originMethod(message)

            //TODO test execution

            return result
        }
    }


    return newRegistrationWrap(
        registrationWrapMethod,
        new Set<string>()
    )
}
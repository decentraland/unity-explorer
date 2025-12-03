import { Logger } from "../loggers/logger";
import { ExecutionMethod, messageFromError, newRegistrationWrap, RegistrationWrap, RegistrationWrapMethod } from "./registrationWrap";

export function newLogsRegistrationWrap(): RegistrationWrap {

    const wrapMethod: RegistrationWrapMethod = (
        _: any,
        methodKey: string,
        originMethod: ExecutionMethod,
        logger: Logger
    ) => {
        return async (message: any): Promise<any> => {
            logger.log(`Js Request for method: ${methodKey} started`)
            try {
                const result = await originMethod(message)
                logger.log(`Js Request for method: ${methodKey} finished`)
                return result
            }
            catch (e: unknown) {
                logger.error(`Js Request for method: ${methodKey} failed: ${messageFromError(e)}`)
            }
            return {}
        }
    }

    return newRegistrationWrap(wrapMethod, new Set<string>())
}
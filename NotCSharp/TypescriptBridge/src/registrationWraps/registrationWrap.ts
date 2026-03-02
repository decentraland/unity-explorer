import { Logger } from "../loggers/logger"
import { RegisteredEntities } from "./entities/registeredEntities"

export interface RegistrationWrap {
    register(
        mutableBundle: any,
        logger: Logger
    ): void
}

export type ExecutionMethod = (message: any) => Promise<any>

export type RegistrationWrapMethod = (
    methodsBundle: any,
    methodKey: string,
    originMethod: ExecutionMethod,
    logger: Logger
) => ExecutionMethod | null

export function newRegistrationWrap(
    registrationWrapMethod: RegistrationWrapMethod,
    alreadyRegisteredEntities: RegisteredEntities
): RegistrationWrap {
    return {
        register: (mutableBundle: any, logger: Logger) => {
            for (const k in mutableBundle) {
                if (alreadyRegisteredEntities.has(k)) {
                    continue
                }

                logger.warning(`Registering ${k}`)
                const originMethod = mutableBundle[k] as ExecutionMethod
                const wrappedMethod = registrationWrapMethod(mutableBundle, k, originMethod, logger)
                if (wrappedMethod !== null) {
                    try {
                        mutableBundle[k] = wrappedMethod
                    } catch (e: unknown) {
                        logger.warning(`Failed to wrap method ${k}: ${messageFromError(e)}`)
                    }
                }
                alreadyRegisteredEntities.add(k)
            }
        }
    }
}

export function newFakeRegistrationWrap(): RegistrationWrap {
    return {
        register: (_: any, logger: Logger) => {
            logger.warning("Fake registration wrap is used")
        }
    }
}

export function messageFromError(error: unknown): string {
    if (typeof error === "string") {
        return error
    }
    if (error instanceof Error) {
        return error.message
    }
    return JSON.stringify(error)
}
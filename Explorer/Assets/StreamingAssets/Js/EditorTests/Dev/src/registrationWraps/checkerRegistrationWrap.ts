import wrapCheckerStorage from "../checkers/wrapCheckerStorage";
import { constNameCheckerTable, INameToCheckerTable } from "../table/nameCheckerTable";
import { messageFromError, newFakeRegistrationWrap, newRegistrationWrap, RegistrationWrap, RegistrationWrapMethod } from "./registrationWrap";
import { Checker, createCheckers, IErrorDetail } from "ts-interface-checker";
import { typeSuites } from '../gen/apis.d-ti'
import { Logger } from "../loggers/logger";

const nameToResponses: Map<string, string> = new Map([
    ["getActiveVideoStreams", "VideoTracksActiveStreamsResponse"],
    //EngineApi
    ["crdtSendToRenderer", "CrdtSendToResponse"],
    ["sendBatch", "SendBatchResponse"],
    ["crdtGetState", "CrdtGetStateResponse"],
    ["subscribe", "SubscribeResponse"],
    ["unsubscribe", "UnsubscribeResponse"],
    ["isServer", "IsServerResponse"],
    //Environment
    ["areUnsafeRequestAllowed", "AreUnsafeRequestAllowedResponse"],
    ["getBootstrapData", "BootstrapDataResponse"],
    ["getCurrentRealm", "GetCurrentRealmResponse"],
    ["getDecentralandTime", "GetDecentralandTimeResponse"],
    ["getExplorerConfiguration", "GetExplorerConfigurationResponse"],
    ["getPlatform", "GetPlatformResponse"],
    ["isPreviewMode", "PreviewModeResponse"],
    //Ethereum
    ["signMessage", "SignMessageResponse"],
    ["sendAsync", "SendAsyncResponse"],
    ["getUserAccount", "GetUserAccountResponse"],
    ["requirePayment", "RequirePaymentResponse"],
    ["convertMessageToObject", "ConvertMessageToObjectResponse"],
    //Player
    ["getPlayerData", "PlayersGetUserDataResponse"],
    ["getPlayersInScene", "PlayerListResponse"],
    ["getConnectedPlayers", "PlayerListResponse"],
    //Portable 
    ["spawn", "SpawnResponse"],
    ["kill", "KillResponse"],
    ["exit", "ExitResponse"],
    ["getPortableExperiencesLoaded", "GetPortableExperiencesLoadedResponse"],
    //RestrictedActions
    ["movePlayerTo", "MovePlayerToResponse"],
    ["teleportTo", "TeleportToResponse"],
    ["triggerEmote", "TriggerEmoteResponse"],
    ["changeRealm", "SuccessResponse"],
    ["openExternalUrl", "SuccessResponse"],
    ["openNftDialog", "SuccessResponse"],
    ["setCommunicationsAdapter", "SuccessResponse"],
    ["triggerSceneEmote", "SuccessResponse"],
    //Runtime
    ["getRealm", "GetRealmResponse"],
    ["getWorldTime", "GetWorldTimeResponse"],
    ["readFile", "ReadFileResponse"],
    ["getSceneInformation", "CurrentSceneEntityResponse"],
    //Scene
    ["getSceneInfo", "GetSceneResponse"],
    //SignedFetch
    ["signedFetch", "FlatFetchResponse"],
    ["getHeaders", "GetHeadersResponse"],
    //Fetching
    ["logTestResult", "TestResultResponse"],
    ["plan", "TestPlanResponse"],
    ["setCameraTransform", "SetCameraTransformTestCommand"],
    //UserActionModule
    ["requestTeleport", "RequestTeleportResponse"],
    //Identity
    ["getUserPublicKey", "GetUserPublicKeyResponse"],
    ["getUserData", "GetUserDataResponse"],
]);

function table(): INameToCheckerTable {
    const checkerSuite = createCheckers(typeSuites())
    const storage = wrapCheckerStorage(checkerSuite)
    const map = new Map<string, Checker>()
    nameToResponses.forEach((value, key) => map.set(key, storage.checker(value)))
    return constNameCheckerTable(map)
}

export function newCheckerRegistrationWrap(): RegistrationWrap {
    const nameTable = table()

    const wrapMethod: RegistrationWrapMethod = (
        methodKey: string,
        originMethod: (message: any) => Promise<any>,
        logger: Logger
    ) => {
        const checker = nameTable.checker(methodKey)
        if (checker === undefined) {
            logger.warning(`Checker for ${methodKey} not found`)
            return null
        }

        return async (message: any) => {
            const result = await originMethod(message)
            if (checker.test(result) === false) {
                const report = reportString(checker, methodKey, result)
                logger.error(report)
                try {
                    checker.check(result)
                }
                catch (e: unknown) {
                    throw new Error(`Cannot get result from ${methodKey}: ${messageFromError(e)}`)
                }

            }
            return result
        }
    }

    return newRegistrationWrap(
        wrapMethod,
        new Set<string>()
    )
}

export function reportString(checker: Checker, methodName: string, result: any): string {
    const jsonifyResult = JSON.stringify(result)
    const errors: IErrorDetail[] = checker.strictValidate(result) as IErrorDetail[]
    let errorMessage = `Errors on ${methodName}:\n`
    errors.forEach((value) => {
        errorMessage += `message: ${value.message} nested: ${value.nested} in path: ${value.path}\n`
    })
    errorMessage += `Value from ${methodName} is not valid: ${jsonifyResult}`
    return errorMessage
}
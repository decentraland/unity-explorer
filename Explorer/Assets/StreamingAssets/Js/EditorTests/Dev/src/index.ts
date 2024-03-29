//to compile: npx webpack --config webpack.config.js

import ICheckerStorage from './checkers/checkerStorage'
import wrapCheckerStorage from './checkers/wrapCheckerStorage'
import { typeSuites } from './gen/apis.d-ti'
import { Checker, createCheckers, IErrorDetail } from "ts-interface-checker"
import { constNameCheckerTable, INameToCheckerTable } from './table/nameCheckerTable'

const checkerSuite = createCheckers(typeSuites())

let cachedChecker: ICheckerStorage | undefined = undefined

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

function checkerStorage(): ICheckerStorage {
    if (cachedChecker === undefined) {
        cachedChecker = wrapCheckerStorage(checkerSuite)
    }
    return cachedChecker
}

const alreadyRegistered: Set<string> = new Set();

function table(): INameToCheckerTable {
    const storage = checkerStorage()
    const map = new Map<string, Checker>()
    nameToResponses.forEach((value, key) => map.set(key, storage.checker(value)))
    return constNameCheckerTable(map)
}

function reportString(checker: Checker, methodName: string, result: any): string {
    const jsonifyResult = JSON.stringify(result)
    const errors: IErrorDetail[] = checker.strictValidate(result) as IErrorDetail[]
    let errorMessage = `Errors on ${methodName}:\n`
    errors.forEach((value) => {
        errorMessage += `message: ${value.message} nested: ${value.nested} in path: ${value.path}\n`
    })
    errorMessage += `Value from ${methodName} is not valid: ${jsonifyResult}`
    return errorMessage
}

//To be called from jsSide
export function registerBundle(
    mutableBundle: any,
    logWarning: (message: string) => void,
    logError: (message: string) => void
) {
    const nameTable = table()
    for (const k in mutableBundle) {
        if (alreadyRegistered.has(k)) {
            continue
        }

        const checker = nameTable.checker(k)
        if (checker === undefined) {
            logWarning(`Checker for ${k} not found`)
            continue;
        }
        const method = mutableBundle[k] as (message: any) => Promise<any>
        mutableBundle[k] = async (message: any) => {
            const result = await method(message)
            if (checker.strictTest(result) === false) {
                const report = reportString(checker, k, result)
                logError(report)
                checker.strictCheck(result)
            }
            return result
        }
        alreadyRegistered.add(k)
    }
}
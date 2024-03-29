//to compile: npx webpack --config webpack.config.js

import ICheckerStorage from './checkers/checkerStorage'
import logCheckerStorage from './checkers/logCheckerStorage'
import wrapCheckerStorage from './checkers/wrapCheckerStorage'
import { typeSuites } from './gen/apis.d-ti'
import { Checker, createCheckers } from "ts-interface-checker"
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

function checkerStorage(log: (message: string) => void): ICheckerStorage {
    if (cachedChecker === undefined) {
        cachedChecker = logCheckerStorage(
            wrapCheckerStorage(checkerSuite),
            log
        )
    }
    return cachedChecker
}

function table(log: (message: string) => void): INameToCheckerTable {
    const storage = checkerStorage(log)
    const map = new Map<string, Checker>()
    nameToResponses.forEach((value, key) => map.set(key, storage.checker(value)))
    return constNameCheckerTable(map)
}

//To be called from jsSide
export function registerBundle(mutableBundle: any, log: (message: string) => void) {
    const nameTable = table(log)
    for (const k in mutableBundle) {
        const checker = nameTable.checker(k)
        if (checker === undefined) {
            log(`Checker for ${k} not found`)
            continue;
        }
        const method = mutableBundle[k] as (message: any) => Promise<any>
        mutableBundle[k] = async (message: any) => {
            const result = await method(message)
            checker.strictCheck(result)
            return result
        }
    }
}
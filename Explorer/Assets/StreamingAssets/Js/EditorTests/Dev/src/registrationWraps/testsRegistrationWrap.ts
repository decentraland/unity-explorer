import { Logger } from "../loggers/logger";
import { crdtGetStateTest, crdtSendToRendererTest, isServerTest, sendBatchTest, subscribeTest, unsubscribeTest } from "../tests/Integrations/engineApiTests";
import { areUnsafeRequestAllowedTest, getBootstrapDataTest, getCurrentRealmTest, getDecentralandTimeTest, getExplorerConfigurationTest, getPlatformTest, isPreviewModeTest } from "../tests/Integrations/environmentTests";
import { convertMessageToObjectTest, getUserAccountTest, requirePaymentTest, sendAsyncTest, signMessageTest } from "../tests/Integrations/ethereumTests";
import { logTestResultTest, planTest, setCameraTransformTest } from "../tests/Integrations/fetchingTests";
import { getActiveVideoStreamsTest } from "../tests/Integrations/getActiveVideoStreamsTest";
import { getUserPublicKeyTest, getUserDataTest } from "../tests/Integrations/identityTests";
import IntegrationTestsSource from "../tests/Integrations/source/integrationTestsSource";
import { getPlayerDataTest, getPlayersInSceneTest, getConnectedPlayersTest } from "../tests/Integrations/playerTests";
import { exitTest, getPortableExperiencesLoadedTest, killTest, spawnTest } from "../tests/Integrations/portableTests";
import { movePlayerToTest, teleportToTest, triggerEmoteTest, changeRealmTest, openExternalUrlTest, openNftDialogTest, setCommunicationsAdapterTest, triggerSceneEmoteTest } from "../tests/Integrations/restrictedActionsTests";
import { getSceneInfoTest } from "../tests/Integrations/sceneTests";
import { getHeadersTest, signedFetchTest } from "../tests/Integrations/signedFetchTests";
import { requestTeleportTest } from "../tests/Integrations/userModuleTests";
import { getRealmTest, getWorldTimeTest, readFileTest, getSceneInformationTest } from "../tests/Integrations/runtimeTests";
import { newFakeRegisteredEntities } from "./entities/registeredEntities";
import { messageFromError, newRegistrationWrap, RegistrationWrap, RegistrationWrapMethod } from "./registrationWrap";

const testsSource: IntegrationTestsSource = new Map([
    ["getActiveVideoStreams", getActiveVideoStreamsTest],
    //EngineApi
    ["crdtSendToRenderer", crdtSendToRendererTest],
    ["sendBatch", sendBatchTest],
    ["crdtGetState", crdtGetStateTest],
    ["subscribe", subscribeTest],
    ["unsubscribe", unsubscribeTest],
    ["isServer", isServerTest],
    //Environment
    ["areUnsafeRequestAllowed", areUnsafeRequestAllowedTest],
    ["getBootstrapData", getBootstrapDataTest],
    ["getCurrentRealm", getCurrentRealmTest],
    ["getDecentralandTime", getDecentralandTimeTest],
    ["getExplorerConfiguration", getExplorerConfigurationTest],
    ["getPlatform", getPlatformTest],
    ["isPreviewMode", isPreviewModeTest],
    //Ethereum
    ["signMessage", signMessageTest],
    ["sendAsync", sendAsyncTest],
    ["getUserAccount", getUserAccountTest],
    ["requirePayment", requirePaymentTest],
    ["convertMessageToObject", convertMessageToObjectTest],
    //Player
    ["getPlayerData", getPlayerDataTest],
    ["getPlayersInScene", getPlayersInSceneTest],
    ["getConnectedPlayers", getConnectedPlayersTest],
    //Portable 
    ["spawn", spawnTest],
    ["kill", killTest],
    ["exit", exitTest],
    ["getPortableExperiencesLoaded", getPortableExperiencesLoadedTest],
    //RestrictedActions
    ["movePlayerTo", movePlayerToTest],
    ["teleportTo", teleportToTest],
    ["triggerEmote", triggerEmoteTest],
    ["changeRealm", changeRealmTest],
    ["openExternalUrl", openExternalUrlTest],
    ["openNftDialog", openNftDialogTest],
    ["setCommunicationsAdapter", setCommunicationsAdapterTest],
    ["triggerSceneEmote", triggerSceneEmoteTest],
    //Runtime
    ["getRealm", getRealmTest],
    ["getWorldTime", getWorldTimeTest],
    ["readFile", readFileTest],
    ["getSceneInformation", getSceneInformationTest],
    //Scene
    ["getSceneInfo", getSceneInfoTest],
    //SignedFetch
    ["signedFetch", signedFetchTest],
    ["getHeaders", getHeadersTest],
    //Fetching
    ["logTestResult", logTestResultTest],
    ["plan", planTest],
    ["setCameraTransform", setCameraTransformTest],
    //UserActionModule
    ["requestTeleport", requestTeleportTest],
    //Identity
    ["getUserPublicKey", getUserPublicKeyTest],
    ["getUserData", getUserDataTest],
])

export function newTestsRegistrationWrap(): RegistrationWrap {

    const registrationWrapMethod: RegistrationWrapMethod = (
        methodsBundle: any,
        methodKey: string,
        originMethod: (message: any) => Promise<any>,
        logger: Logger
    ) => {

        const testMethod = testsSource.get(methodKey)
        if (testMethod === undefined) {
            logger.warning(`Test for ${methodKey} not found!`)
            return null
        }

        return async (message: any) => {
            const result = await originMethod(message)
            try {
                await testMethod(
                    {
                        result: result,
                        methodsBundle: methodsBundle
                    }
                )
            }
            catch (error) {
                logger.error(`Test for js api ${methodKey} failed: ${messageFromError(error)}`)
            }

            return result
        }
    }

    return newRegistrationWrap(
        registrationWrapMethod,
        newFakeRegisteredEntities()
    )
}

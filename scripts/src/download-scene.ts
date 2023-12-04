import {writeFileSync} from "fs";
import {ensureDirSync} from "fs-extra";
import path from "path";
import axios from "axios";

interface SceneDefinition {
    content: [{
        file: string,
        hash: string
    }];
}

async function main() {
    const args = process.argv;
    const baseUrl = args[2];
    const sceneId = args[3];
    const savingPath = args[4];

    await downloadAndSaveSceneFiles(baseUrl, sceneId, savingPath);
}

async function downloadAndSaveSceneFiles(baseUrl: string, sceneId: string, savingPath: string) {
    const sceneDefinitionResponse = await axios(`${baseUrl}/${sceneId}`, {
        method: 'get',
        responseType: 'json'
    })

    if (sceneDefinitionResponse.status != 200) {
        throw Error(`Error fetching scene: ${sceneDefinitionResponse.status} ${sceneDefinitionResponse.statusText}`)
    }

    const sceneJson = await sceneDefinitionResponse.data;
    const sceneDefinition = sceneJson as SceneDefinition;
    const sceneDirectory = path.join(savingPath, sceneId);

    for (const content of sceneDefinition.content) {
        const contentResponse = await axios(`${baseUrl}/${content.hash}`, {
            method: 'get',
            responseType: 'arraybuffer'
        });
        const contentPath = path.join(sceneDirectory, content.file);
        const contentDirectory = path.dirname(contentPath);

        ensureDirSync(contentDirectory);
        writeFileSync(contentPath, Buffer.from(contentResponse.data));
    }
}

main().catch(reason => console.error(reason));
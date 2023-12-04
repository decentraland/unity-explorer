import {readFileSync, writeFileSync} from "fs";
import {SourceMapConsumer, SourceMapGenerator} from "source-map";

async function main() {
    const args = process.argv;
    const minifiedCodePath = args[2];
    const sourceMapPath = args[3];
    const reconstructedFilePath = args[4];

    const minifiedCode = readFileSync(minifiedCodePath, 'utf8');
    const sourceMapContent = readFileSync(sourceMapPath, 'utf8');
    const reconstructedCode = await reconstructOriginalCode(minifiedCode, sourceMapContent);
    
    if (reconstructedCode == undefined) {
        throw Error('Could not reconstruct code');
    }
    
    writeFileSync(reconstructedFilePath, reconstructedCode, 'utf8');
}

async function reconstructOriginalCode(minifiedCode: string, sourceMapContent: string) {
    const sourceMap = JSON.parse(sourceMapContent);
    const consumer = await new SourceMapConsumer(sourceMap);
    const generator = SourceMapGenerator.fromSourceMap(consumer);
    
    const minifiedLines = minifiedCode.split('\n');
    
    // Add mappings from the minified code
    for (let i = 0; i < minifiedLines.length; i++) {
        const line = minifiedLines[i];
        const originalPosition = consumer.originalPositionFor({
            line: i + 1,  // Source maps are 1-based
            column: line.length,  // Assuming the column is at the end of the line
        });

        if (originalPosition.source == null
            || originalPosition.line == null
            || originalPosition.name == null) {
            continue;
        }

        generator.addMapping({
            generated: {line: i + 1, column: line.length},
            original: {line: originalPosition.line, column: originalPosition.column || 0},
            source: originalPosition.source,
            name: originalPosition.name
        });
    }

    return generator.toJSON().sourcesContent?.join('\n');
}

main().catch(reason => console.error(reason));
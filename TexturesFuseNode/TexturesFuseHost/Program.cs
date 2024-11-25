using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using TexturesFuseComms.Config;

const int mb = 1024 * 1024;

using var mmfInput = MemoryMappedFile.CreateNew(IConfig.InputMapFileAddress, mb * 16);
using var mmfOutput = MemoryMappedFile.CreateNew(IConfig.OutputMapFileAddress, mb * 4);
await using var pipe = new NamedPipeServerStream(IConfig.PipeName, PipeDirection.InOut);


await pipe.WaitForConnectionAsync(CancellationToken.None);

using var reader = new StreamReader(pipe);

await using var writer = new StreamWriter(pipe);
writer.AutoFlush = true;

writer.WriteLine("Hello from host!!!");
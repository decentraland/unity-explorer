using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using TexturesFuseComms.Config;

#pragma warning disable CA1416

//Windows only

using var mmfInput = MemoryMappedFile.OpenExisting(IConfig.InputMapFileAddress, MemoryMappedFileRights.Read);
using var mmfOutput = MemoryMappedFile.OpenExisting(IConfig.OutputMapFileAddress, MemoryMappedFileRights.Write);
await using var pipe = new NamedPipeClientStream(".", IConfig.PipeName, PipeDirection.InOut);

await pipe.ConnectAsync(CancellationToken.None);

using var reader = new StreamReader(pipe);

{
    var message = await reader.ReadLineAsync(CancellationToken.None);
    if (message != null)
        Console.WriteLine($"Message received: {message}");
}
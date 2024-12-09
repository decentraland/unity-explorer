using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    internal class NodeTexturesFuse : ITexturesFuse
    {
        private const int MB = 1024 * 1024;

        private const int MMF_INPUT_CAPACITY = MB * 64;
        private const int MMF_OUTPUT_CAPACITY = MB * 64;

        private const int MEMORY_LIMIT = MB * 1024; //GB

        public const string CHILD_PROCESS = "node.exe";

        /// <summary>
        /// Avoid collisions between editor and client
        /// </summary>
#if UNITY_EDITOR
        private const string ENVIRONMENT_POSTFIX = "editor";
#else
        private const string ENVIRONMENT_POSTFIX = "client";
#endif

        private const string MMF_INPUT = "dcl_fuse_i_" + ENVIRONMENT_POSTFIX;
        private const string MMF_OUTPUT = "dcl_fuse_o_" + ENVIRONMENT_POSTFIX;
        private const string NAMED_PIPE = "dcl_fuse_p_" + ENVIRONMENT_POSTFIX;

        private static MemoryMappedFile? mmfInput;
        private static MemoryMappedFile? mmfOutput;

        private static readonly SemaphoreSlim SEMAPHORE_SLIM = new (1, 1);

        private readonly MemoryMappedViewStream inputFileStream;
        private readonly MemoryMappedViewStream outputFileStream;

        private readonly InputArgs inputArgs;

        private NamedPipeServerStream? pipe;
        private BinaryWriter? pipeWriter;
        private BinaryReader? pipeReader;

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct InputArgs
        {
            public int bytesLength;
            public int maxSideLength;
            public NativeMethods.CMP_FORMAT format;
            public float fQuality;
            public NativeMethods.CMP_Compute_type encodeWith;
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct OutputResult
        {
            public NativeMethods.ImageResult code;
            public int outputLength;
            public uint width;
            public uint height;
        }

        public NodeTexturesFuse(NativeMethods.CMP_Compute_type computeType = NativeMethods.CMP_Compute_type.CMP_GPU_DXC) : this(
            new InputArgs
            {
                encodeWith = computeType
            }
        ) { }

        public NodeTexturesFuse(InputArgs inputArgs)
        {
            mmfInput ??= MemoryMappedFile.CreateNew(MMF_INPUT, MMF_INPUT_CAPACITY);
            mmfOutput ??= MemoryMappedFile.CreateNew(MMF_OUTPUT, MMF_OUTPUT_CAPACITY);

            inputFileStream = mmfInput.CreateViewStream(0, MMF_INPUT_CAPACITY);
            outputFileStream = mmfOutput.CreateViewStream(0, MMF_OUTPUT_CAPACITY);

            this.inputArgs = inputArgs;

            StopProcess();
        }

        public void Dispose()
        {
            inputFileStream.Dispose();
            outputFileStream.Dispose();
            pipe?.Dispose();
            pipeWriter?.Dispose();
            pipeReader?.Dispose();

            StopProcess();
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            await SEMAPHORE_SLIM.WaitAsync(token);
            await UniTask.SwitchToThreadPool();

            using var scope = new ReleaseScope();

            await EnsureProcessLaunchedAsync(token);

            WriteToInputStream(bytes, bytesLength);
            await inputFileStream.FlushAsync(token)!;

            var args = inputArgs;
            args.bytesLength = bytesLength;
            args.format = type.AsBC_Format();

            var writeResult = Write(pipeWriter!, args);

            if (writeResult.Success == false)
                return EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.ErrorResult(NativeMethods.ImageResult.ErrorUnknown, writeResult.ErrorMessage!);

            var outputResult = OutputResultFromStream(pipeReader!);

            if (outputResult.code != NativeMethods.ImageResult.Success)
                return EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.ErrorResult(outputResult.code, "Cannot read output message");

            await UniTask.SwitchToMainThread();

            var t = await ManagedOwnedTexture2D.NewTextureFromStreamAsync(
                outputFileStream,
                type.AsBC_TextureFormat(),
                outputResult.outputLength,
                (int)outputResult.width,
                (int)outputResult.height,
                token
            );

            return EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.SuccessResult(t);
        }

        private void WriteToInputStream(IntPtr bytes, int bytesLength)
        {
            unsafe
            {
                inputFileStream.Seek(0, SeekOrigin.Begin);
                inputFileStream.Write(new Span<byte>(bytes.ToPointer()!, bytesLength));
            }
        }

        private static Result Write(BinaryWriter writer, InputArgs inputArgs)
        {
            try
            {
                ReadOnlySpan<byte> span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref inputArgs, 1));
                writer.Write(span);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                StopProcess();
                return Result.ErrorResult($"Cannot write data to named pipe: {e.Message}");
            }
        }

        private static OutputResult OutputResultFromStream(BinaryReader reader)
        {
            unsafe
            {
                Span<byte> buffer = stackalloc byte[sizeof(OutputResult)];
                int result = reader.Read(buffer);
                ReportHub.Log(ReportCategory.TEXTURES, $"Read {result}");

                return MemoryMarshal.Read<OutputResult>(buffer);
            }
        }

        private async UniTask EnsureProcessLaunchedAsync(CancellationToken token)
        {
            bool isMemoryOverwhelmed = IsMemoryOverwhelmed();

            if (isMemoryOverwhelmed)
                StopProcess();

            if (IsProcessRunning() == false || isMemoryOverwhelmed || pipe!.IsConnected == false)
            {
                if (pipe != null)
                {
                    await pipe.DisposeAsync();
                    pipe = null;
                }

                pipeReader?.Dispose();
                pipeReader = null;

                if (pipeWriter != null)
                {
                    await pipeWriter.DisposeAsync();
                    pipeWriter = null;
                }

                pipe = new NamedPipeServerStream(NAMED_PIPE, PipeDirection.InOut);
                pipeReader = new BinaryReader(pipe);
                pipeWriter = new BinaryWriter(pipe);

                var result = StartProcess();

                if (result != 0)
                {
                    ReportHub.LogWarning(ReportCategory.TEXTURES, $"ProcessesHubStart Cannot launch process: {CHILD_PROCESS} with result code: {result}, try again with force terminate");
                    StopProcess();

                    result = StartProcess();

                    if (result != 0)
                        ReportHub.LogError(ReportCategory.TEXTURES, $"ProcessesHubStart Cannot launch process with force terminate: {CHILD_PROCESS} with result code: {result}");
                }

                await pipe.WaitForConnectionAsync(token)!;
            }
        }

        private struct ReleaseScope : IDisposable
        {
            public void Dispose()
            {
                SEMAPHORE_SLIM.Release();
            }
        }

        private static bool IsProcessRunning()
        {
            bool r = NativeMethodsProcessesHub.ProcessesHubIsRunning();

            ReportHub.Log(
                ReportCategory.TEXTURES,
                $"NodeTexturesFuse process is running: {r}"
            );

            return r;
        }

        private static PH_Error StartProcess()
        {
            var r = NativeMethodsProcessesHub.ProcessesHubStart(CHILD_PROCESS);

            ReportHub.Log(
                ReportCategory.TEXTURES,
                $"NodeTexturesFuse start process '{CHILD_PROCESS}' with result: {r}"
            );

            return r;
        }

        private static void StopProcess()
        {
            var r = NativeMethodsProcessesHub.ProcessesHubStop();

            ReportHub.Log(
                ReportCategory.TEXTURES,
                $"NodeTexturesFuse stop process with result: {r}"
            );
        }

        private static bool IsMemoryOverwhelmed()
        {
            ulong usedRAM = NativeMethodsProcessesHub.ProcessesUsedRAM();

            ReportHub.Log(
                ReportCategory.TEXTURES,
                $"NodeTexturesFuse used memory by node process: {(double)usedRAM / MB} MB"
            );

            if (usedRAM == 0)
                return false;

            return usedRAM > MEMORY_LIMIT;
        }
    }
}

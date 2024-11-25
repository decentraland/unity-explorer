using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class NodeTexturesFuse : ITexturesFuse
    {
        private const int MB = 1024 * 1024;

        private const int MMF_INPUT_CAPACITY = MB * 16;
        private const int MMF_OUTPUT_CAPACITY = MB * 4;

        private readonly MemoryMappedFile mmfInput;
        private readonly MemoryMappedFile mmfOutput;
        private readonly NamedPipeServerStream pipe;
        private readonly BinaryWriter writer;
        private readonly BinaryReader reader;

        private readonly MemoryMappedViewStream inputFileStream;
        private readonly MemoryMappedViewStream outputFileStream;

        private readonly InputArgs inputArgs;

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

        public NodeTexturesFuse(InputArgs inputArgs)
        {
            mmfInput = MemoryMappedFile.CreateNew("dcl_fuse_i", MMF_INPUT_CAPACITY);
            mmfOutput = MemoryMappedFile.CreateNew("dcl_fuse_o", MMF_OUTPUT_CAPACITY);
            pipe = new NamedPipeServerStream("dcl_fuse_p", PipeDirection.InOut);
            reader = new BinaryReader(pipe);
            writer = new BinaryWriter(pipe);

            inputFileStream = mmfInput.CreateViewStream(0, MMF_INPUT_CAPACITY);
            outputFileStream = mmfOutput.CreateViewStream(0, MMF_OUTPUT_CAPACITY);

            this.inputArgs = inputArgs;
        }

        public void Dispose()
        {
            mmfInput.Dispose();
            mmfOutput.Dispose();
            pipe.Dispose();
            writer.Dispose();
            reader.Dispose();
            inputFileStream.Dispose();
            outputFileStream.Dispose();
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            if (pipe.IsConnected == false)
                await pipe.WaitForConnectionAsync(token)!;

            WriteToInputStream(bytes, bytesLength);
            await inputFileStream.FlushAsync(token)!;

            var args = inputArgs;
            args.bytesLength = bytesLength;
            args.format = type is TextureType.Albedo ? NativeMethods.CMP_FORMAT.CMP_FORMAT_BC7 : NativeMethods.CMP_FORMAT.CMP_FORMAT_BC5;
            Write(writer, args);

            var outputResult = OutputResultFromStream(reader);

            if (outputResult.code != NativeMethods.ImageResult.Success)
                return EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.ErrorResult(outputResult.code, string.Empty);

            var t = await ManagedOwnedTexture2D.NewTextureFromStreamAsync(
                outputFileStream,
                type is TextureType.Albedo ? TextureFormat.BC7 : TextureFormat.BC5,
                outputResult.outputLength,
                (int)outputResult.width,
                (int)outputResult.height,
                token
            );

            return EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.SuccessResult(t);
        }

        private void WriteToInputStream(IntPtr bytes, int bytesLength)
        {
            unsafe { inputFileStream.Write(new Span<byte>(bytes.ToPointer()!, bytesLength)); }
        }

        private static void Write(BinaryWriter writer, InputArgs inputArgs)
        {
            ReadOnlySpan<byte> span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref inputArgs, 1));
            writer.Write(span);
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
    }
}

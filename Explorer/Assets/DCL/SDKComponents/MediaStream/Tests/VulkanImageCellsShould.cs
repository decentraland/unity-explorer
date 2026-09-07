using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UUAV;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Unity's Vulkan backend dereferences CreateExternalTexture's nativeTex as a VkImage*, every
    ///     other backend takes the handle by value. The Vulkan cell has to stay readable until the
    ///     render thread has consumed the queued create and then be freed exactly once.
    /// </summary>
    public class VulkanImageCellsShould
    {
        private const int FRAME = 100;
        private static readonly IntPtr HANDLE = new (0x7f3a12345678L);
        private static readonly IntPtr OTHER_HANDLE = new (0x7f3a1234abcdL);

        private List<(int frame, IntPtr cell)> cells = null!;
        private List<(int frame, IntPtr cell)> retired = null!;

        [SetUp]
        public void SetUp()
        {
            cells = new List<(int frame, IntPtr cell)>();
            retired = new List<(int frame, IntPtr cell)>();
        }

        [TearDown]
        public void TearDown()
        {
            VulkanImageCells.FreePastLag(cells, int.MaxValue);
            VulkanImageCells.FreePastLag(retired, int.MaxValue);
        }

        [Test]
        public void PassNonVulkanHandlesByValue(
            [Values(GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLCore)] GraphicsDeviceType api)
        {
            Assert.That(VulkanImageCells.HandleNeedsCell(api), Is.False);

            IntPtr argument = VulkanImageCells.NativeTextureArgument(api, HANDLE, FRAME, cells);

            Assert.That(argument, Is.EqualTo(HANDLE));
            Assert.That(cells, Is.Empty, "a by-value handle must not allocate a cell");
        }

        [Test]
        public void PassVulkanHandlesThroughACellHoldingThem()
        {
            Assert.That(VulkanImageCells.HandleNeedsCell(GraphicsDeviceType.Vulkan), Is.True);

            IntPtr argument = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME, cells);

            Assert.That(argument, Is.Not.EqualTo(HANDLE));
            Assert.That(argument, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(Marshal.ReadIntPtr(argument), Is.EqualTo(HANDLE), "the cell must hold the VkImage handle");
            Assert.That(cells, Is.EqualTo(new[] { (FRAME, argument) }));
        }

        [Test]
        public void GiveEveryVulkanCreateAFreshCell()
        {
            IntPtr first = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME, cells);
            IntPtr second = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, OTHER_HANDLE, FRAME + 1, cells);

            Assert.That(second, Is.Not.EqualTo(first), "a cell is never rewritten while the render thread may still read it");
            Assert.That(Marshal.ReadIntPtr(first), Is.EqualTo(HANDLE));
            Assert.That(Marshal.ReadIntPtr(second), Is.EqualTo(OTHER_HANDLE));
            Assert.That(cells, Has.Count.EqualTo(2));
        }

        [Test]
        public void KeepACellReadableThroughTheRenderThreadLag()
        {
            IntPtr cell = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME, cells);

            for (int frame = FRAME; frame <= FRAME + VulkanImageCells.RenderThreadLagFrames; frame++)
            {
                VulkanImageCells.FreePastLag(cells, frame);
                Assert.That(cells, Is.EqualTo(new[] { (FRAME, cell) }), $"the cell was freed at frame {frame}, inside the lag");
                Assert.That(Marshal.ReadIntPtr(cell), Is.EqualTo(HANDLE));
            }

            VulkanImageCells.FreePastLag(cells, FRAME + VulkanImageCells.RenderThreadLagFrames + 1);
            Assert.That(cells, Is.Empty, "the cell must be freed on the first sweep past the lag");
        }

        [Test]
        public void FreeEachCellExactlyOnce()
        {
            IntPtr oldest = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME, cells);
            IntPtr middle = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME + 1, cells);
            IntPtr newest = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME + 2, cells);

            int sweepFrame = FRAME + 2 + VulkanImageCells.RenderThreadLagFrames;
            VulkanImageCells.FreePastLag(cells, sweepFrame);
            Assert.That(cells, Is.EqualTo(new[] { (FRAME + 2, newest) }), "only the cells past the lag are freed");

            // a freed cell has left the list, so a second sweep at the same frame cannot free it again
            VulkanImageCells.FreePastLag(cells, sweepFrame);
            Assert.That(cells, Is.EqualTo(new[] { (FRAME + 2, newest) }));

            VulkanImageCells.FreePastLag(cells, sweepFrame + 1);
            Assert.That(cells, Is.Empty);

            Assert.That(() => VulkanImageCells.FreePastLag(cells, sweepFrame + 2), Throws.Nothing);
            Assert.That(cells, Is.Empty);
            Assert.That(oldest, Is.Not.EqualTo(middle), "distinct creates took distinct cells");
        }

        [Test]
        public void RetireCellsToTheSharedListWithoutLosingOrDuplicatingAny()
        {
            IntPtr first = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, HANDLE, FRAME, cells);
            IntPtr second = VulkanImageCells.NativeTextureArgument(GraphicsDeviceType.Vulkan, OTHER_HANDLE, FRAME + 1, cells);

            VulkanImageCells.Retire(cells, retired);

            Assert.That(cells, Is.Empty, "the destroyed player keeps no cell");
            Assert.That(retired, Is.EqualTo(new[] { (FRAME, first), (FRAME + 1, second) }));
            Assert.That(Marshal.ReadIntPtr(first), Is.EqualTo(HANDLE), "retiring must not free the cell");

            VulkanImageCells.Retire(cells, retired);
            Assert.That(retired, Has.Count.EqualTo(2), "retiring an emptied list adds nothing");

            VulkanImageCells.FreePastLag(retired, FRAME + 1 + VulkanImageCells.RenderThreadLagFrames + 1);
            Assert.That(retired, Is.Empty, "retired cells are freed once past the lag, from the shared list");
        }

        [Test]
        public void KeepAMarginOverUnitysOneFrameRenderThreadBound()
        {
            // the main thread runs at most one frame ahead of the render thread; the lag must be
            // wider than that bound so a stalled render thread never dereferences a freed cell
            Assert.That(VulkanImageCells.RenderThreadLagFrames, Is.GreaterThanOrEqualTo(2));
        }
    }
}

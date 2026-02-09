using Xunit;
using System.Runtime.InteropServices;
using RkDevelopTool.Core;
using RkDevelopTool.Models;

namespace RkDevelopTool.Tests
{
    public class StructTests
    {
        [Fact]
        public void TestRKImageHeadSize()
        {
            int size = Marshal.SizeOf<RkImageHead>();
            // 4+2+4+4 + 7(RKTIME) + 4 + 4+4+4+4 + 61 = 102
            Assert.Equal(102, size);
        }

        [Fact]
        public void TestRKTimeSize()
        {
            int size = Marshal.SizeOf<RkTime>();
            // ushort(2) + 5*byte(5) = 7
            Assert.Equal(7, size);
        }
        
        [Fact]
        public void TestCBWSize()
        {
            int size = Marshal.SizeOf<Cbw>();
            // 4+4+4+1+1+1 + (1+1+4+1+2+7) = 31
            Assert.Equal(31, size);
        }

        [Fact]
        public void TestCBW_Create()
        {
            var cbw = Cbw.Create();
            Assert.Equal(0u, cbw.Signature);
            Assert.NotNull(cbw.Cbwcb.Reserved3);
            Assert.Equal(7, cbw.Cbwcb.Reserved3.Length);
        }

        [Fact]
        public void TestFlashInfoSize()
        {
            int size = Marshal.SizeOf<FlashInfo>();
            // 16 + 4 + 2 + 4 + 4 + 50 + 4 + 1 + 1 + 1 + 2 + 2 + 4 = 95
            Assert.Equal(95, size);
        }

        [Fact]
        public void TestSparseHeaderSize()
        {
            int size = Marshal.SizeOf<SparseHeader>();
            // 4+2+2+2+2+4+4+4+4 = 28
            Assert.Equal(28, size);
        }
    }
}

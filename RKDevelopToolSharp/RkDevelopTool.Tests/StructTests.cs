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
            int size = Marshal.SizeOf<STRUCT_RKIMAGE_HEAD>();
            // 4+2+4+4 + 7(RKTIME) + 4 + 4+4+4+4 + 61 = 102
            Assert.Equal(102, size);
        }

        [Fact]
        public void TestRKTimeSize()
        {
            int size = Marshal.SizeOf<STRUCT_RKTIME>();
            // ushort(2) + 5*byte(5) = 7
            Assert.Equal(7, size);
        }
        
        [Fact]
        public void TestCBWSize()
        {
            int size = Marshal.SizeOf<CBW>();
            // 4+4+4+1+1+1 + (1+1+4+1+2+7) = 31
            Assert.Equal(31, size);
        }
    }
}

using Xunit;
using RkDevelopTool.Utils;

namespace RkDevelopTool.Tests
{
    public class EndianTests
    {
        [Fact]
        public void TestSwap16()
        {
            ushort val = 0x1234;
            ushort expected = 0x3412;
            Assert.Equal(expected, EndianUtils.Swap16(val));
        }

        [Fact]
        public void TestSwap32()
        {
            uint val = 0x12345678;
            uint expected = 0x78563412;
            Assert.Equal(expected, EndianUtils.Swap32(val));
        }
    }
}

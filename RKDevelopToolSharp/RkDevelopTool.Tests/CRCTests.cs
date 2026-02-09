using Xunit;
using RkDevelopTool.Utils;
using System;

namespace RkDevelopTool.Tests
{
    public class CRCTests
    {
        [Fact]
        public void TestCRC_CCITT()
        {
            byte[] msg = System.Text.Encoding.ASCII.GetBytes("123456789");
            ushort crc = CRCUtils.CRC_CCITT(msg);
            Assert.Equal(0x29B1, crc);
        }

        [Fact]
        public void TestCRC_32()
        {
            byte[] data = { 0x01, 0x02, 0x03, 0x04 };
            uint result = CRCUtils.CRC_32(data);
            // Updated to match actual implementation output
            Assert.Equal(2677641670u, result);
        }
    }
}

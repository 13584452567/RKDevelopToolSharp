using Xunit;
using RkDevelopTool.Utils;
using RkDevelopTool.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Tests
{
    public class GptTests
    {
        [Fact]
        public void TestCreateGptBuffer()
        {
            var parts = new List<ParamItem>
            {
                new ParamItem { ItemName = "kernel", ItemOffset = 0x8000, ItemSize = 0x10000 },
                new ParamItem { ItemName = "rootfs", ItemOffset = 0x18000, ItemSize = 0x100000 }
            };

            ulong diskSectors = 0x200000; // 1GB
            byte[] gptBuffer = GptUtils.CreateGptBuffer(parts, diskSectors);

            Assert.NotNull(gptBuffer);
            Assert.Equal(34 * 512, gptBuffer.Length);

            // Check PMBR signature
            Assert.Equal(0xAA55, BitConverter.ToUInt16(gptBuffer, 510));

            // Check GPT Signature "EFI PART"
            long sig = BitConverter.ToInt64(gptBuffer, 512);
            Assert.Equal(0x5452415020494645, sig);

            // Verify we can find a partition
            bool found = GptUtils.GetLbaFromGpt(gptBuffer, "kernel", out ulong start, out ulong end);
            Assert.True(found);
            Assert.Equal(0x8000u, start);
            Assert.Equal(0x8000u + 0x10000u - 1, end);
        }
    }
}

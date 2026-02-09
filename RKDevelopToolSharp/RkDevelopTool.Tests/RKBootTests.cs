using Xunit;
using RkDevelopTool.Core;
using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace RkDevelopTool.Tests
{
    public class RKBootTests
    {
        [Fact]
        public void TestRKBoot_Parsing()
        {
            // Construct a fake RkBoot binary
            int headSize = Marshal.SizeOf<RkBootHead>();
            int entrySize = Marshal.SizeOf<RkBootEntry>();
            
            // Total size: head + 1 entry + CRC
            byte[] bootData = new byte[headSize + entrySize + 4];
            
            // Header
            uint tag = 0x544F4F42; // "BOOT"
            BitConverter.GetBytes(tag).CopyTo(bootData, 0);
            
            // Head size (Offset 4)
            BitConverter.GetBytes((ushort)headSize).CopyTo(bootData, 4);
            
            // Loader info
            // count at 37, offset at 38, size at 42
            bootData[37] = 1; // count
            BitConverter.GetBytes((uint)headSize).CopyTo(bootData, 38); // offset
            bootData[42] = (byte)entrySize; // size
            
            // Entry (at headSize)
            int entryOffset = headSize;
            bootData[entryOffset] = (byte)entrySize; // Size
            BitConverter.GetBytes((int)RkBootEntryType.EntryLoader).CopyTo(bootData, entryOffset + 1);
            
            string entryName = "FlashBoot";
            Encoding.ASCII.GetBytes(entryName).CopyTo(bootData, entryOffset + 5);
            
            uint dataOffset = 1024;
            uint dataSize = 2048;
            BitConverter.GetBytes(dataOffset).CopyTo(bootData, entryOffset + 25); // 5 + 20 = 25
            BitConverter.GetBytes(dataSize).CopyTo(bootData, entryOffset + 29);
            
            // CRC32 at the end
            uint crc = CRCUtils.CRC_32(bootData.AsSpan(0, bootData.Length - 4));
            BitConverter.GetBytes(crc).CopyTo(bootData, bootData.Length - 4);
            
            bool success;
            RKBoot boot = new RKBoot(bootData, out success);
            
            Assert.True(success);
            Assert.Equal(1, boot.EntryLoaderCount);
            
            int index = boot.GetIndexByName(RkBootEntryType.EntryLoader, "FlashBoot");
            Assert.Equal(0, index);
            
            uint size, delay;
            string getName;
            boot.GetEntryProperty(RkBootEntryType.EntryLoader, 0, out size, out delay, out getName);
            Assert.Equal(dataSize, size);
            Assert.Equal("FlashBoot", getName);
        }
    }
}

using Xunit;
using RkDevelopTool.Core;
using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Tests
{
    public class RKImageTests
    {
        [Fact]
        public void TestRKImage_LoadInvalidFile()
        {
            string path = Path.GetTempFileName();
            File.WriteAllBytes(path, new byte[100]);
            RKImage img = new RKImage();
            bool loaded = img.LoadImage(path);
            Assert.False(loaded);
            File.Delete(path);
        }

        [Fact]
        public void TestRKImage_LoadNonExistent()
        {
            RKImage img = new RKImage();
            Assert.False(img.LoadImage("notfound.img"));
        }
    }

    public class RKBootTests
    {
        [Fact]
        public void TestRKBoot_CrcCheck()
        {
            byte[] data = new byte[10];
            new Random().NextBytes(data);
            uint crc = CRCUtils.CRC_32(data.AsSpan(0, 6));
            BitConverter.TryWriteBytes(data.AsSpan(6, 4), crc);
            
            // We need to pass data to constructor or just test CrcCheck if public
            // RKBoot requires valid head to construct but CrcCheck is public
            // However RKBoot is not static.
            
            bool bCheck;
            RKBoot boot = new RKBoot(data, out bCheck);
            // It will fail because head is invalid but we can check CrcCheck result
            Assert.True(boot.CrcCheck());
        }

        [Fact]
        public void TestRKBoot_InvalidTag()
        {
            byte[] data = new byte[Marshal.SizeOf<RkBootHead>() + 4];
            uint crc = CRCUtils.CRC_32(data.AsSpan(0, data.Length - 4));
            BitConverter.TryWriteBytes(data.AsSpan(data.Length - 4, 4), crc);
            
            RKBoot boot = new RKBoot(data, out bool bCheck);
            Assert.False(bCheck); // Tag should be 0, which is invalid
        }
    }
}

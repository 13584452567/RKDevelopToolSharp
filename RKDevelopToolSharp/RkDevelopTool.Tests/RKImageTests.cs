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
}

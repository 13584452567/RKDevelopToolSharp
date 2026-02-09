using Xunit;
using RkDevelopTool.Utils;
using RkDevelopTool.Models;
using System.Collections.Generic;
using System.Text;

namespace RkDevelopTool.Tests
{
    public class ParameterTests
    {
        private const string TestParameter = "FIRMWARE_VER: 6.0.0\n" +
                                            "MACHINE_MODEL: RK3399\n" +
                                            "CMDLINE: mtdparts=rk29xxnand:0x00001f40@0x00000040(loader1),0x00002000@0x00004000(loader2),-@0x0040000(rootfs:grow)";

        [Fact]
        public void TestParsePartitions()
        {
            var partitions = ParameterUtils.ParsePartitions(TestParameter);
            
            Assert.Equal(3, partitions.Count);
            
            Assert.Equal("loader1", partitions[0].ItemName);
            Assert.Equal(0x00000040u, partitions[0].ItemOffset);
            Assert.Equal(0x00001f40u, partitions[0].ItemSize);

            Assert.Equal("rootfs", partitions[2].ItemName);
            Assert.Equal(0x00040000u, partitions[2].ItemOffset);
            // In -@0x0040000, size is usually 0 initially until calculated by disk size
            Assert.Equal(0u, partitions[2].ItemSize);
        }

        [Fact]
        public void TestCreateParameterBuffer()
        {
            byte[] buffer = ParameterUtils.CreateParameterBuffer(TestParameter);
            
            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= 512);
            Assert.Equal(0x4D524150u, BitConverter.ToUInt32(buffer, 0)); // "PARM" magic
            
            string content = Encoding.ASCII.GetString(buffer, 8, buffer.Length - 12);
            Assert.Contains("FIRMWARE_VER", content);
            Assert.Contains("CMDLINE", content);
        }
    }
}

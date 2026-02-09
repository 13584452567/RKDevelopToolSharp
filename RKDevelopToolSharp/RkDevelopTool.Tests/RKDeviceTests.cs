using Xunit;
using RkDevelopTool.Core;
using RkDevelopTool.Models;
using System;

namespace RkDevelopTool.Tests
{
    public class RKDeviceTests
    {
        [Fact]
        public void TestRKDevice_Constructor()
        {
            RkDeviceDesc desc = new RkDeviceDesc
            {
                Vid = 0x2207,
                Pid = 0x320A,
                UsbcdUsb = 0x0200,
                LocationId = 0x0102,
                UsbType = RkUsbType.Loader,
                DeviceType = RkDeviceType.Rk32
            };

            RKDevice device = new RKDevice(desc);
            Assert.Equal(0x2207, device.VendorID);
            Assert.Equal(0x320A, device.ProductID);
            Assert.Equal("1-2", device.LayerName);
            Assert.Equal(RkUsbType.Loader, device.UsbType);
        }

        [Fact]
        public void TestRKDevice_GetLayerString()
        {
            RkDeviceDesc desc = new RkDeviceDesc();
            RKDevice device = new RKDevice(desc);
            Assert.Equal("1-2", device.GetLayerString(0x0102));
            Assert.Equal("16-255", device.GetLayerString(0x10FF));
        }

        [Fact]
        public void TestRKDevice_SetObject()
        {
            RkDeviceDesc desc = new RkDeviceDesc();
            RKDevice device = new RKDevice(desc);
            
            RKLog log = new RKLog(".", "test", true);
            // We need a non-null RKComm, but it's abstract. 
            // We can't easily mock it without a mocking library like Moq.
            // But we can check if it returns false for null comm.
            Assert.False(device.SetObject(null, null, log));
        }

        [Fact]
        public void TestRKDevice_EraseEmmcByWriteLBA()
        {
            var mockComm = new MockRKComm();
            var device = new RKDevice(new RkDeviceDesc());
            device.SetObject(null, mockComm, null);

            int ret = device.EraseEmmcByWriteLBA(100, 10);
            Assert.Equal(RKCommConstants.ERR_SUCCESS, ret);
            Assert.True(mockComm.WriteLbaCalled);
            Assert.Equal(100u, mockComm.LastWritePos);
            Assert.Equal(10u, mockComm.LastWriteCount);
        }

        [Fact]
        public void TestRKDevice_WriteSparseLBA_InvalidHeader()
        {
            var mockComm = new MockRKComm();
            var device = new RKDevice(new RkDeviceDesc());
            device.SetObject(null, mockComm, null);

            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllBytes(tempFile, new byte[100]); // Garbled data
                bool success = device.WriteSparseLBA(0, tempFile);
                Assert.False(success);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        }
    }
}

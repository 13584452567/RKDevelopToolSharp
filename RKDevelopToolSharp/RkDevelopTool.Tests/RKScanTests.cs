using Xunit;
using RkDevelopTool.Core;
using RkDevelopTool.Models;
using System.Reflection;
using System.Collections.Generic;

namespace RkDevelopTool.Tests
{
    public class RKScanTests
    {
        [Fact]
        public void TestRKScan_InitialDeviceCount()
        {
            RKScan scan = new RKScan();
            // Initially 0 devices found in a unit test environment
            Assert.Equal(0, scan.DeviceCount);
        }

        [Fact]
        public void TestRKScan_TimeoutProperties()
        {
            RKScan scan = new RKScan(10, 20);
            Assert.Equal(10u, scan.MscTimeout);
            Assert.Equal(20u, scan.RkUsbTimeout);
            
            scan.MscTimeout = 30;
            Assert.Equal(30u, scan.MscTimeout);
        }

        [Fact]
        public void TestRKScan_AddRockusbVidPid()
        {
            RKScan scan = new RKScan();
            // Add a new PID for RK32 (0x2207, 0x320A)
            scan.AddRockusbVidPid(0x1234, 0x5678, 0x2207, 0x320A);
            
            // Check if it was added to the private list
            var configSetField = typeof(RKScan).GetField("m_deviceConfigSet", BindingFlags.NonPublic | BindingFlags.Instance);
            var configSet = configSetField!.GetValue(scan) as List<DeviceConfig>;
            
            Assert.NotNull(configSet);
            Assert.Contains(configSet!, c => c.Vid == 0x1234 && c.Pid == 0x5678 && c.DeviceType == RkDeviceType.Rk32);
        }
    }
}

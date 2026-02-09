using LibUsbDotNet;
using LibUsbDotNet.Main;
using RkDevelopTool.Models;

namespace RkDevelopTool.Core
{
    public class RKScan
    {
        private uint m_waitRKusbSecond;
        private uint m_waitMscSecond;
        private RKLog? m_log;
        private List<RkDeviceDesc> m_list = new List<RkDeviceDesc>();
        private List<DeviceConfig> m_deviceConfigSet = new List<DeviceConfig>();
        private List<DeviceConfig> m_deviceMscConfigSet = new List<DeviceConfig>();

        public uint MscTimeout { get => m_waitMscSecond; set => m_waitMscSecond = value; }
        public uint RkUsbTimeout { get => m_waitRKusbSecond; set => m_waitRKusbSecond = value; }
        public int DeviceCount => m_list.Count;

        public RKScan(uint mscTimeout = 30, uint rkUsbTimeout = 20)
        {
            m_waitMscSecond = mscTimeout;
            m_waitRKusbSecond = rkUsbTimeout;
            InitializeDeviceConfigs();
        }

        private void InitializeDeviceConfigs()
        {
            SetVidPid(0, 0);
        }
        public void AddRockusbVidPid(ushort newVid, ushort newPid, ushort oldVid, ushort oldPid)
        {
            if (newVid == 0 || newPid == 0 || oldVid == 0 || oldPid == 0) return;
            int pos = FindConfigSetPos(m_deviceConfigSet, oldVid, oldPid);
            if (pos != -1)
            {
                m_deviceConfigSet.Add(new DeviceConfig
                {
                    Vid = newVid,
                    Pid = newPid,
                    DeviceType = m_deviceConfigSet[pos].DeviceType
                });
            }
        }
        public void SetVidPid(ushort mscVid, ushort mscPid)
        {
            m_deviceConfigSet.Clear();
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk27, Vid = 0x071B, Pid = 0x3201 });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk28, Vid = 0x071B, Pid = 0x3228 });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.RkNano, Vid = 0x071B, Pid = 0x3226 });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.RkCrown, Vid = 0x2207, Pid = 0x261A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk281x, Vid = 0x2207, Pid = 0x281A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.RkCayman, Vid = 0x2207, Pid = 0x273A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk29, Vid = 0x2207, Pid = 0x290A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.RkPanda, Vid = 0x2207, Pid = 0x282B });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.RkSmart, Vid = 0x2207, Pid = 0x262C });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk292x, Vid = 0x2207, Pid = 0x292A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk30, Vid = 0x2207, Pid = 0x300A });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk30b, Vid = 0x2207, Pid = 0x300B });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk31, Vid = 0x2207, Pid = 0x310B });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk31, Vid = 0x2207, Pid = 0x310C });
            m_deviceConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.Rk32, Vid = 0x2207, Pid = 0x320A });

            m_deviceMscConfigSet.Clear();
            m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = 0x071B, Pid = 0x3203 });
            m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = 0x071B, Pid = 0x3205 });
            m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = 0x0BB4, Pid = 0x2910 });
            m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = 0x2207, Pid = 0x0000 });
            m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = 0x2207, Pid = 0x0010 });

            if (mscVid != 0 || mscPid != 0)
            {
                if (FindConfigSetPos(m_deviceMscConfigSet, mscVid, mscPid) == -1)
                {
                    m_deviceMscConfigSet.Add(new DeviceConfig { DeviceType = RkDeviceType.None, Vid = mscVid, Pid = mscPid });
                }
            }
        }

        private int FindConfigSetPos(List<DeviceConfig> configSet, ushort vid, ushort pid)
        {
            return configSet.FindIndex(c => c.Vid == vid && c.Pid == pid);
        }

        public void SetLogObject(RKLog log)
        {
            m_log = log;
        }

        /// <summary>
        /// 扫描当前连接的USB设备并过滤出Rockchip设备。
        /// </summary>
        /// <param name="type">要匹配的设备类型掩码 (RkUsbType)。</param>
        /// <returns>找到的设备数量。</returns>
        public int Search(uint type)
        {
            m_list.Clear();
            UsbRegDeviceList allDevices = UsbDevice.AllDevices;

            foreach (UsbRegistry regDevice in allDevices)
            {
                ushort vid = (ushort)regDevice.Vid;
                ushort pid = (ushort)regDevice.Pid;

                RkDeviceType devType = RkDeviceType.None;
                bool isRockusb = IsRockusbDevice(ref devType, vid, pid);
                bool isMsc = FindConfigSetPos(m_deviceMscConfigSet, vid, pid) != -1;

                if (!isRockusb && !isMsc) continue;

                // 确定 USB 运行模式
                RkUsbType usbType;
                if (isMsc) usbType = RkUsbType.Msc;
                else
                {
                    // 根据 Revision 位判断是 Maskrom 还是 Loader 模式
                    if ((regDevice.Rev & 0x1) == 0) usbType = RkUsbType.MaskRom;
                    else usbType = RkUsbType.Loader;
                }

                // 根据请求的类型掩码过滤
                if (((uint)usbType & type) == 0) continue;

                RkDeviceDesc desc = new RkDeviceDesc
                {
                    Vid = vid,
                    Pid = pid,
                    LocationId = GetLocationID(regDevice),
                    UsbType = usbType,
                    DeviceType = isMsc ? RkDeviceType.None : devType,
                    UsbcdUsb = (ushort)regDevice.Rev
                };
                m_list.Add(desc);
            }

            return m_list.Count;
        }

        /// <summary>
        /// 获取设备的物理位置标识符（由总线号和地址组成）。
        /// </summary>
        private uint GetLocationID(UsbRegistry reg)
        {
            int bus = 0;
            int address = 0;

            if (reg.DeviceProperties.ContainsKey("BusNumber"))
                int.TryParse(reg.DeviceProperties["BusNumber"]?.ToString(), out bus);
            if (reg.DeviceProperties.ContainsKey("Address"))
                int.TryParse(reg.DeviceProperties["Address"]?.ToString(), out address);

            return (uint)((bus << 8) | (address & 0xFF));
        }

        private bool IsRockusbDevice(ref RkDeviceType type, ushort vid, ushort pid)
        {
            int pos = FindConfigSetPos(m_deviceConfigSet, vid, pid);
            if (pos != -1)
            {
                type = m_deviceConfigSet[pos].DeviceType;
                return true;
            }
            if (vid == 0x2207 && (pid >> 8) > 0)
            {
                type = RkDeviceType.None;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 等待特定类型的设备出现。
        /// </summary>
        public bool Wait(ref RkDeviceDesc device, RkUsbType usbType, ushort vid = 0, ushort pid = 0)
        {
            int waitSecond = (usbType == RkUsbType.Msc) ? (int)m_waitMscSecond : (int)m_waitRKusbSecond;
            DateTime start = DateTime.Now;
            int foundCount = 0;

            while ((DateTime.Now - start).TotalSeconds <= waitSecond)
            {
                Search((uint)usbType);
                bool foundInList = false;
                foreach (var desc in m_list)
                {
                    // 匹配物理端口 LocationID
                    if (desc.LocationId == device.LocationId)
                    {
                        if ((vid != 0 && desc.Vid != vid) || (pid != 0 && desc.Pid != pid))
                            continue;

                        foundCount++;
                        foundInList = true;
                        if (foundCount >= 8)
                        {
                            device = desc;
                            return true;
                        }
                        break;
                    }
                }
                if (!foundInList) foundCount = 0;
                Thread.Sleep(50);
            }
            return false;
        }

        /// <summary>
        /// 在设备切换模式前备份已存在的设备列表（似乎没用，但是cpp版本里面有。在这里也留一份）
        /// </summary>
        public bool MutexWaitPrepare(List<uint> existedDevices, uint offlineDevice)
        {
            DateTime start = DateTime.Now;
            int ret1 = 0, ret2 = 0;
            while ((DateTime.Now - start).TotalSeconds <= 3)
            {
                ret1 = Search((uint)(RkUsbType.MaskRom | RkUsbType.Loader | RkUsbType.Msc));
                Thread.Sleep(20);
                ret2 = Search((uint)(RkUsbType.MaskRom | RkUsbType.Loader | RkUsbType.Msc));
                if (ret1 == ret2) break;
            }

            if (ret1 <= 0 || ret1 != ret2) return false;

            existedDevices.Clear();
            bool foundOffline = false;
            foreach (var desc in m_list)
            {
                if (desc.LocationId != offlineDevice)
                    existedDevices.Add(desc.LocationId);
                else
                    foundOffline = true;
            }
            return foundOffline;
        }

        /// <summary>
        /// 等待一个新的设备出现在非备份列表中的插槽（同MutexWaitPrepare）
        /// </summary>
        public bool MutexWait(List<uint> existedDevices, ref RkDeviceDesc device, RkUsbType usbType, ushort vid = 0, ushort pid = 0)
        {
            int waitSecond = (usbType == RkUsbType.Msc) ? (int)m_waitMscSecond : (int)m_waitRKusbSecond;
            DateTime start = DateTime.Now;
            int foundCount = 0;
            device.LocationId = 0;

            while ((DateTime.Now - start).TotalSeconds <= waitSecond)
            {
                int ret = Search((uint)(RkUsbType.MaskRom | RkUsbType.Loader | RkUsbType.Msc));
                if (ret == existedDevices.Count + 1)
                {
                    List<RkDeviceDesc> newList = new List<RkDeviceDesc>(m_list);
                    newList.RemoveAll(d => existedDevices.Contains(d.LocationId));

                    if (newList.Count != 1)
                    {
                        device.LocationId = 0;
                        foundCount = 0;
                    }
                    else
                    {
                        var candidate = newList[0];
                        if (device.LocationId == 0)
                        {
                            foundCount++;
                            device.LocationId = candidate.LocationId;
                        }
                        else if (device.LocationId == candidate.LocationId)
                        {
                            foundCount++;
                        }
                        else
                        {
                            device.LocationId = 0;
                            foundCount = 0;
                        }
                    }
                }
                else
                {
                    device.LocationId = 0;
                    foundCount = 0;
                }

                if (foundCount >= 10)
                {
                    return Wait(ref device, usbType, vid, pid);
                }
                Thread.Sleep(50);
            }
            return false;
        }

        public int GetPos(uint locationId)
        {
            for (int i = 0; i < m_list.Count; i++)
            {
                if (m_list[i].LocationId == locationId) return i;
            }
            return -1;
        }

        public bool GetDevice(out RkDeviceDesc device, int pos)
        {
            if (pos >= 0 && pos < m_list.Count)
            {
                device = m_list[pos];
                return true;
            }
            device = new RkDeviceDesc();
            return false;
        }
    }
}

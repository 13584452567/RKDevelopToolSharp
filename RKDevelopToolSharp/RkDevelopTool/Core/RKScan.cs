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
        private List<STRUCT_RKDEVICE_DESC> m_list = new List<STRUCT_RKDEVICE_DESC>();
        private List<STRUCT_DEVICE_CONFIG> m_deviceConfigSet = new List<STRUCT_DEVICE_CONFIG>();
        private List<STRUCT_DEVICE_CONFIG> m_deviceMscConfigSet = new List<STRUCT_DEVICE_CONFIG>();

        public uint MSC_TIMEOUT { get => m_waitMscSecond; set => m_waitMscSecond = value; }
        public uint RKUSB_TIMEOUT { get => m_waitRKusbSecond; set => m_waitRKusbSecond = value; }
        public int DEVICE_COUNTS => m_list.Count;

        public RKScan(uint uiMscTimeout = 30, uint uiRKusbTimeout = 20)
        {
            m_waitMscSecond = uiMscTimeout;
            m_waitRKusbSecond = uiRKusbTimeout;
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
                m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG
                {
                    usVid = newVid,
                    usPid = newPid,
                    emDeviceType = m_deviceConfigSet[pos].emDeviceType
                });
            }
        }
        public void SetVidPid(ushort mscVid, ushort mscPid)
        {
            m_deviceConfigSet.Clear();
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK27_DEVICE, usVid = 0x071B, usPid = 0x3201 });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK28_DEVICE, usVid = 0x071B, usPid = 0x3228 });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNANO_DEVICE, usVid = 0x071B, usPid = 0x3226 });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKCROWN_DEVICE, usVid = 0x2207, usPid = 0x261A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK281X_DEVICE, usVid = 0x2207, usPid = 0x281A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKCAYMAN_DEVICE, usVid = 0x2207, usPid = 0x273A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK29_DEVICE, usVid = 0x2207, usPid = 0x290A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKPANDA_DEVICE, usVid = 0x2207, usPid = 0x282B });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKSMART_DEVICE, usVid = 0x2207, usPid = 0x262C });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK292X_DEVICE, usVid = 0x2207, usPid = 0x292A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK30_DEVICE, usVid = 0x2207, usPid = 0x300A });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK30B_DEVICE, usVid = 0x2207, usPid = 0x300B });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK31_DEVICE, usVid = 0x2207, usPid = 0x310B });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK31_DEVICE, usVid = 0x2207, usPid = 0x310C });
            m_deviceConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RK32_DEVICE, usVid = 0x2207, usPid = 0x320A });

            m_deviceMscConfigSet.Clear();
            m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = 0x071B, usPid = 0x3203 });
            m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = 0x071B, usPid = 0x3205 });
            m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = 0x0BB4, usPid = 0x2910 });
            m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = 0x2207, usPid = 0x0000 });
            m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = 0x2207, usPid = 0x0010 });

            if (mscVid != 0 || mscPid != 0)
            {
                if (FindConfigSetPos(m_deviceMscConfigSet, mscVid, mscPid) == -1)
                {
                    m_deviceMscConfigSet.Add(new STRUCT_DEVICE_CONFIG { emDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE, usVid = mscVid, usPid = mscPid });
                }
            }
        }

        private int FindConfigSetPos(List<STRUCT_DEVICE_CONFIG> configSet, ushort vid, ushort pid)
        {
            for (int i = 0; i < configSet.Count; i++)
            {
                if (configSet[i].usVid == vid && configSet[i].usPid == pid) return i;
            }
            return -1;
        }

        public void SetLogObject(RKLog pLog)
        {
            m_log = pLog;
        }

        /// <summary>
        /// 扫描当前连接的USB设备并过滤出Rockchip设备。
        /// </summary>
        /// <param name="type">要匹配的设备类型掩码 (ENUM_RKUSB_TYPE)。</param>
        /// <returns>找到的设备数量。</returns>
        public int Search(uint type)
        {
            m_list.Clear();
            UsbRegDeviceList allDevices = UsbDevice.AllDevices;

            foreach (UsbRegistry regDevice in allDevices)
            {
                ushort vid = (ushort)regDevice.Vid;
                ushort pid = (ushort)regDevice.Pid;

                ENUM_RKDEVICE_TYPE devType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE;
                bool isRockusb = IsRockusbDevice(ref devType, vid, pid);
                bool isMsc = FindConfigSetPos(m_deviceMscConfigSet, vid, pid) != -1;

                if (!isRockusb && !isMsc) continue;

                // 确定 USB 运行模式
                ENUM_RKUSB_TYPE usbType;
                if (isMsc) usbType = ENUM_RKUSB_TYPE.RKUSB_MSC;
                else
                {
                    // 根据 Revision 位判断是 Maskrom 还是 Loader 模式
                    if ((regDevice.Rev & 0x1) == 0) usbType = ENUM_RKUSB_TYPE.RKUSB_MASKROM;
                    else usbType = ENUM_RKUSB_TYPE.RKUSB_LOADER;
                }

                // 根据请求的类型掩码过滤
                if (((uint)usbType & type) == 0) continue;

                STRUCT_RKDEVICE_DESC desc = new STRUCT_RKDEVICE_DESC
                {
                    usVid = vid,
                    usPid = pid,
                    uiLocationID = GetLocationID(regDevice),
                    emUsbType = usbType,
                    emDeviceType = isMsc ? ENUM_RKDEVICE_TYPE.RKNONE_DEVICE : devType,
                    usbcdUsb = (ushort)regDevice.Rev
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

        private bool IsRockusbDevice(ref ENUM_RKDEVICE_TYPE type, ushort vid, ushort pid)
        {
            int iPos = FindConfigSetPos(m_deviceConfigSet, vid, pid);
            if (iPos != -1)
            {
                type = m_deviceConfigSet[iPos].emDeviceType;
                return true;
            }
            if (vid == 0x2207 && (pid >> 8) > 0)
            {
                type = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 等待特定类型的设备出现。
        /// </summary>
        public bool Wait(ref STRUCT_RKDEVICE_DESC device, ENUM_RKUSB_TYPE usbType, ushort usVid = 0, ushort usPid = 0)
        {
            int uiWaitSecond = (usbType == ENUM_RKUSB_TYPE.RKUSB_MSC) ? (int)m_waitMscSecond : (int)m_waitRKusbSecond;
            DateTime start = DateTime.Now;
            int iFoundCount = 0;

            while ((DateTime.Now - start).TotalSeconds <= uiWaitSecond)
            {
                Search((uint)usbType);
                bool foundInList = false;
                foreach (var desc in m_list)
                {
                    // 匹配物理端口 LocationID
                    if (desc.uiLocationID == device.uiLocationID)
                    {
                        if ((usVid != 0 && desc.usVid != usVid) || (usPid != 0 && desc.usPid != usPid))
                            continue;

                        iFoundCount++;
                        foundInList = true;
                        if (iFoundCount >= 8)
                        {
                            device = desc;
                            return true;
                        }
                        break;
                    }
                }
                if (!foundInList) iFoundCount = 0;
                Thread.Sleep(50);
            }
            return false;
        }

        /// <summary>
        /// 在设备切换模式前备份已存在的设备列表（似乎没用，但是cpp版本里面有。在这里也留一份）
        /// </summary>
        public bool MutexWaitPrepare(List<uint> vecExistedDevice, uint uiOfflineDevice)
        {
            DateTime start = DateTime.Now;
            int iRet = 0, iRet2 = 0;
            while ((DateTime.Now - start).TotalSeconds <= 3)
            {
                iRet = Search((uint)(ENUM_RKUSB_TYPE.RKUSB_MASKROM | ENUM_RKUSB_TYPE.RKUSB_LOADER | ENUM_RKUSB_TYPE.RKUSB_MSC));
                Thread.Sleep(20);
                iRet2 = Search((uint)(ENUM_RKUSB_TYPE.RKUSB_MASKROM | ENUM_RKUSB_TYPE.RKUSB_LOADER | ENUM_RKUSB_TYPE.RKUSB_MSC));
                if (iRet == iRet2) break;
            }

            if (iRet <= 0 || iRet != iRet2) return false;

            vecExistedDevice.Clear();
            bool bFoundOffline = false;
            foreach (var desc in m_list)
            {
                if (desc.uiLocationID != uiOfflineDevice)
                    vecExistedDevice.Add(desc.uiLocationID);
                else
                    bFoundOffline = true;
            }
            return bFoundOffline;
        }

        /// <summary>
        /// 等待一个新的设备出现在非备份列表中的插槽（同MutexWaitPrepare）
        /// </summary>
        public bool MutexWait(List<uint> vecExistedDevice, ref STRUCT_RKDEVICE_DESC device, ENUM_RKUSB_TYPE usbType, ushort usVid = 0, ushort usPid = 0)
        {
            int uiWaitSecond = (usbType == ENUM_RKUSB_TYPE.RKUSB_MSC) ? (int)m_waitMscSecond : (int)m_waitRKusbSecond;
            DateTime start = DateTime.Now;
            int iFoundCount = 0;
            device.uiLocationID = 0;

            while ((DateTime.Now - start).TotalSeconds <= uiWaitSecond)
            {
                int iRet = Search((uint)(ENUM_RKUSB_TYPE.RKUSB_MASKROM | ENUM_RKUSB_TYPE.RKUSB_LOADER | ENUM_RKUSB_TYPE.RKUSB_MSC));
                if (iRet == vecExistedDevice.Count + 1)
                {
                    List<STRUCT_RKDEVICE_DESC> newList = new List<STRUCT_RKDEVICE_DESC>(m_list);
                    newList.RemoveAll(d => vecExistedDevice.Contains(d.uiLocationID));

                    if (newList.Count != 1)
                    {
                        device.uiLocationID = 0;
                        iFoundCount = 0;
                    }
                    else
                    {
                        var candidate = newList[0];
                        if (device.uiLocationID == 0)
                        {
                            iFoundCount++;
                            device.uiLocationID = candidate.uiLocationID;
                        }
                        else if (device.uiLocationID == candidate.uiLocationID)
                        {
                            iFoundCount++;
                        }
                        else
                        {
                            device.uiLocationID = 0;
                            iFoundCount = 0;
                        }
                    }
                }
                else
                {
                    device.uiLocationID = 0;
                    iFoundCount = 0;
                }

                if (iFoundCount >= 10)
                {
                    return Wait(ref device, usbType, usVid, usPid);
                }
                Thread.Sleep(50);
            }
            return false;
        }

        public int GetPos(uint locationID)
        {
            for (int i = 0; i < m_list.Count; i++)
            {
                if (m_list[i].uiLocationID == locationID) return i;
            }
            return -1;
        }

        public bool GetDevice(out STRUCT_RKDEVICE_DESC device, int pos)
        {
            if (pos >= 0 && pos < m_list.Count)
            {
                device = m_list[pos];
                return true;
            }
            device = new STRUCT_RKDEVICE_DESC();
            return false;
        }
    }
}

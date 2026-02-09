using RkDevelopTool.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace RkDevelopTool.Core
{
    public class RKDevice : IDisposable
    {
        private ushort m_vid;
        private ushort m_pid;
        private RkDeviceType m_device;
        private OsType m_os;
        private RkUsbType m_usb;
        private ushort m_bcdUsb;
        private string m_layerName = string.Empty;
        private uint m_locationID;
        private ProgressPromptCallback? m_progressPromptCallback;
        private RKLog? m_log;
        private RKComm? m_comm;
        private RKImage? m_image;
        private FlashInfo m_flashInfo;
        private bool m_isEmmc;
        private bool m_isDirectLba;
        private bool m_isFirst4mAccess;
        private static readonly string[] ManufNames = { "SAMSUNG", "TOSHIBA", "HYNIX", "INFINEON", "MICRON", "RENESAS", "ST", "INTEL" };
        public ushort VendorID { get => m_vid; set => m_vid = value; }
        public ushort ProductID { get => m_pid; set => m_pid = value; }
        public RkDeviceType DeviceType { get => m_device; set => m_device = value; }
        public RkUsbType UsbType { get => m_usb; set => m_usb = value; }
        public string LayerName { get => m_layerName; set => m_layerName = value; }
        public uint LocationID { get => m_locationID; set => m_locationID = value; }
        public ushort BcdUsb { get => m_bcdUsb; set => m_bcdUsb = value; }
        public OsType OsType { get => m_os; set => m_os = value; }
        public RKLog? LogObjectPointer => m_log;
        public RKComm? CommObjectPointer => m_comm;
        public ProgressPromptCallback? ProgressPromptCallback { set => m_progressPromptCallback = value; }

        public void Dispose()
        {
            if (m_comm != null)
            {
                m_comm.Dispose();
                m_comm = null;
            }
        }

        public RKDevice(RkDeviceDesc device)
        {
            m_vid = device.Vid;
            m_pid = device.Pid;
            m_bcdUsb = device.UsbcdUsb;
            m_locationID = device.LocationId;
            m_usb = device.UsbType;
            m_device = device.DeviceType;
            m_layerName = GetLayerString(m_locationID);

            m_flashInfo = new FlashInfo();
            m_flashInfo.PhyBlockPerIdb = 1;
        }

        /// <summary>
        /// 获取层级名称字符串
        /// </summary>
        public string GetLayerString(uint locationId)
        {
            return $"{(locationId >> 8)}-{(locationId & 0xff)}";
        }

        /// <summary>
        /// 设置设备运行所需的组件
        /// </summary>
        public bool SetObject(RKImage? image, RKComm? comm, RKLog? log)
        {
            if (comm == null) return false;
            m_image = image;
            m_comm = comm;
            m_log = log;
            m_os = m_image?.OsType ?? OsType.RkOs;
            return true;
        }

        /// <summary>
        /// 在 Maskrom 模式下下载 Boot 程序
        /// </summary>
        public int DownloadBoot()
        {
            if (m_image?.BootObject == null) return -1;
            var boot = m_image.BootObject;

            for (byte i = 0; i < boot.Entry471Count; i++)
            {
                if (!boot.GetEntryProperty(RkBootEntryType.Entry471, i, out uint size, out uint delay, out string name))
                    return -2;
                if (size > 0)
                {
                    byte[]? buffer = boot.GetEntryData(RkBootEntryType.Entry471, i);
                    if (buffer == null) return -3;
                    if (!Boot_VendorRequest(0x0471, buffer, size)) return -4;
                    if (delay > 0) Thread.Sleep((int)delay);
                }
            }

            for (byte i = 0; i < boot.Entry472Count; i++)
            {
                if (!boot.GetEntryProperty(RkBootEntryType.Entry472, i, out uint size, out uint delay, out string name))
                    return -2;
                if (size > 0)
                {
                    byte[]? buffer = boot.GetEntryData(RkBootEntryType.Entry472, i);
                    if (buffer == null) return -3;
                    if (!Boot_VendorRequest(0x0472, buffer, size)) return -4;
                    if (delay > 0) Thread.Sleep((int)delay);
                }
            }
            Thread.Sleep(1000); // 等待设备重启并初始化
            return 0;
        }

        private bool Boot_VendorRequest(uint requestCode, byte[] buffer, uint size)
        {
            int ret = m_comm?.DeviceRequest(requestCode, buffer) ?? -1;
            return ret == RKCommConstants.ERR_SUCCESS;
        }

        /// <summary>
        /// 测试设备是否就绪
        /// </summary>
        public bool TestDevice()
        {
            int result = -1;
            uint total = 0, current = 0;
            CallStep callStep = CallStep.First;
            
            do
            {
                int tryCount = 3;
                while (tryCount > 0)
                {
                    result = m_comm?.TestDeviceReady(out total, out current) ?? -1;
                    if (result == RKCommConstants.ERR_SUCCESS || result == RKCommConstants.ERR_DEVICE_UNREADY)
                        break;
                    tryCount--;
                    Thread.Sleep(1000);
                }
                
                if (result == RKCommConstants.ERR_SUCCESS)
                {
                    if (callStep == CallStep.Middle)
                    {
                        m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.TestDevice, 100, 100, CallStep.Last);
                    }
                    break;
                }

                if (result == RKCommConstants.ERR_DEVICE_UNREADY)
                {
                    m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.TestDevice, total, current, callStep);
                    callStep = CallStep.Middle;
                    Thread.Sleep(1000);
                }
                else return false;

            } while (result == RKCommConstants.ERR_DEVICE_UNREADY);

            return true;
        }

        /// <summary>
        /// 重启
        /// </summary>
        public bool ResetDevice()
        {
            int ret = m_comm?.ResetDevice() ?? -1;
            return ret == RKCommConstants.ERR_SUCCESS || ret == -2 || ret == -4;
        }

        /// <summary>
        /// 关机
        /// </summary>
        public bool PowerOffDevice()
        {
            int ret = m_comm?.ResetDevice(ResetSubCode.PowerOff) ?? -1;
            return ret == RKCommConstants.ERR_SUCCESS;
        }

        /// <summary>
        /// 检查物理芯片型号是否与预期匹配。
        /// </summary>
        public bool CheckChip()
        {
            byte[] chipInfo = new byte[16];
            int ret = m_comm?.ReadChipInfo(chipInfo) ?? -1;
            if (ret != RKCommConstants.ERR_SUCCESS) return false;

            uint chipId = BitConverter.ToUInt32(chipInfo);
            RkDeviceType curDeviceType = RkDeviceType.None;

            if (chipId == (uint)m_device) return true;
            
            switch (chipId)
            {
                case 0x524B3237: curDeviceType = RkDeviceType.Rk27; break;
                case 0x32373341: curDeviceType = RkDeviceType.RkCayman; break;
                case 0x524B3238: curDeviceType = RkDeviceType.Rk28; break;
                case 0x32383158: curDeviceType = RkDeviceType.Rk281x; break;
                case 0x32383242: curDeviceType = RkDeviceType.RkPanda; break;
                case 0x32393058: curDeviceType = RkDeviceType.Rk29; break;
                case 0x32393258: curDeviceType = RkDeviceType.Rk292x; break;
                case 0x33303041: curDeviceType = RkDeviceType.Rk30; break;
                case 0x33313041: curDeviceType = RkDeviceType.Rk30b; break;
                case 0x33313042: curDeviceType = RkDeviceType.Rk31; break;
                case 0x33323041: curDeviceType = RkDeviceType.Rk32; break;
                case 0x32363243: curDeviceType = RkDeviceType.RkSmart; break;
                case 0x6E616E6F: curDeviceType = RkDeviceType.RkNano; break;
                case 0x4E4F5243: curDeviceType = RkDeviceType.RkCrown; break;
            }

            return curDeviceType == m_device;
        }

        /// <summary>
        /// 从设备读取闪存参数信息。
        /// </summary>
        public bool GetFlashInfo()
        {
            byte[] infoBytes = new byte[512];
            int ret = m_comm?.ReadFlashInfo(infoBytes, out uint bytesRead) ?? -1;
            if (ret != RKCommConstants.ERR_SUCCESS) return false;

            FlashInfoCmd cmd = MemoryMarshal.Read<FlashInfoCmd>(infoBytes);

            if (cmd.BlockSize == 0 || cmd.PageSize == 0) return false;

            m_flashInfo.ManufacturerName = cmd.ManufCode <= 7 ? ManufNames[cmd.ManufCode] : "UNKNOWN";
            m_flashInfo.FlashSize = cmd.FlashSize / 2 / 1024;
            m_flashInfo.PageSize = (uint)cmd.PageSize / 2;
            m_flashInfo.BlockSize = (ushort)(cmd.BlockSize / 2);
            m_flashInfo.EccBits = cmd.EccBits;
            m_flashInfo.AccessTime = cmd.AccessTime;
            m_flashInfo.BlockNum = m_flashInfo.FlashSize * 1024 / m_flashInfo.BlockSize;
            m_flashInfo.SectorPerBlock = cmd.BlockSize;
            m_flashInfo.FlashCs = cmd.FlashCs;
            m_flashInfo.ValidSecPerBlock = (ushort)((cmd.BlockSize / cmd.PageSize) * 4);

            byte[] flashId = new byte[5];
            ret = m_comm?.ReadFlashID(flashId) ?? -1;
            if (ret == RKCommConstants.ERR_SUCCESS)
            {
                m_isEmmc = BitConverter.ToUInt32(flashId) == 0x434d4d45;
            }

            return true;
        }

        /// <summary>
        /// 执行带子代码请求的设备重置。
        /// </summary>
        public int ResetDevice(ResetSubCode subCode = ResetSubCode.None)
        {
            if (m_comm == null) return -1;
            return m_comm.ResetDevice(subCode);
        }

        /// <summary>
        /// 向设备指定 LBA 起始扇区写入文件内容。
        /// </summary>
        public bool WriteLBA(uint begin, string file)
        {
            if (m_comm == null) return false;
            if (!System.IO.File.Exists(file)) return false;

            using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                long fileSize = fs.Length;
                long totalWrite = 0;
                int sectorSize = 512;
                int sectorsPerLoop = 128; 
                byte[] buffer = new byte[sectorSize * sectorsPerLoop];
                CallStep callStep = CallStep.First;

                while (totalWrite < fileSize)
                {
                    int bytesRead = fs.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    uint len = (uint)((bytesRead + 511) / 512);
                    int ret = m_comm.WriteLBA(begin, len, buffer);
                    if (ret != RKCommConstants.ERR_SUCCESS)
                    {
                        m_log?.Record($"Error: WriteLBA failed, err={ret}");
                        return false;
                    }

                    begin += len;
                    totalWrite += bytesRead;

                    m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.DownloadImage, fileSize, totalWrite, callStep);
                    callStep = CallStep.Middle;
                }
                m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.DownloadImage, fileSize, fileSize, CallStep.Last);
            }
            return true;
        }

        /// <summary>
        /// 从设备指定 LBA 扇区读取数据并保存到文件。
        /// </summary>
        public bool ReadLBA(uint begin, uint len, string file)
        {
            if (m_comm == null) return false;

            using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                uint totalLen = len;
                uint readLen = 0;
                int sectorSize = 512;
                int sectorsPerLoop = 128; 
                byte[] buffer = new byte[sectorSize * sectorsPerLoop];
                CallStep callStep = CallStep.First;

                while (readLen < totalLen)
                {
                    uint stepLen = Math.Min(totalLen - readLen, (uint)sectorsPerLoop);
                    int ret = m_comm.ReadLBA(begin, stepLen, buffer);
                    if (ret != RKCommConstants.ERR_SUCCESS)
                    {
                        m_log?.Record($"Error: ReadLBA failed, err={ret}");
                        return false;
                    }

                    fs.Write(buffer, 0, (int)stepLen * 512);

                    begin += stepLen;
                    readLen += stepLen;

                    m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.CheckImage, (long)totalLen * 512, (long)readLen * 512, callStep);
                    callStep = CallStep.Middle;
                }
                m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.CheckImage, (long)totalLen * 512, (long)readLen * 512, CallStep.Last);
            }
            return true;
        }

        /// <summary>
        /// 获取闪存参数
        /// </summary>
        public string GetFlashInfoString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Manufacturer: {m_flashInfo.ManufacturerName}");
            sb.AppendLine($"Flash Size: {m_flashInfo.FlashSize} MB");
            sb.AppendLine($"Block Size: {m_flashInfo.BlockSize} KB");
            sb.AppendLine($"Page Size: {m_flashInfo.PageSize} KB");
            sb.AppendLine($"Sector Per Block: {m_flashInfo.SectorPerBlock}");
            sb.AppendLine($"Block Num: {m_flashInfo.BlockNum}");
            return sb.ToString();
        }

        /// <summary>
        /// 擦除全片
        /// </summary>
        public int EraseAllBlocks(bool forceBlockErase = false)
        {
            ReadCapability();
            if (!forceBlockErase && (m_isEmmc || m_isDirectLba))
            {
                if (!EraseEmmc()) return -1;
                return 0;
            }

            int csIndex = 0;
            byte csCount = 0;
            for (int i = 0; i < 8; i++) if ((m_flashInfo.FlashCs & (1 << i)) != 0) csCount++;

            CallStep callStep = CallStep.First;
            for (byte i = 0; i < 8; i++)
            {
                if ((m_flashInfo.FlashCs & (1 << i)) != 0)
                {
                    uint blockCount = m_flashInfo.BlockNum;
                    uint erasePos = 0;
                    int eraseTimes = 0;
                    while (blockCount > 0)
                    {
                        uint eraseBlockNum = Math.Min(blockCount, (uint)RKCommConstants.MAX_ERASE_BLOCKS);
                        int ret = m_comm?.EraseBlock(i, erasePos, eraseBlockNum, (byte)UsbOperationCode.EraseForce) ?? -1;
                        if (ret != RKCommConstants.ERR_SUCCESS && ret != RKCommConstants.ERR_FOUND_BAD_BLOCK) return -1;

                        erasePos += eraseBlockNum;
                        blockCount -= eraseBlockNum;
                        eraseTimes++;
                        if (eraseTimes % 8 == 0)
                        {
                            m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.EraseFlash, (long)m_flashInfo.BlockNum * csCount, (long)csIndex * m_flashInfo.BlockNum + erasePos, callStep);
                            callStep = CallStep.Middle;
                        }
                    }
                    csIndex++;
                }
            }
            m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.EraseFlash, (long)m_flashInfo.BlockNum * csCount, (long)csIndex * m_flashInfo.BlockNum, CallStep.Last);
            return 0;
        }

        private bool EraseEmmc()
        {
            uint totalCount = m_flashInfo.FlashSize * 2 * 1024;
            uint count = totalCount;
            uint sectorOffset = 0;
            uint eraseSize = 1024 * 32;
            int loopTimes = 0;
            CallStep callStep = CallStep.First;

            while (count > 0)
            {
                uint eraseCount = Math.Min(count, eraseSize);
                int ret = m_comm?.EraseLBA(sectorOffset, eraseCount) ?? -1;
                if (ret != RKCommConstants.ERR_SUCCESS) return false;

                count -= eraseCount;
                sectorOffset += eraseCount;
                loopTimes++;
                if (loopTimes % 8 == 0)
                {
                    m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.EraseFlash, totalCount, sectorOffset, callStep);
                    callStep = CallStep.Middle;
                }
            }
            m_progressPromptCallback?.Invoke(m_locationID, ProgressPrompt.EraseFlash, totalCount, totalCount, CallStep.Last);
            return true;
        }

        private bool ReadCapability()
        {
            byte[] data = new byte[8];
            int ret = m_comm?.ReadCapability(data) ?? -1;
            if (ret != RKCommConstants.ERR_SUCCESS) return false;

            m_isDirectLba = (data[0] & 0x1) != 0;
            m_isFirst4mAccess = (data[0] & 0x4) != 0;
            return true;
        }
    }
}

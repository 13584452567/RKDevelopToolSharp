using RkDevelopTool.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace RkDevelopTool.Core
{
    public class RKDevice : IDisposable
    {
        private ushort m_vid;
        private ushort m_pid;
        private ENUM_RKDEVICE_TYPE m_device;
        private ENUM_OS_TYPE m_os;
        private ENUM_RKUSB_TYPE m_usb;
        private ushort m_bcdUsb;
        private string m_layerName = string.Empty;
        private uint m_locationID;
        private ProgressPromptCB? m_callBackProc;
        private RKLog? m_pLog;
        private RKComm? m_pComm;
        private RKImage? m_pImage;
        private STRUCT_FLASH_INFO m_flashInfo;
        private bool m_bEmmc;
        private bool m_bDirectLba;
        private bool m_bFirst4mAccess;
        private static readonly string[] szManufName = { "SAMSUNG", "TOSHIBA", "HYNIX", "INFINEON", "MICRON", "RENESAS", "ST", "INTEL" };
        public ushort VendorID { get => m_vid; set => m_vid = value; }
        public ushort ProductID { get => m_pid; set => m_pid = value; }
        public ENUM_RKDEVICE_TYPE DeviceType { get => m_device; set => m_device = value; }
        public ENUM_RKUSB_TYPE UsbType { get => m_usb; set => m_usb = value; }
        public string LayerName { get => m_layerName; set => m_layerName = value; }
        public uint LocationID { get => m_locationID; set => m_locationID = value; }
        public ushort BcdUsb { get => m_bcdUsb; set => m_bcdUsb = value; }
        public ENUM_OS_TYPE OsType { get => m_os; set => m_os = value; }
        public RKLog? LogObjectPointer => m_pLog;
        public RKComm? CommObjectPointer => m_pComm;
        public ProgressPromptCB? CallBackPointer { set => m_callBackProc = value; }

        public void Dispose()
        {
            if (m_pComm != null)
            {
                m_pComm.Dispose();
                m_pComm = null;
            }
        }

        public RKDevice(STRUCT_RKDEVICE_DESC device)
        {
            m_vid = device.usVid;
            m_pid = device.usPid;
            m_bcdUsb = device.usbcdUsb;
            m_locationID = device.uiLocationID;
            m_usb = device.emUsbType;
            m_device = device.emDeviceType;
            m_layerName = GetLayerString(m_locationID);

            m_flashInfo = new STRUCT_FLASH_INFO();
            m_flashInfo.usPhyBlokcPerIDB = 1;
        }

        /// <summary>
        /// 获取层级名称字符串
        /// </summary>
        public string GetLayerString(uint dwLocationID)
        {
            return $"{(dwLocationID >> 8)}-{(dwLocationID & 0xff)}";
        }

        /// <summary>
        /// 设置设备运行所需的组件
        /// </summary>
        public bool SetObject(RKImage? pImage, RKComm? pComm, RKLog? pLog)
        {
            if (pComm == null) return false;
            m_pImage = pImage;
            m_pComm = pComm;
            m_pLog = pLog;
            m_os = m_pImage?.OsType ?? ENUM_OS_TYPE.RK_OS;
            return true;
        }

        /// <summary>
        /// 在 Maskrom 模式下下载 Boot 程序
        /// </summary>
        public int DownloadBoot()
        {
            if (m_pImage?.m_bootObject == null) return -1;
            var boot = m_pImage.m_bootObject;

            for (byte i = 0; i < boot.Entry471Count; i++)
            {
                if (!boot.GetEntryProperty(ENUM_RKBOOTENTRY.ENTRY471, i, out uint dwSize, out uint dwDelay, out string name))
                    return -2;
                if (dwSize > 0)
                {
                    byte[]? pBuffer = boot.GetEntryData(ENUM_RKBOOTENTRY.ENTRY471, i);
                    if (pBuffer == null) return -3;
                    if (!Boot_VendorRequest(0x0471, pBuffer, dwSize)) return -4;
                    if (dwDelay > 0) Thread.Sleep((int)dwDelay);
                }
            }

            for (byte i = 0; i < boot.Entry472Count; i++)
            {
                if (!boot.GetEntryProperty(ENUM_RKBOOTENTRY.ENTRY472, i, out uint dwSize, out uint dwDelay, out string name))
                    return -2;
                if (dwSize > 0)
                {
                    byte[]? pBuffer = boot.GetEntryData(ENUM_RKBOOTENTRY.ENTRY472, i);
                    if (pBuffer == null) return -3;
                    if (!Boot_VendorRequest(0x0472, pBuffer, dwSize)) return -4;
                    if (dwDelay > 0) Thread.Sleep((int)dwDelay);
                }
            }
            Thread.Sleep(1000); // 等待设备重启并初始化
            return 0;
        }

        private bool Boot_VendorRequest(uint requestCode, byte[] pBuffer, uint dwDataSize)
        {
            int iRet = m_pComm?.RKU_DeviceRequest(requestCode, pBuffer, dwDataSize) ?? -1;
            return iRet == RKCommConstants.ERR_SUCCESS;
        }

        /// <summary>
        /// 测试设备是否就绪
        /// </summary>
        public bool TestDevice()
        {
            int iResult = -1;
            uint dwTotal = 0, dwCurrent = 0;
            ENUM_CALL_STEP emCallStep = ENUM_CALL_STEP.CALL_FIRST;
            
            do
            {
                int iTryCount = 3;
                while (iTryCount > 0)
                {
                    iResult = m_pComm?.RKU_TestDeviceReady(out dwTotal, out dwCurrent) ?? -1;
                    if (iResult == RKCommConstants.ERR_SUCCESS || iResult == RKCommConstants.ERR_DEVICE_UNREADY)
                        break;
                    iTryCount--;
                    Thread.Sleep(1000);
                }
                
                if (iResult == RKCommConstants.ERR_SUCCESS)
                {
                    if (emCallStep == ENUM_CALL_STEP.CALL_MIDDLE)
                    {
                        m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.TESTDEVICE_PROGRESS, 100, 100, ENUM_CALL_STEP.CALL_LAST);
                    }
                    break;
                }

                if (iResult == RKCommConstants.ERR_DEVICE_UNREADY)
                {
                    m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.TESTDEVICE_PROGRESS, dwTotal, dwCurrent, emCallStep);
                    emCallStep = ENUM_CALL_STEP.CALL_MIDDLE;
                    Thread.Sleep(1000);
                }
                else return false;

            } while (iResult == RKCommConstants.ERR_DEVICE_UNREADY);

            return true;
        }

        /// <summary>
        /// 重启
        /// </summary>
        public bool ResetDevice()
        {
            int iRet = m_pComm?.RKU_ResetDevice() ?? -1;
            return iRet == RKCommConstants.ERR_SUCCESS || iRet == -2 || iRet == -4;
        }

        /// <summary>
        /// 关机
        /// </summary>
        public bool PowerOffDevice()
        {
            int iRet = m_pComm?.RKU_ResetDevice(RESET_SUBCODE.RST_POWEROFF_SUBCODE) ?? -1;
            return iRet == RKCommConstants.ERR_SUCCESS;
        }

        /// <summary>
        /// 检查物理芯片型号是否与预期匹配。
        /// </summary>
        public bool CheckChip()
        {
            byte[] chipInfo = new byte[16];
            int iRet = m_pComm?.RKU_ReadChipInfo(chipInfo) ?? -1;
            if (iRet != RKCommConstants.ERR_SUCCESS) return false;

            uint chipId = BitConverter.ToUInt32(chipInfo, 0);
            ENUM_RKDEVICE_TYPE curDeviceType = ENUM_RKDEVICE_TYPE.RKNONE_DEVICE;

            if (chipId == (uint)m_device) return true;
            
            switch (chipId)
            {
                case 0x524B3237: curDeviceType = ENUM_RKDEVICE_TYPE.RK27_DEVICE; break;
                case 0x32373341: curDeviceType = ENUM_RKDEVICE_TYPE.RKCAYMAN_DEVICE; break;
                case 0x524B3238: curDeviceType = ENUM_RKDEVICE_TYPE.RK28_DEVICE; break;
                case 0x32383158: curDeviceType = ENUM_RKDEVICE_TYPE.RK281X_DEVICE; break;
                case 0x32383242: curDeviceType = ENUM_RKDEVICE_TYPE.RKPANDA_DEVICE; break;
                case 0x32393058: curDeviceType = ENUM_RKDEVICE_TYPE.RK29_DEVICE; break;
                case 0x32393258: curDeviceType = ENUM_RKDEVICE_TYPE.RK292X_DEVICE; break;
                case 0x33303041: curDeviceType = ENUM_RKDEVICE_TYPE.RK30_DEVICE; break;
                case 0x33313041: curDeviceType = ENUM_RKDEVICE_TYPE.RK30B_DEVICE; break;
                case 0x33313042: curDeviceType = ENUM_RKDEVICE_TYPE.RK31_DEVICE; break;
                case 0x33323041: curDeviceType = ENUM_RKDEVICE_TYPE.RK32_DEVICE; break;
                case 0x32363243: curDeviceType = ENUM_RKDEVICE_TYPE.RKSMART_DEVICE; break;
                case 0x6E616E6F: curDeviceType = ENUM_RKDEVICE_TYPE.RKNANO_DEVICE; break;
                case 0x4E4F5243: curDeviceType = ENUM_RKDEVICE_TYPE.RKCROWN_DEVICE; break;
            }

            return curDeviceType == m_device;
        }

        /// <summary>
        /// 从设备读取闪存参数信息。
        /// </summary>
        public bool GetFlashInfo()
        {
            byte[] infoBytes = new byte[512];
            int iRet = m_pComm?.RKU_ReadFlashInfo(infoBytes, out uint uiRead) ?? -1;
            if (iRet != RKCommConstants.ERR_SUCCESS) return false;

            GCHandle handle = GCHandle.Alloc(infoBytes, GCHandleType.Pinned);
            STRUCT_FLASHINFO_CMD cmd = Marshal.PtrToStructure<STRUCT_FLASHINFO_CMD>(handle.AddrOfPinnedObject());
            handle.Free();

            if (cmd.usBlockSize == 0 || cmd.bPageSize == 0) return false;

            m_flashInfo.szManufacturerName = cmd.bManufCode <= 7 ? szManufName[cmd.bManufCode] : "UNKNOWN";
            m_flashInfo.uiFlashSize = cmd.uiFlashSize / 2 / 1024;
            m_flashInfo.uiPageSize = (uint)cmd.bPageSize / 2;
            m_flashInfo.usBlockSize = (ushort)(cmd.usBlockSize / 2);
            m_flashInfo.bECCBits = cmd.bECCBits;
            m_flashInfo.bAccessTime = cmd.bAccessTime;
            m_flashInfo.uiBlockNum = m_flashInfo.uiFlashSize * 1024 / m_flashInfo.usBlockSize;
            m_flashInfo.uiSectorPerBlock = cmd.usBlockSize;
            m_flashInfo.bFlashCS = cmd.bFlashCS;
            m_flashInfo.usValidSecPerBlock = (ushort)((cmd.usBlockSize / cmd.bPageSize) * 4);

            byte[] flashId = new byte[5];
            iRet = m_pComm?.RKU_ReadFlashID(flashId) ?? -1;
            if (iRet == RKCommConstants.ERR_SUCCESS)
            {
                m_bEmmc = BitConverter.ToUInt32(flashId, 0) == 0x434d4d45;
            }

            return true;
        }

        /// <summary>
        /// 执行带子代码请求的设备重置。
        /// </summary>
        public int ResetDevice(RESET_SUBCODE dwSubCode = RESET_SUBCODE.RST_NONE_SUBCODE)
        {
            if (m_pComm == null) return -1;
            return m_pComm.RKU_ResetDevice(dwSubCode);
        }

        /// <summary>
        /// 向设备指定 LBA 起始扇区写入文件内容。
        /// </summary>
        public bool WriteLBA(uint uiBegin, string szFile)
        {
            if (m_pComm == null) return false;
            if (!System.IO.File.Exists(szFile)) return false;

            using (var fs = new System.IO.FileStream(szFile, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                long iFileSize = fs.Length;
                long iTotalWrite = 0;
                int nSectorSize = 512;
                int sectorsPerLoop = 128; 
                byte[] pBuf = new byte[nSectorSize * sectorsPerLoop];
                ENUM_CALL_STEP emCallStep = ENUM_CALL_STEP.CALL_FIRST;

                while (iTotalWrite < iFileSize)
                {
                    int bytesRead = fs.Read(pBuf, 0, pBuf.Length);
                    if (bytesRead <= 0) break;

                    uint uiLen = (uint)((bytesRead + 511) / 512);
                    int iRet = m_pComm.RKU_WriteLBA(uiBegin, uiLen, pBuf);
                    if (iRet != RKCommConstants.ERR_SUCCESS)
                    {
                        m_pLog?.Record($"Error: RKU_WriteLBA failed, err={iRet}");
                        return false;
                    }

                    uiBegin += uiLen;
                    iTotalWrite += bytesRead;

                    m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.DOWNLOADIMAGE_PROGRESS, iFileSize, iTotalWrite, emCallStep);
                    emCallStep = ENUM_CALL_STEP.CALL_MIDDLE;
                }
                m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.DOWNLOADIMAGE_PROGRESS, iFileSize, iFileSize, ENUM_CALL_STEP.CALL_LAST);
            }
            return true;
        }

        /// <summary>
        /// 从设备指定 LBA 扇区读取数据并保存到文件。
        /// </summary>
        public bool ReadLBA(uint uiBegin, uint uiLen, string szFile)
        {
            if (m_pComm == null) return false;

            using (var fs = new System.IO.FileStream(szFile, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            {
                uint uiTotalLen = uiLen;
                uint uiReadLen = 0;
                int nSectorSize = 512;
                int sectorsPerLoop = 128; 
                byte[] pBuf = new byte[nSectorSize * sectorsPerLoop];
                ENUM_CALL_STEP emCallStep = ENUM_CALL_STEP.CALL_FIRST;

                while (uiReadLen < uiTotalLen)
                {
                    uint uiStepLen = Math.Min(uiTotalLen - uiReadLen, (uint)sectorsPerLoop);
                    int iRet = m_pComm.RKU_ReadLBA(uiBegin, uiStepLen, pBuf);
                    if (iRet != RKCommConstants.ERR_SUCCESS)
                    {
                        m_pLog?.Record($"Error: RKU_ReadLBA failed, err={iRet}");
                        return false;
                    }

                    fs.Write(pBuf, 0, (int)uiStepLen * 512);

                    uiBegin += uiStepLen;
                    uiReadLen += uiStepLen;

                    m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.CHECKIMAGE_PROGRESS, (long)uiTotalLen * 512, (long)uiReadLen * 512, emCallStep);
                    emCallStep = ENUM_CALL_STEP.CALL_MIDDLE;
                }
                m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.CHECKIMAGE_PROGRESS, (long)uiTotalLen * 512, (long)uiReadLen * 512, ENUM_CALL_STEP.CALL_LAST);
            }
            return true;
        }

        /// <summary>
        /// 获取闪存参数
        /// </summary>
        public string GetFlashInfoString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Manufacturer: {m_flashInfo.szManufacturerName}");
            sb.AppendLine($"Flash Size: {m_flashInfo.uiFlashSize} MB");
            sb.AppendLine($"Block Size: {m_flashInfo.usBlockSize} KB");
            sb.AppendLine($"Page Size: {m_flashInfo.uiPageSize} KB");
            sb.AppendLine($"Sector Per Block: {m_flashInfo.uiSectorPerBlock}");
            sb.AppendLine($"Block Num: {m_flashInfo.uiBlockNum}");
            return sb.ToString();
        }

        /// <summary>
        /// 擦除全片
        /// </summary>
        public int EraseAllBlocks(bool force_block_erase = false)
        {
            ReadCapability();
            if (!force_block_erase && (m_bEmmc || m_bDirectLba))
            {
                if (!EraseEmmc()) return -1;
                return 0;
            }

            int iCSIndex = 0;
            byte bCSCount = 0;
            for (int i = 0; i < 8; i++) if ((m_flashInfo.bFlashCS & (1 << i)) != 0) bCSCount++;

            ENUM_CALL_STEP emCallStep = ENUM_CALL_STEP.CALL_FIRST;
            for (byte i = 0; i < 8; i++)
            {
                if ((m_flashInfo.bFlashCS & (1 << i)) != 0)
                {
                    uint uiBlockCount = m_flashInfo.uiBlockNum;
                    uint iErasePos = 0;
                    int iEraseTimes = 0;
                    while (uiBlockCount > 0)
                    {
                        uint iEraseBlockNum = Math.Min(uiBlockCount, (uint)RKCommConstants.MAX_ERASE_BLOCKS);
                        int iRet = m_pComm?.RKU_EraseBlock(i, iErasePos, iEraseBlockNum, (byte)USB_OPERATION_CODE.ERASE_FORCE) ?? -1;
                        if (iRet != RKCommConstants.ERR_SUCCESS && iRet != RKCommConstants.ERR_FOUND_BAD_BLOCK) return -1;

                        iErasePos += iEraseBlockNum;
                        uiBlockCount -= iEraseBlockNum;
                        iEraseTimes++;
                        if (iEraseTimes % 8 == 0)
                        {
                            m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.ERASEFLASH_PROGRESS, (long)m_flashInfo.uiBlockNum * bCSCount, (long)iCSIndex * m_flashInfo.uiBlockNum + iErasePos, emCallStep);
                            emCallStep = ENUM_CALL_STEP.CALL_MIDDLE;
                        }
                    }
                    iCSIndex++;
                }
            }
            m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.ERASEFLASH_PROGRESS, (long)m_flashInfo.uiBlockNum * bCSCount, (long)iCSIndex * m_flashInfo.uiBlockNum, ENUM_CALL_STEP.CALL_LAST);
            return 0;
        }

        private bool EraseEmmc()
        {
            uint uiTotalCount = m_flashInfo.uiFlashSize * 2 * 1024;
            uint uiCount = uiTotalCount;
            uint uiSectorOffset = 0;
            uint uiEraseSize = 1024 * 32;
            int iLoopTimes = 0;
            ENUM_CALL_STEP emCallStep = ENUM_CALL_STEP.CALL_FIRST;

            while (uiCount > 0)
            {
                uint uiEraseCount = Math.Min(uiCount, uiEraseSize);
                int iRet = m_pComm?.RKU_EraseLBA(uiSectorOffset, uiEraseCount) ?? -1;
                if (iRet != RKCommConstants.ERR_SUCCESS) return false;

                uiCount -= uiEraseCount;
                uiSectorOffset += uiEraseCount;
                iLoopTimes++;
                if (iLoopTimes % 8 == 0)
                {
                    m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.ERASEFLASH_PROGRESS, uiTotalCount, uiSectorOffset, emCallStep);
                    emCallStep = ENUM_CALL_STEP.CALL_MIDDLE;
                }
            }
            m_callBackProc?.Invoke(m_locationID, ENUM_PROGRESS_PROMPT.ERASEFLASH_PROGRESS, uiTotalCount, uiTotalCount, ENUM_CALL_STEP.CALL_LAST);
            return true;
        }

        private bool ReadCapability()
        {
            byte[] data = new byte[8];
            int ret = m_pComm?.RKU_ReadCapability(data) ?? -1;
            if (ret != RKCommConstants.ERR_SUCCESS) return false;

            m_bDirectLba = (data[0] & 0x1) != 0;
            m_bFirst4mAccess = (data[0] & 0x4) != 0;
            return true;
        }
    }
}

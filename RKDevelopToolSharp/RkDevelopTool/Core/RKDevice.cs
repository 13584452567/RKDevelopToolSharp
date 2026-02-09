using RkDevelopTool.Models;
using RkDevelopTool.Utils;
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

        public int EraseEmmcBlock(byte flashCs, uint pos, uint count)
        {
            if (m_comm == null) return -1;
            uint sectorOffset, nWrittenBlock = 0;
            byte[] emptyData = new byte[4 * (512 + 16)]; // SECTOR_SIZE + SPARE_SIZE
            Array.Fill<byte>(emptyData, 0xff);

            while (count > 0)
            {
                sectorOffset = (flashCs * m_flashInfo.BlockNum + pos + nWrittenBlock) * m_flashInfo.SectorPerBlock;
                int iRet = m_comm.WriteSector(sectorOffset, 4, emptyData);
                if (iRet != 0 && iRet != 10) // ERR_SUCCESS = 0, ERR_FOUND_BAD_BLOCK = 10
                {
                    m_log?.Record($"<LAYER {m_layerName}> ERROR:EraseEmmcBlock-->RKU_WriteSector failed, RetCode({iRet})");
                    return iRet;
                }
                count--;
                nWrittenBlock++;
            }
            return 0;
        }

        public int EraseEmmcByWriteLBA(uint sectorPos, uint count)
        {
            if (m_comm == null) return -1;
            uint nWritten;
            byte[] emptyData = new byte[32 * 512]; // 32 * SECTOR_SIZE
            Array.Fill<byte>(emptyData, 0xff);

            while (count > 0)
            {
                nWritten = Math.Min(count, 32u);
                int iRet = m_comm.WriteLBA(sectorPos, nWritten, emptyData);
                if (iRet != 0)
                {
                    m_log?.Record($"<LAYER {m_layerName}> ERROR:EraseEmmcByWriteLBA-->RKU_WriteLBA failed, RetCode({iRet})");
                    return iRet;
                }
                count -= nWritten;
                sectorPos += nWritten;
            }
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

        public int UpgradeLoader(string loaderPath)
        {
            if (m_comm == null) return -1;
            RKImage image = new RKImage();
            if (!image.LoadImage(loaderPath)) return -2;

            RKBoot? boot = image.BootObject;
            if (boot == null) return -3;

            int index;
            uint dwLoaderSize, dwLoaderDataSize, dwLoaderHeadSize = 0, dwDelay;
            byte[]? loaderCodeBuffer, loaderDataBuffer, loaderHeadBuffer = null;
            bool bNewIDBlock = false;

            index = boot.GetIndexByName(RkBootEntryType.EntryLoader, "FlashBoot");
            if (index == -1) return -4;
            boot.GetEntryProperty(RkBootEntryType.EntryLoader, (byte)index, out dwLoaderSize, out dwDelay, out _);
            loaderCodeBuffer = boot.GetEntryData(RkBootEntryType.EntryLoader, (byte)index);
            if (loaderCodeBuffer == null) return -5;

            index = boot.GetIndexByName(RkBootEntryType.EntryLoader, "FlashData");
            if (index == -1) return -6;
            boot.GetEntryProperty(RkBootEntryType.EntryLoader, (byte)index, out dwLoaderDataSize, out dwDelay, out _);
            loaderDataBuffer = boot.GetEntryData(RkBootEntryType.EntryLoader, (byte)index);
            if (loaderDataBuffer == null) return -7;

            index = boot.GetIndexByName(RkBootEntryType.EntryLoader, "FlashHead");
            if (index != -1)
            {
                boot.GetEntryProperty(RkBootEntryType.EntryLoader, (byte)index, out dwLoaderHeadSize, out dwDelay, out _);
                loaderHeadBuffer = boot.GetEntryData(RkBootEntryType.EntryLoader, (byte)index);

                byte[] capability = new byte[8];
                int iRetCap = m_comm.ReadCapability(capability);
                if (iRetCap != 0 || (capability[1] & 1) == 0)
                {
                    return -8; // Device does not support new IDB
                }
                bNewIDBlock = true;
            }

            uint usFlashDataSec = (uint)((dwLoaderDataSize + 2047) / 2048 * 2048 / 512);
            uint usFlashBootSec = (uint)((dwLoaderSize + 2047) / 2048 * 2048 / 512);
            uint dwSectorNum;
            byte[] pIDBData;

            if (bNewIDBlock && loaderHeadBuffer != null)
            {
                uint usFlashHeadSec = (uint)((dwLoaderHeadSize + 2047) / 2048 * 2048 / 512);
                dwSectorNum = usFlashHeadSec + usFlashDataSec + usFlashBootSec;
                pIDBData = new byte[dwSectorNum * 512];

                if (boot.Rc4DisableFlag)
                {
                    for (int i = 0; i < (int)(dwLoaderHeadSize / 512); i++)
                        CRCUtils.P_RC4(loaderHeadBuffer.AsSpan(i * 512, 512));
                    for (int i = 0; i < (int)(dwLoaderDataSize / 512); i++)
                        CRCUtils.P_RC4(loaderDataBuffer.AsSpan(i * 512, 512));
                    for (int i = 0; i < (int)(dwLoaderSize / 512); i++)
                        CRCUtils.P_RC4(loaderCodeBuffer.AsSpan(i * 512, 512));
                }

                Array.Copy(loaderHeadBuffer, 0, pIDBData, 0, loaderHeadBuffer.Length);
                Array.Copy(loaderDataBuffer, 0, pIDBData, (int)(512 * usFlashHeadSec), loaderDataBuffer.Length);
                Array.Copy(loaderCodeBuffer, 0, pIDBData, (int)(512 * (usFlashHeadSec + usFlashDataSec)), loaderCodeBuffer.Length);
            }
            else
            {
                dwSectorNum = 4 + usFlashDataSec + usFlashBootSec;
                pIDBData = new byte[dwSectorNum * 512];
                int iRetIDB = MakeIDBlockData(loaderDataBuffer, loaderCodeBuffer, pIDBData, (ushort)usFlashDataSec, (ushort)usFlashBootSec, dwLoaderDataSize, dwLoaderSize, !boot.Rc4DisableFlag);
                if (iRetIDB != 0) return -9;
            }

            // Write IDB at LBA 64
            int iRetWrite = m_comm.WriteLBA(64, dwSectorNum, pIDBData);
            return iRetWrite;
        }

        private int MakeIDBlockData(byte[] pDDR, byte[] pLoader, byte[] lpIDBlock, ushort usFlashDataSec, ushort usFlashBootSec, uint dwLoaderDataSize, uint dwLoaderSize, bool rc4Flag)
        {
            Rk28IdbSec0 sec0 = new Rk28IdbSec0();
            Rk28IdbSec1 sec1 = new Rk28IdbSec1();
            Rk28IdbSec2 sec2 = new Rk28IdbSec2();
            Rk28IdbSec3 sec3 = new Rk28IdbSec3();

            MakeSector0(ref sec0, usFlashDataSec, usFlashBootSec, rc4Flag);
            MakeSector1(ref sec1);
            MakeSector2(ref sec2);
            // sec3 is all zeros

            sec2.Sec0Crc = CRCUtils.CRC_16(StructToBytes(sec0));
            sec2.Sec1Crc = CRCUtils.CRC_16(StructToBytes(sec1));
            sec2.Sec3Crc = CRCUtils.CRC_16(StructToBytes(sec3));

            Array.Copy(StructToBytes(sec0), 0, lpIDBlock, 0, 512);
            Array.Copy(StructToBytes(sec1), 0, lpIDBlock, 512, 512);
            // Sector 2 is written later after BootCodeCRC
            Array.Copy(StructToBytes(sec3), 0, lpIDBlock, 512 * 3, 512);

            if (rc4Flag)
            {
                for (int i = 0; i < (int)(dwLoaderDataSize / 512); i++)
                    CRCUtils.P_RC4(pDDR.AsSpan(i * 512, 512));
                for (int i = 0; i < (int)(dwLoaderSize / 512); i++)
                    CRCUtils.P_RC4(pLoader.AsSpan(i * 512, 512));
            }

            Array.Copy(pDDR, 0, lpIDBlock, 512 * 4, (int)dwLoaderDataSize);
            Array.Copy(pLoader, 0, lpIDBlock, 512 * (4 + usFlashDataSec), (int)dwLoaderSize);

            sec2.BootCodeCrc = CRCUtils.CRC_32(lpIDBlock.AsSpan(512 * 4, (int)(sec0.BootCodeSize * 512)));
            Array.Copy(StructToBytes(sec2), 0, lpIDBlock, 512 * 2, 512);

            for (int i = 0; i < 4; i++)
            {
                if (i == 1) continue;
                CRCUtils.P_RC4(lpIDBlock.AsSpan(i * 512, 512));
            }

            return 0;
        }

        private unsafe void MakeSector0(ref Rk28IdbSec0 sec0, ushort usFlashDataSec, ushort usFlashBootSec, bool rc4Flag)
        {
            sec0.Tag = 0x0FF0AA55;
            sec0.Rc4Flag = rc4Flag ? 1u : 0u;
            sec0.BootCode1Offset = 0x4;
            sec0.BootCode2Offset = 0x4;
            sec0.BootDataSize = usFlashDataSec;
            sec0.BootCodeSize = (ushort)(usFlashDataSec + usFlashBootSec);
        }

        private unsafe void MakeSector1(ref Rk28IdbSec1 sec1)
        {
            sec1.SysReservedBlock = 0xC;
            sec1.Disk0Size = 0xFFFF;
            sec1.ChipTag = 0x38324B52; // "RK28"
        }

        private unsafe void MakeSector2(ref Rk28IdbSec2 sec2)
        {
            sec2.VcTag[0] = (byte)'V';
            sec2.VcTag[1] = (byte)'C';
            sec2.CrcTag[0] = (byte)'C';
            sec2.CrcTag[1] = (byte)'R';
            sec2.CrcTag[2] = (byte)'C';
        }

        private byte[] StructToBytes<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(str, ptr, false);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        public bool WriteSparseLBA(uint begin, string file)
        {
            if (m_comm == null) return false;

            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                byte[] headerBytes = reader.ReadBytes(Marshal.SizeOf<SparseHeader>());
                if (headerBytes.Length < Marshal.SizeOf<SparseHeader>()) return false;

                SparseHeader header = MemoryMarshal.Read<SparseHeader>(headerBytes);
                if (header.Magic != Constants.SPARSE_HEADER_MAGIC) return false;

                uint uiBegin = begin;
                long iTotalWrite = 0;
                long iFileSize = (long)header.BlockSize * header.TotalBlocks;

                for (int curChunk = 0; curChunk < header.TotalChunks; curChunk++)
                {
                    byte[] chunkHeaderBytes = reader.ReadBytes(Marshal.SizeOf<ChunkHeader>());
                    if (chunkHeaderBytes.Length < Marshal.SizeOf<ChunkHeader>()) break;

                    ChunkHeader chunk = MemoryMarshal.Read<ChunkHeader>(chunkHeaderBytes);

                    switch (chunk.ChunkType)
                    {
                        case Constants.CHUNK_TYPE_RAW:
                            uint dwChunkDataSize = chunk.TotalSize - (uint)Marshal.SizeOf<ChunkHeader>();
                            while (dwChunkDataSize > 0)
                            {
                                uint dwTransferBytes = Math.Min(dwChunkDataSize, 65536); // DEFAULT_RW_LBA * 512
                                byte[] pBuf = reader.ReadBytes((int)dwTransferBytes);
                                if (pBuf.Length < dwTransferBytes) break;

                                uint uiTransferSec = (dwTransferBytes + 511) / 512;
                                int ret = m_comm.WriteLBA(uiBegin, uiTransferSec, pBuf);
                                if (ret != RKCommConstants.ERR_SUCCESS) return false;

                                dwChunkDataSize -= dwTransferBytes;
                                iTotalWrite += dwTransferBytes;
                                uiBegin += uiTransferSec;
                            }
                            break;

                        case Constants.CHUNK_TYPE_FILL:
                            uint dwFillByte = reader.ReadUInt32();
                            uint fillSize = chunk.ChunkSize * header.BlockSize;
                            byte[] fillBuf = new byte[65536];
                            for (int i = 0; i < fillBuf.Length / 4; i++)
                            {
                                BitConverter.GetBytes(dwFillByte).CopyTo(fillBuf, i * 4);
                            }

                            while (fillSize > 0)
                            {
                                uint dwTransferBytes = Math.Min(fillSize, (uint)fillBuf.Length);
                                uint uiTransferSec = (dwTransferBytes + 511) / 512;
                                int ret = m_comm.WriteLBA(uiBegin, uiTransferSec, fillBuf);
                                if (ret != RKCommConstants.ERR_SUCCESS) return false;

                                fillSize -= dwTransferBytes;
                                iTotalWrite += dwTransferBytes;
                                uiBegin += uiTransferSec;
                            }
                            break;

                        case Constants.CHUNK_TYPE_DONT_CARE:
                            uint skipSize = chunk.ChunkSize * header.BlockSize;
                            iTotalWrite += skipSize;
                            uiBegin += skipSize / 512;
                            break;

                        case Constants.CHUNK_TYPE_CRC32:
                            reader.ReadUInt32(); // Skip CRC
                            break;
                    }
                }
            }

            return true;
        }

        public bool WriteParameter(string parameterPath)
        {
            if (m_comm == null) return false;
            if (!File.Exists(parameterPath)) return false;

            string paramStr = File.ReadAllText(parameterPath);
            byte[] paramData = ParameterUtils.CreateParameterBuffer(paramStr);
            if (paramData == null) return false;

            // Parameter is usually written at LBA 0
            int ret = m_comm.WriteLBA(0, (uint)(paramData.Length / 512), paramData);
            return ret == RKCommConstants.ERR_SUCCESS;
        }

        public bool WriteGpt(string parameterPath)
        {
            if (m_comm == null) return false;
            string paramStr = File.ReadAllText(parameterPath);
            var partitions = ParameterUtils.ParsePartitions(paramStr);
            if (partitions.Count == 0) return false;

            byte[] gptBuffer = GptUtils.CreateGptBuffer(partitions, 0x200000); // 1GB default or actual disk size if known
            if (gptBuffer == null) return false;

            // GPT header starts at LBA 0 (PMBR) then GPT at LBA 1
            int ret = m_comm.WriteLBA(0, (uint)(gptBuffer.Length / 512), gptBuffer);
            return ret == RKCommConstants.ERR_SUCCESS;
        }
    }
}

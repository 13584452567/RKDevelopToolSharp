using LibUsbDotNet;
using LibUsbDotNet.Main;
using RkDevelopTool.Models;
using RkDevelopTool.Utils;
using System.Runtime.InteropServices;

namespace RkDevelopTool.Core
{
/// <summary>
    /// 基于 USB 接口的通讯实现类，使用 LibUsbDotNet 与物理设备进行 Bulk 传输。
    /// </summary>
    public class RKUsbComm : RKComm, IDisposable
    {
        private UsbDevice? m_usbDevice;
        private UsbEndpointWriter? m_writer;
        private UsbEndpointReader? m_reader;
        private int m_interfaceNum = -1;

        public RKUsbComm(STRUCT_RKDEVICE_DESC devDesc, RKLog? pLog, out bool bRet) : base(pLog)
        {
            bRet = InitializeUsb(devDesc);
        }

        /// <summary>
        /// 初始化 USB 连接，查找并打开设备，声明接口并开启端点。
        /// </summary>
        public bool InitializeUsb(STRUCT_RKDEVICE_DESC devDesc)
        {
            m_deviceDesc = devDesc;
            
            var finder = new UsbDeviceFinder(devDesc.usVid, devDesc.usPid);
            m_usbDevice = UsbDevice.OpenUsbDevice(finder);

            if (m_usbDevice == null)
            {
                m_log?.Record("Error:InitializeUsb-->open device failed");
                return false;
            }

            if (m_usbDevice is IUsbDevice wholeUsbDevice)
            {
                // 设置配置并声明接口
                wholeUsbDevice.SetConfiguration(1);
                m_interfaceNum = 0; 
                wholeUsbDevice.ClaimInterface(m_interfaceNum);
            }

            // 打开写入和读取端点（通常 Rockchip 设备使用 EP1）
            m_writer = m_usbDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
            m_reader = m_usbDevice.OpenEndpointReader(ReadEndpointID.Ep01, 4096);

            return true;
        }

        public void UninitializeUsb()
        {
            if (m_usbDevice != null)
            {
                if (m_usbDevice.IsOpen)
                {
                    if (m_usbDevice is IUsbDevice wholeUsbDevice)
                    {
                        wholeUsbDevice.ReleaseInterface(m_interfaceNum);
                    }
                    m_usbDevice.Close();
                }
                m_usbDevice = null;
            }
        }

        public override void Dispose()
        {
            UninitializeUsb();
        }

        public override bool Reset_Usb_Config(STRUCT_RKDEVICE_DESC devDesc)
        {
            UninitializeUsb();
            return InitializeUsb(devDesc);
        }

        public override bool Reset_Usb_Device()
        {
            // 在 LibUsbDotNet 中，简单的重置通常通过重新打开设备实现。
            // 所以 LibUsbDotNet 什么时候发布 3.x 的 Release 呢？
            return m_usbDevice != null && m_usbDevice.IsOpen;
        }

        /// <summary>
        /// 从 USB 设备读取指定长度的数据。
        /// </summary>
        public override bool RKU_Read(byte[] lpBuffer, uint dwSize)
        {
            int transferred;
            ErrorCode ec = m_reader!.Read(lpBuffer, 5000, out transferred);
            if (ec != ErrorCode.None || transferred != dwSize)
            {
                m_log?.Record($"Error:RKU_Read failed, ec={ec}, size={dwSize}, read={transferred}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从 USB 设备读取数据，但不强制检查读取长度是否等于预期长度。
        /// </summary>
        public uint RKU_Read_EX(byte[] lpBuffer, uint dwSize)
        {
            int transferred;
            ErrorCode ec = m_reader!.Read(lpBuffer, 5000, out transferred);
            if (ec != ErrorCode.None)
            {
                m_log?.Record($"Error:RKU_Read_EX failed, ec={ec}, size={dwSize}");
                return 0;
            }
            return (uint)transferred;
        }

        /// <summary>
        /// 向 USB 设备写入指定长度的数据。
        /// </summary>
        public override bool RKU_Write(byte[] lpBuffer, uint dwSize)
        {
            int transferred;
            ErrorCode ec = m_writer!.Write(lpBuffer, 5000, out transferred);
            if (ec != ErrorCode.None || transferred != dwSize)
            {
                m_log?.Record($"Error:RKU_Write failed, ec={ec}, size={dwSize}, write={transferred}");
                return false;
            }
            return true;
        }

        private uint MakeCBWTag()
        {
            Random rng = new Random();
            byte[] bytes = new byte[4];
            rng.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// 根据操作码初始化命令块包装 (CBW)。
        /// </summary>
        private void InitializeCBW(ref CBW pCBW, USB_OPERATION_CODE code)
        {
            pCBW.dwCBWSignature = RKCommConstants.CBW_SIGN;
            pCBW.dwCBWTag = MakeCBWTag();
            pCBW.cbwcb.ucOperCode = (byte)code;

            switch (code)
            {
                case USB_OPERATION_CODE.TEST_UNIT_READY:
                case USB_OPERATION_CODE.READ_FLASH_ID:
                case USB_OPERATION_CODE.READ_FLASH_INFO:
                case USB_OPERATION_CODE.READ_CHIP_INFO:
                case USB_OPERATION_CODE.READ_EFUSE:
                case USB_OPERATION_CODE.READ_CAPABILITY:
                case USB_OPERATION_CODE.READ_STORAGE:
                    pCBW.ucCBWFlags = RKCommConstants.DIRECTION_IN;
                    pCBW.ucCBWCBLength = 0x06;
                    break;
                case USB_OPERATION_CODE.DEVICE_RESET:
                case USB_OPERATION_CODE.ERASE_SYSTEMDISK:
                case USB_OPERATION_CODE.SET_RESET_FLAG:
                case USB_OPERATION_CODE.CHANGE_STORAGE:
                    pCBW.ucCBWFlags = RKCommConstants.DIRECTION_OUT;
                    pCBW.ucCBWCBLength = 0x06;
                    break;
                case USB_OPERATION_CODE.TEST_BAD_BLOCK:
                case USB_OPERATION_CODE.READ_SECTOR:
                case USB_OPERATION_CODE.READ_LBA:
                case USB_OPERATION_CODE.READ_SDRAM:
                case USB_OPERATION_CODE.READ_SPI_FLASH:
                case USB_OPERATION_CODE.READ_NEW_EFUSE:
                    pCBW.ucCBWFlags = RKCommConstants.DIRECTION_IN;
                    pCBW.ucCBWCBLength = 0x0a;
                    break;
                case USB_OPERATION_CODE.WRITE_SECTOR:
                case USB_OPERATION_CODE.WRITE_LBA:
                case USB_OPERATION_CODE.WRITE_SDRAM:
                case USB_OPERATION_CODE.EXECUTE_SDRAM:
                case USB_OPERATION_CODE.ERASE_NORMAL:
                case USB_OPERATION_CODE.ERASE_FORCE:
                case USB_OPERATION_CODE.WRITE_EFUSE:
                case USB_OPERATION_CODE.WRITE_SPI_FLASH:
                case USB_OPERATION_CODE.WRITE_NEW_EFUSE:
                case USB_OPERATION_CODE.ERASE_LBA:
                    pCBW.ucCBWFlags = RKCommConstants.DIRECTION_OUT;
                    pCBW.ucCBWCBLength = 0x0a;
                    break;
            }
        }

        private byte[] StructToBytes<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        private T BytesToStruct<T>(byte[] arr) where T : struct
        {
            T str = new T();
            int size = Marshal.SizeOf(str);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(arr, 0, ptr, size);
            str = Marshal.PtrToStructure<T>(ptr);
            Marshal.FreeHGlobal(ptr);
            return str;
        }

        private bool UFI_CHECK_SIGN(CBW cbw, CSW csw)
        {
            return csw.dwCSWSignature == RKCommConstants.CSW_SIGN && csw.dwCSWTag == cbw.dwCBWTag;
        }

        private bool RKU_ClearBuffer(CBW cbw, ref CSW csw)
        {
            uint dwTotalRead = 0;
            int iTryCount = 3;
            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            do
            {
                uint dwReadBytes = RKU_Read_EX(cswBytes, (uint)cswBytes.Length);
                if (dwReadBytes == cswBytes.Length)
                {
                    csw = BytesToStruct<CSW>(cswBytes);
                    if (UFI_CHECK_SIGN(cbw, csw))
                        return true;
                }
                else
                {
                    iTryCount--;
                    Thread.Sleep(3000);
                }
                dwTotalRead += dwReadBytes;
                if (dwTotalRead >= RKCommConstants.MAX_CLEAR_LEN)
                    break;
            } while (iTryCount > 0);
            return false;
        }

        public override int RKU_ReadChipInfo(byte[] lpBuffer)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
            {
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;
            }

            CBW cbw = CBW.Create();
            CSW csw = new CSW();

            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_CHIP_INFO);
            cbw.dwCBWTransferLength = 16;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!RKU_Read(lpBuffer, 16))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ReadFlashID(byte[] lpBuffer)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_FLASH_ID);
            cbw.dwCBWTransferLength = 5;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!RKU_Read(lpBuffer, 5))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ReadFlashInfo(byte[] lpBuffer, out uint puiRead)
        {
            puiRead = 0;
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_FLASH_INFO);
            cbw.dwCBWTransferLength = 11;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] infoBuffer = new byte[512];
            uint dwRead = RKU_Read_EX(infoBuffer, 512);
            if (dwRead < 11 || dwRead > 512)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            Array.Copy(infoBuffer, lpBuffer, Math.Min(lpBuffer.Length, (int)dwRead));
            puiRead = dwRead;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ReadCapability(byte[] lpBuffer)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_CAPABILITY);
            cbw.dwCBWTransferLength = 8;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            uint dwRead = RKU_Read_EX(cswBytes, (uint)cswBytes.Length);
            if (dwRead != 8)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            Array.Copy(cswBytes, lpBuffer, 8);

            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ReadLBA(uint dwPos, uint dwCount, byte[] lpBuffer, RW_SUBCODE bySubCode = RW_SUBCODE.RWMETHOD_IMAGE)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_LBA);
            cbw.dwCBWTransferLength = dwCount * 512;
            cbw.cbwcb.dwAddress = EndianUtils.LtoB32(dwPos);
            cbw.cbwcb.usLength = EndianUtils.LtoB16((ushort)dwCount);
            cbw.cbwcb.ucReserved = (byte)bySubCode;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!RKU_Read(lpBuffer, dwCount * 512))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_WriteLBA(uint dwPos, uint dwCount, byte[] lpBuffer, RW_SUBCODE bySubCode = RW_SUBCODE.RWMETHOD_IMAGE)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.WRITE_LBA);
            uint dwTotal = dwCount * 512;
            cbw.dwCBWTransferLength = dwTotal;
            cbw.cbwcb.dwAddress = EndianUtils.LtoB32(dwPos);
            cbw.cbwcb.usLength = EndianUtils.LtoB16((ushort)dwCount);
            cbw.cbwcb.ucReserved = (byte)bySubCode;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!RKU_Write(lpBuffer, dwTotal))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_WriteSector(uint dwPos, uint dwCount, byte[] lpBuffer)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (dwCount > 32)
                return RKCommConstants.ERR_CROSS_BORDER;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_SECTOR);
            uint dwTotal = dwCount * 528;
            cbw.dwCBWTransferLength = dwTotal;
            cbw.cbwcb.dwAddress = EndianUtils.LtoB32(dwPos);
            cbw.cbwcb.usLength = EndianUtils.LtoB16((ushort)dwCount);

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            if (!RKU_Write(lpBuffer, dwTotal))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_EraseLBA(uint dwPos, uint dwCount)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.ERASE_LBA);
            cbw.cbwcb.dwAddress = EndianUtils.LtoB32(dwPos);
            cbw.cbwcb.usLength = EndianUtils.LtoB16((ushort)dwCount);

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ResetDevice(RESET_SUBCODE bySubCode = RESET_SUBCODE.RST_NONE_SUBCODE)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.DEVICE_RESET);
            cbw.cbwcb.ucReserved = (byte)bySubCode;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
            {
                if (!RKU_ClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ChangeStorage(byte storage)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.CHANGE_STORAGE);
            cbw.cbwcb.ucReserved = storage;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
            {
                if (!RKU_ClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FAILED;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_ReadStorage(byte[] storage)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.READ_STORAGE);
            cbw.dwCBWTransferLength = 4;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] bitsBuffer = new byte[4];
            uint dwRead = RKU_Read_EX(bitsBuffer, 4);
            if (dwRead != 4)
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            uint storage_bits = BitConverter.ToUInt32(bitsBuffer, 0);

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            storage[0] = 255;
            for (byte i = 0; i < 32; i++)
            {
                if ((storage_bits & (1u << i)) != 0)
                {
                    storage[0] = i;
                    break;
                }
            }
            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_TestDeviceReady(out uint dwTotal, out uint dwCurrent, TESTUNIT_SUBCODE bySubCode = TESTUNIT_SUBCODE.TU_NONE_SUBCODE)
        {
            dwTotal = 0;
            dwCurrent = 0;
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, USB_OPERATION_CODE.TEST_UNIT_READY);
            cbw.cbwcb.ucReserved = (byte)bySubCode;

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
            {
                if (!RKU_ClearBuffer(cbw, ref csw))
                    return RKCommConstants.ERR_CMD_NOTMATCH;
            }

            dwCurrent = (csw.dwCBWDataResidue >> 16);
            dwTotal = (csw.dwCBWDataResidue & 0x0000FFFF);

            dwTotal = EndianUtils.BtoL16((ushort)dwTotal);
            dwCurrent = EndianUtils.BtoL16((ushort)dwCurrent);

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_DEVICE_UNREADY;

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_DeviceRequest(uint dwRequest, byte[] lpBuffer, uint dwDataSize)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (dwRequest != 0x0471 && dwRequest != 0x0472)
                return RKCommConstants.ERR_REQUEST_NOT_SUPPORT;

            bool bSendPendPacket = false;
            switch (dwDataSize % 4096)
            {
                case 4095:
                    dwDataSize++;
                    break;
                case 4094:
                    bSendPendPacket = true;
                    break;
            }

            byte[] pData = new byte[dwDataSize + 2];
            Array.Copy(lpBuffer, pData, lpBuffer.Length);

            ushort crcValue = CRCUtils.CRC_CCITT(pData, dwDataSize);
            pData[dwDataSize] = (byte)((crcValue & 0xff00) >> 8);
            pData[dwDataSize + 1] = (byte)(crcValue & 0x00ff);
            uint totalDataSize = dwDataSize + 2;

            uint dwTotalSended = 0;
            if (m_usbDevice == null) return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            while (dwTotalSended < totalDataSize)
            {
                uint nSendBytes = Math.Min(totalDataSize - dwTotalSended, 4096);
                var setup = new UsbSetupPacket(0x40, 0xC, 0, (short)dwRequest, (short)nSendBytes);
                int transferred;
                byte[] tempBuf = new byte[nSendBytes];
                Array.Copy(pData, (int)dwTotalSended, tempBuf, 0, (int)nSendBytes);
                bool success = m_usbDevice.ControlTransfer(ref setup, tempBuf, (int)nSendBytes, out transferred);

                if (!success || transferred != (int)nSendBytes)
                    return RKCommConstants.ERR_REQUEST_FAIL;

                dwTotalSended += nSendBytes;
            }

            if (bSendPendPacket)
            {
                byte[] ucFillByte = new byte[1] { 0 };
                var setup = new UsbSetupPacket(0x40, 0xC, 0, (short)dwRequest, 1);
                int transferred;
                m_usbDevice.ControlTransfer(ref setup, ucFillByte, 1, out transferred);
            }

            return RKCommConstants.ERR_SUCCESS;
        }

        public override int RKU_EraseBlock(byte ucFlashCS, uint dwPos, uint dwCount, byte ucEraseType)
        {
            if (m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_LOADER && m_deviceDesc.emUsbType != ENUM_RKUSB_TYPE.RKUSB_MASKROM)
                return RKCommConstants.ERR_DEVICE_NOT_SUPPORT;

            if (dwCount > RKCommConstants.MAX_ERASE_BLOCKS)
                return RKCommConstants.ERR_CROSS_BORDER;

            CBW cbw = CBW.Create();
            InitializeCBW(ref cbw, (USB_OPERATION_CODE)ucEraseType);
            cbw.ucCBWLUN = ucFlashCS;
            cbw.cbwcb.dwAddress = EndianUtils.LtoB32(dwPos);
            cbw.cbwcb.usLength = EndianUtils.LtoB16((ushort)dwCount);

            if (!RKU_Write(StructToBytes(cbw), (uint)Marshal.SizeOf<CBW>()))
                return RKCommConstants.ERR_DEVICE_WRITE_FAILED;

            byte[] cswBytes = new byte[Marshal.SizeOf<CSW>()];
            if (!RKU_Read(cswBytes, (uint)cswBytes.Length))
                return RKCommConstants.ERR_DEVICE_READ_FAILED;

            var csw = BytesToStruct<CSW>(cswBytes);
            if (!UFI_CHECK_SIGN(cbw, csw))
                return RKCommConstants.ERR_CMD_NOTMATCH;

            if (csw.ucCSWStatus == 1)
                return RKCommConstants.ERR_FOUND_BAD_BLOCK;

            return RKCommConstants.ERR_SUCCESS;
        }
    }
}

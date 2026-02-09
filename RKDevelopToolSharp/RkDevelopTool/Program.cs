using RkDevelopTool.Core;
using RkDevelopTool.Models;

namespace RkDevelopTool.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
                return;
            }

            string command = args[0].ToLower();

            try
            {
                switch (command)
                {
                    case "ld":
                        ListDevices();
                        break;
                    case "db":
                        if (args.Length < 2) { Console.WriteLine("Usage: db <loader>"); return; }
                        DownloadBoot(args[1]);
                        break;
                    case "ul":
                        if (args.Length < 2) { Console.WriteLine("Usage: ul <loader>"); return; }
                        UpgradeLoader(args[1]);
                        break;
                    case "rl":
                        if (args.Length < 4) { Console.WriteLine("Usage: rl <BeginSec> <SectorLen> <File>"); return; }
                        ReadLBA(uint.Parse(args[1]), uint.Parse(args[2]), args[3]);
                        break;
                    case "wl":
                        if (args.Length < 3) { Console.WriteLine("Usage: wl <BeginSec> <File>"); return; }
                        WriteLBA(uint.Parse(args[1]), args[2]);
                        break;
                    case "ef":
                        EraseFlash();
                        break;
                    case "rd":
                        ResetDevice(args.Length > 1 ? uint.Parse(args[1]) : 0);
                        break;
                    case "rfi":
                        ReadFlashInfo();
                        break;
                    case "-h":
                    case "--help":
                        Usage();
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        Usage();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void Usage()
        {
            Console.WriteLine("\r\n---------------------Tool Usage ---------------------");
            Console.WriteLine("Help:\t\t\t-h or --help");
            Console.WriteLine("ListDevice:\t\tld");
            Console.WriteLine("DownloadBoot:\t\tdb <Loader>");
            Console.WriteLine("UpgradeLoader:\t\tul <Loader>");
            Console.WriteLine("ReadLBA:\t\trl  <BeginSec> <SectorLen> <File>");
            Console.WriteLine("WriteLBA:\t\twl  <BeginSec> <File>");
            Console.WriteLine("EraseFlash:\t\tef");
            Console.WriteLine("ResetDevice:\t\trd [subcode]");
            Console.WriteLine("ReadFlashInfo:\t\trfi");
            Console.WriteLine("-------------------------------------------------------\r\n");
        }

        static RKDevice? GetSelectedDevice(int index = 0)
        {
            RKScan scanner = new RKScan();
            int count = scanner.Search(0xFF);
            if (count == 0 || index >= count) return null;

            if (scanner.GetDevice(out RkDeviceDesc desc, index))
            {
                RKDevice device = new RKDevice(desc);
                RKUsbComm comm = new RKUsbComm(desc, null, out bool success);
                if (!success) return null;
                device.SetObject(null, comm, null);
                device.ProgressPromptCallback = ProgressInfoProc;
                return device;
            }
            return null;
        }

        static void ProgressInfoProc(uint deviceLayer, ProgressPrompt promptId, long totalValue, long currentValue, CallStep step)
        {
            string info = promptId.ToString();
            int percent = totalValue > 0 ? (int)(currentValue * 100 / totalValue) : 0;
            Console.Write($"\r{info}: {percent}% ({currentValue}/{totalValue})");
            if (step == CallStep.Last) Console.WriteLine("\nDone.");
        }

        static void ListDevices()
        {
            RKScan scanner = new RKScan();
            int count = scanner.Search(0xFF);
            if (count == 0)
            {
                Console.WriteLine("No devices found.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (scanner.GetDevice(out RkDeviceDesc desc, i))
                {
                    string usbType = desc.UsbType.ToString();
                    Console.WriteLine($"DevNo={i + 1}\tVid=0x{desc.Vid:X4},Pid=0x{desc.Pid:X4},LocationID=0\t{usbType}");
                }
            }
        }

        static void DownloadBoot(string loaderPath)
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }

            RKImage image = new RKImage();
            if (!image.LoadImage(loaderPath)) { Console.WriteLine("Load loader failed."); return; }

            dev.SetObject(image, dev.CommObjectPointer, null);
            Console.WriteLine("Downloading boot...");
            int ret = dev.DownloadBoot();
            Console.WriteLine($"Result: {ret}");
        }

        static void UpgradeLoader(string loaderPath)
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            Console.WriteLine("UpgradeLoader not fully implemented in CLI yet, but business logic is in CORE.");
        }

        static void ReadLBA(uint begin, uint len, string file)
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            Console.WriteLine($"Reading LBA {begin} - {len} to {file}...");
            if (dev.ReadLBA(begin, len, file)) Console.WriteLine("Read LBA Success.");
            else Console.WriteLine("Read LBA Failed.");
        }

        static void WriteLBA(uint begin, string file)
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            Console.WriteLine($"Writing LBA from {file} to {begin}...");
            if (dev.WriteLBA(begin, file)) Console.WriteLine("Write LBA Success.");
            else Console.WriteLine("Write LBA Failed.");
        }

        static void EraseFlash()
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            if (!dev.GetFlashInfo()) { Console.WriteLine("Get Flash Info failed."); return; }
            Console.WriteLine("Erasing flash...");
            dev.EraseAllBlocks();
        }

        static void ResetDevice(uint subcode)
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            Console.WriteLine($"Resetting device with subcode {subcode}...");
            dev.ResetDevice((ResetSubCode)subcode);
        }

        static void ReadFlashInfo()
        {
            var dev = GetSelectedDevice();
            if (dev == null) { Console.WriteLine("Device not found."); return; }
            if (dev.GetFlashInfo())
            {
                Console.WriteLine(dev.GetFlashInfoString());
            }
            else
            {
                Console.WriteLine("Read Flash Info failed.");
            }
        }
    }
}

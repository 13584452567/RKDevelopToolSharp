using Xunit;
using RkDevelopTool.Core;
using System.IO;

namespace RkDevelopTool.Tests
{
    public class RKLogTests
    {
        [Fact]
        public void TestLogCreation()
        {
            string path = Path.GetTempPath();
            RKLog log = new RKLog(path, "TestLog", true);
            Assert.Equal(path, log.LogSavePath);
            Assert.True(log.EnableLog);
        }

        [Fact]
        public void TestRecord()
        {
            string path = Path.GetTempPath();
            string logName = "TestRecordLog";
            RKLog log = new RKLog(path, logName, true);
            log.Record("Test message {0}", 123);
            
            string expectedFilePrefix = Path.Combine(path, logName);
            // Since it appends date, we just check if any file starting with prefix exists
            string[] files = Directory.GetFiles(path, logName + "*.txt");
            Assert.NotEmpty(files);
            
            // Clean up
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void TestSaveBuffer()
        {
            string logPath = Path.GetTempPath();
            string fileName = Path.Combine(logPath, "test_buffer.bin");
            RKLog log = new RKLog(logPath, "TestLog", true);
            byte[] data = { 0x01, 0x02, 0x03 };
            bool success = log.SaveBuffer(fileName, data);
            Assert.True(success);
            Assert.True(File.Exists(fileName));
            byte[] readData = File.ReadAllBytes(fileName);
            Assert.Equal(data, readData);
            File.Delete(fileName);
        }

        [Fact]
        public void TestPrintBuffer()
        {
            RKLog log = new RKLog(Path.GetTempPath(), "TestLog", true);
            byte[] data = { 0x01, 0x02, 0x10, 0xFF };
            log.PrintBuffer(out string output, data, 2);
            string expected = "01 02 " + Environment.NewLine + "10 FF ";
            Assert.Equal(expected, output);
        }
    }
}

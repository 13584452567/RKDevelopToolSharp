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
    }
}

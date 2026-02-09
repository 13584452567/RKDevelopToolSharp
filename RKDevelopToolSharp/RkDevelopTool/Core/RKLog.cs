using System.Text;

namespace RkDevelopTool.Core
{
    public enum ENUM_FILE_STAT
    {
        STAT_NOT_EXIST = 0,
        STAT_FILE,
        STAT_DIR
    }

    public class RKLog
    {
        private string m_path;
        private string m_name;
        private bool m_enable;

        public string LogSavePath => m_path;

        public bool EnableLog
        {
            get => m_enable;
            set => m_enable = value;
        }

        public RKLog(string logFilePath, string logFileName, bool enable = false)
        {
            if (Directory.Exists(logFilePath))
            {
                if (!logFilePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    logFilePath += Path.DirectorySeparatorChar;
                }
                m_path = logFilePath;
            }
            else
            {
                m_path = "";
            }

            if (string.IsNullOrEmpty(logFileName))
            {
                m_name = "Log";
            }
            else
            {
                m_name = logFileName;
            }

            m_enable = enable;
        }

        public bool SaveBuffer(string fileName, byte[] buffer, uint size)
        {
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(buffer, 0, (int)size);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void PrintBuffer(out string output, byte[] buffer, uint size, uint lineCount = 16)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                if (i % lineCount == 0 && i > 0)
                {
                    sb.AppendLine();
                }
                sb.Append($"{buffer[i]:X2} ");
            }
            output = sb.ToString();
        }

        public void Record(string format, params object[] args)
        {
            if (m_enable && !string.IsNullOrEmpty(m_path))
            {
                string text = string.Format(format, args);
                Write(text);
            }
        }

        private bool Write(string text)
        {
            try
            {
                DateTime now = DateTime.Now;
                string dateStr = now.ToString("yyyy-MM-dd");
                string fileName = Path.Combine(m_path, $"{m_name}{dateStr}.txt");
                string timeStr = now.ToString("HH:mm:ss");
                string entry = $"{timeStr}\t{text}\r\n";

                File.AppendAllText(fileName, entry);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ENUM_FILE_STAT GetFileStat(string path)
        {
            if (File.Exists(path))
            {
                return ENUM_FILE_STAT.STAT_FILE;
            }
            else if (Directory.Exists(path))
            {
                return ENUM_FILE_STAT.STAT_DIR;
            }
            return ENUM_FILE_STAT.STAT_NOT_EXIST;
        }
    }
}

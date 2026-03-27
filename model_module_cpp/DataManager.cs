using System.CodeDom;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace logger_client.model_module_cpp
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct LogEventData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ProviderName;
        public int EventID;
        public int Level;
        public UInt64 TimeCreated;
        public UInt64 RecordId;

        public UInt16 dirCount;
    }

    internal static class DataManager
    {
        public static IntPtr? DataPtr;
        public static List<nint> PtrPart = [];
        public static int MaxCount = 0;
        private static bool RunningManager = false;

        public static string[] LogFileNames =
        [
            "System",
            "Application"
        ];

        [DllImport("Logger_client_module.dll", CharSet = CharSet.Unicode)] // 로그 리스트 가져오기
        public static unsafe extern int GetEventLogs(void* outBuffer, int maxCount, int levelMask, string channel);

        [DllImport("Logger_client_module.dll", CharSet = CharSet.Unicode)]
        public static unsafe extern int GetFormattedMessageForEvent(string providerName, string LogFileName, UInt64 recordId, StringBuilder outBuf, int outBufChars);


        // 초기화 함수
        // ext 파일을 읽어서 최대 할당 메모리 계산
        public static bool InitDataManager()
        {
            if(RunningManager == true)
                return true;

            uint[] allocSize = { 0, 0 };
            uint totalSize = 0;
            int index = 0;
            nint ptrAddr = 0;

            foreach (var logName in LogFileNames)
            {
                EventLog log = new EventLog(logName);

                allocSize[index] = (uint)(log.MaximumKilobytes * 1024);
                totalSize += allocSize[index];

                log.Close();
                index++;
            }

            MaxCount = (int)(totalSize / (uint)Marshal.SizeOf<LogEventData>());

            unsafe
            {
                DataPtr = (nint)NativeMemory.Alloc(totalSize + 256); // 고정공간 할당
            }

            if(DataPtr == null)
                return false;

            ptrAddr = (nint)DataPtr;

            foreach(uint size in allocSize)
            {
                PtrPart.Add(ptrAddr);
                ptrAddr += (nint)(size + 32);
            }

            RunningManager = true;
            return true;

        }

        public unsafe static int GetErrorLogs(int levelMask, string channel)
        {
            if (DataPtr == null)
            {
                return 0;
            }

            int index = 0;
            foreach(string file in LogFileNames)
            {
                if (channel.Contains(file) == true && PtrPart.Count > index)
                    return GetEventLogs((void*)PtrPart[index], MaxCount, levelMask, channel);

                index++;
            }

            return 0;
        }

        public unsafe static List<string>? DetailedLog(ErrorLog log, int currentIdx)
        {
            if (log == null)
                return null;

            List<string> list = new List<string>();

            StringBuilder sb = new StringBuilder(65536);
            int result = GetFormattedMessageForEvent(log.ProviderName, LogFileNames[currentIdx], log.RecordId, sb, sb.Capacity);

            if (result > 0)
            {
                string fullContent = sb.ToString();
                string[] parts = fullContent.Split("[XML_SPLIT]", StringSplitOptions.None);

                list.Add(parts[0]);                         // 포맷팅된 메시지
                list.Add(parts.Length > 1 ? parts[1] : ""); // 원본 XML 데이터
            }

            return list;
        }

        public unsafe static string DetailedLog(string ProviderName, ulong RecordId, int currentIdx)
        {

            StringBuilder sb = new StringBuilder(65536);
            int result = GetFormattedMessageForEvent(ProviderName, LogFileNames[currentIdx], RecordId, sb, sb.Capacity);

            if (result > 0)
            {
                string fullContent = sb.ToString();
                string[] parts = fullContent.Split("[XML_SPLIT]", StringSplitOptions.None);

                return parts[0]; // 포맷팅된 메시지
            }

            return "";
        }
    }
}

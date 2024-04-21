using NLog;
using System;
using System.Diagnostics;


namespace AllLive.UWP.Helper
{
    public enum LogType
    {
        INFO,
        DEBUG,
        ERROR,
        FATAL
    }
    public class LogHelper
    {
        public static bool isConfigSetup = false;
        public static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void Log(string message, LogType type, Exception ex = null)
        {
            if (!isConfigSetup)
            {
                Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var logfile = new NLog.Targets.FileTarget()
                {
                    Name = "logfile",
                    CreateDirs = true,
                    FileName = storageFolder.Path + @"\log\" + DateTime.Now.ToString("yyyyMMdd") + ".log",
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${threadid}|${message}|${exception:format=Message,StackTrace}"
                };
                NLog.LogManager.Setup().LoadConfiguration(builder =>
                {
                    builder.ForLogger().FilterMinLevel(LogLevel.Debug).FilterMaxLevel(LogLevel.Fatal).WriteTo(logfile);
                });
                isConfigSetup = true;
            }
            Debug.WriteLine("[" + LogType.INFO.ToString() + "]" + message);
            switch (type)
            {
                case LogType.INFO:
                    logger.Info(message);
                    break;
                case LogType.DEBUG:
                    logger.Debug(message);
                    break;
                case LogType.ERROR:
                    logger.Error(ex, message);
                    break;
                case LogType.FATAL:
                    logger.Fatal(ex, message);
                    break;
                default:
                    break;
            }
        }
        public static bool IsNetworkError(Exception ex)
        {
            if (ex.HResult == -2147012867 || ex.HResult == -2147012889)
            {
                return true;
            }
            {
                return false;
            }
        }

    }
}

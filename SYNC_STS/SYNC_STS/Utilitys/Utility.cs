using NLog;
using System;
using System.ComponentModel;

namespace SYNC_STS.Utilitys
{
    public class Utility
    {
        public class MSGReturnModel
        {
            /// <summary>
            /// 是否成功
            /// </summary>
            [DisplayName("Message Return Flag")]
            public bool RETURN_FLAG { get; set; }

            /// <summary>
            /// ReasonCode
            /// </summary>
            [DisplayName("Message Reason Code")]
            public string REASON_CODE { get; set; }

            /// <summary>
            /// 回傳訊息
            /// </summary>
            [DisplayName("Message Description")]
            public string DESCRIPTION { get; set; }

            /// <summary>
            /// 回傳資料
            /// </summary>
            public string Datas { get; set; }
        }

        /// <summary>
        /// 傳到前端的Model
        /// </summary>
        public class MSGReturnModel<T>
        {
            /// <summary>
            /// 回傳資料
            /// </summary>
            public T Datas { get; set; }

            /// <summary>
            /// 回傳訊息
            /// </summary>
            [DisplayName("Message Description")]
            public string DESCRIPTION { get; set; }

            /// <summary>
            /// ReasonCode
            /// </summary>
            [DisplayName("Message Reason Code")]
            public string REASON_CODE { get; set; }

            /// <summary>
            /// 是否成功
            /// </summary>
            [DisplayName("Message Return Flag")]
            public bool RETURN_FLAG { get; set; }
        }

        /// <summary>
        /// 判斷文字是否為Null 或 空白
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>    
    }

    public static class Extension
    {
        public static bool IsNullOrWhiteSpace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static void WriteLog(string log, string name = null, Ref.Nlog type = Ref.Nlog.Info)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            if (!string.IsNullOrWhiteSpace(name))
            {
                logger = LogManager.GetLogger(name);
            }
            Console.WriteLine(log);
            switch (type)
            {
                //追蹤
                case Ref.Nlog.Trace:
                    logger.Trace(log);
                    break;
                //開發
                case Ref.Nlog.Debug:
                    logger.Debug(log);
                    break;
                //訊息
                case Ref.Nlog.Info:
                    logger.Info(log);
                    break;
                //警告
                case Ref.Nlog.Warn:
                    logger.Warn(log);
                    break;
                //錯誤
                case Ref.Nlog.Error:
                    logger.Error(log);
                    break;
                //致命
                case Ref.Nlog.Fatal:
                    logger.Fatal(log);
                    break;
            }
        }

        public partial class Ref
        {
            public enum Nlog
            {
                [Description("追蹤")]
                Trace,
                [Description("開發")]
                Debug,
                [Description("訊息")]
                Info,
                [Description("警告")]
                Warn,
                [Description("錯誤")]
                Error,
                [Description("致命")]
                Fatal
            }
        }
    }
}

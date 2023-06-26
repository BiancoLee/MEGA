using Dapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS.Daos
{
    class CheckShutDown
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;
        internal static List<GLSYS_VAR> getRunTime()
        {
            //WriteLog($"GetRunTime start...", "SYNC_STS");
            List<GLSYS_VAR> _runTime = new List<GLSYS_VAR>();
            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select VAR_NAME, VAR_VALUE from GLSYS_VAR where TYPE_ID in ('11003','11004')";

                    _runTime = connGLSI.Query<GLSYS_VAR>(str).ToList();
                    WriteLog($"GetRunTime get...", "SYNC_STS");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"GetRunTime ERROR：{ex}", "SYNC_STS", Ref.Nlog.Error);
            }
            //WriteLog($"GetRunTime end...", "SYNC_STS");
            return _runTime;
        }

        internal static bool checkWorkingTime(string _start, string _end)
        {
            //WriteLog($"CheckWorkingTime start...", "SYNC_STS");
            bool _check = true;
            DateTime startParse;
            DateTime endParse;
            DateTime now = DateTime.Now;
            try
            {
                if (DateTime.TryParse(_start, out startParse) && DateTime.TryParse(_end, out endParse))
                {
                    //TimeSpan start = startParse.TimeOfDay; //10 o'clock
                    //TimeSpan end = endParse.TimeOfDay; //12 o'clock
                    //TimeSpan now = DateTime.Now.TimeOfDay;

                    //if ((now >= _start) && (now <= end))
                    if ((now >= startParse) && (now <= endParse))
                        _check = false;
                    WriteLog($"CheckWorkingTime _check: {_check}", "SYNC_STS");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"CheckWorkingTime ERROR：{ex}", "SYNC_STS", Ref.Nlog.Error);
            }
            //WriteLog($"CheckWorkingTime end...", "SYNC_STS");
            return _check;
        }
    }
    class GLSYS_VAR
    {
        public string VAR_NAME { get; set; }
        public string VAR_VALUE { get; set; }
    }
}

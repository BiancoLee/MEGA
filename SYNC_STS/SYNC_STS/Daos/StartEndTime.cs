using Dapper;
using SYNC_STS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS.Daos
{
    class StartEndTime
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;

        //logStartTime
        internal static async Task<int> logStartTime(string _Type, string _TypeName)
        {
            //WriteLog($"{_TypeName}-LogStartTime start...", _TypeName);
            int result = 0;
            try
            {
                LOG_DATA _LOG_DATA = new LOG_DATA()
                {
                    WF_TYPE = _Type,
                    WF_START_DT = DateTime.Now
                };

                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"
insert into BWLOG_WF (WF_TYPE, WF_START_DT)
OUTPUT INSERTED.WF_RUN_ID
VALUES(@WF_TYPE, @WF_START_DT);
";

                    result = await connGLSI.QuerySingleAsync<int>(str, _LOG_DATA);
                    WriteLog($"{_TypeName}-LogStartTime WF_RUN_ID = {result}...", _TypeName);
                    return result;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"{_TypeName}-logStartTime ERROR：{ex}", _TypeName, Ref.Nlog.Error);
            }
            //WriteLog($"{_TypeName}-LogStartTime end...", _TypeName);
            return result;
        }
        //logEndTime
        internal static async Task<int> logEndTime(int _WF_RUN_ID, string _TypeName)
        {
            //WriteLog($"{_TypeName}-LogEndTime start...", _TypeName);
            int result = 0;
            try
            {
                LOG_DATA _LOG_DATA = new LOG_DATA()
                {
                    WF_RUN_ID = _WF_RUN_ID,
                    WF_END_DT = DateTime.Now
                };

                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"
update BWLOG_WF
set WF_END_DT = @WF_END_DT
where WF_RUN_ID = @WF_RUN_ID
";
                    WriteLog($"{_TypeName}-LogEndTime WF_RUN_ID = {_WF_RUN_ID}...", _TypeName);
                    result = await connGLSI.ExecuteAsync(str, _LOG_DATA);

                    
                }
            }
            catch (Exception ex)
            {
                //NLOG = $" ERROR: logEndTime {ex}";
                WriteLog($"{_TypeName}-LogEndTime ERROR：{ex}", _TypeName, Ref.Nlog.Error);
            }
            //WriteLog($"{_TypeName}-LogEndTime end...", _TypeName);
            return result;
        }
    }

    class LOG_DATA
    {
        public int WF_RUN_ID { get; set; }
        public string WF_TYPE { get; set; }
        public DateTime WF_START_DT { get; set; }
        public DateTime WF_END_DT { get; set; }
    }
}

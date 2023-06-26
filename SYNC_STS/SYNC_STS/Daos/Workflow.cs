using Dapper;
using SYNC_STS.Models;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using static SYNC_STS.Utilitys.Utility;
using SYNC_STS.Utilitys;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS.Daos
{  
    class Workflow
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;
        internal static /*async Task<int>*/ int AddWorkflow(int _WF_RUN_ID, string TRANS_NO)
        {
            //WriteLog($"AddWorkflow start...", TRANS_NO);
            int result = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"
insert into BWLOG_TRNWF (WF_RUN_ID, TRANS_NO)
VALUES(@WF_RUN_ID, @TRANS_NO);
";
                    result = connGLSI.Execute(str, new { WF_RUN_ID = _WF_RUN_ID, TRANS_NO });
                    //result = await connGLSI.ExecuteAsync(str, new { WF_RUN_ID = _WF_RUN_ID, TRANS_NO });

                    WriteLog($"AddWorkflow WF_RUN_ID: {_WF_RUN_ID}, TRANS_NO: {TRANS_NO}...", TRANS_NO);
                }
            }
            catch (Exception ex)
            {
                result = -1;
                WriteLog($"AddWorkflow ERROR：{ex}", TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"AddWorkflow end...", TRANS_NO);
            return result;
        }

        internal static async Task<MSGReturnModel> DeleteLogFiles(DateTime startT)
        {
            //WriteLog($"DeleteLogFiles start...", "DeleteLogFiles");
            DateTime now = DateTime.Now;
            if (now.Hour != startT.Hour || now.Minute != startT.Minute)
                return new MSGReturnModel() { RETURN_FLAG = false };

            int reserve = 30;
            int _temp = 0;
            //1.新增BWLog、取 WF_RUN_ID
            int _WF_RUN_ID = await StartEndTime.logStartTime(WF_TYPE.Clean_Data, "DeleteLogFiles");
            if (_WF_RUN_ID == 0)
                return new MSGReturnModel() { RETURN_FLAG = false };

            //取得資料保留天數
            MSGReturnModel<string> _Glsys_var = await getGlsys_Var();
            if (!_Glsys_var.RETURN_FLAG)
                return new MSGReturnModel() { RETURN_FLAG = false };


            if (int.TryParse(_Glsys_var.Datas, out _temp))
                reserve = 0 - _temp;

            DateTime assignDays = now.AddDays(reserve).Date;

            deleteBwErrTrnMsg(assignDays);
            deleteBwLogTrnef(assignDays);
            deleteBwErrWfMsg(assignDays);
            deleteBwLogWf(assignDays);

            //更新BWLog
            var writeBWLog_cnt = await StartEndTime.logEndTime(_WF_RUN_ID, "DeleteLogFiles");
            if (writeBWLog_cnt == 0)
                return new MSGReturnModel() { RETURN_FLAG = false };

            //WriteLog($"DeleteLogFiles end...", "DeleteLogFiles");
            return new MSGReturnModel() { RETURN_FLAG = true };
        }

        private static async Task<MSGReturnModel<string>> getGlsys_Var()
        {
            //WriteLog($"getGlsys_Var start...", "DeleteLogFiles");
            MSGReturnModel<string> result = new MSGReturnModel<string>();
            result.RETURN_FLAG = false;
            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select VAR_VALUE from GLSYS_VAR where TYPE_ID='11002'";

                    var _VAR_VALUE = await connGLSI.QueryFirstAsync<string>(str);
                    if (!_VAR_VALUE.IsNullOrWhiteSpace())
                    {
                        result.Datas = _VAR_VALUE;
                        result.RETURN_FLAG = true;
                    }
                    WriteLog($"getGlsys_Var _VAR_VALUE = {_VAR_VALUE}...", "DeleteLogFiles");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"getGlsys_Var ERROR：{ex}", "DeleteLogFiles", Ref.Nlog.Error);
            }
            //WriteLog($"getGlsys_Var end...", "DeleteLogFiles");
            return result;
        }

        private static /*async*/ void deleteBwErrTrnMsg(DateTime assignDays)
        {
            //WriteLog($"deleteBwErrTrnMsg start...", "DeleteLogFiles");
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"delete from BWERR_TRNMSG where CRT_DT<=@CRT_DT";
                    //cnt = await connGLSI.ExecuteAsync(str, new { CRT_DT = assignDays});
                    cnt = connGLSI.Execute(str, new { CRT_DT = assignDays });
                    WriteLog($"deleteBwErrTrnMsg cnt = {cnt}...", "DeleteLogFiles");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"deleteBwErrTrnMsg ERROR：{ex}", "DeleteLogFiles", Ref.Nlog.Error);
            }
            //WriteLog($"deleteBwErrTrnMsg end...", "DeleteLogFiles");
        }

        private static /*async*/ void deleteBwErrWfMsg(DateTime assignDays)
        {
            //WriteLog($"deleteBwErrWfMsg start...", "DeleteLogFiles");
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"delete from BWLOG_TRNWF where WF_RUN_ID in (select WF_RUN_ID from BWLOG_WF(NOLOCK) where WF_START_DT <=@WF_START_DT)";
                    //cnt = await connGLSI.ExecuteAsync(str, new { WF_START_DT = assignDays });
                    cnt = connGLSI.Execute(str, new { WF_START_DT = assignDays });
                    WriteLog($"deleteBwErrWfMsg cnt = {cnt}...", "DeleteLogFiles");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"deleteBwErrWfMsg ERROR：{ex}", "DeleteLogFiles", Ref.Nlog.Error);
            }
            //WriteLog($"deleteBwErrWfMsg end...", "DeleteLogFiles");
        }

        private static /*async*/ void deleteBwLogTrnef(DateTime assignDays)
        {
            //WriteLog($"deleteBwLogTrnef start...", "DeleteLogFiles");
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"delete from BWERR_WFMSG where CRT_DT<=@CRT_DT";
                    //cnt = await connGLSI.ExecuteAsync(str, new { CRT_DT = assignDays });
                    cnt = connGLSI.Execute(str, new { CRT_DT = assignDays });
                    WriteLog($"deleteBwLogTrnef cnt = {cnt}...", "DeleteLogFiles");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"deleteBwLogTrnef ERROR：{ex}", "DeleteLogFiles", Ref.Nlog.Error);
            }
            //WriteLog($"deleteBwLogTrnef end...", "DeleteLogFiles");
        }

        private static /*async*/ void deleteBwLogWf(DateTime assignDays)
        {
            //WriteLog($"deleteBwLogWf start...", "DeleteLogFiles");
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"delete from BWLOG_WF where WF_START_DT<=@WF_START_DT";
                    //cnt = await connGLSI.ExecuteAsync(str, new { WF_START_DT = assignDays });
                    cnt = connGLSI.Execute(str, new { WF_START_DT = assignDays });
                    WriteLog($"deleteBwLogWf cnt = {cnt}...", "DeleteLogFiles");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"deleteBwLogWf ERROR：{ex}", "DeleteLogFiles", Ref.Nlog.Error);
            }
            //WriteLog($"deleteBwLogWf end...", "DeleteLogFiles");
        }
    }
}

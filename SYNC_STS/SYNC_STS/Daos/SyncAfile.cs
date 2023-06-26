using Dapper;
using SYNC_STS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.EasycomClient;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using static SYNC_STS.Utilitys.Utility;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS.Daos
{
    class SyncAfile
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;
        private static string connectionString400 = ConfigurationManager.ConnectionStrings["Easycom"].ConnectionString;
        internal static async Task<MSGReturnModel> DoSyncAfile()
        {
            //WriteLog($"SyncAfile-DoSyncAfile start...", "SyncAfile");
            DateTime now = DateTime.Now;

            //取中介平台A File資料
            MSGReturnModel<List<FGLGCTL0S>> AfileModel = await getAfile();
            if (!AfileModel.RETURN_FLAG)
                return new MSGReturnModel() { RETURN_FLAG = false };

            if (AfileModel.Datas.Count > 0)
            {
                //1.新增BWLog、取 WF_RUN_ID
                int _WF_RUN_ID = await StartEndTime.logStartTime(WF_TYPE.Sync_A, "SyncAfile");
                if (_WF_RUN_ID == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };

                foreach (var _data in AfileModel.Datas)
                {
                    ////2.判斷中介平台A File控制檔狀態(是否還有件做,或已在徒但未 Complete 件)
                    //int _cnt = await checkAfile(_data.TRANS_NO);
                    //if (_cnt == 0)
                    //    break;
                    WriteLog($"FLOW_TYPE-{_data.FLOW_TYPE}...", _data.TRANS_NO);
                    //3.紀錄每筆傳輸紀錄
                    int Workflow_cnt = Workflow.AddWorkflow(_WF_RUN_ID, _data.TRANS_NO);
                    if (Workflow_cnt == -1)
                        return new MSGReturnModel() { RETURN_FLAG = false };

                    using (TransactionScope Scope = new TransactionScope())
                    {
                        //4.更新中介平台A File控制檔
                        int SyncA_GLSI_cnt = DoSyncA_GLSI(_data, now);
                        if (SyncA_GLSI_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }                          

                        //5.更新AS400控制檔
                        FGLGCTL0 _FGLGCTL0 = new FGLGCTL0()
                        {
                            TRANS_STS = _data.TRANS_STS,
                            UPD_USRID = "BWSYS",

                            ERR_CODE = _data.ERR_CODE ?? string.Empty,
                            ERR_TYPE = _data.ERR_TYPE ?? string.Empty,
                            SQL_NO = _data.TRANS_NO,

                            UPD_DATE = now.ToString("yyyyMMdd"),
                            UPD_TIME = now.ToString("HHmmss"),
                            IMP_DATE = _data.IMP_DT.ToString("yyyyMMdd"),
                            IMP_TIME = _data.IMP_DT.ToString("HHmmss")
                        };

                        int SyncA_AS400_cnt = DoSyncA_AS400(_FGLGCTL0, now);
                        if (SyncA_AS400_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }
                        
                        if(SyncA_GLSI_cnt != -1 && SyncA_AS400_cnt != -1)
                        {
                            Scope.Complete();
                            WriteLog($"SyncAfile-Scope complete...", _data.TRANS_NO);
                        }

                        Scope.Dispose();
                    }
                }
                //6.更新BWLog
                var writeBWLog_cnt = await StartEndTime.logEndTime(_WF_RUN_ID, "SyncAfile");
                if (writeBWLog_cnt == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };
            }
            //WriteLog($"SyncAfile-DoSyncAfile end...", "SyncAfile");
            return new MSGReturnModel() { RETURN_FLAG = true };
        }

        private static async Task<int> checkAfile(string _TRANS_NO)
        {
            WriteLog($"SyncAfile-CheckAfile start...", _TRANS_NO);
            int cnt = 0;
            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select count(TRANS_NO) from FGLGCTL0S(NOLOCK) where TRANS_STS in ('IMS','IMF') and SYNC_STS='01' and TRANS_NO = @TRANS_NO";
                    //str = $@"select count(TRANS_NO) from FGLGCTL0S(NOLOCK) where TRANS_NO = @TRANS_NO"; //測試
                    cnt = await connGLSI.QueryFirstAsync<int>(str, new { TRANS_NO = _TRANS_NO });
                    WriteLog($"SyncAfile-CheckAfile 已經在執行的件數 = {cnt}...", _TRANS_NO);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncAfile-CheckAfile ERROR：{ex}", _TRANS_NO, Ref.Nlog.Error);
            }
            WriteLog($"SyncAfile-CheckAfile end...", _TRANS_NO);
            return cnt;
        }

        private static async Task<MSGReturnModel<List<FGLGCTL0S>>> getAfile()
        {
            //WriteLog($"SyncAfile-GetAfile start...", "SyncAfile");            
            MSGReturnModel<List<FGLGCTL0S>> result = new MSGReturnModel<List<FGLGCTL0S>>();
            result.RETURN_FLAG = false;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select TRANS_NO,FLOW_TYPE,TRANS_STS,ERR_TYPE,ERR_CODE,IMP_DT from FGLGCTL0S(NOLOCK) where TRANS_STS in ('IMS','IMF') and SYNC_STS='01' and FLOW_TYPE != 'A21'";
                    //str = $@"select TRANS_NO,FLOW_TYPE,TRANS_STS,ERR_TYPE,ERR_CODE,IMP_DT from FGLGCTL0S(NOLOCK) where TRANS_NO = 'B68011007120008'"; //測試

                    var _AfileList = await connGLSI.QueryAsync<FGLGCTL0S>(str);

                    if (_AfileList != null)
                    {
                        result.Datas = _AfileList.ToList();
                        result.RETURN_FLAG = true;
                    }
                }
                if (result.Datas != null && result.Datas?.Count > 0)
                    WriteLog($"SyncAfile-GetAfile 筆數{result.Datas?.Count ?? 0}...", "SyncAfile");
            }
            catch (Exception ex)
            {
                WriteLog($"SyncAfile-GetAfile ERROR：{ex}", "SyncAfile", Ref.Nlog.Error);
            }

            return result;
        }

        private static /*async Task<int>*/ int DoSyncA_GLSI(FGLGCTL0S AfileModel, DateTime Now)
        {
            //WriteLog($"SyncAfile-DoSyncA_GLSI start...", AfileModel.TRANS_NO);
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = @"
UPDATE FGLGCTL0S SET
UPD_DT = @UPD_DT,
SYNC_DT = @SYNC_DT,
SYNC_STS = @SYNC_STS,
UPD_USRID = @UPD_USRID
where TRANS_NO = @TRANS_NO
";
//                    str = @"
//UPDATE FGLGCTL0S SET
//BW_RUN_ID = @BW_RUN_ID,
//UPD_DT = @UPD_DT,
//SYNC_DT = @SYNC_DT,
//SYNC_STS = @SYNC_STS,
//UPD_USRID = @UPD_USRID
//where TRANS_NO = @TRANS_NO
//";
                    //cnt = await connGLSI.ExecuteAsync(str, new { /*BW_RUN_ID = 999999,*/ UPD_DT = Now, SYNC_DT = Now, SYNC_STS = "02", UPD_USRID = "BWSYS", AfileModel.TRANS_NO });
                    cnt = connGLSI.Execute(str, new { /*BW_RUN_ID = 999999,*/ UPD_DT = Now, SYNC_DT = Now, SYNC_STS = "02", UPD_USRID = "BWSYS", AfileModel.TRANS_NO });
                    if (cnt == 0)
                        WriteLog($"SyncAfile-DoSyncA_GLSI TRANS_NO:{AfileModel.TRANS_NO}, FLOW_TYPE:{AfileModel?.FLOW_TYPE}, TRANS_STS:{AfileModel?.TRANS_STS } no data updated...", AfileModel.TRANS_NO);
                    else
                        WriteLog($"SyncAfile-DoSyncA_GLSI TRANS_NO:{AfileModel.TRANS_NO}, FLOW_TYPE:{AfileModel?.FLOW_TYPE}, TRANS_STS:{AfileModel?.TRANS_STS } updated...", AfileModel.TRANS_NO);
                }
            }
            catch (Exception ex)
            {
                cnt = -1;
                WriteLog($"SyncAfile-DoSyncA_GLSI ERROR：{ex}", AfileModel.TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"SyncAfile-DoSyncA_GLSI end...", AfileModel.TRANS_NO);
            return cnt;
        }

        private static /*async Task<int>*/ int DoSyncA_AS400(FGLGCTL0 AfileModel, DateTime Now)
        {
            //WriteLog($"SyncAfile-DoSyncA_AS400 start...", AfileModel.SQL_NO);
            int cnt = 0;

            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();
                    using (EacCommand cmd = new EacCommand(conn400))
                    {
                        string str = string.Empty;
                        str = @"
                    UPDATE FGLGCTL0 SET 
                        TRANS_STS = :TRANS_STS, 
                        UPD_USRID = :UPD_USRID,
                        UPD_DATE = :UPD_DATE,
                        UPD_TIME = :UPD_TIME,
                        IMP_DATE = :IMP_DATE,
                        IMP_TIME = :IMP_TIME
                        WHERE SQL_NO = :SQL_NO
                    ";

                        cmd.CommandText = str;
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add("TRANS_STS", AfileModel.TRANS_STS ?? string.Empty);
                        cmd.Parameters.Add("UPD_USRID", AfileModel.UPD_USRID ?? string.Empty);
                        cmd.Parameters.Add("UPD_DATE", AfileModel.UPD_DATE ?? string.Empty);
                        cmd.Parameters.Add("UPD_TIME", AfileModel.UPD_TIME ?? string.Empty);
                        cmd.Parameters.Add("IMP_DATE", AfileModel.IMP_DATE ?? string.Empty);
                        cmd.Parameters.Add("IMP_TIME", AfileModel.IMP_TIME ?? string.Empty);
                        cmd.Parameters.Add("SQL_NO", AfileModel.SQL_NO ?? string.Empty);

                        cmd.Prepare();
                        cnt = cmd.ExecuteNonQuery();
                        //cnt = await cmd.ExecuteNonQueryAsync();

                        if (cnt == 0)
                            WriteLog($"SyncAfile-DoSyncA_AS400 SQL_NO:{AfileModel?.SQL_NO}, TRANS_STS:{AfileModel?.TRANS_STS } no data updated...", AfileModel.SQL_NO);
                        else
                            WriteLog($"SyncAfile-DoSyncA_AS400 SQL_NO:{AfileModel?.SQL_NO}, TRANS_STS:{AfileModel?.TRANS_STS } updated...", AfileModel.SQL_NO);

                        cmd.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncAfile-DoSyncA_AS400 ERROR：{ex}", AfileModel.SQL_NO, Ref.Nlog.Error);
                cnt = 0;
            }
            //WriteLog($"SyncAfile-DoSyncA_AS400 end...", AfileModel.SQL_NO);
            return cnt;
        }
    }
}

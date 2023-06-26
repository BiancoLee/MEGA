using Dapper;
using SYNC_STS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Data.EasycomClient;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using static SYNC_STS.Utilitys.Utility;
using SYNC_STS.Utilitys;
using static SYNC_STS.Utilitys.Extension;


namespace SYNC_STS.Daos
{
    class SyncBfile
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;
        private static string connectionString400 = ConfigurationManager.ConnectionStrings["Easycom"].ConnectionString;
        internal static async Task<MSGReturnModel> DoSyncBfile()
        {
            //WriteLog($"SyncBfile-DoSyncBfile start...", "SyncBfile");
            DateTime now = DateTime.Now;

            //取中介平台B File資料
            MSGReturnModel<List<FGLBCTL0>> BfileModel = await getBfile();

            if (!BfileModel.RETURN_FLAG)
                return new MSGReturnModel() { RETURN_FLAG = false };

            if (BfileModel.Datas.Count > 0)
            {
                //1.新增BWLog、取 WF_RUN_ID
                int _WF_RUN_ID = await StartEndTime.logStartTime(WF_TYPE.Sync_B, "SyncBfile");
                if (_WF_RUN_ID == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };


                foreach (var _data in BfileModel.Datas)
                {
                    ////2.判斷中介平台B File控制檔狀態(是否還有件做,或已在徒但未 Complete 件)
                    //int _cnt = await checkBfile(_data.TRANS_NO);
                    //if (_cnt == 0)
                    //    break;
                    WriteLog($"FLOW_TYPE-{_data.FLOW_TYPE}...", _data.TRANS_NO);
                    //3.紀錄每筆傳輸紀錄
                    int Workflow_cnt = Workflow.AddWorkflow(_WF_RUN_ID, _data.TRANS_NO);
                    if (Workflow_cnt == -1)
                        return new MSGReturnModel() { RETURN_FLAG = false };

                    //4.取得流程的檔案名稱
                    MSGReturnModel<List<string>> _fileNameList = await getFileName(_data.FLOW_TYPE, _data.TRANS_NO);
                    if (!_fileNameList.RETURN_FLAG)
                        return new MSGReturnModel() { RETURN_FLAG = false };

                    //5.更新SQL端明細檔狀態
                    foreach (var _name in _fileNameList.Datas)
                    {
                        List<FGLGCTL0S> BfileDetail = await GetDetail(_name, _data.TRANS_NO);

                        foreach (var _detail in BfileDetail)
                        {
                            int _Detail_cnt = await UpdateDetail(_name, _detail);
                        }
                    }

                    using (TransactionScope Scope = new TransactionScope())
                    {
                        //6.更新中介平台B File控制檔
                        int SyncB_GLSI_cnt = DoSyncB_GLSI(_data, now);
                        if (SyncB_GLSI_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }
                            
                        int SyncB_AS400_cnt = DoSyncB_AS400(_data, now, _WF_RUN_ID);
                        if (SyncB_AS400_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }
                        
                        if(SyncB_GLSI_cnt != -1 && SyncB_AS400_cnt != -1)
                        {                        
                            Scope.Complete();
                            WriteLog($"SyncBfile-Scope complete...", _data.TRANS_NO);
                        }
                            
                        Scope.Dispose();
                    }
                }
                //7.更新BWLog
                var writeBWLog_cnt = await StartEndTime.logEndTime(_WF_RUN_ID, "SyncBfile");
                if (writeBWLog_cnt == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };
            }
            //WriteLog($"SyncBfile-DoSyncBfile end...", "SyncBfile");
            return new MSGReturnModel() { RETURN_FLAG = true };
        }

        private static async Task<MSGReturnModel<List<FGLBCTL0>>> getBfile()
        {
            //WriteLog($"SyncBfile-GetBfile start...", "SyncBfile");
            MSGReturnModel<List<FGLBCTL0>> result = new MSGReturnModel<List<FGLBCTL0>>();
            result.RETURN_FLAG = false;
            List<FGLBCTL0> _dataList = new List<FGLBCTL0>();
            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();

                    using (EacCommand cmd = new EacCommand(conn400))
                    {
                        string sql = string.Empty;

                        //測試註解
                        sql = $@"
                        select TRANS_NO, TRANS_STS, FLOW_TYPE, ERR_TYPE, ERR_CODE, IMP_DATE, IMP_TIME
                        from FGLBCTL0
                        where TRANS_STS in ('IMS','IMF') and SYNC_STS='01' with UR
                        ";
                        //測試使用
                        //sql = $@"select TRANS_NO, TRANS_STS, FLOW_TYPE, ERR_TYPE, ERR_CODE, IMP_DATE, IMP_TIME from FGLBCTL0 where TRANS_NO in ('B11102100900323') with UR";

                        cmd.CommandText = sql;
                        cmd.Prepare();
                        DbDataReader dbresult = await cmd.ExecuteReaderAsync();
                        while (dbresult.Read())
                        {
                            var model = new FGLBCTL0();
                            model.TRANS_NO = dbresult["TRANS_NO"]?.ToString()?.Trim();
                            model.TRANS_STS = dbresult["TRANS_STS"]?.ToString()?.Trim();
                            model.FLOW_TYPE = dbresult["FLOW_TYPE"]?.ToString()?.Trim();
                            model.ERR_TYPE = dbresult["ERR_TYPE"]?.ToString()?.Trim();
                            model.ERR_CODE = dbresult["ERR_CODE"]?.ToString()?.Trim();
                            model.IMP_DATE = dbresult["IMP_DATE"]?.ToString()?.Trim();
                            model.IMP_TIME = dbresult["IMP_TIME"]?.ToString()?.Trim();
                            
                            _dataList.Add(model);
                        }

                        cmd.Dispose();
                    }
                    conn400.Dispose();
                    conn400.Close();
                }
                result.Datas = _dataList;
                result.RETURN_FLAG = true;

                if(result.Datas != null && result.Datas?.Count > 0)
                    WriteLog($"SyncBfile-GetBfile 筆數: {result.Datas?.Count ?? 0}...", "SyncBfile");
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-GetBfile error：{ex}", "SyncBfile", Ref.Nlog.Error);
            }

            return result;
        }

        private static async Task<MSGReturnModel<List<string>>> getFileName(string _FLOW_TYPE, string _TRANS_NO)
        {
            //WriteLog($"SyncBfile-GetFileName start...", _TRANS_NO);
            MSGReturnModel<List<string>> result = new MSGReturnModel<List<string>>();
            result.RETURN_FLAG = false;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select  File_Name from FLOWFILES_INF(NOLOCK) where Flow_Type= @Flow_Type";
                    var _data = await connGLSI.QueryAsync<string>(str, new { Flow_Type = _FLOW_TYPE });

                    if (_data != null)
                    {
                        result.Datas = _data.ToList();
                        result.RETURN_FLAG = true;
                        WriteLog($"SyncBfile-GetFileName 筆數{result.Datas?.Count ?? 0}...", _TRANS_NO);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-GetFileName ERROR：{ex}", _TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"SyncBfile-GetFileName end...", _TRANS_NO);
            return result;
        }

        private static async Task<List<FGLGCTL0S>> GetDetail(string _fileName, string TRNS_NO)
        {
            //WriteLog($"SyncBfile-GetDetail start...", TRNS_NO);
            List<FGLGCTL0S> result = new List<FGLGCTL0S>();

            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();

                    using (EacCommand cmd = new EacCommand(conn400))
                    {
                        string sql = string.Empty;

                        sql = $@"select TRNS_NO,TRNS_SEQ,STATUS from {_fileName} where TRNS_NO=:TRNS_NO with UR";
                        cmd.CommandText = sql;
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add("TRNS_NO", TRNS_NO);
                        cmd.Prepare();
                        DbDataReader dbresult = await cmd.ExecuteReaderAsync();
                        while (dbresult.Read())
                        {
                            var model = new FGLGCTL0S();
                            model.TRANS_NO = dbresult["TRNS_NO"]?.ToString()?.Trim();
                            model.TRNS_SEQ = dbresult["TRNS_SEQ"]?.ToString()?.Trim();
                            model.FLOW_TYPE = dbresult["STATUS"]?.ToString()?.Trim();

                            result.Add(model);
                        }
                        cmd.Dispose();
                    }
                    conn400.Dispose();
                    conn400.Close();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-GetDetail ERROR：{ex}", TRNS_NO, Ref.Nlog.Error);
            }
            WriteLog($"SyncBfile-GetDetail 筆數: {result?.Count ?? 0}...", TRNS_NO);
            return result;
        }

        private static async Task<int> UpdateDetail(string _fileName, FGLGCTL0S _detail)
        {
            //WriteLog($"SyncBfile-UpdateDetail start...", _detail.TRANS_NO);
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"update {_fileName}
set STATUS= @STATUS 
where TRNS_NO=@TRNS_NO and  TRNS_SEQ=@TRNS_SEQ";

                    cnt = await connGLSI.ExecuteAsync(str, new { STATUS = _detail.FLOW_TYPE, TRNS_NO = _detail.TRANS_NO, TRNS_SEQ = _detail.TRNS_SEQ });

                    if(cnt == 0)
                        WriteLog($"SyncBfile-UpdateDetail fileName = {_fileName}, TRNS_NO ={_detail.TRANS_NO}, TRNS_SEQ = {_detail.TRNS_SEQ}, set STATUS = {_detail.FLOW_TYPE} no data updated...", _detail.TRANS_NO);
                    else
                        WriteLog($"SyncBfile-UpdateDetail fileName = {_fileName}, TRNS_NO ={_detail.TRANS_NO}, TRNS_SEQ = {_detail.TRNS_SEQ}, set STATUS = {_detail.FLOW_TYPE} updated...", _detail.TRANS_NO);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-UpdateDetail ERROR：{ex}", _detail.TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"SyncBfile-UpdateDetail end...", _detail.TRANS_NO);
            return cnt;
        }

        private static async Task<int> checkBfile(string _TRANS_NO)
        {
            WriteLog($"SyncBfile-CheckBfile start...", _TRANS_NO);
            int cnt = 0;
            
            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();
                    using (EacCommand cmd = new EacCommand(conn400))
                    {
                        string sql = string.Empty;
                        sql = $@"select count(TRANS_NO) from FGLBCTL0 where TRANS_STS in ('IMS','IMF') and SYNC_STS='01' and TRANS_NO = @TRANS_NO with UR";
  
                        cmd.CommandText = sql;
                        cmd.Parameters.Add("TRANS_NO", _TRANS_NO);
                        cmd.Prepare();
                        DbDataReader dbresult = await cmd.ExecuteReaderAsync();
                        while (dbresult.Read())
                        {
                            int _count = 0;
                            var _data = dbresult["COUNT"]?.ToString()?.Trim();
                            if (int.TryParse(_data, out _count))
                            {
                                cnt = _count;
                            }
                        }
                        cmd.Dispose();
                    }
                    conn400.Dispose();
                    conn400.Close();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-CheckBfile ERROR：{ex}", _TRANS_NO, Ref.Nlog.Error);
            }
            WriteLog($"SyncBfile-CheckBfile end...cnt:{cnt}", _TRANS_NO);
            return cnt;
        }

        private static /*async Task<int>*/ int DoSyncB_GLSI(FGLBCTL0 BfileModel, DateTime Now)
        {
            //WriteLog($"SyncBfile-DoSyncB_GLSI start...", BfileModel.TRANS_NO);
            int cnt = 0;
            string _IMP_DT = string.Empty;
            try
            {
                if (!BfileModel.IMP_DATE.IsNullOrWhiteSpace() && !BfileModel.IMP_TIME.IsNullOrWhiteSpace())
                {
                    if (BfileModel.IMP_DATE.Length < 8)
                        BfileModel.IMP_DATE = BfileModel.IMP_DATE.PadLeft(8);
                    if (BfileModel.IMP_TIME.Length < 6)
                        BfileModel.IMP_TIME = BfileModel.IMP_TIME.PadLeft(6, '0');

                    string _YY = BfileModel.IMP_DATE?.Substring(0, 4);
                    string _MM = BfileModel.IMP_DATE?.Substring(4, 2);
                    string _DD = BfileModel.IMP_DATE?.Substring(6, 2);
                    string _hh = BfileModel.IMP_TIME?.Substring(0, 2);
                    string _mm = BfileModel.IMP_TIME?.Substring(2, 2);
                    string _tt = BfileModel.IMP_TIME?.Substring(4, 2);
                    _IMP_DT = $"{_YY}-{_MM}-{_DD} {_hh}:{_mm}:{_tt}.000";
                }

                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = @"
update FGLBCTL0S set 
ERR_TYPE=@ERR_TYPE,
ERR_CODE=@ERR_CODE,
TRANS_STS=@TRANS_STS,
SYNC_DT=@SYNC_DT,
IMP_DT=@IMP_DT,
UPD_USRID=@UPD_USRID,
UPD_DT=@UPD_DT 
where TRANS_NO=@TRANS_NO
";
//                    str = @"
//update FGLBCTL0S set 
//ERR_TYPE=@ERR_TYPE,
//ERR_CODE=@ERR_CODE,
//TRANS_STS=@TRANS_STS,
//SYNC_DT=@SYNC_DT,
//IMP_DT=@IMP_DT,
//BW_RUN_ID=@BW_RUN_ID,
//UPD_USRID=@UPD_USRID,
//UPD_DT=@UPD_DT 
//where TRANS_NO=@TRANS_NO
//";
                    //cnt = await connGLSI.ExecuteAsync(str, new { ERR_TYPE = BfileModel.ERR_TYPE, ERR_CODE = BfileModel.ERR_CODE, TRANS_STS = BfileModel.TRANS_STS, SYNC_DT = Now, IMP_DT = _IMP_DT, /*BW_RUN_ID = 999999,*/ UPD_USRID = "BWSYS", UPD_DT = Now, BfileModel.TRANS_NO });
                    cnt = connGLSI.Execute(str, new { ERR_TYPE = BfileModel.ERR_TYPE, ERR_CODE = BfileModel.ERR_CODE, TRANS_STS = BfileModel.TRANS_STS, SYNC_DT = Now, IMP_DT = _IMP_DT, /*BW_RUN_ID = 999999,*/ UPD_USRID = "BWSYS", UPD_DT = Now, BfileModel.TRANS_NO });
                    if (cnt == 0)
                        WriteLog($"SyncBfile-DoSyncB_GLSI TRANS_NO:{BfileModel.TRANS_NO}, FLOW_TYPE:{BfileModel.FLOW_TYPE}, ERR_TYPE:{BfileModel.ERR_TYPE}, ERR_CODE:{BfileModel.ERR_CODE}, TRANS_STS:{BfileModel.TRANS_STS} no data updated...", BfileModel.TRANS_NO);
                    else
                        WriteLog($"SyncBfile-DoSyncB_GLSI TRANS_NO:{BfileModel.TRANS_NO}, FLOW_TYPE:{BfileModel.FLOW_TYPE}, ERR_TYPE:{BfileModel.ERR_TYPE}, ERR_CODE:{BfileModel.ERR_CODE}, TRANS_STS:{BfileModel.TRANS_STS} updated...", BfileModel.TRANS_NO);
                }
            }
            catch (Exception ex)
            {
                cnt = -1;
                WriteLog($"SyncBfile-DoSyncB_GLSI ERROR：{ex}", BfileModel.TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"SyncBfile-DoSyncB_GLSI end...", BfileModel.TRANS_NO);
            return cnt;
        }

        private static /*async Task<int>*/ int DoSyncB_AS400(FGLBCTL0 BfileModel, DateTime Now, int _WF_RUN_ID)
        {
            //WriteLog($"SyncBfile-DoSyncB_AS400 start...", BfileModel.TRANS_NO);
            int cnt = 0;

            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();
                    //EacTransaction transaction400 = conn400.BeginTransaction();
                    using (EacCommand cmd = new EacCommand(conn400))
                    {
                        string str = string.Empty;
                        str = @"
update FGLBCTL0 set 
SYNC_STS=:SYNC_STS,
SYNC_DATE=:SYNC_DATE,
SYNC_TIME=:SYNC_TIME,
WF_RUN_ID=:_WF_RUN_ID,
UPD_USRID=:UPD_USRID,
UPD_DATE=:UPD_DATE,
UPD_TIME=:UPD_TIME
where TRANS_NO=:TRANS_NO
";
//                        str = @"
//update FGLBCTL0 set 
//SYNC_STS=:SYNC_STS,
//SYNC_DATE=:SYNC_DATE,
//SYNC_TIME=:SYNC_TIME,
//WF_RUN_ID=:_WF_RUN_ID,
//BW_RUN_ID=:BW_RUN_ID,
//UPD_USRID=:UPD_USRID,
//UPD_DATE=:UPD_DATE,
//UPD_TIME=:UPD_TIME
//where TRANS_NO=:TRANS_NO
//                    ";

                        cmd.CommandText = str;
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add("SYNC_STS", "02");
                        cmd.Parameters.Add("SYNC_DATE", Now.ToString("yyyyMMdd") ?? string.Empty);
                        cmd.Parameters.Add("SYNC_TIME", Now.ToString("HHmmss") ?? string.Empty);
                        cmd.Parameters.Add("WF_RUN_ID", $"{_WF_RUN_ID}");
                        //cmd.Parameters.Add("BW_RUN_ID", "999999");
                        cmd.Parameters.Add("UPD_USRID", "BWSYS");
                        cmd.Parameters.Add("UPD_DATE", Now.ToString("yyyyMMdd") ?? string.Empty);
                        cmd.Parameters.Add("UPD_TIME", Now.ToString("HHmmss") ?? string.Empty);
                        cmd.Parameters.Add("TRANS_NO", BfileModel.TRANS_NO ?? string.Empty);

                        cmd.Prepare();
                        cnt = cmd.ExecuteNonQuery();
                        //cnt = await cmd.ExecuteNonQueryAsync();

                        if (cnt == 0)
                            WriteLog($"SyncBfile-DoSyncB_AS400 TRANS_NO:{BfileModel.TRANS_NO}, WF_RUN_ID:{_WF_RUN_ID} no data updated...", BfileModel.TRANS_NO);
                        else
                            WriteLog($"SyncBfile-DoSyncB_AS400 TRANS_NO:{BfileModel.TRANS_NO}, WF_RUN_ID:{_WF_RUN_ID} updated...", BfileModel.TRANS_NO);

                        cmd.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"SyncBfile-DoSyncB_AS400 ERROR：{ex}", BfileModel.TRANS_NO, Ref.Nlog.Error);
                cnt = -1;
            }
            //WriteLog($"SyncBfile-DoSyncB_AS400 end...", BfileModel.TRANS_NO);
            return cnt;
        }
    }
}

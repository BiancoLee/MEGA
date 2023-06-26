using Dapper;
using SYNC_STS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.EasycomClient;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using static SYNC_STS.Utilitys.Utility;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS.Daos
{
    class BFilePRG
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["GLSIEXT"].ConnectionString;
        private static string connectionString400 = ConfigurationManager.ConnectionStrings["Easycom"].ConnectionString;
        internal static async Task<MSGReturnModel> CallBFilePRG()
        {
            //WriteLog($"BfilePRG-CallBFilePRG start...", "BfilePRG");
            DateTime now = DateTime.Now;

            //取中介平台B File資料
            MSGReturnModel<List<FGLBCTL0>> BfileModel = await getBfilePRG();

            if (BfileModel.Datas.Count > 0)
            {
                //1.新增BWLog、取 WF_RUN_ID
                int _WF_RUN_ID = await StartEndTime.logStartTime(WF_TYPE.Sync_B_PRGO, "BFilePRG");
                if (_WF_RUN_ID == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };

                foreach (var _data in BfileModel.Datas)
                {
                    using (TransactionScope Scope = new TransactionScope())
                    {
                        int cnt = CallAS400PGM(_data);
                        if (cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }

                        int Workflow_cnt = Workflow.AddWorkflow(_WF_RUN_ID, _data.TRANS_NO);
                        if (Workflow_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }

                        int BPRG_GLSI_cnt = Update_GLSI(_data, now);
                        if (BPRG_GLSI_cnt == -1)
                        {
                            Scope.Dispose();
                            break;
                        }

                        if (cnt != -1 && Workflow_cnt != -1 && BPRG_GLSI_cnt != -1)
                        {
                            Scope.Complete();
                            WriteLog($"BfilePRGScope complete...", _data.TRANS_NO);
                        }
                        Scope.Dispose();
                    }               
                }

                //更新BWLog
                var writeBWLog_cnt = await StartEndTime.logEndTime(_WF_RUN_ID, "BFilePRG");
                if (writeBWLog_cnt == 0)
                    return new MSGReturnModel() { RETURN_FLAG = false };
            }
            //WriteLog($"BfilePRG-CallBFilePRG end...", "BfilePRG");
            return new MSGReturnModel() { RETURN_FLAG = true };
        }

        private static async Task<MSGReturnModel<List<FGLBCTL0>>> getBfilePRG()
        {
            //WriteLog($"BfilePRG-GetBfilePRG start...", "BfilePRG");
            MSGReturnModel<List<FGLBCTL0>> result = new MSGReturnModel<List<FGLBCTL0>>();
            result.RETURN_FLAG = false;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;
                    str = $@"select TRANS_NO, FLOW_TYPE from FGLBCTL0S(NOLOCK) where TRIG_STS = '01' and TRANS_STS = 'TRS' ";

                    var _BfileList = await connGLSI.QueryAsync<FGLBCTL0>(str);

                    if (_BfileList != null)
                    {
                        result.Datas = _BfileList.ToList();
                        result.RETURN_FLAG = true;
                    }
                }
                if (result.Datas != null && result.Datas?.Count > 0)
                    WriteLog($"BfilePRG-GetBfilePRG 筆數:{result.Datas?.Count ?? 0}...", "BfilePRG");
            }
            catch (Exception ex)
            {
                WriteLog($"BfilePRG-GetBfilePRG ERROR：{ex}", "BfilePRG", Ref.Nlog.Error);
            }

            return result;
        }

        private static int CallAS400PGM(FGLBCTL0 BfileModel)
        {
            //WriteLog($"BfilePRG-CallAS400PGM start...", BfileModel.TRANS_NO);

            int cnt = 0;
            try
            {
                using (EacConnection conn400 = new EacConnection(connectionString400))
                {
                    conn400.Open();
                    using (EacCommand cmd = new EacCommand(conn400))
                    {

                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = "*PGM/PGLB000";

                        cmd.Parameters.Clear();
                        EacParameter inputCode = new EacParameter();
                        inputCode.ParameterName = "LK-REC";
                        inputCode.DbType = DbType.String;
                        inputCode.Size = 21;
                        inputCode.Direction = ParameterDirection.Input;
                        inputCode.Value = $"{BfileModel.FLOW_TYPE}{BfileModel.TRANS_NO}".PadRight(21, ' ');
                        cmd.Parameters.Add(inputCode);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                        WriteLog($"BfilePRG-CallAS400PGM InputCode:{ BfileModel.FLOW_TYPE}{ BfileModel.TRANS_NO} done", BfileModel.TRANS_NO);
                    }
                }
            }
            catch (Exception ex)
            {
                cnt = -1;
                WriteLog($"BfilePRG-CallAS400PGM ERROR：{ex}", BfileModel.TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"BfilePRG-CallAS400PGM end...", BfileModel.TRANS_NO);
            return cnt;
        }

        private static /*async Task<int>*/ int Update_GLSI(FGLBCTL0 BfileModel, DateTime Now)
        {
            //WriteLog($"BfilePRG-Update_GLSI start...", BfileModel.TRANS_NO);
            int cnt = 0;

            try
            {
                using (SqlConnection connGLSI = new SqlConnection(connectionString))
                {
                    string str = string.Empty;

                    str = @"
update FGLBCTL0S set 
TRIG_STS=@TRIG_STS,
UPD_DT=@UPD_DT,
UPD_USRID=@UPD_USRID
where TRANS_NO=@TRANS_NO
";
                    cnt = connGLSI.Execute(str, new { TRIG_STS = "02", UPD_DT = Now, UPD_USRID = "BWSYS", BfileModel.TRANS_NO });
                    //cnt = connGLSI.Execute(str, new { TRIG_STS = BfileModel.TRANS_STS, UPD_DT = Now, UPD_USRID = "BWSYS", BfileModel.TRANS_NO });
                    //cnt = await connGLSI.ExecuteAsync(str, new { TRIG_STS = BfileModel.TRANS_STS, UPD_DT = Now, UPD_USRID = "BWSYS", BfileModel.TRANS_NO });
                }
                if (cnt == 0)
                    WriteLog($"BfilePRG-Update_GLSI TRANS_NO:{BfileModel.TRANS_NO}, TRIG_STS: 02 no data updated...", BfileModel.TRANS_NO);
                //WriteLog($"BfilePRG-Update_GLSI TRANS_NO:{BfileModel.TRANS_NO}, TRIG_STS:{BfileModel.TRANS_STS} no data updated...", BfileModel.TRANS_NO);
                else
                    WriteLog($"BfilePRG-Update_GLSI TRANS_NO:{BfileModel.TRANS_NO}, TRIG_STS: 02 updated...", BfileModel.TRANS_NO);
                //WriteLog($"BfilePRG-Update_GLSI TRANS_NO:{BfileModel.TRANS_NO}, TRIG_STS:{BfileModel.TRANS_STS} updated...", BfileModel.TRANS_NO);
            }
            catch (Exception ex)
            {
                cnt = -1;
                WriteLog($"BfilePRG-Update_GLSI ERROR：{ex}", BfileModel.TRANS_NO, Ref.Nlog.Error);
            }
            //WriteLog($"BfilePRG-Update_GLSI end...", BfileModel.TRANS_NO);
            return cnt;
        }
    }
}

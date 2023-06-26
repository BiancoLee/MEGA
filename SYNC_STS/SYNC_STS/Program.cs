using SYNC_STS.Daos;
using SYNC_STS.Utilitys;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using static SYNC_STS.Utilitys.Extension;

namespace SYNC_STS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            WriteLog($"Program start...", "SYNC_STS");
            //檢查作業時間
            var runTime = CheckShutDown.getRunTime();
            string _start = runTime?.FirstOrDefault(x => x.VAR_NAME == "SHUTDNTIME_START")?.VAR_VALUE;
            string _end = runTime?.FirstOrDefault(x => x.VAR_NAME == "SHUTDNTIME_END")?.VAR_VALUE;
            if(!_start.IsNullOrWhiteSpace() && !_end.IsNullOrWhiteSpace())
            {
                bool WorkingTime = CheckShutDown.checkWorkingTime(_start, _end);
                if (!WorkingTime)
                    return;
            }
            //else
            //{
            //    WriteLog($"Program 查無作業時間...", "SYNC_STS");
            //    return;
            //}

            //DateTime _now = DateTime.Now;
            string _hour = "02";
            string _minites = "00";
            string _defult_hour = ConfigurationManager.AppSettings["Hour"] ?? string.Empty;
            string _defult_min = ConfigurationManager.AppSettings["Minutes"] ?? string.Empty;
            DateTime _temp;
            DateTime _startT = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(_defult_hour) && !string.IsNullOrWhiteSpace(_defult_min))
            {
                _hour = _defult_hour;
                _minites = _defult_min;

            }
            string dateString = $"{_startT.Year}/{_startT.Month}/{_startT.Day} {_hour}:{_minites}:00";
            if (DateTime.TryParse(dateString, out _temp))
            {
                _startT = _temp;
            }

            var DoSyncAfile = SyncAfile.DoSyncAfile();
            var DoSyncBfile = SyncBfile.DoSyncBfile();
            var CallBFilePRG = BFilePRG.CallBFilePRG();
            var DeleteLogFiles = Workflow.DeleteLogFiles(_startT);
            var ABfileTasks = new List<Task> { DoSyncAfile, DoSyncBfile, CallBFilePRG, DeleteLogFiles };

            while (ABfileTasks.Count > 0)
            {
                Task finishedTask = await Task.WhenAny(ABfileTasks);
                if (finishedTask == DoSyncAfile)
                {
                    WriteLog($"DoSyncAfile done...", "SYNC_STS");
                }
                if (finishedTask == DoSyncBfile)
                {
                    WriteLog($"DoSyncBfile done...", "SYNC_STS");
                }
                if (finishedTask == CallBFilePRG)
                {
                    WriteLog($"CallBFilePRG done...", "SYNC_STS");
                }
                if (finishedTask == DeleteLogFiles)
                {
                    WriteLog($"DeleteLogFiles done...", "SYNC_STS");
                }

                ABfileTasks.Remove(finishedTask);
            }
            WriteLog($"Program end...", "SYNC_STS");
        }
    }
}

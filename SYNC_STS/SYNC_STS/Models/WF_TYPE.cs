using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SYNC_STS.Models
{
    public static class WF_TYPE
    {
        [Description("SyncAfile")]
        public static string Sync_A { get; set; }
        [Description("SyncBfile")]
        public static string Sync_B { get; set; }
        [Description("BFilePRG")]
        public static string Sync_B_PRGO { get; set; }
        [Description("DeleteLogFiles")]
        public static string Clean_Data { get; set; }

        static WF_TYPE()
        {
            #region WF_TYPE資料
            Sync_A = "13010";
            Sync_B = "13020";
            Sync_B_PRGO = "13030";
            Clean_Data = "13040";
            #endregion WF_TYPE資料
        }
    }
}

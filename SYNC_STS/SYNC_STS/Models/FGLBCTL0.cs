using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SYNC_STS.Models
{
    //AS400
    class FGLBCTL0
    {
        public string TRANS_NO { get; set; }
        public string TRANS_STS { get; set; }
        public string FLOW_TYPE { get; set; }
        public string ERR_TYPE { get; set; }
        public string ERR_CODE { get; set; }
        public string IMP_DATE { get; set; }
        public string IMP_TIME { get; set; } 
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SYNC_STS.Models
{
    //GLSI
    class FGLGCTL0S
    {
        public string TRANS_NO { get; set; }
        public string FLOW_TYPE { get; set; }
        public string TRANS_STS { get; set; }
        public string ERR_TYPE { get; set; }
        public string ERR_CODE { get; set; }
        public DateTime IMP_DT { get; set; }
        public string TRNS_SEQ { get; set; }
        public string STATUS { get; set; }
    }
}

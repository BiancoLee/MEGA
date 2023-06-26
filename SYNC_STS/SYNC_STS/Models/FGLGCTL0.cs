using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SYNC_STS.Models
{
    //AS400
    class FGLGCTL0
    {
        public string TRANS_STS { get; set; }
        public string UPD_USRID { get; set; }
        public string UPD_DATE { get; set; }
        public string UPD_TIME { get; set; }
        public string IMP_DATE { get; set; }
        public string IMP_TIME { get; set; }
        public string ERR_TYPE { get; set; }
        public string ERR_CODE { get; set; }
        public string SQL_NO { get; set; }
    }
}

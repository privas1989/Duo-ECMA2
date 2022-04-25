using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FimSync_Ezma
{
    class DuoUser
    {
        public string username { set; get; }
        public string email { set; get; }
        public string firstname { set; get; }
        public string lastname { set; get; }
        public string realname { set; get; }
        public string status { set; get; }
        public string user_id { set; get; }
        public string alias1 { set; get; }
        public string alias2 { set; get; }
        public string alias3 { set; get; }
        public string alias4 { set; get; }
        public int created { set; get; }
        public bool is_enrolled { set; get; }
        public string notes { set; get; }
    }
}

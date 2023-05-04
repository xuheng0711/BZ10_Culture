using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BZ10.Model
{
    public class MQTTClientInfo
    {
        public MQTTClient result { get; set; }
        public int code { get; set; }
        public string msg { get; set; }
    }
    public class MQTTClient
    {
        public string clientId { get; set; }
        public string passwords { get; set; }
        public string userName { get; set; }
    }
}

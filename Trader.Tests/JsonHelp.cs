using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trader.Tests
{
    public static class JsonHelp
    {
        public static string Json(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Generator
{
    public class JSONBack
    {
        [JsonProperty("CellID")]
        public Int64 CellID;

        [JsonProperty("hash")]
        public String hash;

        [JsonProperty("time")]
        public Int64 time;

        [JsonProperty("ins")]
        public List<Input> ins;

        [JsonProperty("outs")]
        public List<string> outs;

        [JsonProperty("amount")]
        public Int64 amount;

        public static JSONBack ConvertToJSONBack(string jsonStr)
        {
            JSONBack JSONBack = new JSONBack();
            try
            {
                JSONBack = JsonConvert.DeserializeObject<JSONBack>(jsonStr);
            }
            catch (Exception e)
            {
                return null;
            }
            return JSONBack;
        }
    }

    public class Input
    {
        public Input(string _addr, Int64 _tx_index)
        {
            this.addr = _addr;
            this.tx_index = _tx_index;
        }

        [JsonProperty("addr")]
        public string addr;

        [JsonProperty("tx_index")]
        public Int64 tx_index;
    }
}

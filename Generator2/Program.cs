using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trinity;
using TSLBit;
using System.IO;
using Trinity.Storage;
using Trinity.TSL.Lib;
using Newtonsoft.Json;

namespace Generator2
{
    class BitQuery : BitQueryBaseBase
    {
        public override void IsAmountHandler(LongReq request, out IntRes response)
        {
            Console.WriteLine("foreach begin...");
            int count = 0;
            List<Int64> cellIDs = new List<long>();
            foreach (Tx tx in Global.LocalStorage.Tx_Accessor_Selector())
            {
                if (tx.amount > request.value)
                {
                    count++;
                    cellIDs.Add(tx.CellID);
                }
            }
            Console.WriteLine("update begin...");
            foreach (Int64 cellID in cellIDs)
            {
                using (var tx = Global.LocalStorage.UseTx(cellID))
                {
                    tx.isAmount = true;
                }
            }
            response.value = count;
        }

        struct freqPara
        {
            public Int64 CellID;
            public List<string> addrs;
            public Int64 time;
        }

        public override void IsFreqHandler(LongReqReader request, IntResWriter response)
        {
            Queue<freqPara> txsQueue = new Queue<freqPara>();
            foreach (Tx tx in Global.LocalStorage.Tx_Selector())
            {
                while (tx.time - txsQueue.Peek().time > request.value)
                    txsQueue.Dequeue();

                List<string> addrs = new List<string>();
                foreach (In _in in tx.ins)
                    addrs.Add(_in.addr);
                foreach (string _out in tx.outs)
                    addrs.Add(_out);

                foreach (freqPara pre in txsQueue)
                {
                    foreach (string addr in pre.addrs)
                    {
                        if (addrs.Contains(addr))
                        {
                            Tx preTx = Global.LocalStorage.UseTx(pre.CellID);
                            preTx.isFreq = true;
                            Tx _tx = Global.LocalStorage.UseTx(tx.CellID);
                            _tx.isFreq = true;
                        }
                    }
                }

                freqPara para;
                para.CellID = tx.CellID;
                para.addrs = addrs;
                para.time = tx.time;
                txsQueue.Enqueue(para);
            }
        }

        public override void IsCircleHandler(LongReqReader request, IntResWriter response)
        {

        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            BitQueryBase server = new BitQueryBase();
            server.Start();
            LoadBlocks();
        }

        public static void LoadBlocks()
        {
            using (StreamReader reader = new StreamReader("../../../block.txt"))
            {
                string line;
                int count = 0;
                while (null != (line = reader.ReadLine()))
                {
                    JSONBack jsonBack = ConvertToJSONBack(line);
                    List<In> ins = new List<In>();
                    foreach (Input _in in jsonBack.ins)
                    {
                        In __in = new In(_in.addr, _in.tx_index);
                        ins.Add(__in);
                    }
                    try
                    {
                        Tx tx = new Tx(jsonBack.CellID, jsonBack.time, jsonBack.hash, ins, jsonBack.outs, jsonBack.amount, Convert.ToBoolean(jsonBack.isAmount), Convert.ToBoolean(jsonBack.isFreq), Convert.ToBoolean(jsonBack.isCycle));
                        Global.LocalStorage.SaveTx(tx);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        throw;
                    }

                    count++;
                }
                Console.WriteLine(count);
            }
        }

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
}
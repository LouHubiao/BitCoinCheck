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
using System.Threading;

namespace Generator
{
    class BitQuery : BitQueryBaseBase
    {

    }

    internal class Program
    {
        struct freqPara
        {
            public Int64 CellID;
            public List<string> addrs;
            public Int64 time;
        }

        static void Main(string[] args)
        {

            BitQuery server = new BitQuery();
            server.Start();
            LoadBlocks(2000000000, 1, 2);
            updateCircle(2);


            //LongReqWriter reqMsg = new LongReqWriter(1);
            //IntResReader resMsg = Global.CloudStorage.IsFreqToBitQueryBase(0, reqMsg);
            //Console.WriteLine(resMsg.value);

            Global.LocalStorage.SaveStorage();
            Console.WriteLine("Main done...");
        }

        //load block and update isAmount and isFreq
        public static void LoadBlocks(long amountMax, long timespan, int step)
        {
            Console.WriteLine("LoadBlocks begin...");

            //load cellIDs
            List<Int64> cellIDs = new List<Int64>();
            using (StreamReader cellIDsReader = new StreamReader(@"D:\Bit\cellIDs.txt"))
            {
                string cellIDLine;
                while (null != (cellIDLine = cellIDsReader.ReadLine()))
                {
                    cellIDs.Add(Int64.Parse(cellIDLine));
                }
            }

            //read json line by line
            using (StreamReader reader = new StreamReader(@"D:\Bit\block.txt"))
            {
                //statistics some characters
                int count = 0;

                Queue<freqPara> txsQueue = new Queue<freqPara>();
                List<Int64> cellIDsFreq = new List<long>();
                Int64 preCellID = -1;
                Int64 preTime = -1;

                //for circle
                List<Int64> circleTxs = new List<Int64>();

                string line;
                while (null != (line = reader.ReadLine()))
                {
                    //convert string to object
                    JSONBack jsonBack = ConvertToJSONBack(line);
                    if (jsonBack.amount > 0)
                    {
                        //convert List<Input> to List<In> for compatible
                        List<In> ins = new List<In>();
                        List<Int64> inIDs = new List<long>();
                        foreach (Input _in in jsonBack.ins)
                        {
                            In __in = new In(_in.tx_index, _in.addr);
                            ins.Add(__in);
                            inIDs.Add(_in.tx_index);
                        }

                        //high frequency from second 
                        if (jsonBack.time - preTime > timespan)
                        {
                            if (txsQueue.Count > 0)
                                txsQueue.Clear();
                        }
                        else
                        {
                            while (txsQueue.Count > 0 && jsonBack.time - txsQueue.Peek().time > timespan)
                                txsQueue.Dequeue();

                            List<string> addrs = new List<string>();
                            foreach (Input _in in jsonBack.ins)
                                addrs.Add(_in.addr);
                            foreach (string _out in jsonBack.outs)
                                addrs.Add(_out);

                            foreach (freqPara pre in txsQueue)
                            {
                                foreach (string addr in pre.addrs)
                                {
                                    if (addrs.Contains(addr))
                                    {
                                        if (!cellIDsFreq.Contains(pre.CellID))
                                        {
                                            cellIDsFreq.Add(pre.CellID);
                                        }
                                        //count++;
                                    }
                                }
                            }
                            freqPara para;
                            para.CellID = jsonBack.CellID;
                            para.addrs = addrs;
                            para.time = jsonBack.time;
                            txsQueue.Enqueue(para);
                        }
                        preCellID = jsonBack.CellID;
                        preTime = jsonBack.time;

                        try
                        {
                            Tx tx = new Tx(jsonBack.CellID, jsonBack.time, jsonBack.hash, ins, inIDs, jsonBack.outs, jsonBack.amount, (int)(jsonBack.amount / amountMax), cellIDsFreq.Count);
                            Global.LocalStorage.SaveTx(tx);
                            //if (jsonBack.amount > amountMax)
                            //{
                            //    Int64 cellID = jsonBack.CellID;
                            //    Thread t = new Thread(() => { DFSFind(step, 0, cellID, jsonBack.outs, cellID, circleTxs, cellIDs); });
                            //    t.IsBackground = true;
                            //    t.Start();
                            //    //DFSFind(step, 0, cellID, jsonBack.outs, cellID, circleTxs, cellIDs);
                            //}
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            throw;
                        }

                        if (cellIDsFreq.Count > 0)
                        {
                            foreach (Int64 cellID in cellIDsFreq)
                            {
                                using (var tx = Global.LocalStorage.UseTx(cellID))
                                {
                                    if (tx.isFreq < cellIDsFreq.Count)
                                        tx.isFreq = cellIDsFreq.Count;
                                }
                            }
                            cellIDsFreq.Clear();
                        }
                    }
                }
                foreach (Int64 cellID in circleTxs)
                {
                    count++;
                    using (var tx = Global.LocalStorage.UseTx(cellID))
                    {
                        tx.isCycle = true;
                    }
                }
                Console.WriteLine(count);
            }
        }


        public struct Indulge
        {
            public Int64 cellID;
            public string addrBegin;
            public string addrEnd;
        }

        public static void updateCircle(int step)
        {
            Console.WriteLine("updateCircle begin...");
            int count = 0;
            List<Int64> circleTxs = new List<Int64>();
            //List<Indulge> indulges = new List<Indulge>();

            //read cellIDs
            List<Int64> cellIDs = new List<Int64>();
            using (StreamReader reader = new StreamReader(@"D:\Bit\cellIDs.txt"))
            {
                string line;
                while (null != (line = reader.ReadLine()))
                {
                    cellIDs.Add(Int64.Parse(line));
                }
            }

            foreach (Int64 cellID in cellIDs)
            {
                List<string> outs = new List<string>();
                using (var tx = Global.LocalStorage.UseTx(cellID, CellAccessOptions.ReturnNullOnCellNotFound))
                {
                    if (tx == null)
                        continue;
                    //Console.WriteLine("foreach begin..." + tx.CellID);
                    foreach (string _out in tx.outs)
                    {
                        outs.Add(_out);
                    }
                }
                Int64 cellIDCopy = cellID;
                Thread t = new Thread(() => { DFSFind(step, 0, cellIDCopy, outs, cellIDCopy, circleTxs, cellIDs); });
                t.IsBackground = true;
                t.Start();
                //DFSFind(step, 0, cellID, outs, cellID, circleTxs, cellIDs);
            }
            foreach (Int64 cellID in circleTxs)
            {
                count++;
                using (var tx = Global.LocalStorage.UseTx(cellID))
                {
                    tx.isCycle = true;
                }
            }
            Console.WriteLine(count);
            Console.WriteLine("updateCircle end...");
        }

        public static bool DFSFind(int step, int stepNow, Int64 inCellID, List<string> outs, Int64 outCellID, List<Int64> circleTxs, List<Int64> cellIDs)
        {
            if (stepNow >= step)
                return false;

            List<Input> ins = new List<Input>();
            using (var tx = Global.LocalStorage.UseTx(inCellID, CellAccessOptions.ReturnNullOnCellNotFound))
            {
                if (tx == null)
                    return false;
                foreach (In _in in tx.ins)
                {
                    ins.Add(new Input(_in.addr, _in.tx_index));
                }
            }

            if (stepNow != 0)
            {
                //before in = now out
                foreach (Input input in ins)
                {
                    foreach (string _out in outs)
                    {
                        if (input.addr == _out)
                        {
                            if (!circleTxs.Contains(outCellID))
                                circleTxs.Add(outCellID);
                            if (!circleTxs.Contains(inCellID))
                                circleTxs.Add(inCellID);
                            Console.WriteLine(outCellID);
                        }
                    }
                }
            }

            //compare paster
            List<Thread> threads = new List<Thread>();
            bool[] HasCircles = new bool[ins.Count];
            for (int i = 0; i < ins.Count; i++)
            {
                int index = i;
                Input input = ins[index];
                if (cellIDs.Contains(input.tx_index))
                {
                    Thread t = new Thread(() => { HasCircles[index] = DFSFind(step, stepNow + 1, input.tx_index, outs, outCellID, circleTxs, cellIDs); });
                    t.IsBackground = true;
                    t.Start();
                    threads.Add(t);
                    //HasCircles[index] = DFSFind(step, stepNow + 1, input.tx_index, outs, outCellID, circleTxs, cellIDs);
                }
            }
            foreach (Thread t in threads)
            {
                t.Join();
            }
            foreach (bool hasCircle in HasCircles)
            {
                if (hasCircle == true)
                {
                    lock (circleTxs)
                    {
                        if (!circleTxs.Contains(inCellID))
                            circleTxs.Add(inCellID);
                        return true;
                    }
                }
            }
            return false;
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
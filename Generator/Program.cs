using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Trinity;
using TSLBit;
using System.IO;
using Trinity.Storage;
using Trinity.TSL.Lib;
using Newtonsoft.Json;

namespace Generator
{
    internal class Program
    {
        const Int64 MAXAMOUNT = 5000000000; //transaction with amount abouve 50BTC is suspicious
        const int FREQ = 1;  //transactions from same body in one second is suspicious
        const int CYCLECOUNT = 2;   //transaction like A->B->A (and amount>MAXAMOUNT) is suspicious

        private static int nearbyLen = 100000;
        private static freqPara[] txsNearby = new freqPara[nearbyLen];  //cache txs nearby, for high frequent transactions
        private static int nearbyBegin = 0;
        private static int nearbyEnd = 0;
        private static List<Int64> circleTxs = new List<Int64>();   //txs list which has cycle

        /// <summary>
        /// example for test:
        /// 1B6RSnKdNNknzNePjfmf7rMGh5mi6ntv3x
        /// 1Cuju1Sw97U1SYSYuBiMdJz3NPrjNEzJtP
        /// 1F6F2JTK2abJLKBU1Fb2LQ1NJjucPQhRdx
        /// 1XPTgDRhN8RFnzniWCddobD9iKZatrvH4
        /// </summary>

        //freqPara for freq queue
        struct freqPara
        {
            public Int64 CellID;
            public List<string> addrsIn;
            public List<string> addrsOut;
            public Int64 time;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Main begin..." + DateTime.Now.ToString());
            BitQuery server = new BitQuery();
            server.Start();

            //Global.LocalStorage.LoadStorage();
            LoadOriginal();
            //Global.LocalStorage.SaveStorage();

            Console.WriteLine("Main done..." + DateTime.Now.ToString());
        }

        //load from json block file
        public static void LoadOriginal()
        {
            Console.WriteLine("LoadBlocks begin...");
            //load blocks
            DirectoryInfo dirInfo = new DirectoryInfo(@"D:\\Bit\\TSLBit\\Generator\\bin\x64\\Debug\\fullBlocks");
            foreach (FileInfo file in dirInfo.GetFiles("block1.txt"))
            {
                //read json line by line
                using (StreamReader reader = new StreamReader(file.FullName))
                {
                    string line;
                    while (null != (line = reader.ReadLine()))
                    {
                        //load a transaction, update isAmount, isFreq and isCycle
                        loadTx(line);
                    }
                }
            }

            ////update isCycle
            //foreach (Int64 cellID in circleTxs)
            //{
            //    using (var _tx = Global.LocalStorage.UseTx(cellID))
            //    {
            //        _tx.isCycle = true;
            //    }
            //}
            Console.WriteLine("LoadBlocks done...");
        }

        //load a transaction from json line
        private static void loadTx(string readLine)
        {
            //string to object
            JSONBack jsonBack = JSONBack.ConvertToJSONBack(readLine);

            //paraper parameters
            List<In> ins = new List<In>();
            List<string> addrsIn = new List<string>();
            List<string> addrsOut = new List<string>();
            foreach (Input _in in jsonBack.ins)
            {
                In __in = new In(_in.tx_index, _in.addr);
                ins.Add(__in);
                addrsIn.Add(_in.addr);
            }
            foreach (string _out in jsonBack.outs)
            {
                addrsOut.Add(_out);
            }

            ////Dequeue no use items
            //while (nearbyBegin != nearbyEnd && (jsonBack.time - txsNearby[nearbyBegin].time >= FREQ || jsonBack.time - txsNearby[nearbyBegin].time < 0))
            //    nearbyBegin = (nearbyBegin + 1) % nearbyLen;

            ////judge isFreq, only out
            //List<Int64> cellIDsFreq = new List<long>();
            //for (int nearbyIndex = nearbyBegin; nearbyIndex != nearbyEnd; nearbyIndex = (nearbyIndex + 1) % nearbyLen)
            //{
            //    foreach (string addr in txsNearby[nearbyIndex].addrsOut)
            //    {
            //        if (addrsOut.Contains(addr) && !cellIDsFreq.Contains(txsNearby[nearbyIndex].CellID))
            //        {
            //            cellIDsFreq.Add(txsNearby[nearbyIndex].CellID);
            //            //Console.WriteLine(addr);
            //            break;
            //        }
            //    }
            //}

            ////Enqueue next item
            //txsNearby[nearbyEnd].CellID = jsonBack.CellID;
            //txsNearby[nearbyEnd].addrsIn = addrsIn;
            //txsNearby[nearbyEnd].addrsOut = addrsOut;
            //txsNearby[nearbyEnd].time = jsonBack.time;
            //nearbyEnd = (nearbyEnd + 1) % nearbyLen;

            //Console.WriteLine(nearbyEnd-nearbyBegin);

            //save tx
            Tx tx = new Tx(jsonBack.CellID, jsonBack.time, jsonBack.hash, ins, jsonBack.outs, jsonBack.amount, (int)(jsonBack.amount / MAXAMOUNT), 0, false);
            Global.LocalStorage.SaveTx(tx);

            ////save TxCellID
            //TxCellID txCellID = new TxCellID(jsonBack.CellID);
            //Global.LocalStorage.SaveTxCellID(txCellID);

            //save addrCellIDs
            //foreach (string addr in addrsIn)
            //{
            //    Int64 addrHash = GetCellIDFromAddr(addr);
            //    using (var addrInfo = Global.LocalStorage.UseAddrInfo(addrHash, CellAccessOptions.CreateNewOnCellNotFound))
            //    {
            //        addrInfo.txs.Add(jsonBack.CellID);
            //    }
            //}
            foreach (string addr in jsonBack.outs)
            {
                Int64 addrHash = GetCellIDFromAddr(addr);
                using (var addrInfo = Global.LocalStorage.UseAddrInfo(addrHash, CellAccessOptions.CreateNewOnCellNotFound))
                {
                    addrInfo.txs.Add(jsonBack.CellID);
                }
            }

            //judege isCycle
            //List<Int64> isReading = new List<Int64>();
            //DFSFind(CYCLECOUNT, 0, jsonBack.CellID, jsonBack.ins, jsonBack.outs, circleTxs, isReading, MAXAMOUNT);
        }

        public static bool DFSFind(int stepSum, int stepNow, Int64 inCellID, List<Input> ins, List<string> targetOuts, List<Int64> results, List<Int64> isReading, Int64 cycleAmount)
        {
            bool isCycle = false;

            if (!isReading.Contains(inCellID))
            {
                isReading.Add(inCellID);
                //judge is cycle
                if (stepNow != 0)
                {
                    foreach (Input input in ins)
                    {
                        foreach (string _out in targetOuts)
                        {
                            if (input.addr == _out)
                            {
                                if (!results.Contains(inCellID))
                                {
                                    isCycle = true;
                                    break;
                                }
                            }
                        }
                        if (isCycle == true)
                            break;
                    }
                }

                //recursive
                if (stepNow < stepSum - 1)
                {
                    for (int i = 0; i < ins.Count; i++)
                    {
                        int index = i;
                        Input input = ins[index];
                        if (!isReading.Contains(input.tx_index))
                        {
                            List<Input> nextIns = new List<Input>();
                            //Console.WriteLine("UseTx:" + input.tx_index);
                            using (var tx = Global.LocalStorage.UseTx(input.tx_index, CellAccessOptions.ReturnNullOnCellNotFound))
                            {
                                if (tx == null || tx.amount < cycleAmount)
                                {
                                    continue;
                                }
                                foreach (In _in in tx.ins)
                                {
                                    nextIns.Add(new Input(_in.addr, _in.tx_index));
                                }
                                if (DFSFind(stepSum, stepNow + 1, input.tx_index, nextIns, targetOuts, results, isReading, cycleAmount))
                                {
                                    isCycle = true;
                                }
                            }
                        }
                    }
                }

                //return true if judged is cycle or in DFS path
                if (isCycle)
                {
                    results.Add(inCellID);
                    return true;
                }
            }
            return false;
        }

        //Get hash from 64byte characters for cell AddrInfo
        public static Int64 GetCellIDFromAddr(string addr)
        {
            Int64 hashLeft = System.Math.Abs(addr.Substring(0, addr.Length / 2).GetHashCode());
            Int64 hashRight = System.Math.Abs(addr.Substring(addr.Length / 2).GetHashCode());
            return (hashLeft << 32) + hashRight;
        }
    }
}
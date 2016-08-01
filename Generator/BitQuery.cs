using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trinity;
using Trinity.TSL.Lib;
using TSLBit;

namespace Generator
{
    class BitQuery : BitQueryBase
    {
        const Int64 MAXAMOUNT = 5000000000; //default max amount

        struct TxInfo
        {
            public Int64 cellID;
            public string hash;
            public Int64 amount;
            public Int64 time;
            public List<Input> ins;
            public List<string> outs;
            public int isAmount;
            public int isFreq;
            public bool isCycle;
        }

        struct freqPara
        {
            public Int64 CellID;
            public List<string> addrsIn;
            public List<string> addrsOut;
            public Int64 time;
        }

        public override void GetTxByAddrHttpHandler(addrRequest request, out jsonRes response)
        {
            Int64 amount = request.amount < 0 ? MAXAMOUNT : request.amount;

            //response json
            StringBuilder res = new StringBuilder();
            res.Append("[");

            //get transactions about address
            Int64 addrHash = GetCellIDFromAddr(request.addr);
            List<Int64> addr_cellIDs = new List<long>();
            using (var cellIDs = Global.LocalStorage.UseAddrInfo(addrHash, CellAccessOptions.ReturnNullOnCellNotFound))
            {
                if (cellIDs == null)
                {
                    response.value = "";
                    return;
                }
                foreach (Int64 cellID in cellIDs.txs)
                {
                    addr_cellIDs.Add(cellID);
                }
            }

            //get txs info
            List<TxInfo> txInfos = new List<TxInfo>();
            foreach (Int64 cellID in addr_cellIDs)
            {
                using (var tx = Global.LocalStorage.UseTx(cellID))
                {
                    TxInfo txInfo;
                    txInfo.cellID = (Int64)tx.CellID;
                    txInfo.hash = tx.hash;
                    txInfo.amount = tx.amount;
                    txInfo.time = tx.time;
                    txInfo.ins = new List<Input>();
                    foreach (In _in in tx.ins)
                    {
                        txInfo.ins.Add(new Input(_in.addr, _in.tx_index));
                    }
                    txInfo.outs = new List<string>();
                    foreach (string _out in tx.outs)
                    {
                        txInfo.outs.Add(_out);
                    }
                    txInfo.isAmount = tx.isAmount;
                    txInfo.isFreq = tx.isFreq;
                    txInfo.isCycle = tx.isCycle;
                    txInfos.Add(txInfo);
                }
            }

            //get suspicious info
            foreach (TxInfo txInfo in txInfos)
            {
                StringBuilder resPart = new StringBuilder();
                resPart.Append(@"{ ""hashVal"":""" + txInfo.hash + @""",");
                bool noAmount = false;
                bool noFreq = false;
                bool noCycle = false;
                //judge amount
                if (request.amount < 1)
                {
                    resPart.Append(@"""isAmount"":" + txInfo.isAmount + @",");
                    if (txInfo.isAmount == 0)
                        noAmount = true;
                }
                else
                {
                    if (txInfo.amount > request.amount)
                    {
                        resPart.Append(@"""isAmount"":" + (int)(txInfo.amount / request.amount) + @",");
                    }
                    else
                    {
                        noAmount = true;
                        resPart.Append(@"""isAmount"":0,");
                    }
                }
                //judge freq
                if (request.freq < 1)
                {
                    resPart.Append(@"""isFreq"":" + txInfo.isFreq + @",");
                    if (txInfo.isFreq == 0)
                        noFreq = true;
                }
                else
                {
                    int freqResult = 0;
                    Int64 neighborCellID = txInfo.cellID - 1;
                    while (true)
                    {
                        using (var tx = Global.LocalStorage.UseTx(neighborCellID, CellAccessOptions.ReturnNullOnCellNotFound))
                        {
                            if (tx == null || tx.amount < request.freq_amount || (request.freq_amount < 0 && tx.amount < amount))
                                continue;
                            if (tx.time - txInfo.time < 0 || txInfo.time - tx.time > request.freq)
                                break;
                            bool isTargetTx = false;
                            foreach (string _out in tx.outs)
                            {
                                if (_out == request.addr)
                                {
                                    isTargetTx = true;
                                    break;
                                }
                            }
                            if (isTargetTx == false)
                                continue;
                            foreach (string _out in tx.outs)
                            {
                                if (_out == request.addr)
                                {
                                    foreach (string __out in txInfo.outs)
                                    {
                                        if (_out == __out)
                                            freqResult++;
                                    }
                                }
                            }
                        }
                        neighborCellID--;
                    }
                    resPart.Append(@"""isFreq"":" + freqResult + @",");
                    if (freqResult == 0)
                    {
                        noFreq = true;
                    }
                }
                //judge cycle
                if (request.cycle < 1)
                {
                    resPart.Append(@"""isCycle"":""" + txInfo.isCycle + @"""},");
                    if (txInfo.isCycle == false)
                        noCycle = true;
                }
                else
                {
                    List<Int64> circleTxs = new List<Int64>();
                    List<Int64> isReading = new List<Int64>();
                    if (Program.DFSFind(request.cycle, 0, txInfo.cellID, txInfo.ins, txInfo.outs, circleTxs, isReading, request.cycle_amount < 0 ? amount : request.cycle_amount) == true)
                    {
                        resPart.Append(@"""isCycle"":""" + true + @"""},");
                    }
                    else
                    {
                        noCycle = true;
                        resPart.Append(@"""isCycle"":""" + false + @"""},");
                    }
                }
                //add resPart
                if (noAmount != true || noFreq != true || noCycle != true)
                {
                    res.Append(resPart);
                }
            }

            if (res.Length > 1)
                res.Remove(res.Length - 1, 1);
            res.Append("]");
            response.value = res.ToString();
        }

        public override void GetStatisticByDefaultHandler(out jsonRes response)
        {
            int amountResult = Global.LocalStorage.Tx_Accessor_Selector().Where(node => node.isAmount != 0).Count();
            int freqResult = Global.LocalStorage.Tx_Accessor_Selector().Where(node => node.isFreq != 0).Count();
            int cycleResult = Global.LocalStorage.Tx_Accessor_Selector().Where(node => node.isCycle).Count();
            response.value = @"{""amountResult"":" + amountResult + @",""freqResult"":" + freqResult + @",""cycleResult"":" + cycleResult + @"}";
        }

        public override void GetStatisticByAmountHttpHandler(amountRequest request, out jsonRes response)
        {
            Int64 inputAmount = request.amount;
            int result = Global.LocalStorage.Tx_Accessor_Selector().Where(node => node.amount >= request.amount).Count();
            response.value = result.ToString();
        }

        public override void GetStatisticByFreqHttpHandler(freqRequest request, out jsonRes response)
        {
            int result = 0;

            int nearbyLen = 100000;
            freqPara[] txsNearby = new freqPara[nearbyLen];  //cache txs nearby, for high frequent transactions
            int nearbyBegin = 0;
            int nearbyEnd = 0;

            foreach (var tx in Global.LocalStorage.Tx_Accessor_Selector())
            {
                if (tx.amount > request.amount)
                {
                    List<string> addrsIn = new List<string>();
                    List<string> addrsOut = new List<string>();
                    foreach (In _in in tx.ins)
                    {
                        addrsIn.Add(_in.addr);
                    }
                    foreach (string _out in tx.outs)
                    {
                        addrsOut.Add(_out);
                    }

                    while (nearbyBegin != nearbyEnd && (tx.time - txsNearby[nearbyBegin].time >= request.interval || tx.time - txsNearby[nearbyBegin].time < 0))
                        nearbyBegin = (nearbyBegin + 1) % nearbyLen;

                    for (int nearbyIndex = nearbyBegin; nearbyIndex != nearbyEnd; nearbyIndex = (nearbyIndex + 1) % nearbyLen)
                    {
                        foreach (string addr in txsNearby[nearbyIndex].addrsOut)
                        {
                            if (addrsOut.Contains(addr))
                            {
                                result++;
                                break;
                            }
                        }
                    }

                    //Enqueue next item
                    txsNearby[nearbyEnd].CellID = (Int64)tx.CellID;
                    txsNearby[nearbyEnd].addrsIn = addrsIn;
                    txsNearby[nearbyEnd].addrsOut = addrsOut;
                    txsNearby[nearbyEnd].time = tx.time;
                    nearbyEnd = (nearbyEnd + 1) % nearbyLen;
                }
            }
            response.value = result.ToString();
        }

        public override void GetStatisticByCycleHttpHandler(cycleRequest request, out jsonRes response)
        {
            int result = 0;
            List<Int64> circleTxs = new List<Int64>();
            List<Int64> isReading = new List<Int64>();
            List<Int64> cellIDs = new List<Int64>();
            foreach (var cell in Global.LocalStorage.TxCellID_Accessor_Selector())
            {
                cellIDs.Add(cell.cellID);
            }
            foreach (Int64 cellID in cellIDs)
            {
                List<Input> ins = new List<Input>();
                List<string> outs = new List<string>();
                bool isCycle = false;
                
                using (var tx = Global.LocalStorage.UseTx(cellID))
                {
                    foreach (In _in in tx.ins)
                    {
                        ins.Add(new Input(_in.addr, _in.tx_index));
                    }
                    foreach (string _out in tx.outs)
                    {
                        outs.Add(_out);
                    }
                    if (tx.isCycle == true)
                        isCycle = true;
                }
                if (isCycle == true)
                {
                    result++;
                }
                else if (Program.DFSFind(request.cycleCount, 0, cellID, ins, outs, circleTxs, isReading, request.amount) == true)
                {
                    result++;
                }
            }
            response.value = result.ToString();
        }

        //convert addr to cellID for quick location
        public Int64 GetCellIDFromAddr(string addr)
        {
            Int64 hashLeft = System.Math.Abs(addr.Substring(0, addr.Length / 2).GetHashCode());
            Int64 hashRight = System.Math.Abs(addr.Substring(addr.Length / 2).GetHashCode());
            return (hashLeft << 32) + hashRight;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trinity;
using TSLBit;

namespace Generator2
{
    internal class BitQueryBase : TSLBit.BitQueryBaseBase
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

        public override void IsCircleHandler(LongReqReader request, IntResWriter response)
        {
            // TODO: Add your code here
            throw new NotImplementedException();
        }

        public override void IsFreqHandler(LongReqReader request, IntResWriter response)
        {
            // TODO: Add your code here
            throw new NotImplementedException();
        }
    }
}
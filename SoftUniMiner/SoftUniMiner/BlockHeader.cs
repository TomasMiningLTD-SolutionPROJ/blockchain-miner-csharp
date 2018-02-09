using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftUniMiner
{
    class BlockHeader
    {
        public string BlockHash;
        public int Difficulty;
        public DateTime Timestamp;
        public ulong Nonce;
    }
}

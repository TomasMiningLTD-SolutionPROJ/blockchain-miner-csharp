using System;

namespace SoftUniMiner
{
    public class BlockHeader
    {
        public string BlockHash { get; set; }

        public int Difficulty { get; set; }

        public DateTime Timestamp { get; set; }

        public ulong Nonce { get; set; }
    }
}

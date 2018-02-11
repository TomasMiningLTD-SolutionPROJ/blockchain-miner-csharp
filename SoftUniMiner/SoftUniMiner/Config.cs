using System;

namespace SoftUniMiner
{
    public class Config
    {
        public string NodeURL { get; set; }

        public string GetTaskURL { get; set; }

        public string GivePoWURL { get; set; }

        public string MyAddress { get; set; }

        public TimeSpan UpdateInterval { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using System.Security.Cryptography;

namespace SoftUniMiner
{
    class Program
    {
        static BlockHeader header = new BlockHeader();
        static Config config = new Config();
        static Timer blockUpdateTimer;
        static CancellationTokenSource cts;
        static int MaxNonceLength = ulong.MaxValue.ToString().Length;
        static ulong NonceRangeStart = Convert.ToUInt64(Math.Pow(10, MaxNonceLength - 1));
        static int HashPerfCounter;

        static void Main(string[] args)
        {
            config.NodeURL = "http://localhost:5555/";
            config.MyAddress = "1337";
            config.GetTaskURL = $"mineBlock/{config.MyAddress}";
            config.GivePoWURL = $"mining/submit-block/{config.MyAddress}";
            config.UpdateInterval = TimeSpan.FromSeconds(3);

            UpdateBlock(null);
            blockUpdateTimer = new Timer(UpdateBlock, null, TimeSpan.Zero, config.UpdateInterval);

            int threads = Environment.ProcessorCount - 1;
            ulong chunkSize = (ulong.MaxValue - NonceRangeStart) / (ulong)threads;

            while (true)
            {
                cts = new CancellationTokenSource();

                if (header.BlockHash == null)
                {
                    continue;
                }

                var taskList = new List<Task<Tuple<string, ulong, string>>>();
                for (var i = 0; i < threads; i++)
                {
                    taskList.Add(MineRange(NonceRangeStart + (ulong)i * chunkSize, chunkSize, cts.Token));
                }

                Task.WaitAll(taskList.ToArray());
                var success = taskList.FirstOrDefault(n => n.Result != null);
                if (success != null)
                {
                    SubmitBlock(success.Result.Item1, success.Result.Item2, success.Result.Item3);
                }

                UpdateBlock(null);
            }
        }

        static Task<Tuple<string, ulong, string>> MineRange(ulong start, ulong length, CancellationToken cancel)
        {
            return Task.Run(() =>
            {
                if (header.BlockHash == null)
                {
                    return null;
                }

                byte[] blockHash = Encoding.UTF8.GetBytes(header.BlockHash);
                byte[] timestamp = Encoding.UTF8.GetBytes(UnixEpoch(header.Timestamp).ToString());
                byte[] startNonce = Encoding.UTF8.GetBytes(start.ToString());

                byte[] toHash = new byte[blockHash.Length + timestamp.Length + MaxNonceLength];
                Array.Copy(blockHash, toHash, blockHash.Length);
                Array.Copy(timestamp, 0, toHash, blockHash.Length, timestamp.Length);
                Array.Copy(startNonce, 0, toHash, blockHash.Length + timestamp.Length, startNonce.Length);

                SHA256Managed hasher = new SHA256Managed();

                for (var i = start; i < start + length; i++)
                {
                    if (cancel.IsCancellationRequested)
                    {
                        return null;
                    }

                    var res = hasher.ComputeHash(toHash);
                    Interlocked.Increment(ref HashPerfCounter);

                    if (CheckHash(res))
                    {
                        cts.Cancel();
                        var resString = BitConverter.ToString(res).Replace("-", "").ToLowerInvariant();
                        return Tuple.Create(i.ToString(), ulong.Parse(Encoding.UTF8.GetString(timestamp)), resString);
                    }

                    for (var j = toHash.Length - 1; j > toHash.Length - MaxNonceLength; j--)
                    {
                        if (toHash[j] != '9')
                        {
                            toHash[j]++;
                            break;
                        }
                        else
                        {
                            toHash[j] = (byte)'0';
                        }
                    }
                }

                return null;
            });
        }

        static void UpdateBlock(object state)
        {
            try
            {
                string res = new WebClient().DownloadString(config.NodeURL + config.GetTaskURL);
                dynamic obj = JsonConvert.DeserializeObject(res);

                if (header.BlockHash == null || !header.BlockHash.Equals(obj[0].blockDataHash.ToString()))
                {
                    header.BlockHash = obj[0].blockDataHash;
                    header.Difficulty = obj[0].difficulty;
                    header.Timestamp = DateTime.UtcNow;
                    header.Nonce = 0;

                    Console.WriteLine("New work {0}", header.BlockHash);
                }
            }
            catch
            {
                Console.WriteLine("Error fetching new block.");
            }

            var hashesSinceLastUpdate = Interlocked.Exchange(ref HashPerfCounter, 0);
            Console.WriteLine("Hashrate: {0} kh/s", Math.Round(hashesSinceLastUpdate / config.UpdateInterval.TotalSeconds / 1000));
        }

        static void SubmitBlock(string nonce, ulong timestamp, string blockHash)
        {
            var data = new { nounce = nonce, dateCreated = timestamp, blockHash = blockHash };
            var dataString = JsonConvert.SerializeObject(data);

            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                try
                {
                    var res = client.UploadString(config.NodeURL + config.GivePoWURL, "POST", dataString);
                    Console.WriteLine("Submit share response: {0}", res);
                    header.BlockHash = null;
                }
                catch
                {
                    Console.WriteLine("Error submiting response");
                }
            }
        }

        static bool CheckHash(byte[] hash)
        {
            var i = 0;

            for (; i < header.Difficulty / 2; i++)
            {
                if (hash[i] != 0)
                {
                    return false;
                }
            }

            if (header.Difficulty % 2 == 1 && hash[i] > 0x0f)
            {
                return false;
            }

            return true;
        }

        static ulong UnixEpoch(DateTime dt)
        {
            return Convert.ToUInt64(dt.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);
        }
    }
}

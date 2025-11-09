using System;
using System.Collections.Generic;
using System.Linq;

namespace CS3103Assignment4
{
    class UnreliableChannel : Channel
    {
        private List<byte[]> _receiveBuffer = new List<byte[]>();

        private UnreliableMetricsCollector _metrics = new UnreliableMetricsCollector();

        public UnreliableChannel()
        {
            this._metrics.Start();
        }

        public virtual void ListenForConnection(ushort port)
        {
            
        }

        public virtual void Connect()
        {
            
        }

        public virtual void Send(byte[] data, int length)
        {
            this.Sender(data, length);
            this._metrics.AddSentPackets();
        }

        public override void OnReceivedData(byte[] data)
        {
            this._receiveBuffer.Add(data);
            this._metrics.AddReceivedPackets(data.Length);
        }

        public override byte[][] GetPackets()
        {
            byte[][] packets = this._receiveBuffer.ToArray();
            this._receiveBuffer.Clear();
            return packets;
        }

        public override void PrintMetrics()
        {
            this._metrics.PrintSummary();
        }
    }

    class UnreliableMetricsCollector : BaseMetricsCollector
    {
        string name = "Unreliable Channel";
        public override void PrintSummary()
        {
            double avgLatency = this.latencies.Count > 0 ? this.latencies.Average() : 0;
            double jitter = ComputeJitter();
            double throughput = ComputeThroughput();
            double pdr = ComputePDR();
            double duration = (DateTime.Now - this.connectionStartTime).TotalSeconds;

            Console.WriteLine($"\n=== {this.name} Metrics Summary ===");
            Console.WriteLine($"Average Latency: {avgLatency:F3} ms");
            Console.WriteLine($"Average Jitter: {jitter:F3} ms");
            Console.WriteLine($"Throughput: {throughput:F2} bytes/sec");
            Console.WriteLine($"Packet Delivery Ratio: {pdr:F2}%");
            Console.WriteLine($"Test Duration: {duration:F2} sec");
            Console.WriteLine("==========================\n");
        }
    }
}

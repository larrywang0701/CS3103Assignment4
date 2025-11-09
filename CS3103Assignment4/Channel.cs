using System;

namespace CS3103Assignment4
{
    abstract class Channel
    {
        public string Name { get; set; }
        public delegate void SendDelegate(byte[] data, int dataLength);
        public SendDelegate Sender { get; set; }
        protected void Log(string message)
        {
            Console.WriteLine("[{0}] {1}", this.Name, message);
        }

        public abstract void OnReceivedData(byte[] data);

        public abstract byte[][] GetPackets();

        public abstract void PrintMetrics();
    }
}

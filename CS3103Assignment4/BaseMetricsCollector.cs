using System;
using System.Collections.Generic;
using System.Linq;

namespace CS3103Assignment4
{
    abstract class BaseMetricsCollector
    {
        protected DateTime connectionStartTime;
        protected long totalBytesRecevied = 0;
        protected long totalPacketsSent = 0;
        protected long totalPacketsReceived = 0;

        protected List<double> latencies = new List<double>();
        protected List<double> arrivalIntervals = new List<double>();
        protected double lastArrivalTime = 0;

        public void Start()
        {
            this.connectionStartTime = DateTime.Now;
        }

        public virtual void AddSentPackets()
        {
            this.totalPacketsSent++;
        }

        public virtual void AddReceivedPackets(int bytes, double? latency = null)
        {
            this.totalPacketsReceived++;
            this.totalBytesRecevied += bytes;
            
            if (latency.HasValue)
            {
                this.latencies.Add(latency.Value);
            }

            double now = (DateTime.Now - this.connectionStartTime).TotalMilliseconds;
            if (this.lastArrivalTime != 0)
            {
                this.arrivalIntervals.Add(now - this.lastArrivalTime);
            }
            this.lastArrivalTime = now;
        }

        protected double ComputeJitter()
        {
            if (this.arrivalIntervals.Count <= 1)
            {
                return 0;
            }

            double avgInterval = this.arrivalIntervals.Average();
            return arrivalIntervals.Average(x => Math.Abs(x - avgInterval));
        }

        protected double ComputeThroughput()
        {
            double duration = (DateTime.Now - this.connectionStartTime).TotalSeconds;
            return this.totalBytesRecevied / duration;
        }

        protected double ComputePDR()
        {
            if (this.totalPacketsSent == 0)
            {
                return 0;
            }
            return (double)this.totalPacketsReceived / totalPacketsSent * 100.0;
        }


        public abstract void PrintSummary();

    }
}

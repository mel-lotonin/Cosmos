using System.Collections.Generic;

using Serilog.Core;
using Serilog.Events;

namespace Cosmos.TestRunner.Core
{
    internal class KernelTestResult : IKernelTestResult, ILogEventSink
    {
        public string KernelName { get; }
        public RunConfiguration RunConfiguration { get; }

        public bool Result { get; set; }

        private List<LogEvent> mTestLog;
        public IReadOnlyList<LogEvent> TestLog => mTestLog;

        public KernelTestResult(string aKernelName, RunConfiguration aRunConfiguration)
        {
            KernelName = aKernelName;
            RunConfiguration = aRunConfiguration;

            mTestLog = new List<LogEvent>();
        }
        
        public void Emit(LogEvent aLogEvent)
        {
            mTestLog.Add(aLogEvent);
        }
    }
}

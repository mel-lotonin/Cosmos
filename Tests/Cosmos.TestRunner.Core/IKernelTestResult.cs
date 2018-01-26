using System.Collections.Generic;

using Serilog.Events;

namespace Cosmos.TestRunner.Core
{
    public interface IKernelTestResult
    {
        string KernelName { get; }
        RunConfiguration RunConfiguration { get; }

        bool Result { get; }
        IReadOnlyList<LogEvent> TestLog { get; }
    }
}

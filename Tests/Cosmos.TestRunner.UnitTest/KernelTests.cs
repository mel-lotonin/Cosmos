using System;
using System.Collections.Generic;
using System.IO;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using NUnit.Framework;

using Cosmos.TestRunner.Core;

namespace Cosmos.TestRunner.UnitTest
{
    using Assert = NUnit.Framework.Assert;

    [TestFixture]
    public class KernelTests
    {
        private static IEnumerable<Type> KernelsToRun => TestKernelSets.GetStableKernelTypes();

        [TestCaseSource(nameof(KernelsToRun))]
        public void TestKernel(Type aKernelType)
        {
            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(typeof(KernelTests).Assembly.Location));

                var xLogger = new LoggerConfiguration().WriteTo.Sink(new LogSink()).CreateLogger();
                var xEngine = new Engine(new EngineConfiguration(aKernelType), xLogger);

                Assert.IsTrue(xEngine.Execute().KernelTestResults[0].Result);
            }
            catch (AssertionException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occurred: " + e.ToString());
                Assert.Fail();
            }
        }

        private class LogSink : ILogEventSink
        {
            public void Emit(LogEvent logEvent)
            {
                var xMessage = $"{logEvent.Timestamp.ToString("hh:mm:ss.ffffff")} {logEvent.RenderMessage()}";
                TestContext.WriteLine(xMessage);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Serilog;

using Cosmos.Build.Common;

namespace Cosmos.TestRunner.Core
{
    public partial class Engine
    {
        private static readonly string WorkingDirectoryBase = Path.Combine(
            Path.GetDirectoryName(typeof(Engine).Assembly.Location), "WorkingDirectory");

        // configuration: in process eases debugging, but means certain errors (like stack overflow) kill the test runner.
        protected bool DebugIL2CPU => mConfiguration.DebugIL2CPU;
        protected string KernelPkg => mConfiguration.KernelPkg;
        protected TraceAssemblies TraceAssembliesLevel => mConfiguration.TraceAssembliesLevel;
        protected bool EnableStackCorruptionChecks => mConfiguration.EnableStackCorruptionChecks;
        protected StackCorruptionDetectionLevel StackCorruptionDetectionLevel => mConfiguration.StackCorruptionDetectionLevel;

        protected bool RunWithGDB => mConfiguration.RunWithGDB;
        protected bool StartBochsDebugGui => mConfiguration.StartBochsDebugGUI;

        public IEnumerable<Type> KernelsToRun => mConfiguration.KernelTypesToRun;

        private IEngineConfiguration mConfiguration;
        private ILogger mLogger;

        public Engine(IEngineConfiguration aEngineConfiguration, ILogger aLogger = null)
        {
            mConfiguration = aEngineConfiguration;
            mLogger = aLogger;
        }

        public ITestResult Execute()
        {
            if (!RunTargets.Any())
            {
                throw new InvalidOperationException("No run targets were specified!");
            }

            var xTestResult = new TestResult();

            LogInformation("Start executing");

            foreach (var xConfig in GetRunConfigurations())
            {
                LogInformation("Start configuration. IsELF = {0}, Target = {1}", xConfig.IsELF, xConfig.RunTarget);

                foreach (var xKernelType in KernelsToRun)
                {
                    var xKernelName = xKernelType.Assembly.GetName().Name;
                    var xKernelTestResult = new KernelTestResult(xKernelName, xConfig);

                    var xWorkingDirectory = Path.Combine(WorkingDirectoryBase, xKernelName);

                    if (Directory.Exists(xWorkingDirectory))
                    {
                        Directory.Delete(xWorkingDirectory, true);
                    }

                    Directory.CreateDirectory(xWorkingDirectory);

                    try
                    {
                        xKernelTestResult.Result = ExecuteKernel(
                            xKernelType.Assembly.Location, xWorkingDirectory, xConfig, xKernelTestResult);
                    }
                    catch (Exception e)
                    {
                        LogException(e, "Exception occurred.");
                    }

                    xTestResult.AddKernelTestResult(xKernelTestResult);

                    if (!xKernelTestResult.Result)
                    {
                        foreach(var xLogMessage in xKernelTestResult.TestLog)
                        {
                            mLogger.Write(xLogMessage);
                        }

                        break;
                    }
                }

                LogInformation("End configuration. IsELF = {0}, Target = {1}", xConfig.IsELF, xConfig.RunTarget);
            }

            var xPassedTestsCount = xTestResult.KernelTestResults.Count(r => r.Result);
            var xFailedTestsCount = xTestResult.KernelTestResults.Count(r => !r.Result);

            LogInformation("Done executing: {0} test(s) passed, {1} test(s) failed.", xPassedTestsCount, xFailedTestsCount);

            return xTestResult;
        }

        private IEnumerable<RunConfiguration> GetRunConfigurations()
        {
            foreach (var xTarget in RunTargets)
            {
                yield return new RunConfiguration { IsELF = true, RunTarget = xTarget };
                //yield return new RunConfiguration { IsELF = false, RunTarget = xTarget };
            }
        }

        private void LogInformation(string message, params object[] args) => mLogger?.Information(message, args);
        private void LogException(Exception exception, string message, params object[] args) =>
            mLogger?.Error(exception, message, args);
    }
}

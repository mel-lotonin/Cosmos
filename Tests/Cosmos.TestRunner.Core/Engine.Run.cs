using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Serilog;

namespace Cosmos.TestRunner.Core
{
    partial class Engine
    {
        protected int AllowedSecondsInKernel => mConfiguration.AllowedSecondsInKernel;
        protected IEnumerable<RunTargetEnum> RunTargets => mConfiguration.RunTargets;

        private bool ExecuteKernel(string kernelAssemblyPath, string workingDirectory, RunConfiguration configuration,
            KernelTestResult aKernelTestResult)
        {
            var xLoggerConfiguration = new LoggerConfiguration()
                .WriteTo.Sink(aKernelTestResult);

            if (mLogger != null)
            {
                xLoggerConfiguration = xLoggerConfiguration.WriteTo.Logger(mLogger);
            }

            var xLogger = xLoggerConfiguration.CreateLogger();

            xLogger.Information("Starting kernel '{0}'", aKernelTestResult.KernelName);

            var xStopwatch = new Stopwatch();
            xStopwatch.Start();

            var xAssemblyFile = Path.Combine(workingDirectory, "Kernel.asm");
            var xObjectFile = Path.Combine(workingDirectory, "Kernel.obj");
            var xTempObjectFile = Path.Combine(workingDirectory, "Kernel.o");
            var xIsoFile = Path.Combine(workingDirectory, "Kernel.iso");

            if (KernelPkg == "X86")
            {
                RunTask("TheRingMaster", () => RunTheRingMaster(kernelAssemblyPath, xLogger), xLogger);
            }
            RunTask("IL2CPU", () => RunIL2CPU(kernelAssemblyPath, xAssemblyFile, xLogger), xLogger);
            RunTask("Nasm", () => RunNasm(xAssemblyFile, xObjectFile, configuration.IsELF, xLogger), xLogger);
            if (configuration.IsELF)
            {
                File.Move(xObjectFile, xTempObjectFile);

                RunTask("Ld", () => RunLd(xTempObjectFile, xObjectFile), xLogger);
                RunTask("ExtractMapFromElfFile", () => RunExtractMapFromElfFile(
                    workingDirectory, xObjectFile, xLogger), xLogger);
            }

            string xHarddiskPath;
            if (configuration.RunTarget == RunTargetEnum.HyperV)
            {
                xHarddiskPath = Path.Combine(workingDirectory, "Harddisk.vhdx");
                var xOriginalHarddiskPath = Path.Combine(GetCosmosUserkitFolder(), "Build", "HyperV", "Filesystem.vhdx");
                File.Copy(xOriginalHarddiskPath, xHarddiskPath);
            }
            else
            {
                xHarddiskPath = Path.Combine(workingDirectory, "Harddisk.vmdk");
                var xOriginalHarddiskPath = Path.Combine(GetCosmosUserkitFolder(), "Build", "VMware", "Workstation", "Filesystem.vmdk");
                File.Copy(xOriginalHarddiskPath, xHarddiskPath);
            }

            RunTask("MakeISO", () => MakeIso(xObjectFile, xIsoFile), xLogger);

            switch (configuration.RunTarget)
            {
                case RunTargetEnum.Bochs:
                    RunTask("RunISO", () => RunIsoInBochs(xIsoFile, xHarddiskPath, workingDirectory, xLogger), xLogger);
                    break;
                case RunTargetEnum.VMware:
                    RunTask("RunISO", () => RunIsoInVMware(xIsoFile, xHarddiskPath, xLogger), xLogger);
                    break;
                case RunTargetEnum.HyperV:
                    RunTask("RunISO", () => RunIsoInHyperV(xIsoFile, xHarddiskPath, xLogger), xLogger);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("RunTarget " + configuration.RunTarget + " not implemented!");
            }

            xLogger.Information("Done running kernel '{0}'. Took {1}.", aKernelTestResult.KernelName, xStopwatch.Elapsed);

            return mKernelResult;
        }

        private void RunTask(string aTaskName, Action aAction, ILogger aLogger)
        {
            if (aAction == null)
            {
                throw new ArgumentNullException(nameof(aAction));
            }

            aLogger.Information("Running task '{0}'", aTaskName);

            var xStopwatch = new Stopwatch();
            xStopwatch.Start();

            try
            {
                aAction();
            }
            finally
            {
                xStopwatch.Stop();
                aLogger.Information("Done running task '{0}'. Took {1}.", aTaskName, xStopwatch.Elapsed);
            }
        }
    }
}

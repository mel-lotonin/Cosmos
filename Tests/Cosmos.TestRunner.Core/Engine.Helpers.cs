using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Serilog;

using Cosmos.Build.Common;
using Cosmos.IL2CPU;

namespace Cosmos.TestRunner.Core
{
    partial class Engine
    {
        private string FindCosmosRoot()
        {
            var xCurrentDirectory = AppContext.BaseDirectory;
            var xCurrentInfo = new DirectoryInfo(xCurrentDirectory);
            while (xCurrentInfo.Parent != null)
            {
                if (xCurrentInfo.GetDirectories("source").Any())
                {
                    return xCurrentDirectory;
                }
                xCurrentInfo = xCurrentInfo.Parent;
                xCurrentDirectory = xCurrentInfo.FullName;
            }
            return string.Empty;
        }

        private void RunProcess(string aProcess, string aWorkingDirectory, List<string> aArguments, ILogger aLogger,
            bool aAttachDebugger = false)
        {
            if (string.IsNullOrWhiteSpace(aProcess))
            {
                throw new ArgumentNullException(aProcess);
            }

            var xArgsString = aArguments.Aggregate("", (aArgs, aArg) => $"{aArgs} \"{aArg}\"");

            RunProcess(aProcess, aWorkingDirectory, xArgsString, aLogger, aAttachDebugger);
        }

        private void RunProcess(string aProcess, string aWorkingDirectory, string aArguments, ILogger aLogger,
            bool aAttachDebugger = false)
        {
            if (string.IsNullOrWhiteSpace(aProcess))
            {
                throw new ArgumentNullException(aProcess);
            }

            if (aAttachDebugger)
            {
                aArguments += " \"AttachVsDebugger:True\"";
            }

            Action<string> xErrorReceived = e => aLogger.Error(e);
            Action<string> xOutputReceived = o => aLogger.Information(o);

            bool xResult = false;

            var xProcessStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = aWorkingDirectory,
                FileName = aProcess,
                Arguments = aArguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            xOutputReceived($"Executing command line '{aProcess} {aArguments}'");
            xOutputReceived($"Working directory = '{aWorkingDirectory}'");

            using (var xProcess = new Process())
            {
                xProcess.StartInfo = xProcessStartInfo;

                xProcess.ErrorDataReceived += delegate (object aSender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        xErrorReceived(e.Data);
                    }
                };
                xProcess.OutputDataReceived += delegate (object aSender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        xOutputReceived(e.Data);
                    }
                };

                xProcess.Start();
                xProcess.BeginErrorReadLine();
                xProcess.BeginOutputReadLine();
                xProcess.WaitForExit(AllowedSecondsInKernel * 1000);

                if (!xProcess.HasExited)
                {
                    xProcess.Kill();
                    xErrorReceived($"'{aProcess}' timed out.");
                }
                else
                {
                    if (xProcess.ExitCode == 0)
                    {
                        xResult = true;
                    }
                    else
                    {
                        xErrorReceived($"Error invoking '{aProcess}'.");
                    }
                }
            }

            if (!xResult)
            {
                throw new Exception("Error running process!");
            }
        }

        public static string RunObjDump(string cosmosBuildDir, string workingDir, string inputFile, Action<string> errorReceived, Action<string> outputReceived)
        {
            var xMapFile = Path.ChangeExtension(inputFile, "map");
            File.Delete(xMapFile);
            if (File.Exists(xMapFile))
            {
                throw new Exception("Could not delete " + xMapFile);
            }

            var xTempBatFile = Path.Combine(workingDir, "ExtractElfMap.bat");
            File.WriteAllText(xTempBatFile, "@ECHO OFF\r\n\"" + Path.Combine(cosmosBuildDir, @"tools\cygwin\objdump.exe") + "\" --wide --syms \"" + inputFile + "\" > \"" + Path.GetFileName(xMapFile) + "\"");

            var xProcessStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDir,
                FileName = xTempBatFile,
                Arguments = "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var xProcess = Process.Start(xProcessStartInfo);

            xProcess.WaitForExit(20000);

            File.Delete(xTempBatFile);

            return xMapFile;
        }

        private void RunExtractMapFromElfFile(string workingDir, string kernelFileName, ILogger logger)
        {
            RunObjDump(CosmosPaths.Build, workingDir, kernelFileName, o => logger.Information(o), e => logger.Error(e));
        }

        private void RunTheRingMaster(string kernelFileName, ILogger logger)
        {
            var xArgs =  new List<string>() { kernelFileName };

            bool xUsingUserKit = false;
            string xTheRingMasterPath = Path.Combine(FindCosmosRoot(), "source", "TheRingMaster");
            if (!Directory.Exists(xTheRingMasterPath))
            {
                xUsingUserKit = true;
                xTheRingMasterPath = Path.Combine(GetCosmosUserkitFolder(), "Build", "TheRingMaster");
            }

            if (xUsingUserKit)
            {
                RunProcess("TheRingMaster.exe", xTheRingMasterPath, xArgs, logger);
            }
            else
            {
                xArgs.Insert(0, "run");
                xArgs.Insert(1, "--no-build");
                RunProcess("dotnet", xTheRingMasterPath, xArgs, logger);
            }
        }

        private void RunIL2CPU(string kernelFileName, string outputFile, ILogger logger)
        {
            var xReferences = new List<string>() { kernelFileName };

            if (KernelPkg == "X86")
            {
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.CPU_Plugs")).Location);
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.CPU_Asm")).Location);
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.Plugs.TapRoot")).Location);
            }
            else
            {
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.Core_Plugs")).Location);
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.Core_Asm")).Location);
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.System2_Plugs")).Location);
                xReferences.Add(Assembly.Load(new AssemblyName("Cosmos.Debug.Kernel.Plugs.Asm")).Location);
            }

            var xArgs = new List<string>
            {
                "KernelPkg:" + KernelPkg,
                "EnableDebug:True",
                "EnableStackCorruptionDetection:" + EnableStackCorruptionChecks,
                "StackCorruptionDetectionLevel:" + StackCorruptionDetectionLevel,
                "DebugMode:Source",
                "TraceAssemblies:" + TraceAssembliesLevel,
                "DebugCom:1",
                "OutputFilename:" + outputFile,
                "EnableLogging:True",
                "EmitDebugSymbols:True",
                "IgnoreDebugStubAttribute:False"
            };

            xArgs.AddRange(xReferences.Select(aReference => "References:" + aReference));

            bool xUsingUserkit = false;
            string xIL2CPUPath = Path.Combine(FindCosmosRoot(), "..", "IL2CPU", "source", "IL2CPU");
            if (!Directory.Exists(xIL2CPUPath))
            {
                xUsingUserkit = true;
                xIL2CPUPath = Path.Combine(GetCosmosUserkitFolder(), "Build", "IL2CPU");
            }

            if (xUsingUserkit)
            {
                RunProcess("IL2CPU.exe", xIL2CPUPath, xArgs, logger, DebugIL2CPU);
            }
            else
            {
                if (DebugIL2CPU)
                {
                    if (KernelsToRun.Count() > 1)
                    {
                        throw new Exception("Cannot run multiple kernels with in-process compilation!");
                    }

                    // ensure we're using the referenced (= solution) version
                    Cosmos.IL2CPU.CosmosAssembler.ReadDebugStubFromDisk = false;

                    Program.Run(xArgs.ToArray(), o => logger.Information(o), e => logger.Error(e));
                }
                else
                {
                    xArgs.Insert(0, "run");
                    xArgs.Insert(1, "--no-build");
                    xArgs.Insert(2, " -- ");
                    RunProcess("dotnet", xIL2CPUPath, xArgs, logger);
                }
            }
        }

        private void RunNasm(string inputFile, string outputFile, bool isElf, ILogger logger)
        {
            bool xUsingUserkit = false;
            string xNasmPath = Path.Combine(FindCosmosRoot(), "Tools", "NASM");
            if (!Directory.Exists(xNasmPath))
            {
                xUsingUserkit = true;
                xNasmPath = Path.Combine(GetCosmosUserkitFolder(), "Build", "NASM");
            }
            if (!Directory.Exists(xNasmPath))
            {
                throw new DirectoryNotFoundException("NASM path not found.");
            }

            var xArgs = new List<string>
            {
                $"ExePath:{Path.Combine(xUsingUserkit ? GetCosmosUserkitFolder() : FindCosmosRoot(), "Build", "Tools", "NAsm", "nasm.exe")}",
                $"InputFile:{inputFile}",
                $"OutputFile:{outputFile}",
                $"IsELF:{isElf}"
            };

            if (xUsingUserkit)
            {
                RunProcess("NASM.exe", xNasmPath, xArgs, logger);
            }
            else
            {
                xArgs.Insert(0, "run");
                xArgs.Insert(1, " -- ");
                RunProcess("dotnet", xNasmPath, xArgs, logger);
            }
        }

        private void RunLd(string inputFile, string outputFile)
        {
            string[] arguments = new[]
                       {
                           "-Ttext", "0x2000000",
                           "-Tdata", " 0x1000000",
                           "-e", "Kernel_Start",
                           "-o",outputFile.Replace('\\', '/'),
                           inputFile.Replace('\\', '/')
                       };

            var xArgsString = arguments.Aggregate("", (a, b) => a + " \"" + b + "\"");

            var xProcess = Process.Start(Path.Combine(GetCosmosUserkitFolder(), "build", "tools", "cygwin", "ld.exe"), xArgsString);

            xProcess.WaitForExit(10000);

            //RunProcess(Path.Combine(GetCosmosUserkitFolder(), "build", "tools", "cygwin", "ld.exe"),
            //           mBaseWorkingDirectory,
            //           new[]
            //           {
            //               "-Ttext", "0x2000000",
            //               "-Tdata", " 0x1000000",
            //               "-e", "Kernel_Start",
            //               "-o",outputFile.Replace('\\', '/'),
            //               inputFile.Replace('\\', '/')
            //           });
        }

        private static string GetCosmosUserkitFolder()
        {
            CosmosPaths.Initialize();
            return CosmosPaths.UserKit;
        }

        private void MakeIso(string objectFile, string isoFile)
        {
            IsoMaker.Generate(objectFile, isoFile);
            if (!File.Exists(isoFile))
            {
                throw new Exception("Error building iso");
            }
        }
    }
}

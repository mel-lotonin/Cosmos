using System;
using System.Threading;

using Cosmos.Debug.DebugConnectors;
using Cosmos.Debug.Hosts;

using Serilog;

namespace Cosmos.TestRunner.Core
{
    partial class Engine
    {
        // this file contains code handling situations when a kernel is running
        // most of this is debug stub related

        private volatile bool mKernelRunning;
        private volatile bool mKernelResult;
        private int mSucceededAssertions;

        private void InitializeDebugConnector(DebugConnector aDebugConnector, ILogger aLogger)
        {
            void LogKernelInformation(string aMessage, params object[] aArgs) => aLogger.Information(aMessage, aArgs);

            void AbortTestAndLogError(string aMessage, params object[] aArgs)
            {
                aLogger.Error(aMessage, aArgs);
                mKernelRunning = false;
            }

            void AbortTestAndLogException(Exception aException, string aMessage, params object[] aArgs)
            {
                aLogger.Error(aException, aMessage, aArgs);
                mKernelRunning = false;
            }

            if (aDebugConnector == null)
            {
                throw new ArgumentNullException(nameof(aDebugConnector));
            }

            aDebugConnector.OnDebugMsg = s => LogKernelInformation(s);

            aDebugConnector.ConnectionLost = e => AbortTestAndLogException(e, "DC: Connection lost.");

            aDebugConnector.CmdChannel = (a1, a2, a3) => ChannelPacketReceived(
                a1, a2, a3, aLogger.Information, aLogger.Error);

            aDebugConnector.CmdStarted = () =>
            {
                LogKernelInformation("DC: Started");
                aDebugConnector.SendCmd(Vs2Ds.BatchEnd);
            };

            aDebugConnector.Error = e => AbortTestAndLogException(e, "DC Error.");

            aDebugConnector.CmdText += s => LogKernelInformation("Text from kernel: " + s);

            aDebugConnector.CmdSimpleNumber += n => LogKernelInformation(
                "Number from kernel: 0x" + n.ToString("X8").ToUpper());

            aDebugConnector.CmdSimpleLongNumber += n => LogKernelInformation(
                "Number from kernel: 0x" + n.ToString("X16").ToUpper());

            aDebugConnector.CmdComplexNumber += f => LogKernelInformation(
                "Number from kernel: 0x" + f.ToString("X8").ToUpper());

            aDebugConnector.CmdComplexLongNumber += d => LogKernelInformation(
                "Number from kernel: 0x" + d.ToString("X16").ToUpper());

            aDebugConnector.CmdMessageBox = s => LogKernelInformation(
                "MessageBox from kernel: " + s);

            aDebugConnector.CmdKernelPanic = n =>
            {
                LogKernelInformation("Kernel panic! Number = " + n);
                // todo: add core dump here, call stack.
            };

            aDebugConnector.CmdTrace = t => { };

            aDebugConnector.CmdBreak = t => { };

            aDebugConnector.CmdStackCorruptionOccurred = a => AbortTestAndLogError(
                "Stackcorruption occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdStackOverflowOccurred = a => AbortTestAndLogError(
                "Stack overflow occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdNullReferenceOccurred = a => AbortTestAndLogError(
                "Null Reference Exception occurred at: 0x" + a.ToString("X8"));

            aDebugConnector.CmdCoreDump = b =>
            {
                string xCallStack = "";
                int i = 0;

                LogKernelInformation("Core dump:");
                string eax = "EAX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ebx = "EBX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ecx = "ECX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string edx = "EDX = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string edi = "EDI = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string esi = "ESI = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string ebp = "EBP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string eip = "EIP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;
                string esp = "ESP = 0x" +
                             b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                             b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                i += 4;

                LogKernelInformation(eax + " " + ebx + " " + ecx + " " + edx);
                LogKernelInformation(edi + " " + esi);
                LogKernelInformation(ebp + " " + esp + " " + eip);
                LogKernelInformation("");

                while (i < b.Length)
                {
                    string xAddress = "0x" +
                                      b[i + 3].ToString("X2") + b[i + 2].ToString("X2") +
                                      b[i + 0].ToString("X2") + b[i + 1].ToString("X2");
                    xCallStack += xAddress + " ";
                    if ((i != 0) && (i % 12 == 0))
                    {
                        LogKernelInformation(xCallStack.Trim());
                        xCallStack = "";
                    }
                    i += 4;
                }
                if (xCallStack != "")
                {
                    LogKernelInformation(xCallStack.Trim());
                    xCallStack = "";
                }
            };

            if (RunWithGDB)
            {
                aDebugConnector.CmdInterruptOccurred = a =>
                {
                    LogKernelInformation("Interrupt {0} occurred", a);
                };
            }
        }

        private void HandleRunning(DebugConnector debugConnector, Host host)
        {
            if (debugConnector == null)
            {
                throw new ArgumentNullException("debugConnector");
            }
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            mKernelRunning = true;
            host.Start();

            try
            {
                var xStartTime = DateTime.Now;
                Interlocked.Exchange(ref mSucceededAssertions, 0);

                while (mKernelRunning)
                {
                    Thread.Sleep(50);

                    if (Math.Abs(DateTime.Now.Subtract(xStartTime).TotalSeconds) > AllowedSecondsInKernel)
                    {
                        throw new TimeoutException("Timeout exceeded!");
                    }
                }
            }
            finally
            {
                host.Stop();
                debugConnector.Dispose();
                Thread.Sleep(50);
            }
        }

        private void ChannelPacketReceived(byte arg1, byte arg2, byte[] arg3, Action<string> logInfo, Action<string> logError)
        {
            if (arg1 == 129)
            {
                // for now, skip
                return;
            }
            if (arg1 == TestController.TestChannel)
            {
                switch (arg2)
                {
                    case (byte)TestChannelCommandEnum.TestCompleted:
                        KernelTestCompleted(logInfo);
                        break;
                    case (byte)TestChannelCommandEnum.TestFailed:
                        KernelTestFailed(logError);
                        break;
                    case (byte)TestChannelCommandEnum.AssertionSucceeded:
                        KernelAssertionSucceeded();
                        break;
                }
            }
            else
            {
                logInfo($"ChannelPacketReceived, Channel = {arg1}, Command = {arg2}");
            }
        }

        private void KernelAssertionSucceeded()
        {
            Interlocked.Increment(ref mSucceededAssertions);
        }

        private void KernelTestCompleted(Action<string> aLogInformation)
        {
            aLogInformation("Test completed");
            mKernelResult = true;
            mKernelRunning = false;
        }

        private void KernelTestFailed(Action<string> aLogError)
        {
            aLogError("Test failed");
            mKernelRunning = false;
        }
    }
}

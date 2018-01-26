using System;
using System.IO;

using Serilog;

using Cosmos.TestRunner.Core;

namespace Cosmos.TestRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var xLogPath = Path.Combine(
                Path.GetDirectoryName(typeof(Program).Assembly.Location), "WorkingDirectory", "TestRunnerLog.xml");

            if (args.Length == 1)
            {
                xLogPath = args[0];
            }

            var xEngineConfiguration = new DefaultEngineConfiguration();
            var xLogger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            var xEngine = new Engine(xEngineConfiguration, xLogger);
            var xResult = xEngine.Execute();
            
            try
            {
                xResult.SaveXmlToFile(xLogPath);
                Console.WriteLine("Log written to '{0}'.", xLogPath);
            }
            catch (Exception e)
            {
                xLogger.Error(e, "Exception occurred.");
                Console.ReadKey(true);
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey(true);
        }
    }
}

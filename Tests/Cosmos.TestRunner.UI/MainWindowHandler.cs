using System.Threading;
using System.Windows.Controls;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Cosmos.TestRunner.Core;

namespace Cosmos.TestRunner.UI
{
    internal class MainWindowHandler
    {
        public ITestResult TestResult { get; private set; }

        public delegate void TestFinishedEventHandler();
        public TestFinishedEventHandler TestFinished = delegate { };

        private Thread TestEngineThread = null;
        private ListView message_display_list;

        public MainWindowHandler(ListView wpflistView)
        {
            message_display_list = wpflistView;
        }

        public void RunTestEngine()
        {
            TestEngineThread = new Thread(TestEngineThreadMain);
            TestEngineThread.Start();
        }

        private void TestEngineThreadMain()
        {
            var xLogger = new LoggerConfiguration().WriteTo.Sink(new LogSink(this)).CreateLogger();
            var xEngine = new Engine(new DefaultEngineConfiguration(), xLogger);
            
            TestResult = xEngine.Execute();

            TestFinished();
        }

        private class LogSink : ILogEventSink
        {
            private MainWindowHandler mHandler;

            public LogSink(MainWindowHandler aHandler)
            {
                mHandler = aHandler;
            }

            public void Emit(LogEvent logEvent)
            {
                mHandler.message_display_list.Items.Add(new ListViewLogMessage(
                    logEvent.Timestamp.ToString("hh:mm:ss.ffffff"), logEvent.Level.ToString(), logEvent.RenderMessage()));

                foreach (var column in (mHandler.message_display_list.View as GridView).Columns)
                {
                    if (double.IsNaN(column.Width))
                    {
                        column.Width = column.ActualWidth;
                    }

                    column.Width = double.NaN;
                }

                if (mHandler.message_display_list.SelectedIndex == mHandler.message_display_list.Items.Count - 2)
                {
                    mHandler.message_display_list.SelectedIndex = mHandler.message_display_list.Items.Count - 1;
                    mHandler.message_display_list.ScrollIntoView(mHandler.message_display_list.SelectedItem);
                }
            }
        }
    }
}

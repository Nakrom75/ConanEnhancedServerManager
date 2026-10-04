using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ConanServerManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogException("AppDomain UnhandledException", ex, showDialog: true);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogException("DispatcherUnhandledException", args.Exception, showDialog: true);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogException("TaskScheduler UnobservedTaskException", args.Exception, showDialog: false);
                args.SetObserved();
            };

            base.OnStartup(e);
        }

        private static void LogException(string source, Exception? ex, bool showDialog)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now}] [{source}]");

                var current = ex;
                while (current != null)
                {
                    sb.AppendLine($"Exception Type: {current.GetType().FullName}");
                    sb.AppendLine($"Message: {current.Message}");
                    sb.AppendLine($"StackTrace:\n{current.StackTrace}");
                    sb.AppendLine(new string('-', 50));
                    current = current.InnerException;
                }
                sb.AppendLine();

                string message = sb.ToString();
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath, message);

                if (showDialog)
                {
                    string detailStr = ex?.InnerException != null ? $"{ex.Message}\n\nInner Exception: {ex.InnerException.Message}" : ex?.Message ?? "Unknown Error";
                    MessageBox.Show($"Startup Error ({source}):\n{detailStr}\n\nDetails saved to crash.log", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch { }
        }
    }
}

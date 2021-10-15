using MgAl2O4.Utils;
using System;
using System.Windows.Forms;

namespace HarpHeroConverter
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Logger.Initialize(args);

            bool updatePending = GithubUpdater.FindAndApplyUpdates();
            if (!updatePending)
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }

            Logger.Close();
        }
    }
}

// stubs for running stuff
namespace Dalamud.Logging
{
    public class PluginLog
    {
        public static void Log(string str)
        {
            Logger.WriteLine(str);
        }

        public static void Error(Exception ex, string str)
        {
            Logger.WriteLine("Exception: " + str);
            Logger.WriteLine(ex.ToString());
        }
    }
}


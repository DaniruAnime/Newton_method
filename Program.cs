using System;
using System.Windows.Forms;

namespace LocalMinimumGoldenSection
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length == 1 && args[0] == "--self-test")
                return SelfTests.Run();

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Ошибка запуска приложения: " + ex.Message);
                return 1;
            }
        }
    }
}

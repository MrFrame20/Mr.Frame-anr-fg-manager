using System;
using System.Windows.Forms;

namespace AnrFgManager
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                try { Uninstaller.Run(); }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao desinstalar:\n\n" + ex.Message, "ANR FG Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            AppUpdater.CleanupOldInBackground();
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ocorreu um erro inesperado no ANR FG Manager:\n\n" + ex.Message,
                    "ANR FG Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}

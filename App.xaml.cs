using System.Windows;

namespace DBReader
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Mantém a aplicação viva mesmo se a janela de login fechar
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoginWindow login = new LoginWindow();

            if (login.ShowDialog() == true)
            {
                // Extrai os dados da LoginWindow usando os nomes dos campos (agora públicos)
                string host = login.TxtHost.Text;
                string port = login.TxtPort.Text;
                string user = login.TxtUser.Text;
                string pass = login.TxtPass.Password;
                string db = "gf_gs"; // Fixo como solicitado

                string connStr = $"Host={host};Port={port};Username={user};Password={pass};Database={db}";
                string clientPath = login.TxtPath.Text;

                // Cria a MainWindow passando os DOIS argumentos: Conexão e Caminho
                MainWindow main = new MainWindow(connStr, clientPath);

                this.MainWindow = main;
                this.ShutdownMode = ShutdownMode.OnLastWindowClose;
                main.Show();
            }
            else
            {
                this.Shutdown();
            }
        }
    }
}
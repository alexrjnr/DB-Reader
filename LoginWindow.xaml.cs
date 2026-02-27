using System.Windows;

namespace DBReader
{
    public partial class LoginWindow : Window
    {
        private AppConfig _config;

        public LoginWindow()
        {
            InitializeComponent();

            _config = AppConfig.Load();

            // Restaurar valores salvos
            if (_config.RememberHost)
            {
                TxtHost.Text = _config.Host;
                ChkRememberHost.IsChecked = true;
            }

            if (_config.RememberClientPath)
            {
                TxtPath.Text = _config.ClientPath;
                ChkRememberPath.IsChecked = true;
            }

            if (_config.RememberPassword)
            {
                TxtPass.Password = _config.Password;
                ChkRememberPassword.IsChecked = true;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                TxtPath.Text = dialog.FolderName;
            }
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPath.Text))
            {
                MessageBox.Show("Por favor, selecione o diretório do cliente!");
                return;
            }

            // Salvar Host
            if (ChkRememberHost.IsChecked == true)
            {
                _config.Host = TxtHost.Text;
                _config.RememberHost = true;
            }
            else
            {
                _config.Host = "";
                _config.RememberHost = false;
            }

            // Salvar Pasta
            if (ChkRememberPath.IsChecked == true)
            {
                _config.ClientPath = TxtPath.Text;
                _config.RememberClientPath = true;
            }
            else
            {
                _config.ClientPath = "";
                _config.RememberClientPath = false;
            }

            // Salvar Senha
            if (ChkRememberPassword.IsChecked == true)
            {
                _config.Password = TxtPass.Password;
                _config.RememberPassword = true;
            }
            else
            {
                _config.Password = "";
                _config.RememberPassword = false;
            }

            _config.Save();

            this.DialogResult = true;
        }
    }
}
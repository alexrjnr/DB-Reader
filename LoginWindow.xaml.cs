using System.Windows;

namespace DBReader
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
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

            // Define que o login foi um sucesso e fecha a janela para o App.xaml.cs prosseguir
            this.DialogResult = true;
        }
    }
}
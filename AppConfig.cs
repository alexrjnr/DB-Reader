using System;
using System.IO;
using System.Text.Json;

namespace DBReader
{
    public class AppConfig
    {
        // Caminho do arquivo de configuração
        private static readonly string ConfigFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DBReader", "config.json");

        // Dados do login
        public string Host { get; set; } = "";
        public bool RememberHost { get; set; } = false;

        public string ClientPath { get; set; } = ""; // renomeado para evitar conflito com System.IO.Path
        public bool RememberClientPath { get; set; } = false;

        public string Password { get; set; } = "";
        public bool RememberPassword { get; set; } = false;

        // Carrega a configuração do arquivo (ou cria padrão)
        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch
            {
                // Ignora erro e retorna configuração padrão
            }

            return new AppConfig();
        }

        // Salva a configuração no arquivo
        public void Save()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(ConfigFilePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // Ignora erro de gravação
            }
        }
    }
}
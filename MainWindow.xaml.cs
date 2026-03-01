using ImageMagick;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DBReader
{
    public partial class MainWindow : Window
    {
        private readonly string _connStr;
        private readonly string _clientPath;
        private Dictionary<string, ItemData> _itemDb = new Dictionary<string, ItemData>();
        private Dictionary<string, EnchantData> _enchantDb = new Dictionary<string, EnchantData>();
        private Dictionary<string, ItemTranslateData> _itemTranslateDb = new Dictionary<string, ItemTranslateData>();
        private Dictionary<string, EnchantTranslateData> _enchantTranslateDb = new Dictionary<string, EnchantTranslateData>();
        private Dictionary<string, string> _nodes = new Dictionary<string, string>();
        private List<Player> _allPlayers = new List<Player>();
        private string RemoveColorTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Remove qualquer sequência que comece com $, tenha qualquer caractere (exceto $) e termine com $
            return System.Text.RegularExpressions.Regex.Replace(input, @"\$[^$]*\$", "").Trim();
        }
        private Dictionary<int, int> LoadBags(int playerId, int containerIndex)
        {
            var bags = new Dictionary<int, int>();
            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();
                string sql = "SELECT loc, durability FROM bags WHERE player_id = @pid AND container_index = @containerIndex ORDER BY loc";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("pid", playerId);
                cmd.Parameters.AddWithValue("containerIndex", containerIndex);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int loc = reader.GetInt32(0);
                    int slots = reader.GetInt32(1);
                    bags[loc] = slots;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao carregar bags do containerIndex {containerIndex}: {ex.Message}");
            }
            return bags;
        }

        private readonly Dictionary<int, string> CLASS_MAP = new Dictionary<int, string> {
     {0, "Aprendiz"}, {1, "Lutador"}, {2, "Guerreiro"}, {3, "Berserker"}, {4, "Paladino"},
     {5, "Caçador"}, {6, "Arqueiro"}, {7, "Ranger"}, {8, "Assassino"}, {9, "Acólito"},
     {10, "Sacerdote"}, {11, "Clérigo"}, {12, "Sábio"}, {13, "Bruxo"}, {14, "Mago"},
     {15, "Feiticeiro"}, {16, "Necromante"}, {17, "Senhor da Guerra"}, {18, "Templário"}, {19, "Franco Atirador"},
     {20, "Sicário Sombrio"}, {21, "Profeta"}, {22, "Místico"}, {23, "Arquimago"}, {24, "Demonologista"},
     {25, "Maquinista Aprendiz"}, {26, "Maquinista"}, {27, "Agressor"}, {28, "Demolidor"}, {29, "Prime"},
     {30, "Optimus"}, {32, "Cavaleiro da Morte"}, {33, "Cruzado"}, {34, "Mercenário"}, {35, "Ninja"},
     {36, "Santo"}, {37, "Xamã"}, {38, "Avatar"}, {39, "Emissário dos Mortos"}, {40, "Destruidor"},
     {41, "Cavaleiro Sagrado"}, {42, "Predador"}, {43, "Shinobi"}, {44, "Arcanjo"}, {45, "Druida"},
     {46, "Bruxo"}, {47, "Shinigami"}, {48, "Megatron"}, {49, "Galvatron"}, {50, "Ômega"},
     {51, "Titã Celeste"}, {52, "Viajante"}, {53, "Nômade"}, {54, "Espadachim"}, {55, "Ilusionista"},
     {56, "Samurai"}, {57, "Áugure"}, {58, "Ronin"}, {59, "Oráculo"}, {60, "Mestre Dimensional"},
     {61, "Cronos"}
    };

        public MainWindow(string connectionString, string clientPath)
        {
            InitializeComponent();
            _connStr = connectionString;
            _clientPath = clientPath;

            // Registrar suporte a Big5 (Chinês Tradicional)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.Loaded += (s, e) => {
                LoadIniFiles(_clientPath);
                //InitializeEmptyInventory(); 
                LoadPlayers();
            };
        }

        #region Modelos
        public class Player
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Account { get; set; } = "";
            public int ClassId { get; set; }
            public int Level { get; set; }
            public long Gold { get; set; }
            public int NodeId { get; set; }
            public Dictionary<string, int> Talents { get; set; } = new Dictionary<string, int>();
        }

        public class ItemData
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string IconCode { get; set; } = "";
            public string FlagHide { get; set; } = "";
            public int Level { get; set; }
            public string Enchant { get; set; } = "";
        }

        public class ItemTranslateData
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public class EnchantTranslateData
        {
            public string Name { get; set; } = "";
            public string Desc1 { get; set; } = "";
            public string Desc2 { get; set; } = "";
            public string Desc3 { get; set; } = "";
        }

        public class InventorySlot
        {
            public BitmapSource? Icon { get; set; }
            public int Amount { get; set; }
            public string ToolTip { get; set; } = "";
        }

        public class TalentDisplay
        {
            public string Category { get; set; } = "";
            public string Name { get; set; } = "";
            public int Level { get; set; }
            public string Enchant { get; set; } = "";
        }


        public class EnchantData
        {
            public string Id { get; set; } = "";
            public string IconCode { get; set; } = "";
        }
        #endregion

        private void LoadIniFiles(string path)
        {
            _itemDb.Clear();
            _enchantDb.Clear();
            _nodes.Clear();
            _itemTranslateDb.Clear();
            _enchantTranslateDb.Clear();

            try
            {
                var big5 = Encoding.GetEncoding(950);
                var ansi = Encoding.GetEncoding(1252);

                // 🔹 CARREGA ITENS DO BANCO DE DADOS (C_Item.ini e C_ItemMall.ini) - 93 colunas
                string[] itemFiles = { "data/db/C_Item.ini", "data/db/C_ItemMall.ini" };
                foreach (var file in itemFiles)
                {
                    string fullPath = Path.Combine(path, file);
                    if (!File.Exists(fullPath)) continue;

                    string[] lines = File.ReadAllLines(fullPath, big5);
                    if (lines.Length > 1)
                    {
                        string contentRemaining = string.Join("\n", lines.Skip(1));
                        string[] raw = contentRemaining.Split('|');

                        for (int i = 0; i <= raw.Length - 93; i += 93)
                        {
                            string id = raw[i].Trim();
                            if (!string.IsNullOrEmpty(id))
                            {
                                _itemDb[id] = new ItemData
                                {
                                    Id = id,
                                    IconCode = raw[i + 1].Trim(),
                                    Name = raw[i + 9].Trim(),
                                    Level = int.TryParse(raw[i + 16].Trim(), out var lvl) ? lvl : 0,
                                    Enchant = raw[i + 69].Trim(),
                                    FlagHide = raw[i + 81].Trim()
                                };
                            }
                        }
                    }
                }

                // 🔹 CARREGA ENCHANTS DO BANCO DE DADOS (C_Enchant.ini) - 63 colunas
                string enchantPath = Path.Combine(path, "data/db/C_Enchant.ini");
                LoadEnchantIni(enchantPath); // Este método já deve preencher _enchantDb

                // 🔹 CARREGA TRADUÇÕES DE ITENS (T_Item.ini e T_ItemMall.ini) - 3 colunas (ID, Nome, Descrição)
                string[] translateItemFiles = { "data/Translate/T_Item.ini", "data/Translate/T_ItemMall.ini" };
                foreach (var file in translateItemFiles)
                {
                    string fullPath = Path.Combine(path, file);
                    if (!File.Exists(fullPath)) continue;

                    string content = File.ReadAllText(fullPath, ansi);
                    string[] raw = content.Split('|');

                    for (int i = 0; i <= raw.Length - 3; i += 3)
                    {
                        string id = raw[i].Trim();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _itemTranslateDb[id] = new ItemTranslateData
                            {
                                Name = raw[i + 1].Trim(),
                                Description = raw[i + 2].Trim()
                            };
                        }
                    }
                }

                // 🔹 CARREGA TRADUÇÕES DE ENCHANT (T_Enchant.ini) - 5 colunas (ID, Nome, Desc1, Desc2, Desc3)
                string enchantTranslatePath = Path.Combine(path, "data/Translate/T_Enchant.ini");
                if (File.Exists(enchantTranslatePath))
                {
                    string content = File.ReadAllText(enchantTranslatePath, ansi);
                    string[] raw = content.Split('|');

                    for (int i = 0; i <= raw.Length - 5; i += 5)
                    {
                        string id = raw[i].Trim();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _enchantTranslateDb[id] = new EnchantTranslateData
                            {
                                Name = raw[i + 1].Trim(),
                                Desc1 = raw[i + 2].Trim(),
                                Desc2 = raw[i + 3].Trim(),
                                Desc3 = raw[i + 4].Trim()
                            };
                        }
                    }
                }

                // 🔹 CARREGA NODES (T_Node.ini) - originalmente com 11 colunas? Mantendo a lógica anterior
                string nodePath = Path.Combine(path, "data/Translate/T_Node.ini");
                if (File.Exists(nodePath))
                {
                    string nodeContent = File.ReadAllText(nodePath, ansi);
                    string[] nodeRaw = nodeContent.Split('|');

                    for (int i = 0; i <= nodeRaw.Length - 11; i += 11)
                    {
                        string nodeId = nodeRaw[i].Trim();
                        if (!string.IsNullOrEmpty(nodeId))
                        {
                            _nodes[nodeId] = nodeRaw[i + 1].Trim();
                        }
                    }
                }

                Debug.WriteLine($"Itens carregados: {_itemDb.Count}");
                Debug.WriteLine($"Enchants carregados: {_enchantDb.Count}");
                Debug.WriteLine($"Traduções de itens: {_itemTranslateDb.Count}");
                Debug.WriteLine($"Traduções de enchants: {_enchantTranslateDb.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar INIs: " + ex.Message);
            }
        }


        private void LoadEnchantIni(string path)
        {
            if (!File.Exists(path))
                return;

            var big5 = Encoding.GetEncoding(950);

            string[] lines = File.ReadAllLines(path, big5);

            if (lines.Length <= 1)
                return;

            // Ignora Versão (primeira linha dos Arquivos C_*.ini) 
            string content = string.Join("", lines.Skip(1));

            string[] raw = content.Split('|');

            _enchantDb.Clear();

            int blockSize = 63;

            for (int i = 0; i <= raw.Length - blockSize; i += blockSize)
            {
                string id = raw[i].Trim();
                string icon = raw[i + 1].Trim();

                if (!string.IsNullOrWhiteSpace(id))
                {
                    _enchantDb[id] = new EnchantData
                    {
                        Id = id,
                        IconCode = icon
                    };
                }
            }

            Debug.WriteLine("Enchants carregados FINAL: " + _enchantDb.Count);
        }

        private void LoadPlayers()
        {
            try
            {
                _allPlayers.Clear();
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();
                string sql = @"SELECT id, given_name, node_id, class_id, level, gold, account_name, 
                               spell_card_ground, spell_card_moon, spell_card_star, spell_card_sun, 
                               spell_card_ground2, spell_card_moon2, spell_card_star2, spell_card_sun2 
                               FROM player_characters ORDER BY id DESC";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var p = new Player
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? "---" : reader.GetString(1),
                        NodeId = reader.GetInt32(2),
                        ClassId = reader.GetInt32(3),
                        Level = reader.GetInt32(4),
                        Gold = reader.GetInt64(5),
                        Account = reader.IsDBNull(6) ? "---" : reader.GetString(6)
                    };
                    // Pegando IDs do Banco
                    p.Talents["G1"] = reader.GetInt32(7); p.Talents["M1"] = reader.GetInt32(8);
                    p.Talents["S1"] = reader.GetInt32(9); p.Talents["U1"] = reader.GetInt32(10);
                    p.Talents["G2"] = reader.GetInt32(11); p.Talents["M2"] = reader.GetInt32(12);
                    p.Talents["S2"] = reader.GetInt32(13); p.Talents["U2"] = reader.GetInt32(14);
                    _allPlayers.Add(p);
                }
                GridPlayers.ItemsSource = null;
                GridPlayers.ItemsSource = _allPlayers;
            }
            catch (Exception ex) { MessageBox.Show("Erro DB: " + ex.Message); }
        }

        private void UpdateTalents(Player p)
        {
            var talentDisplays = new List<TalentDisplay>();

            var categories = new Dictionary<string, string> {
        {"G1", "Talento Terrestre :"}, {"S1", "Talento Estrela :"}, {"M1", "Talento da Lua :"}, {"U1", "Talento do Sol :"},
        {"G2", "Ancião Terrestre :"}, {"S2", "Ancião Estrela :"},
        {"M2", "Ancião da Lua :"}, {"U2", "Ancião do Sol :"}
    };

            foreach (var cat in categories)
            {
                int idDoBanco = p.Talents[cat.Key];

                string tName = "Vazio";
                int lvl = 0;
                string enchantId = "";
                BitmapSource? icon = null;

                if (idDoBanco > 0)
                {
                    string itemId = idDoBanco.ToString();

                    if (_itemDb.TryGetValue(itemId, out var itemInfo))
                    {
                        lvl = itemInfo.Level;
                        enchantId = itemInfo.Enchant;

                        // Nome do item (traduzido ou original) com remoção de tags
                        if (_itemTranslateDb.TryGetValue(itemId, out var trans))
                            tName = RemoveColorTags(trans.Name);
                        else
                            tName = RemoveColorTags(itemInfo.Name);

                        // Ícone do enchant (se houver)
                        if (!string.IsNullOrWhiteSpace(enchantId) &&
                            _enchantDb.TryGetValue(enchantId, out var enchantInfo))
                        {
                            icon = GetIcon(enchantInfo.IconCode);
                        }
                    }
                    else
                    {
                        tName = $"Desconhecido ({idDoBanco})";
                    }
                }

                var imgControl = this.FindName($"Slot{cat.Key}") as Image;

                if (imgControl != null)
                {
                    imgControl.Source = icon;

                    if (idDoBanco > 0)
                    {
                        string itemId = idDoBanco.ToString();
                        // Informações do enchant para tooltip
                        string enchantDisplay = "";
                        string enchantDesc1 = "", enchantDesc2 = "", enchantDesc3 = "";

                        if (!string.IsNullOrWhiteSpace(enchantId))
                        {
                            if (_enchantTranslateDb.TryGetValue(enchantId, out var enchantTrans))
                            {
                                string enchantName = RemoveColorTags(enchantTrans.Name);
                                enchantDesc1 = RemoveColorTags(enchantTrans.Desc1);
                                enchantDesc2 = RemoveColorTags(enchantTrans.Desc2);
                                enchantDesc3 = RemoveColorTags(enchantTrans.Desc3);
                                enchantDisplay = $"{enchantName} (ID: {enchantId})";
                            }
                            else
                            {
                                enchantDisplay = $"Enchant ID: {enchantId}";
                            }
                        }

                        var tooltipBuilder = new StringBuilder();
                        tooltipBuilder.AppendLine($"{tName} (ID: {itemId})");
                        tooltipBuilder.AppendLine($"Level: {lvl}");

                        if (!string.IsNullOrWhiteSpace(enchantDisplay))
                            tooltipBuilder.AppendLine($"Encantamento: {enchantDisplay}");
                        if (!string.IsNullOrWhiteSpace(enchantDesc1))
                            tooltipBuilder.AppendLine(enchantDesc1);
                        if (!string.IsNullOrWhiteSpace(enchantDesc2))
                            tooltipBuilder.AppendLine(enchantDesc2);
                        if (!string.IsNullOrWhiteSpace(enchantDesc3))
                            tooltipBuilder.AppendLine(enchantDesc3);

                        imgControl.ToolTip = tooltipBuilder.ToString().Trim();
                    }
                    else
                    {
                        imgControl.ToolTip = null;
                    }
                }

                talentDisplays.Add(new TalentDisplay
                {
                    Category = cat.Value,
                    Name = tName,
                    Level = lvl,
                    Enchant = enchantId
                });
            }

            TalentNameList.ItemsSource = talentDisplays;
        }

        private void LoadInventory(int playerId, string tableName, Dictionary<int, int> bags, ItemsControl targetGrid)
        {
            // Total de slots: container 0 fixo (24) + soma das bags
            int totalSlots = 24;
            foreach (var slots in bags.Values)
                totalSlots += slots;

            var slotsList = new List<InventorySlot>(totalSlots);
            for (int i = 0; i < totalSlots; i++)
                slotsList.Add(new InventorySlot()); // Preenche com vazio

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                // Inclui container_index e loc para posicionar corretamente
                string sql = $"SELECT item_id, durability, strengthen, container_index, loc FROM {tableName} WHERE player_id = @pid ORDER BY container_index, loc";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("pid", playerId);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string itemId = reader["item_id"].ToString()?.Trim() ?? "";
                    int amount = Convert.ToInt32(reader["durability"]);
                    int strengthen = reader["strengthen"] != DBNull.Value ? Convert.ToInt32(reader["strengthen"]) : 0;
                    int containerIdx = reader["container_index"] != DBNull.Value ? Convert.ToInt32(reader["container_index"]) : 0;
                    int loc = reader["loc"] != DBNull.Value ? Convert.ToInt32(reader["loc"]) : 0;

                    if (_itemDb.TryGetValue(itemId, out var info))
                    {
                        int displayAmount = amount;
                        if (info.FlagHide == "1" || string.IsNullOrEmpty(info.FlagHide))
                            displayAmount = 0;

                        // Nome do item (traduzido)
                        string itemName = _itemTranslateDb.TryGetValue(itemId, out var trans)
                            ? RemoveColorTags(trans.Name)
                            : RemoveColorTags(info.Name);

                        // Informações do enchant
                        string enchantDisplay = "";
                        string enchantDesc1 = "", enchantDesc2 = "", enchantDesc3 = "";
                        if (!string.IsNullOrWhiteSpace(info.Enchant))
                        {
                            if (_enchantTranslateDb.TryGetValue(info.Enchant, out var enchantTrans))
                            {
                                string enchantName = RemoveColorTags(enchantTrans.Name);
                                enchantDesc1 = RemoveColorTags(enchantTrans.Desc1);
                                enchantDesc2 = RemoveColorTags(enchantTrans.Desc2);
                                enchantDesc3 = RemoveColorTags(enchantTrans.Desc3);
                                enchantDisplay = $"{enchantName} (ID: {info.Enchant})";
                            }
                            else
                            {
                                enchantDisplay = $"Enchant ID: {info.Enchant}";
                            }
                        }

                        // Monta tooltip
                        StringBuilder tooltip = new StringBuilder();
                        string itemDisplay = strengthen > 0
                            ? $"{itemName} +{strengthen} (ID: {itemId})"
                            : $"{itemName} (ID: {itemId})";
                        tooltip.AppendLine(itemDisplay);
                        tooltip.AppendLine($"Level: {info.Level}");
                        if (!string.IsNullOrWhiteSpace(enchantDisplay))
                            tooltip.AppendLine($"Encantamento: {enchantDisplay}");
                        if (!string.IsNullOrWhiteSpace(enchantDesc1))
                            tooltip.AppendLine(enchantDesc1);
                        if (!string.IsNullOrWhiteSpace(enchantDesc2))
                            tooltip.AppendLine(enchantDesc2);
                        if (!string.IsNullOrWhiteSpace(enchantDesc3))
                            tooltip.AppendLine(enchantDesc3);

                        // Calcular índice global
                        int globalIndex = 0;
                        if (containerIdx == 0)
                        {
                            // Container 0: loc deve ser 0-23
                            globalIndex = loc;
                        }
                        else
                        {
                            // Soma os 24 do container 0
                            globalIndex = 24;
                            // Soma os slots dos containers anteriores a este
                            for (int c = 1; c < containerIdx; c++)
                            {
                                if (bags.TryGetValue(c, out int slots))
                                    globalIndex += slots;
                            }
                            // Adiciona a posição dentro deste container
                            globalIndex += loc;
                        }

                        if (globalIndex >= 0 && globalIndex < totalSlots)
                        {
                            slotsList[globalIndex] = new InventorySlot
                            {
                                Icon = GetIcon(info.IconCode),
                                Amount = displayAmount,
                                ToolTip = tooltip.ToString().Trim()
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao carregar {tableName}: {ex.Message}");
            }

            targetGrid.ItemsSource = null;
            targetGrid.ItemsSource = slotsList;
        }

        private BitmapSource? GetIcon(string code)
        {
            if (string.IsNullOrEmpty(_clientPath) || string.IsNullOrEmpty(code))
                return null;

            string cleanCode = code.Trim();

            string itemPath = Path.Combine(_clientPath, "UI", "itemicon", $"{cleanCode}.dds");
            string skillPath = Path.Combine(_clientPath, "UI", "skillicon", $"{cleanCode}.dds");

            string? finalPath = null;

            if (File.Exists(itemPath))
                finalPath = itemPath;
            else if (File.Exists(skillPath))
                finalPath = skillPath;
            else
                return null;

            try
            {
                using var image = new MagickImage(finalPath);
                image.Format = MagickFormat.Png;

                if (image.Width > 64)
                    image.Resize(42, 42);

                var bytes = image.ToByteArray();
                using var ms = new MemoryStream(bytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void InitializeEmptyInventory()
        {
            var empty = new List<InventorySlot>();
            for (int i = 0; i < 240; i++) empty.Add(new InventorySlot());

            // Inicializa os 3 grids vazios
            InventoryGrid.ItemsSource = empty;
            ElfInventoryGrid.ItemsSource = empty;
            StorageGrid.ItemsSource = empty;
        }

        private void GridPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPlayers.SelectedItem is Player p)
            {
                StatusName.Text = p.Name;
                StatusAccount.Text = p.Account;
                StatusClass.Text = CLASS_MAP.GetValueOrDefault(p.ClassId, $"ID {p.ClassId}");
                StatusLevel.Text = p.Level.ToString();
                StatusLoc.Text = _nodes.GetValueOrDefault(p.NodeId.ToString(), $"Mapa {p.NodeId}");
                UpdateGoldDisplay(p.Gold);
                UpdateTalents(p);

                // Carrega as bags para cada tipo usando o container_index correto
                var inventoryBags = LoadBags(p.Id, 0);   // container_index 0 = inventory1
                var elfBags = LoadBags(p.Id, 8);         // container_index 8 = elfinventory
                var storageBags = LoadBags(p.Id, 4);     // container_index 4 = storage1

                LoadInventory(p.Id, "inventory1", inventoryBags, InventoryGrid);
                LoadInventory(p.Id, "elfinventory", elfBags, ElfInventoryGrid);
                LoadInventory(p.Id, "storage1", storageBags, StorageGrid);
            }
        }

        private void UpdateGoldDisplay(long gold)
        {
            GoldContainer.Children.Clear();
            string s = gold.ToString().PadLeft(5, '0');
            AddGoldText(s.Substring(0, Math.Max(0, s.Length - 4)), "#FFD700");
            AddGoldText(s.Substring(Math.Max(0, s.Length - 4), 2), "#C0C0C0");
            AddGoldText(s.Substring(Math.Max(0, s.Length - 2)), "#CD7F32");
        }

        private void AddGoldText(string val, string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var txt = new TextBlock { Text = val + " ", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            var dot = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(color), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            GoldContainer.Children.Add(txt); GoldContainer.Children.Add(dot);
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlayer = GridPlayers.SelectedItem as Player;
            LoadPlayers();

            if (selectedPlayer != null)
            {
                var updatedPlayer = _allPlayers.FirstOrDefault(p => p.Id == selectedPlayer.Id);
                if (updatedPlayer != null)
                {
                    GridPlayers.SelectedItem = updatedPlayer;
                    UpdateTalents(updatedPlayer);

                    // Recarrega as bags com os container_index corretos
                    var inventoryBags = LoadBags(updatedPlayer.Id, 0);
                    var elfBags = LoadBags(updatedPlayer.Id, 8);
                    var storageBags = LoadBags(updatedPlayer.Id, 4);

                    LoadInventory(updatedPlayer.Id, "inventory1", inventoryBags, InventoryGrid);
                    LoadInventory(updatedPlayer.Id, "elfinventory", elfBags, ElfInventoryGrid);
                    LoadInventory(updatedPlayer.Id, "storage1", storageBags, StorageGrid);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = TxtSearch.Text.ToLower();
            GridPlayers.ItemsSource = _allPlayers.Where(p => p.Name.ToLower().Contains(filter)).ToList();
        }
    }
}
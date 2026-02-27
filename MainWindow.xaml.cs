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
        private Dictionary<string, string> _nodes = new Dictionary<string, string>();
        private List<Player> _allPlayers = new List<Player>();

        private readonly Dictionary<int, string> CLASS_MAP = new Dictionary<int, string> {
            {0, "Novice"}, {1, "Fighter"}, {2, "Warrior"}, {3, "Berserker"}, {4, "Paladin"},
            {5, "Hunter"}, {6, "Archer"}, {7, "Ranger"}, {8, "Assassin"}, {9, "Acolyte"},
            {10, "Priest"}, {11, "Cleric"}, {12, "Sage"}, {13, "Spellcaster"}, {14, "Mage"},
            {15, "Wizard"}, {16, "Necromancer"}, {17, "Warlord"}, {18, "Templar"}, {19, "Sharpshooter"},
            {20, "Darkstalker"}, {21, "Prophet"}, {22, "Mystic"}, {23, "Archmage"}, {24, "Demonologist"},
            {25, "Mechanic"}, {26, "Machinist"}, {27, "Engineer"}, {28, "Demolitionist"}, {29, "Gearmaster"},
            {30, "Gunner"}, {32, "Deathknight"}, {33, "Crusader"}, {34, "Hawkeye"}, {35, "Windshadow"},
            {36, "Saint"}, {37, "Shaman"}, {38, "Avatar"}, {39, "Shadowlord"}, {40, "Destroyer"},
            {41, "Holy Knight"}, {42, "Predator"}, {43, "Shinobi"}, {44, "Archangel"}, {45, "Druid"},
            {46, "Warlock"}, {47, "Shinigami"}, {48, "Cogmaster"}, {49, "Bombardier"}, {50, "Mechmaster"},
            {51, "Artillerist"}, {52, "Wanderer"}, {53, "Drifter"}, {54, "Void Runner"}, {55, "Time Traveler"},
            {56, "Dimensionalist"}, {57, "Key Master"}, {58, "Reaper"}, {59, "Chronomancer"}, {60, "Phantom"},
            {61, "Chronoshifter"}
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
                InitializeEmptyInventory();
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

            try
            {
                var big5 = Encoding.GetEncoding(950);

                // 🔹 CARREGA APENAS ITENS (93 COLUNAS)
                string[] itemFiles =
                {
            "data/db/C_Item.ini",
            "data/db/C_ItemMall.ini"
        };

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
                                    Enchant = raw[i + 69].Trim(), // 🔥 ID DO ENCHANT
                                    FlagHide = raw[i + 81].Trim()
                                };
                            }
                        }
                    }
                }

                // 🔹 CARREGA ENCHANT SEPARADO
                string enchantPath = Path.Combine(path, "data/db/C_Enchant.ini");
                LoadEnchantIni(enchantPath);

                // 🔹 CARREGA NODES
                string nodePath = Path.Combine(path, "data/Translate/T_Node.ini");
                if (File.Exists(nodePath))
                {
                    string nodeContent = File.ReadAllText(nodePath, Encoding.GetEncoding(1252));
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

                System.Diagnostics.Debug.WriteLine($"Itens carregados: {_itemDb.Count}");
                System.Diagnostics.Debug.WriteLine($"Enchants carregados: {_enchantDb.Count}");
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

            // Ignora primeira linha
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
        {"G1", "TERRA"}, {"S1", "ESTRELA"}, {"M1", "LUA"}, {"U1", "SOL"},
        {"G2", "ANCIÃO TERRA"}, {"S2", "ANCIÃO ESTRELA"},
        {"M2", "ANCIÃO LUA"}, {"U2", "ANCIÃO SOL"}
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
                        tName = itemInfo.Name;
                        lvl = itemInfo.Level;
                        enchantId = itemInfo.Enchant; // 🔥 PEGAMOS O ENCHANT DO ITEM

                        // 🔥 AGORA BUSCA O ÍCONE NO C_ENCHANT
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
                        imgControl.ToolTip =
                            $"Nome: {tName}\n" +
                            $"ID Item: {idDoBanco}\n" +
                            $"Enchant ID: {enchantId}\n" +
                            $"Level: {lvl}";
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

        private void LoadInventory(int playerId)
        {
            var slots = new List<InventorySlot>();
            for (int i = 0; i < 240; i++) slots.Add(new InventorySlot());

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();
                string sql = "SELECT item_id, durability FROM inventory1 WHERE player_id = @pid ORDER BY container_index, loc";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("pid", playerId);
                using var reader = cmd.ExecuteReader();

                int idx = 0;
                while (reader.Read() && idx < 240)
                {
                    string itemId = reader["item_id"].ToString()?.Trim() ?? "";
                    int amount = Convert.ToInt32(reader["durability"]);

                    if (_itemDb.TryGetValue(itemId, out var info))
                    {
                        // LÓGICA SOLICITADA:
                        // Se a coluna 81 for "1" ou estiver vazia, não exibimos a quantidade (definimos como 0 ou 1)
                        // Geralmente, se não deve exibir a contagem, tratamos como 0 para o Badge de quantidade sumir
                        int displayAmount = amount;
                        if (info.FlagHide == "1" || string.IsNullOrEmpty(info.FlagHide))
                        {
                            displayAmount = 0;
                        }

                        slots[idx] = new InventorySlot
                        {
                            Icon = GetIcon(info.IconCode),
                            Amount = displayAmount,
                            ToolTip =
                                $"{info.Name}\n" +
                                $"Lv: {info.Level}\n" +
                                $"Enchant: {info.Enchant}\n" +
                                $"ID: {itemId}"
                        };
                    }
                    idx++;
                }
            }
            catch { /* Tratamento de erro */ }

            InventoryGrid.ItemsSource = null;
            InventoryGrid.ItemsSource = slots;
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
            InventoryGrid.ItemsSource = empty;
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
                LoadInventory(p.Id);
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
            // 1. Salva quem é o jogador selecionado antes de recarregar
            var selectedPlayer = GridPlayers.SelectedItem as Player;

            // 2. Recarrega a lista lateral (isso limpa o ItemsSource e busca do banco)
            LoadPlayers();

            // 3. Se havia alguém selecionado, re-seleciona e atualiza os dados
            if (selectedPlayer != null)
            {
                // Tenta achar o mesmo jogador na nova lista carregada
                var updatedPlayer = _allPlayers.FirstOrDefault(p => p.Id == selectedPlayer.Id);
                if (updatedPlayer != null)
                {
                    GridPlayers.SelectedItem = updatedPlayer;

                    // Força a atualização dos componentes que buscam dados extras do banco
                    LoadInventory(updatedPlayer.Id);
                    UpdateTalents(updatedPlayer);
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
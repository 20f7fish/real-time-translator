using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;

namespace RealtimeTranslator
{
    public partial class ConfigForm : Form
    {
        private readonly string _configPath;
        private TextBox? _appKeyTextBox;
        private TextBox? _appSecretTextBox;
        private Label? _appKeyLabel;
        private Label? _appSecretLabel;
        private Button? _saveButton;
        private Button? _getApiButton;
        private LinkLabel? _apiLinkLabel;
        private const string YoudaoApiUrl = "https://ai.youdao.com/doc.s#guide";

        public ConfigForm()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            InitializeComponent();
            LoadCurrentConfig();
        }

        private void InitializeComponent()
        {
            this.Text = "有道翻译API配置";
            this.Size = new Size(500, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            _getApiButton = new Button
            {
                Text = "获取有道API",
                Location = new Point(20, 20),
                Size = new Size(100, 30)
            };
            _getApiButton.Click += GetApiButton_Click;

            _apiLinkLabel = new LinkLabel
            {
                Text = "点击访问有道API申请页面",
                Location = new Point(140, 25),
                Size = new Size(200, 20)
            };
            _apiLinkLabel.LinkClicked += ApiLinkLabel_LinkClicked;

            _appKeyLabel = new Label
            {
                Text = "应用ID (AppKey):",
                Location = new Point(20, 80),
                Size = new Size(120, 20)
            };

            _appKeyTextBox = new TextBox
            {
                Location = new Point(150, 80),
                Size = new Size(300, 20)
            };

            _appSecretLabel = new Label
            {
                Text = "应用密钥 (AppSecret):",
                Location = new Point(20, 120),
                Size = new Size(120, 20)
            };

            _appSecretTextBox = new TextBox
            {
                Location = new Point(150, 120),
                Size = new Size(300, 20)
            };

            _saveButton = new Button
            {
                Text = "保存配置",
                Location = new Point(200, 180),
                Size = new Size(100, 30)
            };
            _saveButton.Click += SaveButton_Click;

            this.Controls.AddRange(new Control[] {
                _getApiButton,
                _apiLinkLabel,
                _appKeyLabel,
                _appKeyTextBox,
                _appSecretLabel,
                _appSecretTextBox,
                _saveButton
            });
        }

        private void GetApiButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("请访问有道智云网站申请API", "申请说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenUrl();
        }

        private void ApiLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl();
        }

        private void OpenUrl()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = YoudaoApiUrl;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开网页: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCurrentConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var jsonString = File.ReadAllText(_configPath);
                    using var document = JsonDocument.Parse(jsonString);
                    var root = document.RootElement;

                    if (root.TryGetProperty("Youdao", out var youdao))
                    {
                        if (_appKeyTextBox != null && youdao.TryGetProperty("AppKey", out var appKey))
                        {
                            _appKeyTextBox.Text = appKey.GetString();
                        }
                        if (_appSecretTextBox != null && youdao.TryGetProperty("AppSecret", out var appSecret))
                        {
                            _appSecretTextBox.Text = appSecret.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (_appKeyTextBox == null || _appSecretTextBox == null) return;

            if (string.IsNullOrWhiteSpace(_appKeyTextBox.Text) || string.IsNullOrWhiteSpace(_appSecretTextBox.Text))
            {
                MessageBox.Show("请输入应用ID和应用密钥", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var jsonString = File.ReadAllText(_configPath);
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;
                
                var newConfig = new Dictionary<string, JsonElement>();
                foreach (var property in root.EnumerateObject())
                {
                    newConfig[property.Name] = property.Value.Clone();
                }

                var youdaoConfig = new Dictionary<string, string>
                {
                    { "AppKey", _appKeyTextBox.Text },
                    { "AppSecret", _appSecretTextBox.Text }
                };

                using var fs = File.Create(_configPath);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();

                foreach (var kvp in newConfig)
                {
                    if (kvp.Key == "Youdao")
                    {
                        writer.WritePropertyName(kvp.Key);
                        writer.WriteStartObject();
                        writer.WriteString("AppKey", youdaoConfig["AppKey"]);
                        writer.WriteString("AppSecret", youdaoConfig["AppSecret"]);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(kvp.Key);
                        kvp.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();

                MessageBox.Show("配置已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 
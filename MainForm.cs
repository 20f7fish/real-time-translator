using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace RealtimeTranslator
{
    public partial class MainForm : Form
    {
        private ITranslationService _translationService;
        private LiveCaptionService _captionService;
        private string _currentFromLanguage = "auto";
        private string _currentToLanguage = "zh";
        private bool _isAutoTranslate = false;
        private bool _isCaptureEnabled = false;
        private System.Windows.Forms.Timer? _autoTranslateTimer;
        private TranslationOverlay? _translationOverlay;
        private TranslationServiceType _currentServiceType;
        private readonly IConfiguration _config;

        // 将字段标记为可空
        private TextBox? sourceTextBox;
        private TextBox? targetTextBox;
        private Button? translateButton;
        private ComboBox? sourceComboBox;
        private ComboBox? targetComboBox;
        private CheckBox? autoTranslateCheckBox;
        private Button? captureButton;
        private Button? clearButton;
        private TextBox? debugLogTextBox;
        private CheckBox? overlayCheckBox;
        private NumericUpDown? lengthNumericUpDown;
        private NumericUpDown? delayNumericUpDown;
        private Label? sourceLabel;
        private Label? targetLabel;
        private Button? swapButton;
        private Label? lengthLabel;
        private Label? delayLabel;

        private const int DefaultMaxLength = 200;
        private const int DefaultTranslationDelay = 1500;
        private int _currentMaxLength = DefaultMaxLength;
        private DateTime _lastTranslationTime = DateTime.MinValue;
        private StringBuilder _pendingText = new StringBuilder();

        private Label? _currentServiceLabel;
        private Button? configButton;

        // 用于存储已翻译过的文本，避免重复翻译
        private HashSet<string> _translatedTexts = new HashSet<string>();

        public MainForm(IConfiguration config)
        {
            _config = config;
            _currentServiceType = TranslationServiceType.Microsoft; // 默认使用Microsoft翻译
            _translationService = TranslationServiceFactory.CreateTranslationService(_currentServiceType, _config);
            
            _captionService = new LiveCaptionService();

            InitializeComponent();
            InitializeLanguageComboBoxes();
            SetupAutoTranslateTimer();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            CenterToScreen();
        }

        private async void OnCaptionReceived(object sender, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                await this.InvokeAsync(async () =>
                {
                    if (sourceTextBox != null)
                    {
                        LogDebug($"收到字幕: {text}");
                        
                        // 检查是否已经翻译过这段文本
                        if (_translatedTexts.Contains(text))
                        {
                            LogDebug("跳过已翻译过的字幕");
                            return;
                        }
                        
                        // 将新文本添加到待处理文本中
                        _pendingText.AppendLine(text);
                        
                        // 如果待处理文本超过最大长度，进行翻译
                        if (_pendingText.Length >= _currentMaxLength || text.EndsWith(".") || text.EndsWith("。"))
                        {
                            sourceTextBox.AppendText(_pendingText.ToString());
                            if (_isAutoTranslate)
                            {
                                await TranslateText();
                            }
                            _pendingText.Clear();
                        }
                        
                        // 如果文本框内容过长，清空它
                        if (sourceTextBox.TextLength > _currentMaxLength * 5)
                        {
                            sourceTextBox.Clear();
                            _pendingText.Clear();
                        }
                    }
                });
            }
        }

        private void InitializeComponent()
        {
            // 窗体设置
            this.Text = "实时翻译器";
            this.Size = new Size(800, 650);
            this.MinimumSize = new Size(600, 450);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Font;

            // 创建API选择下拉框
            var apiComboBox = new ComboBox
            {
                Location = new Point(10, 12),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 添加支持的API
            apiComboBox.Items.AddRange(new object[]
            {
                "Microsoft翻译",
                "谷歌翻译",
                "有道翻译"
            });

            // 设置默认选项
            apiComboBox.SelectedIndex = 0;

            // 添加事件处理
            apiComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (s is not ComboBox combo) return;

                // 根据选择切换翻译服务
                _currentServiceType = combo.SelectedIndex switch
                {
                    0 => TranslationServiceType.Microsoft,
                    1 => TranslationServiceType.Google,
                    2 => TranslationServiceType.Youdao,
                    _ => _currentServiceType
                };

                // 更新当前翻译源标签
                if (_currentServiceLabel != null)
                {
                    _currentServiceLabel.Text = $"当前翻译源：{combo.SelectedItem}";
                }

                // 显示或隐藏有道配置面板
                var youdaoConfigPanel = this.Controls.Find("youdaoConfigPanel", false).FirstOrDefault() as Panel;
                if (youdaoConfigPanel != null)
                {
                    youdaoConfigPanel.Visible = _currentServiceType == TranslationServiceType.Youdao;
                }

                // 释放旧的翻译服务
                _translationService?.Dispose();

                // 创建新的翻译服务
                _translationService = TranslationServiceFactory.CreateTranslationService(_currentServiceType, _config);
                
                // 调整控件位置
                AdjustControlPositions();
            };

            // 添加当前翻译源标签
            _currentServiceLabel = new Label
            {
                Text = "当前翻译源：Microsoft翻译",
                Location = new Point(140, 15),
                AutoSize = true,
                ForeColor = Color.Green
            };

            // 创建控件
            sourceLabel = new Label
            {
                Text = "源文本：",
                Location = new Point(10, 45),
                AutoSize = true
            };

            targetLabel = new Label
            {
                Text = "译文：",
                Location = new Point(10, 275),
                AutoSize = true
            };

            sourceComboBox = new ComboBox
            {
                Name = "sourceComboBox",
                Location = new Point(70, 42),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            targetComboBox = new ComboBox
            {
                Name = "targetComboBox",
                Location = new Point(70, 272),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            swapButton = new Button
            {
                Text = "⇅",
                Location = new Point(200, 42),
                Size = new Size(40, 25),
                Font = new Font(this.Font.FontFamily, 12)
            };
            swapButton.Click += SwapButton_Click;

            autoTranslateCheckBox = new CheckBox
            {
                Text = "实时翻译",
                Location = new Point(250, 45),
                AutoSize = true
            };
            autoTranslateCheckBox.CheckedChanged += AutoTranslateCheckBox_CheckedChanged;

            // 添加悬浮窗控制
            overlayCheckBox = new CheckBox
            {
                Text = "显示悬浮窗",
                Location = new Point(350, 45),
                AutoSize = true,
                Checked = false  // 初始状态为不选中
            };
            overlayCheckBox.CheckedChanged += (s, e) =>
            {
                try
                {
                    if (overlayCheckBox?.Checked == true)
                    {
                        if (_translationOverlay == null || _translationOverlay.IsDisposed)
                        {
                            _translationOverlay = new TranslationOverlay();
                            LogDebug("创建新的悬浮窗");
                        }
                        _translationOverlay.Show();
                        _translationOverlay.BringToFront();
                        _translationOverlay.UpdateTranslation(targetTextBox?.Text ?? string.Empty);
                        LogDebug("显示悬浮窗");
                    }
                    else if (_translationOverlay != null && !_translationOverlay.IsDisposed)
                    {
                        _translationOverlay.Hide();
                        LogDebug("隐藏悬浮窗");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"悬浮窗操作错误: {ex.Message}");
                }
            };

            // 添加文本长度控制
            lengthLabel = new Label
            {
                Text = "最大字符数：",
                Location = new Point(450, 45),
                AutoSize = true
            };

            lengthNumericUpDown = new NumericUpDown
            {
                Location = new Point(540, 43),
                Size = new Size(80, 25),
                Minimum = 50,
                Maximum = 1000,
                Value = DefaultMaxLength,
                Increment = 50
            };
            lengthNumericUpDown.ValueChanged += (s, e) =>
            {
                _currentMaxLength = (int)lengthNumericUpDown.Value;
            };

            // 添加翻译延迟控制
            delayLabel = new Label
            {
                Text = "延迟(ms)：",
                Location = new Point(630, 45),
                AutoSize = true
            };

            delayNumericUpDown = new NumericUpDown
            {
                Location = new Point(700, 43),
                Size = new Size(80, 25),
                Minimum = 500,
                Maximum = 3000,
                Value = DefaultTranslationDelay,
                Increment = 100
            };
            delayNumericUpDown.ValueChanged += (s, e) =>
            {
                if (_autoTranslateTimer != null)
                {
                    _autoTranslateTimer.Interval = (int)delayNumericUpDown.Value;
                }
            };

            captureButton = new Button
            {
                Text = "开始捕获",
                Location = new Point(450, 75),
                Size = new Size(80, 25)
            };
            captureButton.Click += (s, e) =>
            {
                try
                {
                    if (!_isCaptureEnabled)
                    {
                        _captionService.StartCapturing((s, e) => OnCaptionReceived(s, e));
                        captureButton.Text = "停止捕获";
                        _isCaptureEnabled = true;

                        // 自动显示悬浮窗
                        try
                        {
                            if (_translationOverlay == null || _translationOverlay.IsDisposed)
                            {
                                _translationOverlay = new TranslationOverlay();
                                LogDebug("创建新的悬浮窗");
                            }
                            _translationOverlay.UpdateTranslation("等待翻译...");
                            _translationOverlay.Show();
                            overlayCheckBox.Checked = true;
                            LogDebug("显示悬浮窗");
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"显示悬浮窗时出错: {ex.Message}");
                        }
                    }
                    else
                    {
                        _captionService.Dispose();
                        _captionService = new LiveCaptionService();
                        captureButton.Text = "开始捕获";
                        _isCaptureEnabled = false;

                        // 自动隐藏悬浮窗
                        try
                        {
                            if (_translationOverlay != null && !_translationOverlay.IsDisposed)
                            {
                                _translationOverlay.Hide();
                                overlayCheckBox.Checked = false;
                                LogDebug("隐藏悬浮窗");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"隐藏悬浮窗时出错: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            clearButton = new Button
            {
                Text = "清空",
                Location = new Point(540, 42),
                Size = new Size(80, 25)
            };
            clearButton.Click += (s, e) =>
            {
                sourceTextBox?.Clear();
                targetTextBox?.Clear();
                _translatedTexts.Clear();
                LogDebug("已清空翻译缓存");
            };

            sourceTextBox = new TextBox
            {
                Name = "sourceTextBox",
                Location = new Point(10, 75),
                Size = new Size(760, 180),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            sourceTextBox.TextChanged += SourceTextBox_TextChanged;

            targetTextBox = new TextBox
            {
                Name = "targetTextBox",
                Location = new Point(10, 305),
                Size = new Size(760, 140),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            translateButton = new Button
            {
                Text = "翻译",
                Location = new Point(350, 42),
                Size = new Size(80, 25)
            };
            translateButton.Click += TranslateButton_Click;

            // 添加调试日志文本框
            var debugLabel = new Label
            {
                Text = "调试日志：",
                Location = new Point(10, 495),
                AutoSize = true
            };

            debugLogTextBox = new TextBox
            {
                Name = "debugLogTextBox",
                Location = new Point(10, 520),
                Size = new Size(760, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9)
            };

            // 添加配置按钮
            configButton = new Button
            {
                Text = "配置有道API",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                Visible = true
            };
            configButton.Click += ConfigButton_Click;

            // 添加有道API配置控件
            var youdaoConfigPanel = new Panel
            {
                Name = "youdaoConfigPanel",
                Location = new Point(10, 10),
                Size = new Size(760, 90),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            var youdaoAppKeyLabel = new Label
            {
                Text = "应用ID (AppKey):",
                Location = new Point(10, 10),
                Size = new Size(120, 20)
            };

            var youdaoAppKeyTextBox = new TextBox
            {
                Name = "youdaoAppKeyTextBox",
                Location = new Point(140, 10),
                Size = new Size(300, 20),
                UseSystemPasswordChar = false,
                PasswordChar = '\0',
                ShortcutsEnabled = true
            };
            // 添加粘贴快捷键支持
            youdaoAppKeyTextBox.KeyDown += (s, e) => 
            {
                if (e.Control && e.KeyCode == Keys.V)
                {
                    youdaoAppKeyTextBox.Paste();
                    e.Handled = true;
                }
            };
            // 添加右键菜单
            var keyContextMenu = new ContextMenuStrip();
            keyContextMenu.Items.Add("粘贴", null, (s, e) => youdaoAppKeyTextBox.Paste());
            keyContextMenu.Items.Add("复制", null, (s, e) => youdaoAppKeyTextBox.Copy());
            keyContextMenu.Items.Add("剪切", null, (s, e) => youdaoAppKeyTextBox.Cut());
            keyContextMenu.Items.Add("全选", null, (s, e) => youdaoAppKeyTextBox.SelectAll());
            youdaoAppKeyTextBox.ContextMenuStrip = keyContextMenu;

            var youdaoAppSecretLabel = new Label
            {
                Text = "应用密钥 (AppSecret):",
                Location = new Point(10, 40),
                Size = new Size(120, 20)
            };

            var youdaoAppSecretTextBox = new TextBox
            {
                Name = "youdaoAppSecretTextBox",
                Location = new Point(140, 40),
                Size = new Size(300, 20),
                UseSystemPasswordChar = false,
                PasswordChar = '\0',
                ShortcutsEnabled = true
            };
            // 添加粘贴快捷键支持
            youdaoAppSecretTextBox.KeyDown += (s, e) => 
            {
                if (e.Control && e.KeyCode == Keys.V)
                {
                    youdaoAppSecretTextBox.Paste();
                    e.Handled = true;
                }
            };
            // 添加右键菜单
            var secretContextMenu = new ContextMenuStrip();
            secretContextMenu.Items.Add("粘贴", null, (s, e) => youdaoAppSecretTextBox.Paste());
            secretContextMenu.Items.Add("复制", null, (s, e) => youdaoAppSecretTextBox.Copy());
            secretContextMenu.Items.Add("剪切", null, (s, e) => youdaoAppSecretTextBox.Cut());
            secretContextMenu.Items.Add("全选", null, (s, e) => youdaoAppSecretTextBox.SelectAll());
            youdaoAppSecretTextBox.ContextMenuStrip = secretContextMenu;

            var youdaoSaveButton = new Button
            {
                Text = "保存配置",
                Location = new Point(450, 25),
                Size = new Size(100, 30)
            };

            youdaoSaveButton.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(youdaoAppKeyTextBox.Text) || string.IsNullOrWhiteSpace(youdaoAppSecretTextBox.Text))
                {
                    MessageBox.Show("请输入应用ID和应用密钥", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    string jsonString;
                    Dictionary<string, JsonElement> newConfig = new Dictionary<string, JsonElement>();
                    
                    // 读取配置文件并关闭文件流
                    using (var fileStream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var reader = new StreamReader(fileStream))
                    {
                        jsonString = reader.ReadToEnd();
                    }
                    
                    // 解析JSON
                    using (var document = JsonDocument.Parse(jsonString))
                    {
                        var root = document.RootElement;
                        foreach (var property in root.EnumerateObject())
                        {
                            newConfig[property.Name] = property.Value.Clone();
                        }
                    }

                    // 创建新的配置内容
                    var updatedJson = new MemoryStream();
                    using (var writer = new Utf8JsonWriter(updatedJson, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();

                        foreach (var kvp in newConfig)
                        {
                            if (kvp.Key == "Youdao")
                            {
                                writer.WritePropertyName(kvp.Key);
                                writer.WriteStartObject();
                                writer.WriteString("AppKey", youdaoAppKeyTextBox.Text);
                                writer.WriteString("AppSecret", youdaoAppSecretTextBox.Text);
                                writer.WriteEndObject();
                            }
                            else
                            {
                                writer.WritePropertyName(kvp.Key);
                                kvp.Value.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }

                    // 将内存流写入文件
                    updatedJson.Position = 0;
                    byte[] jsonBytes = updatedJson.ToArray();
                    
                    // 使用FileShare.None确保独占访问
                    using (var fileStream = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(jsonBytes, 0, jsonBytes.Length);
                    }

                    MessageBox.Show("配置已保存，有道翻译API已启用", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // 重新加载配置
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    
                    var newConfiguration = builder.Build();
                    
                    // 重新创建翻译服务实例
                    _translationService?.Dispose();
                    _translationService = TranslationServiceFactory.CreateTranslationService(TranslationServiceType.Youdao, newConfiguration);
                    
                    // 更新当前服务类型
                    _currentServiceType = TranslationServiceType.Youdao;
                    
                    // 更新当前翻译源标签
                    if (_currentServiceLabel != null)
                    {
                        _currentServiceLabel.Text = "当前翻译源：有道翻译";
                    }
                    
                    // 更新API下拉框选择
                    var apiComboBox = this.Controls.OfType<ComboBox>().FirstOrDefault(c => c.Items.Contains("有道翻译"));
                    if (apiComboBox != null)
                    {
                        apiComboBox.SelectedIndex = 2; // 有道翻译的索引
                    }
                    
                    LogDebug("有道翻译API配置已更新并启用");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存配置时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // 将控件添加到面板
            youdaoConfigPanel.Controls.AddRange(new Control[] {
                youdaoAppKeyLabel,
                youdaoAppKeyTextBox,
                youdaoAppSecretLabel,
                youdaoAppSecretTextBox,
                youdaoSaveButton
            });

            // 添加面板到窗体
            this.Controls.Add(youdaoConfigPanel);

            // 添加控件到窗体
            this.Controls.AddRange(new Control[] {
                apiComboBox,
                _currentServiceLabel,
                sourceLabel,
                targetLabel,
                sourceComboBox,
                targetComboBox,
                swapButton,
                autoTranslateCheckBox,
                overlayCheckBox,
                lengthLabel,
                lengthNumericUpDown,
                delayLabel,
                delayNumericUpDown,
                captureButton,
                clearButton,
                sourceTextBox,
                targetTextBox,
                translateButton,
                debugLabel,
                debugLogTextBox,
                configButton
            });

            // 设置最小化和关闭按钮
            this.MinimizeBox = true;
            this.MaximizeBox = true;

            // 设置窗体大小调整时的行为
            this.Resize += (s, e) =>
            {
                if (sourceTextBox != null) sourceTextBox.Width = this.ClientSize.Width - 30;
                if (targetTextBox != null) targetTextBox.Width = this.ClientSize.Width - 30;
                if (debugLogTextBox != null) debugLogTextBox.Width = this.ClientSize.Width - 30;
            };

            // 窗体关闭时释放资源
            this.FormClosing += (s, e) =>
            {
                _captionService?.Dispose();
            };
        }

        private void SetupAutoTranslateTimer()
        {
            _autoTranslateTimer = new System.Windows.Forms.Timer
            {
                Interval = DefaultTranslationDelay
            };
            _autoTranslateTimer.Tick += async (s, e) =>
            {
                _autoTranslateTimer!.Stop();
                await TranslateText();
            };
        }

        private void InitializeLanguageComboBoxes()
        {
            foreach (var lang in LanguageConfig.SupportedLanguages)
            {
                sourceComboBox.Items.Add(new LanguageItem(lang.Key, lang.Value));
                if (lang.Key != "auto") // 目标语言不包含自动检测
                {
                    targetComboBox.Items.Add(new LanguageItem(lang.Key, lang.Value));
                }
            }

            // 设置默认选项
            sourceComboBox.SelectedIndex = 0; // auto
            targetComboBox.SelectedIndex = targetComboBox.FindStringExact("中文"); // zh

            // 添加事件处理
            sourceComboBox.SelectedIndexChanged += async (s, e) =>
            {
                _currentFromLanguage = ((LanguageItem)sourceComboBox.SelectedItem).Code;
                if (_isAutoTranslate)
                {
                    await TranslateText();
                }
            };

            targetComboBox.SelectedIndexChanged += async (s, e) =>
            {
                _currentToLanguage = ((LanguageItem)targetComboBox.SelectedItem).Code;
                if (_isAutoTranslate)
                {
                    await TranslateText();
                }
            };
        }

        private void SwapButton_Click(object sender, EventArgs e)
        {
            if (_currentFromLanguage == "auto" || 
                sourceTextBox == null || 
                targetTextBox == null || 
                sourceComboBox == null || 
                targetComboBox == null) return;

            var tempLang = _currentFromLanguage;
            var tempText = sourceTextBox.Text;

            sourceComboBox.SelectedIndex = sourceComboBox.FindStringExact(targetComboBox.Text);
            targetComboBox.SelectedIndex = targetComboBox.FindStringExact(sourceComboBox.Text);

            sourceTextBox.Text = targetTextBox.Text;
            targetTextBox.Text = tempText;
        }

        private void AutoTranslateCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoTranslateCheckBox == null) return;
            _isAutoTranslate = autoTranslateCheckBox.Checked;
            if (_isAutoTranslate)
            {
                _ = TranslateText();
            }
        }

        private void SourceTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_isAutoTranslate)
            {
                _autoTranslateTimer?.Stop();
                _autoTranslateTimer?.Start();
            }
        }

        private async void TranslateButton_Click(object sender, EventArgs e)
        {
            await TranslateText();
        }

        private async Task TranslateText()
        {
            if (sourceTextBox == null || targetTextBox == null || translateButton == null) return;

            var sourceText = sourceTextBox.Text.Trim();
            if (string.IsNullOrEmpty(sourceText))
            {
                // 当源文本为空时，不清空目标文本框和悬浮窗
                return;
            }

            // 检查翻译频率
            var now = DateTime.Now;
            if ((now - _lastTranslationTime).TotalMilliseconds < DefaultTranslationDelay)
            {
                return;
            }
            _lastTranslationTime = now;

            translateButton.Enabled = false;
            try
            {
                // 获取所有段落
                var paragraphs = sourceText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                // 找出最新且未翻译过的段落
                string textToTranslate = null;
                for (int i = paragraphs.Length - 1; i >= 0; i--)
                {
                    var paragraph = paragraphs[i].Trim();
                    if (!string.IsNullOrEmpty(paragraph) && !_translatedTexts.Contains(paragraph))
                    {
                        textToTranslate = paragraph;
                        break;
                    }
                }

                // 如果找到了需要翻译的文本
                if (!string.IsNullOrEmpty(textToTranslate))
                {
                    try
                    {
                        LogDebug($"翻译文本: {textToTranslate.Substring(0, Math.Min(50, textToTranslate.Length))}...");
                        
                        // 如果文本过长，截取一部分
                        var textForTranslation = textToTranslate.Length > _currentMaxLength 
                            ? textToTranslate.Substring(0, _currentMaxLength) 
                            : textToTranslate;
                        
                        var translation = await _translationService.TranslateAsync(
                            textForTranslation,
                            _currentFromLanguage,
                            _currentToLanguage
                        );
                        
                        // 添加到已翻译集合中
                        _translatedTexts.Add(textToTranslate);
                        
                        // 如果已翻译集合过大，移除最早的项
                        if (_translatedTexts.Count > 100)
                        {
                            _translatedTexts = new HashSet<string>(_translatedTexts.Skip(_translatedTexts.Count - 50));
                        }
                        
                        targetTextBox.AppendText(translation + Environment.NewLine);
                        LogDebug($"翻译结果: {translation.Substring(0, Math.Min(50, translation.Length))}...");
                        
                        // 更新悬浮窗内容
                        if (_translationOverlay?.Visible == true)
                        {
                            LogDebug("更新悬浮窗内容");
                            _translationOverlay.UpdateTranslation(translation);
                        }
                        
                        // 保持显示最近的几行翻译
                        var lines = targetTextBox.Lines;
                        if (lines.Length > 10)
                        {
                            targetTextBox.Lines = lines.Skip(lines.Length - 10).ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"翻译错误：{ex.Message}");
                        // 翻译出错时不清空现有的翻译文本
                        if (_translationOverlay?.Visible == true)
                        {
                            _translationOverlay.UpdateTranslation($"翻译错误：{ex.Message}");
                        }
                    }
                }
                else
                {
                    LogDebug("没有新的内容需要翻译");
                }
            }
            finally
            {
                translateButton.Enabled = true;
            }
        }

        private void LogDebug(string message)
        {
            if (debugLogTextBox != null)
            {
                debugLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                // 保持最新的日志可见
                debugLogTextBox.SelectionStart = debugLogTextBox.TextLength;
                debugLogTextBox.ScrollToCaret();
            }
        }

        private void ConfigButton_Click(object sender, EventArgs e)
        {
            using var configForm = new ConfigForm();
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                // 重新加载配置
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                
                var newConfig = builder.Build();
                
                // 如果当前使用的是有道翻译，则重新创建翻译服务实例
                if (_currentServiceType == TranslationServiceType.Youdao)
                {
                    _translationService?.Dispose();
                    _translationService = TranslationServiceFactory.CreateTranslationService(TranslationServiceType.Youdao, newConfig);
                }
            }
        }

        private void AdjustControlPositions()
        {
            // 如果选择了有道翻译，调整其他控件的位置
            if (_currentServiceType == TranslationServiceType.Youdao)
            {
                if (sourceLabel != null) sourceLabel.Location = new Point(10, 110);
                if (targetLabel != null) targetLabel.Location = new Point(10, 340);
                if (sourceComboBox != null) sourceComboBox.Location = new Point(70, 107);
                if (targetComboBox != null) targetComboBox.Location = new Point(70, 337);
                if (swapButton != null) swapButton.Location = new Point(200, 107);
                if (autoTranslateCheckBox != null) autoTranslateCheckBox.Location = new Point(250, 110);
                if (overlayCheckBox != null) overlayCheckBox.Location = new Point(350, 110);
                if (lengthLabel != null) lengthLabel.Location = new Point(450, 110);
                if (lengthNumericUpDown != null) lengthNumericUpDown.Location = new Point(540, 108);
                if (delayLabel != null) delayLabel.Location = new Point(630, 110);
                if (delayNumericUpDown != null) delayNumericUpDown.Location = new Point(700, 108);
                if (captureButton != null) captureButton.Location = new Point(450, 140);
                if (clearButton != null) clearButton.Location = new Point(540, 107);
                if (sourceTextBox != null) sourceTextBox.Location = new Point(10, 140);
                if (targetTextBox != null) targetTextBox.Location = new Point(10, 370);
                if (translateButton != null) translateButton.Location = new Point(350, 107);
            }
            else
            {
                // 恢复原来的位置
                if (sourceLabel != null) sourceLabel.Location = new Point(10, 45);
                if (targetLabel != null) targetLabel.Location = new Point(10, 275);
                if (sourceComboBox != null) sourceComboBox.Location = new Point(70, 42);
                if (targetComboBox != null) targetComboBox.Location = new Point(70, 272);
                if (swapButton != null) swapButton.Location = new Point(200, 42);
                if (autoTranslateCheckBox != null) autoTranslateCheckBox.Location = new Point(250, 45);
                if (overlayCheckBox != null) overlayCheckBox.Location = new Point(350, 45);
                if (lengthLabel != null) lengthLabel.Location = new Point(450, 45);
                if (lengthNumericUpDown != null) lengthNumericUpDown.Location = new Point(540, 43);
                if (delayLabel != null) delayLabel.Location = new Point(630, 45);
                if (delayNumericUpDown != null) delayNumericUpDown.Location = new Point(700, 43);
                if (captureButton != null) captureButton.Location = new Point(450, 75);
                if (clearButton != null) clearButton.Location = new Point(540, 42);
                if (sourceTextBox != null) sourceTextBox.Location = new Point(10, 75);
                if (targetTextBox != null) targetTextBox.Location = new Point(10, 305);
                if (translateButton != null) translateButton.Location = new Point(350, 42);
            }
        }

        private class LanguageItem
        {
            public string Code { get; }
            public string Name { get; }

            public LanguageItem(string code, string name)
            {
                Code = code;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }
    }

    public static class ControlExtensions
    {
        public static async Task InvokeAsync(this Control control, Func<Task> action)
        {
            if (control.InvokeRequired)
            {
                await (Task)control.Invoke(new Func<Task>(async () => await action()));
            }
            else
            {
                await action();
            }
        }
    }
} 
using System;
using System.Windows.Automation;
using Debug = System.Diagnostics.Debug;
using System.Windows.Forms;

namespace RealtimeTranslator
{
    public class LiveCaptionService : IDisposable
    {
        private AutomationElement? _captionsWindow;
        private AutomationPropertyChangedEventHandler? _eventHandler;
        private Action<object, string>? _textCallback;
        private bool _isCapturing;
        private System.Windows.Forms.Timer? _updateTimer;
        private string _lastCapturedText = string.Empty;

        public void Log(string message)
        {
            try
            {
                var form = Application.OpenForms[0];
                if (form != null)
                {
                    var debugLogTextBox = form.Controls.Find("debugLogTextBox", true)[0] as TextBox;
                    if (debugLogTextBox != null)
                    {
                        form.Invoke(() =>
                        {
                            debugLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                            debugLogTextBox.SelectionStart = debugLogTextBox.TextLength;
                            debugLogTextBox.ScrollToCaret();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error logging: {ex.Message}");
            }
        }

        private AutomationElement? FindCaptionsWindow()
        {
            try
            {
                Log("开始查找字幕窗口...");
                var root = AutomationElement.RootElement;
                var allWindows = root.FindAll(TreeScope.Children, Condition.TrueCondition);
                Log($"找到 {allWindows.Count} 个顶级窗口");

                foreach (AutomationElement window in allWindows)
                {
                    try
                    {
                        var name = window.Current.Name;
                        var className = window.Current.ClassName;
                        Log($"窗口: {name}, 类名: {className}");
                        
                        if (name == "Live captions" || name == "实时辅助字幕")
                        {
                            Log($"找到字幕窗口：{name}");
                            return window;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"处理窗口时出错: {ex.Message}");
                        continue;
                    }
                }

                Log("未找到字幕窗口");
                return null;
            }
            catch (Exception ex)
            {
                Log($"查找字幕窗口时出错：{ex.Message}");
                return null;
            }
        }

        private string GetCaptionText()
        {
            try
            {
                if (_captionsWindow == null) return string.Empty;

                // 尝试获取所有文本元素
                var textElements = _captionsWindow.FindAll(TreeScope.Subtree, 
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                
                Log($"找到 {textElements.Count} 个文本元素");
                
                foreach (AutomationElement textElement in textElements)
                {
                    try
                    {
                        var text = textElement.Current.Name;
                        if (!string.IsNullOrEmpty(text))
                        {
                            Log($"获取到字幕文本: {text}");
                            return text;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"获取文本元素值时出错: {ex.Message}");
                    }
                }
                
                // 如果没有找到文本元素，尝试直接获取窗口的名称
                var windowText = _captionsWindow.Current.Name;
                if (!string.IsNullOrEmpty(windowText) && windowText != "Live captions" && windowText != "实时辅助字幕")
                {
                    Log($"从窗口名称获取字幕: {windowText}");
                    return windowText;
                }
            }
            catch (Exception ex)
            {
                Log($"获取字幕文本时出错: {ex.Message}");
            }
            return string.Empty;
        }

        public void StartCapturing(Action<object, string> callback)
        {
            if (_isCapturing)
            {
                Log("已经在捕获中");
                return;
            }

            Log("开始捕获字幕");
            _textCallback = callback;
            _captionsWindow = FindCaptionsWindow();

            if (_captionsWindow == null)
            {
                Log("未找到字幕窗口，请确保Windows 11实时字幕功能已开启（Win + Ctrl + L）");
                throw new Exception("未找到 Windows 11 实时字幕窗口。请确保已启用实时字幕功能（Win + Ctrl + L）。");
            }

            try
            {
                // 设置定时器定期检查文本变化
                _updateTimer = new System.Windows.Forms.Timer
                {
                    Interval = 100 // 每100毫秒检查一次
                };

                _updateTimer.Tick += (s, e) =>
                {
                    try
                    {
                        if (_captionsWindow == null || !IsWindowAvailable(_captionsWindow))
                        {
                            Log("字幕窗口已关闭，重新查找...");
                            _captionsWindow = FindCaptionsWindow();
                        }
                        
                        var currentText = GetCaptionText();
                        if (!string.IsNullOrEmpty(currentText) && currentText != _lastCapturedText)
                        {
                            _lastCapturedText = currentText;
                            _textCallback?.Invoke(this, currentText);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"定时器检查文本时出错: {ex.Message}");
                    }
                };

                _updateTimer.Start();
                _isCapturing = true;
                Log("字幕捕获已启动");
            }
            catch (Exception ex)
            {
                Log($"设置自动化事件监听时出错：{ex.Message}");
                throw;
            }
        }

        private bool IsWindowAvailable(AutomationElement element)
        {
            try
            {
                var name = element.Current.Name;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _isCapturing = false;
            Log("字幕捕获已停止");
        }
    }
} 
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RealtimeTranslator
{
    public class TranslationOverlay : Form
    {
        private Label _translationLabel = null!;
        private System.Windows.Forms.Timer _positionTimer = null!;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private bool _isInitialized = false;
        private const int CORNER_RADIUS = 12;  // 圆角半径
        private System.Drawing.Drawing2D.GraphicsPath? _formPath;
        private const float BORDER_WIDTH = 1.2f;  // 边框宽度

        public TranslationOverlay()
        {
            InitializeOverlay();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void InitializeOverlay()
        {
            if (_isInitialized) return;

            try
            {
                // 设置窗体属性
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopMost = true;
                this.StartPosition = FormStartPosition.Manual;
                this.BackColor = Color.White;
                this.Size = new Size(800, 60);
                this.Opacity = 0.9;
                this.ShowIcon = false;
                this.MinimizeBox = false;
                this.MaximizeBox = false;
                this.ControlBox = false;
                this.DoubleBuffered = true;

                // 创建标签
                _translationLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 16, FontStyle.Regular),
                    AutoSize = false,
                    Padding = new Padding(20, 10, 20, 10)
                };
                this.Controls.Add(_translationLabel);

                // 设置初始位置
                var screen = Screen.PrimaryScreen;
                if (screen != null)
                {
                    var initialLocation = new Point(
                        screen.WorkingArea.Width / 2 - this.Width / 2,
                        screen.WorkingArea.Height - this.Height - 100
                    );
                    this.Location = initialLocation;
                    Debug.WriteLine($"初始化悬浮窗位置: X={initialLocation.X}, Y={initialLocation.Y}");
                }

                // 添加圆角和边框效果
                this.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    
                    if (_formPath == null)
                    {
                        _formPath = CreateRoundRectPath(this.Width, this.Height, CORNER_RADIUS);
                    }

                    // 创建一个稍微大一点的路径用于边框
                    var borderPath = CreateRoundRectPath(this.Width - 1, this.Height - 1, CORNER_RADIUS);

                    // 填充背景
                    using (var brush = new SolidBrush(this.BackColor))
                    {
                        g.FillPath(brush, _formPath);
                    }

                    // 绘制边框
                    using (var pen = new Pen(Color.FromArgb(200, 200, 200), BORDER_WIDTH))
                    {
                        pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;
                        g.DrawPath(pen, borderPath);
                    }

                    borderPath.Dispose();
                };

                // 设置窗体形状
                UpdateFormRegion();

                // 创建定时器
                _positionTimer = new System.Windows.Forms.Timer
                {
                    Interval = 100
                };
                _positionTimer.Tick += UpdatePosition;
                _positionTimer.Start();

                _isInitialized = true;
                Debug.WriteLine("悬浮窗初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化悬浮窗时出错: {ex}");
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundRectPath(int width, int height, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var r2 = radius * 2;
            var rect = new Rectangle(0, 0, width, height);
            
            // 使用贝塞尔曲线创建更平滑的圆角
            path.AddArc(rect.X, rect.Y, r2, r2, 180, 90);
            path.AddArc(rect.Right - r2, rect.Y, r2, r2, 270, 90);
            path.AddArc(rect.Right - r2, rect.Bottom - r2, r2, r2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r2, r2, r2, 90, 90);
            path.CloseFigure();
            
            return path;
        }

        private void UpdateFormRegion()
        {
            if (_formPath != null)
            {
                this.Region?.Dispose();
                this.Region = new Region(_formPath);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            _formPath?.Dispose();
            _formPath = CreateRoundRectPath(this.Width, this.Height, CORNER_RADIUS);
            UpdateFormRegion();
            this.Invalidate();
        }

        protected override bool ShowWithoutActivation => true;

        public new void Show()
        {
            try
            {
                if (!_isInitialized)
                {
                    InitializeOverlay();
                }

                base.Show();
                this.BringToFront();
                this.TopMost = true;
                this.Visible = true;
                Debug.WriteLine($"显示悬浮窗: Location={this.Location}, Size={this.Size}, Visible={this.Visible}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示悬浮窗时出错: {ex}");
            }
        }

        public void UpdateTranslation(string text)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(UpdateTranslation), text);
                    return;
                }

                _translationLabel.Text = text;
                Debug.WriteLine($"更新翻译文本: {text}");
                
                if (!this.Visible)
                {
                    Show();
                }
                
                this.BringToFront();
                this.TopMost = true;
                this.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新翻译时出错: {ex}");
            }
        }

        private void UpdatePosition(object? sender, EventArgs e)
        {
            try
            {
                if (!this.Visible) return;

                var captionWindow = FindCaptionWindow();
                Debug.WriteLine($"字幕窗口句柄: {captionWindow}");

                if (captionWindow != IntPtr.Zero)
                {
                    if (User32.GetWindowRect(captionWindow, out var rect))
                    {
                        Debug.WriteLine($"字幕窗口位置: Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}");
                        
                        var screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                        var newLocation = new Point(
                            Math.Max(screenBounds.Left, rect.Left),
                            Math.Min(screenBounds.Bottom - this.Height, rect.Bottom + 10)
                        );
                        var newWidth = Math.Min(rect.Right - rect.Left, screenBounds.Width);

                        if (this.Location != newLocation || this.Width != newWidth)
                        {
                            this.Location = newLocation;
                            this.Width = newWidth;
                            this.BringToFront();
                            this.TopMost = true;
                            Debug.WriteLine($"更新悬浮窗位置: X={newLocation.X}, Y={newLocation.Y}, Width={newWidth}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("未找到字幕窗口，使用默认位置");
                    var screen = Screen.PrimaryScreen;
                    if (screen != null)
                    {
                        var defaultLocation = new Point(
                            screen.WorkingArea.Width / 2 - this.Width / 2,
                            screen.WorkingArea.Height - this.Height - 100
                        );
                        this.Location = defaultLocation;
                        Debug.WriteLine($"设置默认位置: X={defaultLocation.X}, Y={defaultLocation.Y}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新位置时出错: {ex}");
            }
        }

        private IntPtr FindCaptionWindow()
        {
            try
            {
                var hwnd = User32.FindWindow(null, "Live captions");
                if (hwnd != IntPtr.Zero)
                {
                    Debug.WriteLine("找到英文字幕窗口");
                    return hwnd;
                }
            
                hwnd = User32.FindWindow(null, "实时辅助字幕");
                if (hwnd != IntPtr.Zero)
                {
                    Debug.WriteLine("找到中文字幕窗口");
                }
                else
                {
                    Debug.WriteLine("未找到字幕窗口");
                }
                return hwnd;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"查找字幕窗口时出错: {ex}");
                return IntPtr.Zero;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _formPath?.Dispose();
                _positionTimer?.Stop();
                _positionTimer?.Dispose();
                Debug.WriteLine("悬浮窗已释放");
            }
            base.Dispose(disposing);
        }
    }

    public class User32
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
} 
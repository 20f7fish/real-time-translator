using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Windows.Forms;
using RealtimeTranslator;

namespace RealtimeTranslator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            
            // 加载配置
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            
            Application.Run(new MainForm(config));
        }
    }
}
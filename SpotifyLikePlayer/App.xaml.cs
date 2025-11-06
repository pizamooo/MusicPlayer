using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;


namespace SpotifyLikePlayer
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static LoginWindow CurrentLoginWindow { get; set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        }
    }
}

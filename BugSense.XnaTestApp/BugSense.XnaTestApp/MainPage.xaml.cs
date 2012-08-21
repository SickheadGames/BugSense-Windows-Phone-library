using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BugSense.XnaTestApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            new Game1();
        }

        public SwapChainBackgroundPanel SwapChainPanel
        {
            get { return SwapChain; }
        }

        private void SwapChain_Loaded(object sender, RoutedEventArgs e)
        {
            Game1.Instance.Initialize();
        }

        private void SwapChain_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var dpi = DisplayProperties.LogicalDpi;

            var manager = SharedGraphicsDeviceManager.Current;
            manager.PreferredBackBufferWidth = (int)((e.NewSize.Width * (dpi / 96.0)) + 0.5);
            manager.PreferredBackBufferHeight = (int)((e.NewSize.Height * (dpi / 96.0)) + 0.5);
            manager.SwapChainPanel = SwapChain;
            manager.ApplyChanges();
        }
    }
}

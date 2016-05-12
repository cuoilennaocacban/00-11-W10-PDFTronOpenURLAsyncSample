using System;
using Windows.UI.Xaml.Controls;
// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Utilities
{
    public sealed partial class PopupCommandMenubuttonHost : UserControl
    {
        public PopupCommandMenubuttonHost()
        {
            this.InitializeComponent();
            UseSmallButtons = false;
        }

        public PopupCommandMenubuttonHost(bool useSmallButtons)
        {
            this.InitializeComponent();
            UseSmallButtons = useSmallButtons;
        }

        public bool UseSmallButtons { get; set; }

        public Grid HostGrid
        {
            get
            {
                if (UseSmallButtons)
                {
                    return SmallSizeGrid;
                }
                return NormalSizeGrid;
            }
        }
    }
}

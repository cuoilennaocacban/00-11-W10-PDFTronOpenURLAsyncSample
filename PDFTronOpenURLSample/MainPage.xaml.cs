using System;
using Windows.System;
using Windows.UI.Xaml;
using pdftron;
using pdftron.PDF;
using pdftron.PDF.Tools;

namespace PDFTronOpenURLSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public ToolManager ToolManager { get; set; }

        public MainPage()
        {
            InitializeComponent();
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            PDFNet.Initialize();
            PDFViewCtrl viewCtrl = new PDFViewCtrl();

            viewCtrl.SetPagePresentationMode(PDFViewCtrlPagePresentationMode.e_facing_cover);
            viewCtrl.FlowDirection = FlowDirection.LeftToRight;
            viewCtrl.AnimateAllPageFlipping = true;

            MainBorder.Child = viewCtrl;
            
            viewCtrl.SetDrawAnnotations(false);
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.System.MemoryManager"))
            {
                ulong mem = MemoryManager.AppMemoryUsageLimit / (5000000 * 2);
                viewCtrl.SetRenderedContentCacheSize(Math.Min(mem, 40)); // 96 MB is plenty
            }

            ToolManager = new ToolManager(viewCtrl)
            {
                EnablePopupMenuOnLongPress = false,
                PanToolTextSelectionMode = ToolManager.TextSelectionBehaviour.Mixed,
                TextMarkupAdobeHack = true,
                IsPopupMenuEnabled = false
            };
            
            await viewCtrl.OpenURLAsync("https://www.math.ucdavis.edu/~linear/linear.pdf");
        }
    }
}

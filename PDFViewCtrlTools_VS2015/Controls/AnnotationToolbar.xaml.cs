using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Controls
{
    /// <summary>
    /// The AnnotationToolbar works with a ToolManager to allow quick selection of different tools.
    /// The toolbar shows a list of buttons which prompts the associated ToolManager to switch to that tool.
    /// </summary>
    public sealed partial class AnnotationToolbar : pdftron.PDF.Tools.Controls.AnnotationToolbarBase
    {
        /// <summary>
        /// Creates a new AnnotationToolbar that is not associated with a ToolManager.
        /// Use the ToolManager property to make the AnnotationToolbar Work with a ToolManager.
        /// </summary>
        public AnnotationToolbar()
        {
            this.InitializeComponent();
            Init();
        }

        /// <summary>
        /// Creates a new AnnotationToolbar that works with the ToolManager.
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="toolManager"></param>
        public AnnotationToolbar(ToolManager toolManager)
        {
            this.InitializeComponent();
            this.ToolManager = toolManager;
            Init();
        }

        private void Init()
        {

            MINIMUM_BUTTON_WIDTH = (double)this.Resources["AnnotationButtonWidth"];
            MINIMUM_BUTTON_WIDTH_SMALL_SCREEN = (double)this.Resources["AnnotationButtonSmallScreenWidth"];

            ToolbarButtonRanking = new ButtonBase[ToolbarButtonMainStack.Children.Count];

            int index = 0;

            // Highest priority: Will always be displayed
            ToolbarButtonRanking[index++] = ToolbarCloseButton;
            ToolbarButtonRanking[index++] = AnnotPanButton;
            ToolbarButtonRanking[index++] = AnnotStickyNoteButton;
            ToolbarButtonRanking[index++] = AnnotHighlightButton;
            ToolbarButtonRanking[index++] = AnnotFreeHandButton;
            ToolbarButtonRanking[index++] = AnnotEraserButton;
            ToolbarButtonRanking[index++] = AnnotSignatureButton;
            ToolbarButtonRanking[index++] = AnnotStrikeoutButton;
            ToolbarButtonRanking[index++] = AnnotUnderlineButton;
            ToolbarButtonRanking[index++] = AnnotFreeTextButton;
            ToolbarButtonRanking[index++] = AnnotArrowButton;
            ToolbarButtonRanking[index++] = AnnotSquareButton;
            ToolbarButtonRanking[index++] = AnnotCircleButton;
            ToolbarButtonRanking[index++] = AnnotLineButton;
            ToolbarButtonRanking[index++] = AnnotSquigglyButton;

            bool isPhone = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
            if (isPhone)
            {
                SwitchToSmallIcons();
            }
            else
            {
                SwitchToLargeIcons();
            }
        }

        protected override void AnnotationToolbar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RectangleGeometry clipRect = new RectangleGeometry();
            clipRect.Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            ContentPanel.Clip = clipRect;

            base.AnnotationToolbar_SizeChanged(sender, e);
        }

        /// <summary>
        /// Call this function to make the buttons and icons smaller, to work better on a phone size screen.
        /// </summary>
        public void SwitchToSmallIcons()
        {
            double inkWidth = (double)this.Resources["InkToolbarButtonSmallScreenWidth"];
            double inkHeight = (double)this.Resources["AnnotationToolbarSmallScreenHeight"];
            double buttonFontSize = (double)this.Resources["AnnotationButtonSmallScreenFontSize"];

            ContentPanel.Height = inkHeight;
            InkToolbar.Height = inkHeight;

            SetButtonSize(inkWidth, inkHeight, buttonFontSize);

            InkButtonGrid2.Visibility = Visibility.Collapsed;

           
        }

        /// <summary>
        /// Call this function to make the buttons and icons larger, to work better on a tablet or desktop size screen.
        /// </summary>
        public void SwitchToLargeIcons()
        {
            double inkWidth = (double)this.Resources["InkToolbarButtonWidth"];
            double inkHeight = (double)this.Resources["AnnotationToolbarHeight"];
            double buttonFontSize = (double)this.Resources["AnnotationButtonFontSize"];

            ContentPanel.Height = inkHeight;
            InkToolbar.Height = inkHeight;

            SetButtonSize(inkWidth, inkHeight, buttonFontSize);

            InkButtonGrid2.Visibility = Visibility.Visible;
        }

        private void SetButtonSize(double width, double height, double fontSize)
        {
            foreach (ButtonBase button in ToolbarButtonRanking)
            {
                button.FontSize = fontSize;
                button.Height = height;
            }

            CancelButton.MinWidth = width;

            InkButtonGrid0.Width = width;
            InkButtonGrid1.Width = width;
            InkButtonGrid2.Width = width;
            InkSymbol0.FontSize = fontSize;
            InkSymbol1.FontSize = fontSize;
            InkSymbol2.FontSize = fontSize;

            InkUndoButton.Width = width;
            InkUndoButton.FontSize = fontSize;
            InkRedoButton.Width = width;
            InkRedoButton.FontSize = fontSize;
            InkSaveButton.MinWidth = width;
        }

        /// <summary>
        /// Will go back if possible
        /// </summary>
        /// <returns>true if it went back, false otherwise</returns>
        public bool GoBack()
        {
            if (this.DataContext is pdftron.PDF.Tools.Controls.ViewModels.AnnotationToolbarViewModel)
            {
                return (this.DataContext as pdftron.PDF.Tools.Controls.ViewModels.AnnotationToolbarViewModel).GoBack();
            }
            return false;
        }
    }
}

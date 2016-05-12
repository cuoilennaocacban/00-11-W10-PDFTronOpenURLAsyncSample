using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    /// 
    /// </summary>
    public sealed partial class ThumbnailViewer : UserControl, pdftron.PDF.Tools.Controls.ControlBase.ICloseableControl
    {
        private ViewModels.ThumbnailsViewViewModel _ViewModel;
        public ViewModels.ThumbnailsViewViewModel ViewModel { get { return _ViewModel; } }

        private PDFViewCtrl _PDFViewCtrl;
        public PDFViewCtrl PDFViewCtrl
        {
            private get { return _PDFViewCtrl; }
            set
            {
                if (!string.IsNullOrWhiteSpace(_DocumentTag) && _PDFViewCtrl != null)
                {
                    _ViewModel.CleanUp();
                }

                _PDFViewCtrl = value;
                if (!string.IsNullOrWhiteSpace(_DocumentTag) && _PDFViewCtrl != null)
                {
                    SetPDFViewCtrl(_PDFViewCtrl);
                }
            }
        }
        private string _DocumentTag;
        public string DocumentTag
        {
            get { return _DocumentTag; }
            set
            {
                _DocumentTag = value;
                if (!string.IsNullOrWhiteSpace(_DocumentTag) && _PDFViewCtrl != null)
                {
                    SetPDFViewCtrl(_PDFViewCtrl);
                }
            }
        }

        public Color BlankPageDefaultColor
        {
            get
            {
                return _ViewModel.PageDefaultColor;
            }
            set
            {
                _ViewModel.PageDefaultColor = value;
            }
        }

        public new Brush Background
        {
            get { return BackgroundGrid.Background; }
            set { BackgroundGrid.Background = value; }
        }

        public ThumbnailViewer()
        {
            this.InitializeComponent();
            Init();
        }

        public ThumbnailViewer(PDFViewCtrl ctrl, string docTag)
        {
            this.InitializeComponent();
            Init();

            PDFViewCtrl = ctrl;
            DocumentTag = docTag;
        }

        void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            ControlClosed();
        }

        public ThumbnailViewer(PDFViewCtrl ctrl, string docTag, Color background)
        {
            this.InitializeComponent();
            Init();

            Background = new SolidColorBrush(background);
            PDFViewCtrl = ctrl;
            DocumentTag = docTag;
        }

        private void Init()
        {
            MainListView.SizeChanged += MainListView_SizeChanged;
        }

        ScrollViewer _LWScroller = null;
        EventHandler<object> _LWScrollChanged = null;
        void MainListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 600 && e.NewSize.Height > 600)
            {
                MainListView.ItemTemplate = Resources["ThumbnailTemplateLarge"] as DataTemplate;
            }
            else
            {
                MainListView.ItemTemplate = Resources["ThumbnailTemplateSmall"] as DataTemplate;
            }

            if (_LWScroller == null)
            {
                _LWScroller = UtilityFunctions.FindVisualChild<ScrollViewer>(MainListView);
            }
            if (_LWScroller != null)
            {
                if (_LWScrollChanged == null)
                {
                    _LWScrollChanged = new EventHandler<object>(LWScroller_LayoutUpdated);
                    _LWScroller.LayoutUpdated += _LWScrollChanged;
                }
                if (_ViewModel.CurrentListViewBase == MainListView)
                {
                    _ViewModel._CurrentScrollViewer = _LWScroller;
                    _ViewModel.UpdateSize(MainListView);
                }
            }
        }

        void LWScroller_LayoutUpdated(object sender, object e)
        {
            if (_ViewModel.CurrentListViewBase == MainListView)
            {
                _ViewModel.ScrollChanged(_LWScroller);
            }
        }

        private void SetPDFViewCtrl(PDFViewCtrl ctrl)
        {
            _ViewModel = new ViewModels.ThumbnailsViewViewModel(ctrl, _DocumentTag, this);
            this.DataContext = _ViewModel;
            MainListView.DataContext = _ViewModel;
            _ViewModel.CurrentListViewBase = MainListView;
        }

        public event pdftron.PDF.Tools.Controls.ControlBase.ControlClosedDelegate ControlClosed = delegate { };

        public void CloseControl()
        {
            if (_LWScrollChanged != null)
            {
                _LWScroller.LayoutUpdated -= _LWScrollChanged;
            }
            _ViewModel.CleanUp();
            ControlClosed();
        }
        
        /// <summary>
        /// Will perform a back operation if applicable.
        /// Use this when the back button is pressed to give this control a chance to handle it.
        /// </summary>
        /// <returns>true if the control could go back</returns>
        public bool GoBack()
        {
            return _ViewModel.GoBack();
        }
    }
}

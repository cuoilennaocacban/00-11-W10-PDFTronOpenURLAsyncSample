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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

using PDFDouble = pdftron.Common.DoubleRef;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Controls
{
    public sealed partial class AnnotationList : UserControl
    {
#region Item Width
        // We need to do this, since setting the items to stretch produces jumpy behaviour when a long text block is
        // loaded into the list. That is, during the phase of adding a document that would take more room than the width of the 
        // screen if the text didn't wrap, the list will make a small horizontal jump before settling back.
        // This lets us circumvent that by always setting the width manually.
        static DependencyProperty ListViewItemWidthProperty =
          DependencyProperty.Register("ListViewItemWidth", typeof(double),
          typeof(AnnotationList), new PropertyMetadata(10));

        public double ListViewItemWidth
        {
            get { return (double)(GetValue(ListViewItemWidthProperty)); }
            set { SetValue(ListViewItemWidthProperty, value); }
        }

#endregion


        ViewModels.AnnotationListViewModel _ViewModel;
        public AnnotationList()
        {
            this.InitializeComponent();
        }

        public AnnotationList(PDFViewCtrl ctrl)
        {
            this.InitializeComponent();
            PDFViewCtrl = ctrl;
        }

        public PDFViewCtrl PDFViewCtrl
        {
            get { return _ViewModel != null ? _ViewModel.PDFViewCtrl : null; }
            set
            {
                SetPDFViewCtrl(value);
            }
        }

        private void SetPDFViewCtrl(PDFViewCtrl ctrl)
        {
            if (ctrl != null)
            {
                _ViewModel = new ViewModels.AnnotationListViewModel(ctrl);
                SetItemWidth(this.ActualWidth);
                this.DataContext = _ViewModel;
                InitAnimations();
                this.SizeChanged += AnnotationList_SizeChanged;
            }
        }

        void AnnotationList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_ViewModel != null)
            {
                SetItemWidth(e.NewSize.Width);
            }
        }

        private void SetItemWidth(double width)
        {
            if (!double.IsNaN(width))
            {
                ListViewItemWidth = Math.Max(0, width - 10);
            }
        }

        public event EventHandler<ItemClickEventArgs> ItemClicked;

        private void AnnotationList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClicked != null)
            {
                ItemClicked(this, e);
            }
        }


        #region Animation
        private SolidColorBrush _FlashBrush;
        public SolidColorBrush FlashBrush
        {
            get { return _FlashBrush; }
            set
            {
                _FlashBrush = value;
                if (_ViewModel != null)
                {
                    _ViewModel.FlashBrush = value;
                }
            }
        }

        private void InitAnimations()
        {
            _ViewModel.FlashAnimation = FlashTheRectangle;
            if (FlashBrush != null)
            {
                _ViewModel.FlashBrush = FlashBrush;
            }
            else
            {
                _ViewModel.FlashBrush = this.Resources["PrimaryHighlightBrighBrush"] as SolidColorBrush;
            }
        }
        #endregion Animation

        /// <summary>
        /// Will go back if possible.
        /// </summary>
        /// <returns>Return true if it went back, false otherwise</returns>
        public bool GoBack()
        {
            if (_ViewModel != null)
            {
                return _ViewModel.GoBack();
            }
            return false;
        }
    }
}

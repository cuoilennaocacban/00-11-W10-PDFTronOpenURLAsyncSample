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
    /// The ThumbnailSlider uses the GetThumbAsync API to show thumbnails of the current page as the slider moves.
    /// </summary>
    public sealed partial class ThumbnailSlider : UserControl
    {
        private enum ThumbnailState
        {
            None, // No thumbnail
            Lingering, // The current thumbnail does not match the current page on the slider, but it has yet to be removed
            Correct, // The current thumbnail matches that of the slider's page number
        }

        private PDFViewCtrl _PDFViewCtrl;

        private int _CurrentPage = -1;
        private Popup _ThumbnailPopup;
        private Border _ThumbnailDisplayBorder;
        private Canvas _BlankPageCanvas;
        private TextBlock _PageNumberTextBlock;
        private int _CurrentThumbnailPageNumber = -1;

        private double _ScreenWidth;
        private double _ScreenHeight;
        private double _HorizontalCenter;
        private double _VerticalBottom;

        private double _LargestWidth;
        private double _LargestHeight;

        private DispatcherTimer _RemoveOldThumbTimer;
        private ThumbnailState _ThumbnailState;

        private bool _IsMoving = false;
        private DispatcherTimer _IsMovingTimer;

        private bool _IsRegistered;

        /// <summary>
        /// Give the ThumbSlider a new PDFViewCtrl.
        /// </summary>
        public PDFViewCtrl PDFViewCtrl
        {
            get { return _PDFViewCtrl; }
            set
            {
                if (_PDFViewCtrl != null)
                {
                    UnRegisterForEvents();
                }
                _PDFViewCtrl = value;
                if (_PDFViewCtrl != null)
                {
                    if (_PDFViewCtrl.GetDoc() != null)
                    {
                        HandleNewdoc();
                    }
                    RegisterForEvents();
                }
            }
        }

        /// <summary>
        /// Register the ThumbnailSlider for events from the PDFViewCtrl
        /// </summary>
        public void RegisterForEvents()
        {
            if (!_IsRegistered && PDFViewCtrl != null)
            {
                _IsRegistered = true;
                _PDFViewCtrl.OnSetDoc += PDFViewCtrl_OnSetDoc;
                _PDFViewCtrl.OnPageNumberChanged += PDFViewCtrl_OnPageNumberChanged;
                _PDFViewCtrl.OnThumbnailGenerated += PDFViewCtrl_OnThumbnailGenerated;
                ThumbSlider.Value =  (double)_PDFViewCtrl.GetCurrentPage();
                CreateBlankPageAndRequestThumb();
            }
        }

        /// <summary>
        /// UnRegister the ThumbnailSlider for events from the PDFViewCtrl. This is essential for the PDFViewCtrl to be garbage collected properly.
        /// </summary>
        public void UnRegisterForEvents()
        {
            if (_ThumbnailPopup != null)
            {
                _ThumbnailPopup.IsOpen = false;
            }
            if (_IsRegistered && PDFViewCtrl != null)
            {
                _IsRegistered = false;
                _PDFViewCtrl.OnSetDoc -= PDFViewCtrl_OnSetDoc;
                _PDFViewCtrl.OnPageNumberChanged -= PDFViewCtrl_OnPageNumberChanged;
                _PDFViewCtrl.OnThumbnailGenerated -= PDFViewCtrl_OnThumbnailGenerated;
                //_PDFViewCtrl.Cancel
            }
        }

        /// <summary>
        /// Creates a Thumbnail Slider that is not associated with a PDFViewCtrl.
        /// Use the PDFViewCtrl property to assoicate it with a PDFViewCtrl later.
        /// 
        /// Note: It is up to your to register and unregister this control from the PDFViewCtrl
        /// </summary>
        public ThumbnailSlider()
        {
            this.InitializeComponent();
            InitTimers();
            SetUpThumbDisplay();
        }

        /// <summary>
        /// Creates a new Thumbnail Slider associated with the PDFViewCtrl.
        /// 
        /// Note: It is up to your to register and unregister this control from the PDFViewCtrl
        /// </summary>
        /// <param name="ctrl"></param>
        public ThumbnailSlider(PDFViewCtrl ctrl)
        {
            this.InitializeComponent();
            InitTimers();
            SetUpThumbDisplay();
            this.PDFViewCtrl = ctrl;
        }

        private void InitTimers()
        {
            _RemoveOldThumbTimer = new DispatcherTimer();
            _RemoveOldThumbTimer.Interval = TimeSpan.FromMilliseconds(100);
            _RemoveOldThumbTimer.Tick += RemoveOldThumbTimer_Tick;

            _IsMovingTimer = new DispatcherTimer();
            _IsMovingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _IsMovingTimer.Tick += IsMovingTimer_Tick;
        }

        private void PDFViewCtrl_OnSetDoc()
        {
            HandleNewdoc();
        }


        private void HandleNewdoc()
        {
            _ThumbnailState = ThumbnailState.None;
            int pageCount = _PDFViewCtrl.GetPageCount();
            ThumbSlider.Minimum = 1;
            ThumbSlider.Maximum = pageCount;
            ThumbSlider.Value = _PDFViewCtrl.GetCurrentPage();
            _LargestWidth = 0;
            _LargestHeight = 0;
        }

        private void SetUpThumbDisplay()
        {
            // calculate width and height of screen in portrait mode.
            _ScreenHeight = Window.Current.Bounds.Height;
            _ScreenWidth = Window.Current.Bounds.Width;
            if (_ScreenWidth > _ScreenHeight)
            {
                double temp = _ScreenWidth;
                _ScreenWidth = _ScreenHeight;
                _ScreenHeight = temp;
            }

            _ThumbnailPopup = new Popup();

            _ThumbnailDisplayBorder = new Border();
            _ThumbnailDisplayBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 0, 0));
            _ThumbnailDisplayBorder.Width = (_ScreenWidth / 5) + 6;
            _ThumbnailDisplayBorder.Height = (_ScreenHeight / 5) + 30;
            _ThumbnailDisplayBorder.CornerRadius = new CornerRadius(3);
            _ThumbnailPopup.Child = _ThumbnailDisplayBorder;

            Grid thumbnailDisplayGrid = new Grid();
            thumbnailDisplayGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            thumbnailDisplayGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            thumbnailDisplayGrid.Margin = new Thickness(3);
            _ThumbnailDisplayBorder.Child = thumbnailDisplayGrid;

            _PageNumberTextBlock = new TextBlock();
            _PageNumberTextBlock.Text = "5";
            _PageNumberTextBlock.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            _PageNumberTextBlock.FontSize = 16;
            thumbnailDisplayGrid.Children.Add(_PageNumberTextBlock);

            _BlankPageCanvas = new Canvas();
            _BlankPageCanvas.SetValue(Grid.RowProperty, 1);
            _BlankPageCanvas.Background = new SolidColorBrush(Windows.UI.Colors.White);
            _BlankPageCanvas.HorizontalAlignment = HorizontalAlignment.Center;
            _BlankPageCanvas.VerticalAlignment = VerticalAlignment.Center;
            thumbnailDisplayGrid.Children.Add(_BlankPageCanvas);

            // This is always handled by the slider, so we need to register this way.
            ThumbSlider.AddHandler(PointerPressedEvent, new PointerEventHandler(ThumbSlider_PointerPressed), true);
            ThumbSlider.PointerReleased += ThumbSlider_PointerReleased;
            ThumbSlider.PointerCanceled += ThumbSlider_PointerReleased;

        }

        void ThumbSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            this._ThumbnailPopup.IsOpen = false;

            int pgNum = (int)ThumbSlider.Value;
            PDFViewCtrl.SetCurrentPage(pgNum);           
        }

        void ThumbSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            this._ThumbnailPopup.IsOpen = true;
            if (_ThumbnailState == ThumbnailState.None)
            {
                CreateBlankPageAndRequestThumb();
            }
            Windows.Foundation.Rect rect = GetElementRect(this);
            _HorizontalCenter = (rect.Left + rect.Right) / 2;
            _VerticalBottom = rect.Top - 15;
        }



        void RemoveOldThumbTimer_Tick(object sender, object e)
        {
            _RemoveOldThumbTimer.Stop();

            if (_ThumbnailState == ThumbnailState.Lingering)
            {
                _CurrentThumbnailPageNumber = -1;
                _BlankPageCanvas.Children.Clear();
                _ThumbnailState = ThumbnailState.None;
            }
        }


        void IsMovingTimer_Tick(object sender, object e)
        {
            _IsMovingTimer.Stop();
            _IsMoving = false;
        }


        void PDFViewCtrl_OnThumbnailGenerated(int pageNumber, byte[] buff, int w, int h)
        {
            bool addThumb = true;

            // If we have no thumb, or the incorrect thumb, we want to replace it with this new thumb.
            if (_ThumbnailState == ThumbnailState.None || _ThumbnailState == ThumbnailState.Lingering)
            {
                pdftron.PDF.Rect size = GetPageDimensions(pageNumber);

                _BlankPageCanvas.Width = size.Width();
                _BlankPageCanvas.Height = size.Height();

                if (!_IsMoving && pageNumber != _CurrentPage)
                {
                    addThumb = false;
                }
            }
            else if (_ThumbnailState == ThumbnailState.Correct)
            {
                addThumb = false;
            }

            if (addThumb)
            {
                Windows.UI.Xaml.Controls.Image img = new Windows.UI.Xaml.Controls.Image();

                // copy buffer data into WriteableBitmap
                Windows.UI.Xaml.Media.Imaging.WriteableBitmap wb = new Windows.UI.Xaml.Media.Imaging.WriteableBitmap(w, h);
                Stream pixelStream = wb.PixelBuffer.AsStream();
                pixelStream.Seek(0, SeekOrigin.Begin);
                pixelStream.Write(buff, 0, buff.Length);

                img.Source = wb;
                img.Width = _BlankPageCanvas.Width;
                img.Height = _BlankPageCanvas.Height;
                _BlankPageCanvas.Children.Clear();
                _BlankPageCanvas.Children.Add(img);
                _CurrentThumbnailPageNumber = pageNumber;
                _RemoveOldThumbTimer.Stop();

                // If this is not the correct thumbnail, we want to start the timer.
                if (pageNumber != _CurrentPage)
                {
                    _RemoveOldThumbTimer.Start();
                    _ThumbnailState = ThumbnailState.Lingering;
                }
                else
                {
                    _ThumbnailState = ThumbnailState.Correct;
                }
            }
        }


        #region Manipulation

        
        void PDFViewCtrl_OnPageNumberChanged(int current_page, int num_pages)
        {
            ThumbSlider.Value = current_page;
        }

        private void ThumbSlider_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _ThumbnailPopup.IsOpen = false;
            _PDFViewCtrl.CancelAllThumbRequests();  

            int pgNum = (int)ThumbSlider.Value;
            PDFViewCtrl.SetCurrentPage(pgNum);           
        }

        private void ThumbSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _IsMoving = true;
            _IsMovingTimer.Stop();
            _IsMovingTimer.Start();
            if (_ThumbnailPopup.IsOpen)
            {
                CreateBlankPageAndRequestThumb();
            }  
        }

        private void CreateBlankPageAndRequestThumb()
        {
            PDFDoc pdfDoc = _PDFViewCtrl.GetDoc();
            if (pdfDoc == null)
            {
                return;
            }

            int pageNumber = (int)ThumbSlider.Value;
            _CurrentPage = pageNumber;
            _PageNumberTextBlock.Text = "" + pageNumber;

            bool newBlankPage = false;
            bool requestThumbnail = true;
            if (_ThumbnailState == ThumbnailState.None)
            {
                newBlankPage = true;
                requestThumbnail = true;
            }
            else
            {
                if (_CurrentPage == _CurrentThumbnailPageNumber)
                {
                    requestThumbnail = false;
                    _ThumbnailState = ThumbnailState.Correct;
                    _RemoveOldThumbTimer.Stop();
                }
                else
                {
                    _ThumbnailState = ThumbnailState.Lingering;
                    if (!_RemoveOldThumbTimer.IsEnabled)
                    {
                        _RemoveOldThumbTimer.Start();
                    }
                }
            }

            if (requestThumbnail)
            {
                _PDFViewCtrl.GetThumbAsync(_CurrentPage);
            }

            if (newBlankPage)
            {
                pdftron.PDF.Rect size = GetPageDimensions(_CurrentPage);

                _BlankPageCanvas.Width = size.Width();
                _BlankPageCanvas.Height = size.Height();
            }

            PositionPopup();
        }

        #endregion Manipulation



        #region Utilities

        private pdftron.PDF.Rect GetPageDimensions(int pageNumber)
        {            
            // These are the max dimensions we allow.
            double thumbWidth = _ScreenWidth / 5;
            double thumbHeight = _ScreenHeight / 5;

            // We now want to scale the thumb so that it takes up as much room as possible without
            // extending past the max dimensions above, and preserves apsect ratio.
            double pageWidth = 0; // thumbWidth; // default in case we can't get it.
            double pageHeight = 0; // thumbHeight;

            try
            {
                _PDFViewCtrl.DocLockRead();
                PDFDoc pdfDoc = _PDFViewCtrl.GetDoc();
                if (pdfDoc != null)
                {
                    pdftron.PDF.Page page = pdfDoc.GetPage(pageNumber);
                    Rect pageRect = page.GetCropBox();
                    pageWidth = pageRect.Width();
                    pageHeight = pageRect.Height();

                    PageRotate rotation = page.GetRotation();
                    if (rotation == PageRotate.e_90 || rotation == PageRotate.e_270)
                    {
                        double tmp = pageWidth;
                        pageWidth = pageHeight;
                        pageHeight = tmp;
                    }

                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception: {0}", e);
            }
            finally
            {
                _PDFViewCtrl.DocUnlockRead();
            }

            double scale = Math.Min(thumbWidth / pageWidth, thumbHeight / pageHeight);
            thumbWidth = scale * pageWidth;
            thumbHeight = scale * pageHeight;

            if (thumbWidth > _LargestWidth || thumbHeight > _LargestHeight)
            {
                _LargestWidth = Math.Max(thumbWidth, _LargestWidth);
                _LargestHeight = Math.Max(thumbHeight, _LargestHeight);
                PositionPopup();
            }

            return new pdftron.PDF.Rect(0, 0, thumbWidth, thumbHeight);
        }


        private Windows.Foundation.Rect GetElementRect(FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            Windows.Foundation.Rect rect = elementtransform.TransformBounds(new Windows.Foundation.Rect(new Windows.Foundation.Point(0, 0), new Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

        private void PositionPopup()
        {
            _ThumbnailDisplayBorder.Width = _LargestWidth + 6;
            _ThumbnailDisplayBorder.Height = _LargestHeight + 30;
            _ThumbnailPopup.HorizontalOffset = _HorizontalCenter - (_LargestWidth / 2) - 3;   //(_CurrentThumbWidth + 6) / 2;
            _ThumbnailPopup.VerticalOffset = _VerticalBottom - _LargestHeight - 30; //_CurrentThumbHeight - 20;

            //_ThumbnailPopup.HorizontalOffset = _HorizontalCenter - (_ScreenWidth / 10) - 3;   //(_CurrentThumbWidth + 6) / 2;
            //_ThumbnailPopup.VerticalOffset = _VerticalBottom - (_ScreenHeight / 5) - 30; //_CurrentThumbHeight - 20;
        }

        #endregion Utilities



    }
}

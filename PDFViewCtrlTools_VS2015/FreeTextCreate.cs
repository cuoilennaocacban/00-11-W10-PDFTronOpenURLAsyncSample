using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using Windows.UI.ViewManagement;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;

using pdftron.PDF;
using pdftron.SDF;
using PDFDouble = pdftron.Common.DoubleRef;
using PDFRect = pdftron.PDF.Rect;
using pdftron.PDF.Tools.Utilities;


namespace pdftron.PDF.Tools
{
    class FreeTextCreate : Tool
    {
        protected int mDownPageNumber = -1;
        protected PDFRect mPageCropOnClient;
        protected PDFRect mTextRect;
        protected Canvas mViewerCanvas;
        protected RichEditBox mTextBox = null;
        protected double mTextBoxWidth = 0, mTextBoxHeight = 0;
        protected double mFontSize = 11;
        protected double mZoomFactor;
        protected SolidColorBrush mTextBrush;
        protected double mOpacity = 1;

        protected bool mFixedSize = false;
        protected int mCreationMode = 0; // used to keep track of what state the click - drag - release cycle we have.
        protected int mPointerID = -1;

        protected double mTranslateOffset = 0;
        protected Popup mTextPopup;

        protected Rectangle mTextBoundaryRectangle;

        // keyboard
        protected KeyboardHelper mKeyboardHelper;
        protected bool mIsKeyboardOpen = false;

        protected bool mSaveWhenClosing = false;
        protected bool mTextHasBeenSaved = false;

        protected PointerEventHandler mMouseWheelHandler = null;

        public FreeTextCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_text_annot_create;
            mToolMode = ToolType.e_text_annot_create;
            SettingsColor colors = Settings.GetShapeColor(mToolMode, false);
            mTextBrush = new SolidColorBrush(Color.FromArgb(255, colors.R, colors.G, colors.B));
            mFontSize = Settings.GetFontSize(mToolMode);
            mOpacity = Settings.GetOpacity(this.ToolMode);

            mKeyboardHelper = new KeyboardHelper();
            mKeyboardHelper.SubscribeToKeyboard(true);
        }

        internal override void OnClose()
        {
            if (mViewerCanvas != null)
            {
                mViewerCanvas.Children.Clear();
            }
            if (mSaveWhenClosing)
            {
                SaveText();
            }
            if (mTextPopup != null)
            {
                mTextPopup.IsOpen = false;
                mTextPopup = null;
            }
            if (mTextBoundaryRectangle != null && this.Children.Contains(mTextBoundaryRectangle))
            {
                this.Children.Remove(mTextBoundaryRectangle);
            }
            if (mMouseWheelHandler != null)
            {
                this.PointerWheelChanged -= mMouseWheelHandler;
            }
            EnableScrolling();
            DelayedUnsubscribe();
        }


        // This delays the unsubscribing of the keyboard, so that the UI will have time to invoke the callbacks it needs.
        private async void DelayedUnsubscribe()
        {
            await Task.Delay(50);
            if (mTextBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mTextBox);
                mKeyboardHelper.SetHidingHandler(null);
            }
            mKeyboardHelper.SubscribeToKeyboard(false);
            TranslateTransform tt = mPDFView.RenderTransform as TranslateTransform;
            if (tt != null)
            {
                Storyboard sb = KeyboardTranslate(mPDFView, tt.Y, 0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
            }
        }

        internal override bool OnPageNumberChanged(int current_page, int num_pages)
        {
            //EndCurrentTool(ToolType.e_pan);
            base.OnPageNumberChanged(current_page, num_pages);
            return false;
        }


        internal override bool OnSize()
        {
            EndCurrentTool(ToolType.e_pan);
            return false;
        }


        public void SetTargetPoint(UIPoint point)
        {
            //UIPoint screenPoint = e.GetPosition(mPDFView);
            mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(point.X, point.Y);
            if (mDownPageNumber < 1) // out of bounds
            {
                EndCurrentTool(ToolType.e_pan);
                return;
            }
            mCreationMode = 3; // successfully created textbox

            mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);
            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            UIPoint canvasPoint = new UIPoint(point.X + mPDFView.GetAnnotationCanvasHorizontalOffset(), point.Y + mPDFView.GetAnnotationCanvasVerticalOffset());//e.GetPosition(mViewerCanvas);
            mZoomFactor = mPDFView.GetZoom();

            // Offset the textbox so that the text appears at the click
            double leftPoint = Math.Max(0, canvasPoint.X - 3 - (3 * mZoomFactor));
            double topPoint = Math.Max(0, canvasPoint.Y - 2 - (6 * mZoomFactor));

            // Text box is allowed to grow until it hits edge of page or edge of view ctrl
            double rightPoint = Math.Min(mPageCropOnClient.x2, mPDFView.GetAnnotationCanvasHorizontalOffset() + mPDFView.ActualWidth);
            double bottomPoint = Math.Min(mPageCropOnClient.y2, mPDFView.GetAnnotationCanvasVerticalOffset() + mPDFView.ActualHeight);

            mTextRect = new Rect(leftPoint, topPoint, rightPoint, bottomPoint);

            SetUpTextBox();
        }

        internal override bool PointerPressedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);

            if (mTextBoundaryRectangle != null && this.Children.Contains(mTextBoundaryRectangle))
            {
                this.Children.Remove(mTextBoundaryRectangle);
            }

            if (mToolManager.ContactPoints.Count > 1)
            {
                if (mTextBox != null)
                {
                    return false;
                    //                    EndCurrentTool(ToolType.e_pan);
                }
                mPointerID = -1;
                if (mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
                {
                    mViewerCanvas.Children.Remove(this);
                }
                mCreationMode = 0;
                mToolManager.EnableScrollByManipulation = true;
                return false; // ignore additional pointer presses
            }

            if (mTextBox != null)
            {
                DisableScrolling();
                return false;
            }

            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                return false; // only left mouse button
            }
            PDFDoc doc = mPDFView.GetDoc(); // just a safety check
            if (doc == null)
            {
                EndCurrentTool(ToolType.e_pan);
                return false;
            }
            UIPoint screenPoint = e.GetCurrentPoint(mPDFView).Position;
            mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);
            if (mDownPageNumber < 1)
            {
                DisableScrolling();
                mPointerID = -1;
                mToolManager.EnableOneFingerScroll = true;
                mToolManager.EnableScrollByManipulation = true;
                return false;
            }


            if (mCreationMode == 0) // all we want to handle.
            {
                mPointerID = (int)e.Pointer.PointerId;
                DisableScrolling();
                mCreationMode = 1;
                mViewerCanvas = mPDFView.GetAnnotationCanvas();
                mViewerCanvas.CapturePointer(e.Pointer);
                
                UIPoint canvasPoint = e.GetCurrentPoint(mViewerCanvas).Position;
                if (mDownPageNumber < 1) // out of bounds
                {
                    EndCurrentTool(ToolType.e_pan);
                    return false;
                }
                mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);

                mTextRect = new PDFRect(canvasPoint.X, canvasPoint.Y, canvasPoint.X, canvasPoint.Y);

                return false;
            }
            else if (mCreationMode == -1) // an error occurred, we're switching back to pan. Happen if neither PointerRelease or Tapped create the textbox
            {
                EndCurrentTool(ToolType.e_pan);
            }
            return false;
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID >= 0)
            {
                UIPoint cn_MovePoint = e.GetCurrentPoint(mViewerCanvas).Position;
                mTextRect.x2 = cn_MovePoint.X;
                mTextRect.y2 = cn_MovePoint.Y;
                if (mTextBoundaryRectangle != null)
                {
                    mTextBoundaryRectangle.SetValue(Canvas.LeftProperty, Math.Min(mTextRect.x1, mTextRect.x2));
                    mTextBoundaryRectangle.SetValue(Canvas.TopProperty, Math.Min(mTextRect.y1, mTextRect.y2));
                    mTextBoundaryRectangle.Width = mTextRect.Width();
                    mTextBoundaryRectangle.Height = mTextRect.Height();
                }
            }

            return base.PointerMovedHandler(sender, e);
        }

        internal override bool ManipulationStartedEventHandler(Object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
        {
            base.ManipulationStartedEventHandler(sender, e);
            if (mCreationMode == 1) 
            {
                mCreationMode = 2; // this means we've dragged far enough
                mTextBoundaryRectangle = new Rectangle();
                this.Children.Add(mTextBoundaryRectangle);
                mTextBoundaryRectangle.SetValue(Canvas.LeftProperty, Math.Min(mTextRect.x1, mTextRect.x2));
                mTextBoundaryRectangle.SetValue(Canvas.TopProperty, Math.Min(mTextRect.y1, mTextRect.y2));
                mTextBoundaryRectangle.Width = mTextRect.Width();
                mTextBoundaryRectangle.Height = mTextRect.Height();
                mTextBoundaryRectangle.Stroke = new SolidColorBrush(Colors.Black);
                mTextBoundaryRectangle.StrokeThickness = 2;
                mTextBoundaryRectangle.StrokeDashArray = new DoubleCollection() { 2.0, 2.0 };

                if (!mViewerCanvas.Children.Contains(this))
                {
                    mViewerCanvas.Children.Add(this);
                }

                return true;
            }
            return false;
        }

        internal override bool PointerReleasedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Remove contact from dictionary.
            RemovePointer(e.Pointer);
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                EnableScrolling();
                if (mTextBox != null)
                {
                    EndCurrentTool(ToolType.e_pan);
                }
                return false;
            }
            mPointerID = -1;
            if (mCreationMode == 2) // we've dragged far enough to get a box
            {
                mCreationMode = 3; // successfully created textbox
                UIPoint canvasPoint = e.GetCurrentPoint(mViewerCanvas).Position;
                mTextRect.x2 = canvasPoint.X;
                mTextRect.y2 = canvasPoint.Y;
                mTextRect.Normalize();

                if (mTextBoundaryRectangle != null && this.Children.Contains(mTextBoundaryRectangle))
                {
                    this.Children.Remove(mTextBoundaryRectangle);
                }

                // check bounds and expand a little. These numbers place the text roughly where you'd expect it.
                //mTextRect.x1 = Math.Max(mTextRect.x1 - 3 - (3 * mZoomFactor), mPageCropOnClient.x1);
                //mTextRect.y1 = Math.Max(mTextRect.y1 - 2 - (6 * mZoomFactor), mPageCropOnClient.y1);
                //mTextRect.x2 = Math.Min(mTextRect.x2 + 3 + (3 * mZoomFactor), mPageCropOnClient.x2);
                //mTextRect.y2 = Math.Min(mTextRect.y2 + 2 + (6 * mZoomFactor), mPageCropOnClient.y2);
                mFixedSize = true;
                SetUpTextBox();
            }
            else
            {
                mCreationMode = -1; // we failed to create a textbox
                EnableScrolling();
            }
            mViewerCanvas.ReleasePointerCaptures();
            return true;
        }

        // in case the user goes out of bounds or something
        internal override bool PointerCaptureLostHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (mToolManager.ContactPoints.ContainsKey(e.Pointer.PointerId)) // We've lost capture of the active pointer
            {
                if (mCreationMode == 1 || mCreationMode == 2)
                {
                    EndCurrentTool(ToolType.e_pan);
                }
                mPointerID = -1;
            }
            RemovePointer(e.Pointer);

            return false;
        }

        internal override bool PointerCanceledHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            EndCurrentTool(ToolType.e_pan);
            return false;
        }


        /// <summary>
        /// Creates a Free Text Area that is capable of growing as Text is added
        /// The text will stay within the visible bounds of the PDFViewCtrl
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        /// 
        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool)
            {
                return false;
            }
            if (mTextBox == null)
            {
                UIPoint screenPoint = e.GetPosition(mPDFView);
                mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);
                if (mDownPageNumber < 1) // out of bounds
                {
                    EndCurrentTool(ToolType.e_pan);
                    return false;
                }

                Pan pan = new Pan(mPDFView, mToolManager);
                pan.mIsInHelperMode = true;
                pan.TappedHandler(sender, e);
                Tool t = pan;
                if (pan.NextToolMode == ToolType.e_annot_edit && pan.CurrentAnnot != null && pan.CurrentAnnot.GetAnnotType() == AnnotType.e_FreeText)
                {
                    mToolManager.ToolModeAfterEditing = mToolMode;
                    mNextToolMode = pan.NextToolMode;
                    mAnnot = pan.CurrentAnnot;
                    mAnnotPageNum = pan.CurrentAnnotPageNumber;
                    mAnnotBBox = pan.CurrentAnnotBBox;
                    return false;
                }

                mCreationMode = 3; // successfully created textbox

                mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);
                mViewerCanvas = mPDFView.GetAnnotationCanvas();
                UIPoint canvasPoint = e.GetPosition(mViewerCanvas);
                mZoomFactor =  mPDFView.GetZoom();

                // Offset the textbox so that the text appears at the click
                double leftPoint = Math.Max(0, canvasPoint.X - 3 - (3 * mZoomFactor)); 
                double topPoint = Math.Max(0, canvasPoint.Y - 2 - (6 * mZoomFactor));

                // Text box is allowed to grow until it hits edge of page or edge of view ctrl
                double rightPoint = Math.Min(mPageCropOnClient.x2, mPDFView.GetAnnotationCanvasHorizontalOffset() + mPDFView.ActualWidth);
                double bottomPoint = Math.Min(mPageCropOnClient.y2, mPDFView.GetAnnotationCanvasVerticalOffset() + mPDFView.ActualHeight);

                mTextRect = new Rect(leftPoint, topPoint, rightPoint, bottomPoint);

                SetUpTextBox();
            }
            else
            {
                // We've tapped outside of the box.
                EndCurrentTool(ToolType.e_pan);
                mViewerCanvas.Children.Remove(this);
            }
            return true;
        }

        internal override bool KeyDownAction(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (mPointerID >= 0)
            {
                return false;
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                mSaveWhenClosing = false;
                if (mViewerCanvas.Children.Contains(this))
                {
                    mViewerCanvas.Children.Remove(this);
                    EndCurrentTool(ToolType.e_pan);
                }
            }
            if (mTextBox != null)
            {
                return false;
            }

            base.KeyDownAction(sender, e);
            mJustSwitchedFromAnotherTool = false;
            return true;
        }

        internal override bool PointerWheelChangedHandler(Object sender, PointerRoutedEventArgs e)
        {
            if (mTextPopup != null)
            {
                e.Handled = true;
            }
            return false;
        }

        protected void SetUpTextBox()
        {
            if (mToolManager.IsOnPhone)
            {
                SetUpTextBoxForPhone();
            }
            else
            {
                SetUpTextBoxForWindows();
            }
        }

        /// <summary>
        /// Positions the text box according to where the user tapped
        /// If the user clicks and drags, the text box is of fixed size.
        /// If the user taps, it will grow to the bounds of the page or the screen
        /// </summary>
        protected void SetUpTextBoxForWindows()
        {
            // Get settings again, in case they've been changed from the toolbar.
            SettingsColor colors = Settings.GetShapeColor(mToolMode, false);
            mTextBrush = new SolidColorBrush(Color.FromArgb(255, colors.R, colors.G, colors.B));
            mFontSize = Settings.GetFontSize(mToolMode);
            mOpacity = Settings.GetOpacity(this.ToolMode);

            if (!mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Add(this);
            }

            mTextBox = new RichEditBox();
            mTextBox.AcceptsReturn = true;
            mTextBox.MaxWidth = mTextRect.x2 - mTextRect.x1;
            mTextBox.MaxHeight = mTextRect.y2 - mTextRect.y1;
            mTextBox.AddHandler(UIElement.TappedEvent, new Windows.UI.Xaml.Input.TappedEventHandler(mTextBox_Tapped), true);
            mTextBox.KeyDown += FreeTextPopupKeyDown;

            SettingsColor color = Settings.GetShapeColor(mToolMode, false);
            mTextBrush = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, color.R, color.G, color.B));
            mTextBox.Foreground = mTextBrush;

            Windows.UI.Xaml.Style style = new Windows.UI.Xaml.Style { TargetType = typeof(RichEditBox) };
            style.Setters.Add(new Windows.UI.Xaml.Setter(RichEditBox.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
            style.Setters.Add(new Windows.UI.Xaml.Setter(RichEditBox.ForegroundProperty, new SolidColorBrush(Colors.Red)));
            if (mFixedSize) // display a border if we're fixed size
            {
                style.Setters.Add(new Windows.UI.Xaml.Setter(RichEditBox.BorderBrushProperty, new SolidColorBrush(Colors.Black)));
            }
            else
            {
                style.Setters.Add(new Windows.UI.Xaml.Setter(RichEditBox.BorderBrushProperty, new SolidColorBrush(Colors.Transparent)));
            }

            mTextBox.Style = style;

            mTextBox.FontSize = mFontSize * mPDFView.GetZoom();
            mTextBox.Document.SetText(Windows.UI.Text.TextSetOptions.ApplyRtfDocumentDefaults, "");

            mTextBoxWidth = mTextBox.Width = mTextBox.MaxWidth; // this won't change
            mTextBoxHeight = mTextBox.Height = mTextBox.MaxHeight;
            mTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap;

            mTextBox.UpdateLayout();
            mTextBox.Loaded += mTextBox_Loaded;
            mTextBox.LostFocus += mTextBox_LostFocus;

            Windows.Foundation.Rect pdfViewRect = GetElementRect(mPDFView);

            mTextPopup = new Popup();
            mTextPopup.Height = mTextRect.x2 - mTextRect.x1 + 2 + (mFontSize * 0.2);
            mTextPopup.Height = mTextRect.y2 - mTextRect.y1 + (mFontSize * 0.5);
            mTextPopup.HorizontalOffset = mTextRect.x1 + pdfViewRect.Left + mPDFView.GetAnnotationCanvasHorizontalOffset();
            mTextPopup.VerticalOffset = mTextRect.y1 - -pdfViewRect.Top - mPDFView.GetAnnotationCanvasVerticalOffset();
            if (!mFixedSize)
            {
                mTextBox.Margin = new Thickness(mFontSize * 0, mFontSize * 0.5, 0, 0); // this works fairly well on different sizes
            }

            mTextPopup.Child = mTextBox;
            mTextPopup.IsOpen = true;

            mTextBox.Opacity = mOpacity;

            this.Background = new SolidColorBrush(Color.FromArgb(150, 0, 50, 0));
            this.Height = mViewerCanvas.Height;
            this.Width = mViewerCanvas.Width;
            this.Opacity = 0;
            mMouseWheelHandler = new PointerEventHandler(FreeTextCreate_PointerWheelChanged);
            this.PointerWheelChanged += mMouseWheelHandler;

            mSaveWhenClosing = true;

            mKeyboardHelper.AddShowingHandler(mTextBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(InputPaneHiding));
        }

        void FreeTextCreate_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        void mTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string str = "";
            mTextBox.Document.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out str);
            
            if (string.IsNullOrWhiteSpace(str))
            {
                EndCurrentTool(ToolType.e_pan);
            }
        }

        private void FreeTextPopupKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)

        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                mSaveWhenClosing = false;
                if (mReturnToPanModeWhenFinished)
                {
                    mToolManager.CreateTool(ToolType.e_pan, this);
                }
                else
                {
                    mToolManager.CreateTool(ToolType.e_text_annot_create, this, true);
                }
            }
        }

        void mTextBox_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // only applies to the variable size text
            if (mFixedSize)
            {
                return;
            }

            // get the text
            Windows.UI.Text.ITextDocument itd = mTextBox.Document;
            string str = "";
            itd.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out str);

            int i;
            int lastIndex = 0;
            int crlfs = 0; // The Range parameters count the \r\n combo as only 1, so we need to count how many newlines we've had so far.
            int hit = 0; // stores the hit result, not sure what it stands for...
            double maxX = 0, maxY = 0; // the rightmost and bottommost character positions

            Windows.UI.Text.ITextRange itr;
            UIRect outrect = new UIRect();

            // iterate through the text, looking for whole words
            for (i = 0; i < str.Length; i++)
            {
                if (str[i] == '\n' || str[i] == ' ' || str[i] == '\r' || str[i] == '\t')
                {
                    if (i - lastIndex > 1)
                    {
                        itr = itd.GetRange(lastIndex - crlfs, i - crlfs); // subtract the number of carriage return - line feeds we've had.
                        itr.GetRect(Windows.UI.Text.PointOptions.None, out outrect, out hit);
                        if (outrect.Right > maxX)
                        {
                            maxX = outrect.Right;
                        }
                        if (outrect.Bottom > maxY)
                        {
                            maxY = outrect.Bottom;
                        }
                    }
                    if (str[i] == '\r')
                    {
                        crlfs++; // count carriage returns
                    }
                    lastIndex = i;
                }
            }
            // get the last chunk of text, since it might end without a whitespace character
            if (i - lastIndex > 1)
            {
                itr = itd.GetRange(lastIndex - crlfs, i - crlfs);
                itr.GetRect(Windows.UI.Text.PointOptions.AllowOffClient, out outrect, out hit);
                if (outrect.Right > maxX)
                {
                    maxX = outrect.Right;
                }
                if (outrect.Bottom > maxY)
                {
                    maxY = outrect.Bottom;
                }
            }

            // Check if we're within the bounding box
            UIPoint tapPoint = e.GetPosition(mTextBox);
            if (tapPoint.X > maxX || tapPoint.Y > maxY)
            {
                if (mReturnToPanModeWhenFinished)
                {
                    mToolManager.CreateTool(ToolType.e_pan, this, false); 
                }
                else
                {
                    mToolManager.CreateTool(mToolMode, this, true);
                }
            }
        }

        /// <summary>
        /// Lets the RichEditBox grow until it becomes the size of the canvas.
        /// </summary>
        void mTextBox_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            if (mTextBoxWidth > 0 || mTextBoxHeight > 0)
            {
                if (mTextBox.ActualWidth > mTextBox.MaxWidth - 5) // we've hit the width of the allowed space
                {
                    mTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap;
                }
            }
            
            mTextBoxWidth = mTextBox.ActualWidth;
            mTextBoxHeight = mTextBox.ActualHeight;
        }

        /// <summary>
        /// Needed to set focus on the RichEditBox once it's loaded.
        /// </summary>
        void mTextBox_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // This somehow refreshes the appearance of the text position indicator sometimes
            // Seems to stop working once you hit very large fonts.
            //mTextBox.Text = "a";
            //mTextBox.Text = "";
            mTextBox.Focus(Windows.UI.Xaml.FocusState.Pointer);
        }


        private NoteDialogPhone _NoteDialog = null;
        protected void SetUpTextBoxForPhone()
        {
            // Get settings again, in case they've been changed from the toolbar.
            SettingsColor colors = Settings.GetShapeColor(mToolMode, false);
            mTextBrush = new SolidColorBrush(Color.FromArgb(255, colors.R, colors.G, colors.B));
            mFontSize = Settings.GetFontSize(mToolMode);
            mOpacity = Settings.GetOpacity(this.ToolMode);

            _NoteDialog = NoteDialogPhone.CreateAsTextBoxHelper(mPDFView, mToolManager, mDownPageNumber);
            _NoteDialog.FocusWhenOpened = true;
            _NoteDialog.NoteClosed += NoteDialog_NoteClosed;
        }

        private void NoteDialog_NoteClosed(bool deleted)
        {
            if (!deleted)
            {
                UpdateDoc(_NoteDialog.AnnotText);
            }
            CreateNewTool(ToolType.e_pan);
        }


        /// <summary>
        /// Update the document with the new text
        /// </summary>
        protected void UpdateDoc(string text = null)
        {
            if (mTextHasBeenSaved)
            {
                return;
            }
            mTextHasBeenSaved = true;
            mPDFView.DocLock(true);
            double xPos = mTextRect.x1 - mPDFView.GetAnnotationCanvasHorizontalOffset();
            double yPos = mTextRect.y1 - mPDFView.GetAnnotationCanvasVerticalOffset();

            double xOffset = 0;
            double yOffset = 0;
            // do some number magic to make it appear where the textbox is.
            if (!mFixedSize)
            {
                if (mFontSize <= 16)
                {
                    xOffset = 10;
                    yOffset = 10;
                }
                else
                {
                    xOffset = 5 + ((24 - mFontSize) * 0.6);
                    yOffset = mFontSize - 4;
                }
            }
            xPos += xOffset;
            yPos += yOffset;


            PDFDouble x1 = new PDFDouble(mTextRect.x1 - mPDFView.GetAnnotationCanvasHorizontalOffset());
            PDFDouble y1 = new PDFDouble(mTextRect.y1 - mPDFView.GetAnnotationCanvasVerticalOffset());
            PDFDouble x2 = new PDFDouble(mTextRect.x1 + mTextBoxWidth - mPDFView.GetAnnotationCanvasHorizontalOffset());
            PDFDouble y2 = new PDFDouble(mTextRect.y1 + mTextBoxHeight - mPDFView.GetAnnotationCanvasVerticalOffset());
            
            // we need to give the text box a minimum size, to make sure that at least some text fits in it.
            double marg = 50;
            if (x2.Value - x1.Value < marg)
            {
                x2.Value = x1.Value + marg;
            }
            if (y2.Value - y1.Value < marg)
            {
                y2.Value = y1.Value + marg;
            }


            mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);
            mPDFView.ConvScreenPtToPagePt(x2, y2, mDownPageNumber);

            try
            {
                pdftron.PDF.Rect rect;
                PageRotate pr = mPDFView.GetDoc().GetPage(mDownPageNumber).GetRotation();
                double xDist, yDist;
                if (pr == PageRotate.e_90 || pr == PageRotate.e_270)
                {
                    xDist = Math.Abs(y1.Value - y2.Value);
                    yDist = Math.Abs(x1.Value - x2.Value);
                }
                else
                {
                    xDist = Math.Abs(x1.Value - x2.Value);
                    yDist = Math.Abs(y1.Value - y2.Value);
                }
                rect = new pdftron.PDF.Rect(x1.Value, y1.Value, x1.Value + xDist, y1.Value + yDist);

                pdftron.PDF.Annots.FreeText textAnnot = pdftron.PDF.Annots.FreeText.Create(mPDFView.GetDoc().GetSDFDoc(), rect);

                // set text color
                double red = mTextBrush.Color.R / 255.0;
                double green = mTextBrush.Color.G / 255.0;
                double blue = mTextBrush.Color.B / 255.0;
                ColorPt color = new ColorPt(red, green, blue);
                textAnnot.SetTextColor(color, 3);

                SettingsColor fillColor = Settings.GetShapeColor(mToolMode, true);

                // Set background color if necessary
                if (fillColor.Use)
                {
                    color = new ColorPt(fillColor.R / 255.0, fillColor.G / 255.0, fillColor.B / 255.0);
                    textAnnot.SetColor(color, 3);
                }

                // set appearance and contents
                textAnnot.SetOpacity(mOpacity);
                textAnnot.SetFontSize(mFontSize);
                string outString = "";
                if (text != null)
                {
                    outString = text;
                }
                else
                {
                    mTextBox.Document.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out outString);
                }
                textAnnot.SetContents(outString);
                AnnotBorderStyle bs = textAnnot.GetBorderStyle();
                bs.width = 0;
                textAnnot.SetBorderStyle(bs);

                textAnnot.RefreshAppearance(); // else we can't get content stream

                // Get the annotation's content stream 
                Obj firstObj = textAnnot.GetSDFObj();
                Obj secondObj = firstObj.FindObj("AP");
                Obj contentStream = secondObj.FindObj("N");

                // use element reader to iterate through elements and union their bounding boxes
                ElementReader er = new ElementReader();
                er.Begin(contentStream);
                Rect unionRect = null;
                if (mFixedSize)
                {
                    unionRect = new Rect();
                    unionRect.Set(rect);
                }
                else
                {
                    Rect r = new Rect();
                    Element element = er.Next();
                    while (element != null)
                    {
                        if (element.GetBBox(r))
                        {
                            if (element.GetType() == ElementType.e_text)
                            {
                                if (unionRect == null)
                                {
                                    unionRect = r;
                                }
                                unionRect = GetRectUnion(r, unionRect);
                            }
                        }

                        element = er.Next();
                    }
                    unionRect.y1 -= 25;
                    unionRect.x2 += 25;
                }

                // Move annotation back into position
                x1.Value = unionRect.x1;
                y1.Value = unionRect.y1;
                x2.Value = unionRect.x2;
                y2.Value = unionRect.y2;
                mPDFView.ConvPagePtToScreenPt(x1, y1, mDownPageNumber);
                mPDFView.ConvPagePtToScreenPt(x2, y2, mDownPageNumber);

                pr = mPDFView.GetDoc().GetPage(mDownPageNumber).GetRotation();
                if (pr == PageRotate.e_90 || pr == PageRotate.e_270)
                {
                    xDist = Math.Abs(y1.Value - y2.Value);
                    yDist = Math.Abs(x1.Value - x2.Value);
                }
                else
                {
                    xDist = Math.Abs(x1.Value - x2.Value);
                    yDist = Math.Abs(y1.Value - y2.Value);
                }
                x1.Value = xPos;
                y1.Value = yPos;
                x2.Value = xPos + xDist;
                y2.Value = yPos + yDist;
                mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);
                mPDFView.ConvScreenPtToPagePt(x2, y2, mDownPageNumber);
                rect = new pdftron.PDF.Rect(x1.Value, y1.Value, x2.Value, y2.Value);

                SetAuthor(textAnnot);

                textAnnot.Resize(rect);
                textAnnot.RefreshAppearance();
                mPage = mPDFView.GetDoc().GetPage(mDownPageNumber);
                mPage.AnnotPushBack(textAnnot);
                textAnnot.RefreshAppearance();

                mAnnot = textAnnot;

                mPDFView.UpdateWithAnnot(textAnnot, mDownPageNumber);
                mToolManager.RaiseAnnotationAddedEvent(mAnnot);
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }
        }

        private Rect GetRectUnion(Rect r1, Rect r2)
        {
            Rect rectUnion = new Rect();
            rectUnion.x1 = Math.Min(r1.x1, r2.x1);
            rectUnion.y1 = Math.Min(r1.y1, r2.y1);
            rectUnion.x2 = Math.Max(r1.x2, r2.x2);
            rectUnion.y2 = Math.Max(r1.y2, r2.y2);
            return rectUnion;
        }


        private void SaveText()
        {
            string outString = "";
            mTextBox.Document.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out outString);
            if (outString.Trim().Length > 0)
            {
                UpdateDoc();
            }
        }


        #region Virtual Keyboard

        /// <summary>
        /// This function will translate the entire view control if necessary in order to make sure the text box is visible.
        /// </summary>
        protected void CustomKeyboardHandler(object sender, InputPaneVisibilityEventArgs e)
        {
            double RemainingHeight = e.OccludedRect.Y; // Y value of keyboard top (thus, also height of rest).
            double TotalHeight = mPDFView.ActualHeight;
            double KeyboardHeight = e.OccludedRect.Height; // height of keyboard

            e.EnsuredFocusedElementInView = true;

            mIsKeyboardOpen = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(mTextBox);
            UIRect viewRect = GetElementRect(mPDFView);
            double bottomMargin = RemainingHeight - textRect.Bottom - 10;
            double topMargin = textRect.Top - viewRect.Top;
            
            // figure out translation
            if (mFixedSize)
            {
                if (bottomMargin < 0)
                {
                    mTranslateOffset = -bottomMargin;
                    if (mTranslateOffset > topMargin)
                    {
                        mTranslateOffset = topMargin;
                    }
                    //Storyboard sb = KeyboardTranslate(mPDFView, 0, -mTranslateOffset, TimeSpan.FromMilliseconds(733));
                    //sb.Begin();
                }
            }
            else
            {
                double idealTop = RemainingHeight - (50 + mTextBox.FontSize * 5);
                if (idealTop < textRect.Top)
                {
                    mTranslateOffset = textRect.Top - idealTop;
                    if (mTranslateOffset > topMargin)
                    {
                        mTranslateOffset = topMargin;
                    }
                    if (mTranslateOffset > KeyboardHeight)
                    {
                        mTranslateOffset = KeyboardHeight;
                    }
                }
            }
            Storyboard sb = KeyboardTranslate(mPDFView, 0, -mTranslateOffset, TimeSpan.FromMilliseconds(733));
            Storyboard sbText = KeyboardTranslate(mTextBox, 0, -mTranslateOffset, TimeSpan.FromMilliseconds(733));
            sb.Begin();
            sbText.Begin();
        }

        /// <summary>
        /// Returns a rectangle with the coordinates of the framework element in screenspace
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        protected UIRect GetElementRect(FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

        /// <summary>
        /// This function translates the view control back down to it's original position
        /// </summary>
        protected void InputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            mIsKeyboardOpen = false;
            if (mTranslateOffset != 0)
            {
                Storyboard sb = KeyboardTranslate(mPDFView, -mTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
                Storyboard sbText = KeyboardTranslate(mTextBox, -mTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
                sbText.Begin();
            }
        }
        #endregion Virtual Keyboard

    }
}

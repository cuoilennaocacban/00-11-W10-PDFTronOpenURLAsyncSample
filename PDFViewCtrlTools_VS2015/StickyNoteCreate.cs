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
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;
using UIImage = Windows.UI.Xaml.Controls.Image;

using pdftron.PDF;
using PDFDouble = pdftron.Common.DoubleRef;
using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;

namespace pdftron.PDF.Tools
{
    class StickyNoteCreate : SimpleShapeCreate
    {
        protected FixedSizedBoxPopup mBoxPopup; // Holds fixed size rectangular elements
        protected KeyboardHelper mKeyboardHelper;
        protected StackPanel mStickyNoteDisplay = null;
        protected TextBox mStickyNoteTextBox = null;

        protected double mPageSpaceIconWidth = 20; // The size of the icon in page space
        protected double mPageSpaceIconHeight = 20;
        protected int mIconWidth = 31; // the default size of the icon in screen space
        protected int mIconHeight = 28;
        protected bool mCancelledByTap = false;

        protected UIImage mStickyIcon;

        // keyboard
        protected bool mKeyboardOpen = false;
        protected double mKeyboardTranslateOffset = 0;

        public StickyNoteCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_sticky_note_create;
            mToolMode = ToolType.e_sticky_note_create;
            mKeyboardHelper = new KeyboardHelper();
            mKeyboardHelper.SubscribeToKeyboard(true);

        }

        internal override void OnClose()
        {
            if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
            }
            DelayedUnsubscribe();
            EnableScrolling();
            base.OnClose();
        }


        // This delays the unsubscribing of the keyboard, so that the UI will have time to invoke the callbacks it needs.
        private async void DelayedUnsubscribe()
        {
            await Task.Delay(50);
            if (mStickyNoteTextBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mStickyNoteTextBox);
                mKeyboardHelper.SetHidingHandler(null);
            }
            mKeyboardHelper.SubscribeToKeyboard(false);
        }

        internal override bool OnSize()
        {
            EndCurrentTool(ToolType.e_pan);
            return true;
        }

        /// <summary>
        /// This will make the popup menu pop up at the targeted point
        /// </summary>
        /// <param name="point">The point in screen space.</param>
        public void SetTargetPoint(UIPoint point)
        {
            m_cnPt1 = new PDFPoint(point.X + mPDFView.GetAnnotationCanvasHorizontalOffset(), point.Y + mPDFView.GetAnnotationCanvasVerticalOffset());
            mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(point.X, point.Y);
            mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);
            Finish();
            //CreateTypingArea();
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);
            if (mToolManager.ContactPoints.Count > 1)
            {
                mSimpleShapeCanceledAtPress = true;
                mCancelledDrawing = true;
                mPointerID = -1;
                if (mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
                {
                    mViewerCanvas.Children.Remove(this);
                }
                if (mStickyIcon != null && this.Children.Contains(mStickyIcon))
                {
                    this.Children.Remove(mStickyIcon);
                }
                mToolManager.EnableScrollByManipulation = true;
                return false; // ignore additional pointer presses
            }
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                mSimpleShapeCanceledAtPress = true;
                return false; // if mouse, only allow left button
            }

            PDFDoc doc = mPDFView.GetDoc();
            if (doc == null)
            {
                mSimpleShapeCanceledAtPress = true;
                return false;
            }

            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            UIPoint screenPoint = e.GetCurrentPoint(mPDFView).Position;

            DisableScrolling();

            // Ensure we're on a page
            mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);
            if (mDownPageNumber < 1)
            {
                mSimpleShapeCanceledAtPress = true;
                mPointerID = -1;
                mToolManager.EnableOneFingerScroll = true;
                mToolManager.EnableScrollByManipulation = true;
                return false;
            }

            UIPoint canvasPoint = e.GetCurrentPoint(mViewerCanvas).Position;
            mPointerID = (int)e.Pointer.PointerId;
            mViewerCanvas.CapturePointer(e.Pointer);


            // set up tool's canvas
            mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);
            this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
            this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
            this.Width = mPageCropOnClient.x2 - mPageCropOnClient.x1;
            this.Height = mPageCropOnClient.y2 - mPageCropOnClient.y1;
            mViewerCanvas.Children.Add(this);

            m_cnPt1 = new PDFPoint(canvasPoint.X, canvasPoint.Y);

            if (mPageCropOnClient != null)
            {
                if (m_cnPt1.x < mPageCropOnClient.x1)
                {
                    m_cnPt1.x = mPageCropOnClient.x1;
                }
                if (m_cnPt1.x > mPageCropOnClient.x2)
                {
                    m_cnPt1.x = mPageCropOnClient.x2;
                }
                if (m_cnPt1.y < mPageCropOnClient.y1)
                {
                    m_cnPt1.y = mPageCropOnClient.y1;
                }
                if (m_cnPt1.y > mPageCropOnClient.y2)
                {
                    m_cnPt1.y = mPageCropOnClient.y2;
                }
            }

            mStickyIcon = new UIImage();

            mStickyIcon.Width = mIconWidth;
            mStickyIcon.Height = mIconHeight;

            WriteableBitmap imageBitmap = new WriteableBitmap(mIconWidth, mIconHeight);
            SetBitmapSource(imageBitmap);
            mStickyIcon.Source = imageBitmap;

            return false;
        }

        private async void SetBitmapSource(WriteableBitmap imageBitmap)
        {
            Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///pdftron.PDF.Tools/Icons/PopupIcon.png"));
            using (Windows.Storage.Streams.IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
            {
                try
                {
                    await imageBitmap.SetSourceAsync(fileStream);
                }
                catch (TaskCanceledException)
                {
                    // The async action to set the WriteableBitmap's source may be canceled if the source is changed again while the action is in progress
                }
            }
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != mPointerID)
            {
                return false;
            }
            UIPoint canvasPoint = e.GetCurrentPoint(mViewerCanvas).Position;
            m_cnPt1.x = canvasPoint.X;
            m_cnPt1.y = canvasPoint.Y;
            if (mPageCropOnClient != null)
            {
                if (m_cnPt1.x < mPageCropOnClient.x1)
                {
                    m_cnPt1.x = mPageCropOnClient.x1;
                }
                if (m_cnPt1.x > mPageCropOnClient.x2)
                {
                    m_cnPt1.x = mPageCropOnClient.x2;
                }
                if (m_cnPt1.y < mPageCropOnClient.y1)
                {
                    m_cnPt1.y = mPageCropOnClient.y1;
                }
                if (m_cnPt1.y > mPageCropOnClient.y2)
                {
                    m_cnPt1.y = mPageCropOnClient.y2;
                }
            }
            mStickyIcon.SetValue(Canvas.LeftProperty, m_cnPt1.x - mPageCropOnClient.x1);
            mStickyIcon.SetValue(Canvas.TopProperty, m_cnPt1.y - mIconHeight - mPageCropOnClient.y1);

            return false;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            // Remove contact from dictionary.
            RemovePointer(e.Pointer);

            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                EnableScrolling();
                return false; // ignore any secondary pointer
            }

            mCancelledByTap = false;
            //Finish();
            AddNotePendingTapped();

            mPointerID = -1;
            mViewerCanvas.ReleasePointerCaptures();
            return false;
        }

        private async void AddNotePendingTapped()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (!mCancelledByTap)
                {
                    Finish();
                }
            });
        }

        internal override bool ManipulationStartedEventHandler(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (mToolManager.ContactPoints.Count == 1 && mDownPageNumber > 0)
            {
                this.Children.Add(mStickyIcon);

                mStickyIcon.SetValue(Canvas.LeftProperty, m_cnPt1.x - mPageCropOnClient.x1);
                mStickyIcon.SetValue(Canvas.TopProperty, m_cnPt1.y - mIconHeight - mPageCropOnClient.y1);
            }
            return base.ManipulationStartedEventHandler(sender, e);
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            Pan pan = new Pan(mPDFView, mToolManager);
            pan.mIsInHelperMode = true;
            pan.TappedHandler(sender, e);
            if (pan.NextToolMode == ToolType.e_annot_edit && pan.CurrentAnnot != null && pan.CurrentAnnot.GetAnnotType() == AnnotType.e_Text)
            {
                mToolManager.ToolModeAfterEditing = mToolMode;
                mNextToolMode = pan.NextToolMode;
                mAnnot = pan.CurrentAnnot;
                mAnnotPageNum = pan.CurrentAnnotPageNumber;
                mAnnotBBox = pan.CurrentAnnotBBox;
                mCancelledByTap = true;
            }

            return true;
        }

        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)
        {
            if (mPointerID >= 0) // if we're currently moving the widget, don't allow key presses
            {
                return false;
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (mBoxPopup != null)
                {
                    mBoxPopup.Hide();
                    mBoxPopup = null;
                    mPDFView.Focus(Windows.UI.Xaml.FocusState.Pointer);
                    EndCurrentTool(ToolType.e_pan);
                    return false;
                }
            }
            if (mBoxPopup != null) // take no action
            {
                return false;
            }
            return base.KeyDownAction(sender, e);
        }


        // This is expected by SimpleShapeCreate, but it's not needed by StickyNoteCreate
        internal override void Finish()
        {
            pdftron.PDF.Annots.IMarkup markup = null;
            try
            {
                mPDFView.DocLock(true);

                PDFDouble x1 = new PDFDouble(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                PDFDouble y1 = new PDFDouble(m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset());

                mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);

                PDFRect rect = new PDFRect();

                // set the size of the rect in page space, and take rotation into account
                PageRotate pr = mPDFView.GetDoc().GetPage(mDownPageNumber).GetRotation();
                switch (pr)
                {
                    case PageRotate.e_90:
                        rect.Set(x1.Value, y1.Value, x1.Value - mPageSpaceIconHeight, y1.Value + mPageSpaceIconWidth);
                        break;
                    case PageRotate.e_180:
                        rect.Set(x1.Value, y1.Value, x1.Value - mPageSpaceIconWidth, y1.Value - mPageSpaceIconHeight);
                        break;
                    case PageRotate.e_270:
                        rect.Set(x1.Value, y1.Value, x1.Value + mPageSpaceIconHeight, y1.Value - mPageSpaceIconWidth);
                        break;
                    default:
                        rect.Set(x1.Value, y1.Value, x1.Value + mPageSpaceIconWidth, y1.Value + mPageSpaceIconHeight);
                        break;
                }
                rect.Normalize();

                // check bounds in page space
                PDFRect boundRect = mPage.GetCropBox();

                if (rect.x1 < boundRect.x1)
                {
                    rect.x1 = boundRect.x1;
                    rect.x2 = boundRect.x1 + mPageSpaceIconWidth;
                }
                if (rect.y1 < boundRect.y1)
                {
                    rect.y1 = boundRect.y1;
                    rect.y2 = boundRect.y1 + mPageSpaceIconHeight;
                }
                if (rect.x2 > boundRect.x2)
                {
                    rect.x2 = boundRect.x2;
                    rect.x1 = boundRect.x2 - mPageSpaceIconWidth;
                }
                if (rect.y2 > boundRect.y2)
                {
                    rect.y2 = boundRect.y2;
                    rect.y1 = boundRect.y2 - mPageSpaceIconHeight;
                }


                pdftron.PDF.Annots.Text text = pdftron.PDF.Annots.Text.Create(mPDFView.GetDoc().GetSDFDoc(), rect);
                text.SetIcon(pdftron.PDF.Annots.TextIcon.e_Comment);
                text.SetColor(new ColorPt(1, 1, 0), 3);

                // We need to give this a larger rect, else it won't show up properly in adobe reader.
                pdftron.PDF.Annots.Popup pop = pdftron.PDF.Annots.Popup.Create(mPDFView.GetDoc().GetSDFDoc(), new PDFRect(rect.x2 + 50, rect.y2 + 50, rect.x2 + 200, rect.y2 + 200));
                pop.SetParent(text);
                text.SetPopup(pop);

                text.RefreshAppearance();
                mPage = mPDFView.GetDoc().GetPage(mDownPageNumber);
                mPage.AnnotPushBack(text);
                mPage.AnnotPushBack(pop);

                // required to make it appear upright in rotated documents
                text.RefreshAppearance();

                mAnnot = text;
                mPDFView.UpdateWithAnnot(text, mDownPageNumber);

                markup = text;
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            // back to pan
            if (mReturnToPanModeWhenFinished)
            {
                mToolManager.CreateTool(ToolType.e_pan, this);
            }
            else
            {
                mToolManager.CreateTool(ToolType.e_sticky_note_create, this, true);
            }

            if (markup != null)
            {
                Utilities.NoteDialogBase dlg = null;
                if (mToolManager.IsOnPhone)
                {
                    dlg = new Utilities.NoteDialogPhone(markup, mPDFView, mToolManager, mDownPageNumber, true);
                }
                else
                {
                    dlg = new Utilities.NoteDialogWindows(markup, mPDFView, mToolManager, mDownPageNumber, true);
                }
                dlg.FocusWhenOpened = true;
                dlg.InitialAnnot = true;
            }
        }

        private void CreateTypingArea()
        {
            mStickyNoteDisplay = new StackPanel();
            mStickyNoteDisplay.Width = 300;
            mStickyNoteDisplay.Height = 350;
            mStickyNoteDisplay.Background = new SolidColorBrush(Colors.Black);
            Grid g = new Grid();
            g.Margin = new Windows.UI.Xaml.Thickness(5, 5, 5, 0);

            // Save button
            Button save = new Button();
            save.Content = ResourceHandler.GetString("NoteDialog_Save");
            save.BorderThickness = new Windows.UI.Xaml.Thickness(2);
            save.Width = 100;
            save.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            save.Click += (s, e) =>
            {
                StickyNoteFinished(mStickyNoteTextBox.Text);
            };
            g.Children.Add(save);
            mStickyNoteDisplay.Children.Add(g);

            // Cancel button
            Button delete = new Button();
            delete.Content = ResourceHandler.GetString("NoteDialog_Cancel");
            delete.BorderThickness = new Windows.UI.Xaml.Thickness(2);
            delete.Width = 100;
            delete.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
            delete.Click += (s, e) =>
            {
                mBoxPopup.Hide();
                if (mReturnToPanModeWhenFinished)
                {
                    mToolManager.CreateTool(ToolType.e_pan, this);
                }
                else
                {
                    mToolManager.CreateTool(ToolType.e_sticky_note_create, this, true);
                }
            };
            g.Children.Add(delete);

            // Title
            TextBlock tb = new TextBlock();
            tb.Text = ResourceHandler.GetString("NoteDialog_Title");
            tb.FontSize = 26;
            tb.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            tb.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            g.Children.Add(tb);

            // The textbox
            mStickyNoteTextBox = new TextBox();
            Windows.UI.Xaml.Style style = new Windows.UI.Xaml.Style { TargetType = typeof(TextBox) };
            style.Setters.Add(new Windows.UI.Xaml.Setter(TextBox.BackgroundProperty, new SolidColorBrush(Colors.White)));
            style.Setters.Add(new Windows.UI.Xaml.Setter(TextBox.ForegroundProperty, new SolidColorBrush(Colors.Black)));
            style.Setters.Add(new Windows.UI.Xaml.Setter(TextBox.BorderBrushProperty, new SolidColorBrush(Colors.Transparent)));
            mStickyNoteTextBox.Style = style;
            mStickyNoteTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap;
            mStickyNoteTextBox.AcceptsReturn = true;
            mStickyNoteTextBox.Margin = new Windows.UI.Xaml.Thickness(5);
            mStickyNoteTextBox.Height = 298; // cannot stretch height, so explicitly set it.
            mStickyNoteTextBox.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch;
            mStickyNoteTextBox.Loaded += (s, e) =>
            {
                mStickyNoteTextBox.Focus(Windows.UI.Xaml.FocusState.Pointer); // can't set focus until loaded
            };
            mStickyNoteDisplay.Children.Add(mStickyNoteTextBox);

            // Display it in a BoxPopup
            mBoxPopup = new FixedSizedBoxPopup(mPDFView, mStickyNoteDisplay);
            
            mBoxPopup.KeyDown += StickyNotePopupKeyDown; // lets us intercept the escape key

            mKeyboardHelper.AddShowingHandler(mStickyNoteTextBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(NoteInputPaneHiding));

            mBoxPopup.Show(new UIRect(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt1.y - mIconHeight - mPDFView.GetAnnotationCanvasVerticalOffset(), mIconWidth, mIconHeight));
            
        }

        protected void StickyNotePopupKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                mBoxPopup.Hide();
                if (mReturnToPanModeWhenFinished)
                {
                    mToolManager.CreateTool(ToolType.e_pan, this);
                }
                else
                {
                    mToolManager.CreateTool(ToolType.e_sticky_note_create, this, true);
                }
            }
        }
        public void StickyNoteFinished(string content)
        {
            try
            {
                mPDFView.DocLock(true);

                PDFDouble x1 = new PDFDouble(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                PDFDouble y1 = new PDFDouble(m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset());

                mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);

                PDFRect rect = new PDFRect();

                // set the size of the rect in page space, and take rotation into account
                PageRotate pr = mPDFView.GetDoc().GetPage(mDownPageNumber).GetRotation();
                switch (pr)
                {
                    case PageRotate.e_90:
                        rect.Set(x1.Value, y1.Value, x1.Value - mPageSpaceIconHeight, y1.Value + mPageSpaceIconWidth);
                        break;
                    case PageRotate.e_180:
                        rect.Set(x1.Value, y1.Value, x1.Value - mPageSpaceIconWidth, y1.Value - mPageSpaceIconHeight);
                        break;
                    case PageRotate.e_270:
                        rect.Set(x1.Value, y1.Value, x1.Value + mPageSpaceIconHeight, y1.Value - mPageSpaceIconWidth);
                        break;
                    default:
                        rect.Set(x1.Value, y1.Value, x1.Value + mPageSpaceIconWidth, y1.Value + mPageSpaceIconHeight);
                        break;
                }
                rect.Normalize();

                // check bounds in page space
                PDFRect boundRect = mPage.GetCropBox();

                if (rect.x1 < boundRect.x1)
                {
                    rect.x1 = boundRect.x1;
                    rect.x2 = boundRect.x1 + mPageSpaceIconWidth;
                }
                if (rect.y1 < boundRect.y1)
                {
                    rect.y1 = boundRect.y1;
                    rect.y2 = boundRect.y1 + mPageSpaceIconHeight;
                }
                if (rect.x2 > boundRect.x2)
                {
                    rect.x2 = boundRect.x2;
                    rect.x1 = boundRect.x2 - mPageSpaceIconWidth;
                }
                if (rect.y2 > boundRect.y2)
                {
                    rect.y2 = boundRect.y2;
                    rect.y1 = boundRect.y2 - mPageSpaceIconHeight;
                }


                pdftron.PDF.Annots.Text text = pdftron.PDF.Annots.Text.Create(mPDFView.GetDoc().GetSDFDoc(), rect);
                text.SetIcon(pdftron.PDF.Annots.TextIcon.e_Comment);
                text.SetColor(new ColorPt(1, 1, 0), 3);

                // We need to give this a larger rect, else it won't show up properly in adobe reader.
                pdftron.PDF.Annots.Popup pop = pdftron.PDF.Annots.Popup.Create(mPDFView.GetDoc().GetSDFDoc(), new PDFRect(rect.x1 + 50, rect.y1 + 50, rect.x1 + 200, rect.y1 + 200));
                pop.SetParent(text);
                text.SetPopup(pop);
                pop.SetContents(content);

                SetAuthor(text);

                text.RefreshAppearance();
                mPage = mPDFView.GetDoc().GetPage(mDownPageNumber);
                mPage.AnnotPushBack(text);
                mPage.AnnotPushBack(pop);

                // required to make it appear upright in rotated documents
                text.RefreshAppearance();

                mAnnot = text;
                mPDFView.UpdateWithAnnot(text, mDownPageNumber);
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            // back to pan
            if (mReturnToPanModeWhenFinished)
            {
                mToolManager.CreateTool(ToolType.e_pan, this);
            }
            else
            {
                mToolManager.CreateTool(ToolType.e_sticky_note_create, this, true);
            }
        }

        /// <summary>
        /// Translates the typing area if necessary in order to keep the text area above virtual keyboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void CustomKeyboardHandler(object sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            double RemainingHeight = e.OccludedRect.Y; // Y value of keyboard top (thus, also height of rest).
            double TotalHeight = mPDFView.ActualHeight;
            double KeyboardHeight = e.OccludedRect.Height; // height of keyboard

            e.EnsuredFocusedElementInView = true;
            mKeyboardOpen = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(mStickyNoteDisplay);
            UIRect viewRect = GetElementRect(mPDFView);
            double bottomMargin = RemainingHeight - textRect.Bottom - 10;
            double topMargin = textRect.Top - viewRect.Top;

            // figure out translation
            if (bottomMargin < 0)
            {
                mKeyboardTranslateOffset = -bottomMargin;
                if (mKeyboardTranslateOffset > topMargin)
                {
                    mKeyboardTranslateOffset = topMargin;
                }
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mStickyNoteDisplay, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
                sb.Begin();
            }
        }

        /// <summary>
        /// Translates the PDFViewCtrl back to offset 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void NoteInputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            mKeyboardOpen = false;
            if (mKeyboardTranslateOffset != 0)
            {
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mStickyNoteDisplay, -mKeyboardTranslateOffset, 
                    0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
            }
        }

        private UIRect GetElementRect(Windows.UI.Xaml.FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

    }
}

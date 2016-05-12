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
using Windows.Devices.Input;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;

using pdftron.PDF;
using PDFDouble = pdftron.Common.DoubleRef;


namespace pdftron.PDF.Tools
{
    public class TextMarkupCreate : Tool
    {
        protected Canvas mViewerCanvas;
        protected Point mPointerDownPoint; // the position where the pointer went down
        protected Point mCurrentPointerPoint; // the current pointer position
        protected Point mCurrentPointOffset; // the offset from the mouse down to the selection of text.

        protected int mPointerID = -1;
        protected bool mSelectingByMouse = false;
        protected bool mHasMoved = false;
        protected bool mTouchScrolling = false;

        protected SolidColorBrush mSelectionRectangleBrush;

        // set to true if you have set yourself to delayed removal
        protected bool mDelayedRemovalSet = false;

        internal TextMarkupCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mPointerDownPoint = new Point();
            mCurrentPointerPoint = new Point(); // how far above the touch area we select
            mCurrentPointOffset = new Point(0, -20);

            mSelectionStartPage = -1;
            mSelectionEndPage = -1;

            mSelectionRectangleBrush = new SolidColorBrush(Color.FromArgb(80, 40, 70, 200));
        }

        // This is called after the previous tool's OnClose, so we need to put these here to not get overridden
        internal override void OnCreate()
        {
        }


        internal override void OnClose()
        {
            if (!mDelayedRemovalSet && mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Remove(this);
            }
            mPDFView.ClearSelection();
            EnableScrolling();
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
            base.OnClose();
        }

        internal override bool OnScale()
        {  // shouldn't happen
            base.OnScale();
            return false;
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);
            DisableScrolling();
            if (mToolManager.ContactPoints.Count > 1)
            {
                mHasMoved = false;
                mPointerID = -1;
                mPDFView.ClearSelection();
                DrawSelection();
                mToolManager.EnableScrollByManipulation = true;
                return false; // ignore additional pointer presses
            }

            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                return false; // if mouse, only allow left button
            }

            UIPoint sc_DownPoint = e.GetCurrentPoint(mPDFView).Position;
            int downPgNumber = mPDFView.GetPageNumberFromScreenPoint(sc_DownPoint.X, sc_DownPoint.Y);
            if (downPgNumber < 1)
            {
                mPointerID = -1;
                mToolManager.EnableOneFingerScroll = true;
                mToolManager.EnableScrollByManipulation = true;
                return false;
            }

            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            this.Width = mViewerCanvas.ActualWidth;
            this.Height = mViewerCanvas.ActualHeight;
            this.IsHitTestVisible = false;

            mPointerID = (int)e.Pointer.PointerId;
            mViewerCanvas.CapturePointer(e.Pointer);
            UIPoint canvasPoint = e.GetCurrentPoint(mViewerCanvas).Position;

            mHasMoved = false;

            double x = canvasPoint.X;
            double y = canvasPoint.Y;

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse || e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
            {
                mPointerDownPoint.x = x;
                mPointerDownPoint.y = y;
                mSelectingByMouse = true;
            }
            else // touch
            {
                mPointerDownPoint.x = x;
                mPointerDownPoint.y = y;
                mCurrentPointerPoint.x = x;
                mCurrentPointerPoint.y = y;
            }
            DisableScrolling();
            return false;
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)

        {
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);

            if (e.Pointer.PointerId != mPointerID)
            {
                return false;
            }
            PointerPoint pp = e.GetCurrentPoint(mViewerCanvas);
            mCurrentPointerPoint.x = pp.Position.X;
            mCurrentPointerPoint.y = pp.Position.Y;

            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

            double p1x = mPointerDownPoint.x - sx;
            double p1y = mPointerDownPoint.y - sy;

            double p2x = mCurrentPointerPoint.x - sx;
            double p2y = mCurrentPointerPoint.y - sy;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            {
                p1x += mCurrentPointOffset.x;
                p1y += mCurrentPointOffset.y;
                p2x += mCurrentPointOffset.x;
                p2y += mCurrentPointOffset.y;
            }

            if (!mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Add(this);
            }

            // new selection
            mPDFView.ClearSelection();

            mPDFView.SetTextSelectionMode(TextSelectionMode.e_structural);
            mPDFView.Select(p1x, p1y, p2x, p2y);

            if (!mHasMoved && // make sure we move at least 4 points in some direction
                        (Math.Abs(mCurrentPointerPoint.x - mPointerDownPoint.x) > 10 || Math.Abs(mCurrentPointerPoint.y - mPointerDownPoint.y) > 10))
            {
                mHasMoved = true;
            }
            else
            {
                DrawSelection();
            }
            return false;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            EnableScrolling();
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                // do nothing
                return false;
            }
            mPointerID = -1;
            if (mViewerCanvas != null)
            {
                mViewerCanvas.ReleasePointerCaptures();
            }
            MarkupHandler();
            EndCurrentTool(ToolType.e_pan);

            return true;
        }

        internal override bool PointerCaptureLostHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mToolManager.ContactPoints.ContainsKey(e.Pointer.PointerId)) // We've lost capture of the active pointer
            {
                EndCurrentTool(ToolType.e_pan);
            }
            RemovePointer(e.Pointer);
            return false;
        }

        internal override bool PointerCanceledHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            EndCurrentTool(ToolType.e_pan);
            return true;
        }
        internal override bool PointerEnteredHandler(Object sender, PointerRoutedEventArgs e)
        {
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);
            return true;
        }

        internal override bool PointerExitedHandler(Object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
            return false;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (!mReturnToPanModeWhenFinished)
            {
                Pan pan = new Pan(mPDFView, mToolManager);
                pan.mIsInHelperMode = true;
                pan.TappedHandler(sender, e);
                if (pan.NextToolMode == ToolType.e_annot_edit || pan.NextToolMode == ToolType.e_line_edit || pan.NextToolMode == ToolType.e_annot_edit_text_markup)
                {
                    mToolManager.ToolModeAfterEditing = mToolMode;
                    mNextToolMode = pan.NextToolMode;
                    mAnnot = pan.CurrentAnnot;
                    mAnnotPageNum = pan.CurrentAnnotPageNumber;
                    mAnnotBBox = pan.CurrentAnnotBBox;
                }
            }
            return true;
        }

        internal override bool KeyDownAction(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)

        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                EndCurrentTool(ToolType.e_pan);
                return true;
            }
            
            return base.KeyDownAction(sender, e);
        }

        protected void DrawSelection()
        {
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
            mSelectionStartPage = mPDFView.GetSelectionBeginPage();
            mSelectionEndPage = mPDFView.GetSelectionEndPage();
            this.Children.Clear();
            for (int pgnm = mSelectionStartPage; pgnm <= mSelectionEndPage; pgnm++)
            {
                PDFViewCtrlSelection sel = mPDFView.GetSelection(pgnm);
                if (!mPDFView.HasSelectionOnPage(pgnm))
                {
                    continue;
                }

                double[] quads = sel.GetQuads();
                int sz = quads.Length / 8;

                int k = 0;
                Rect drawRect;
                PDFDouble xpt = new PDFDouble(0); // for translating coordinates
                PDFDouble ypt = new PDFDouble(0);

                // each quad consists of 8 consecutive points
                for (int i = 0; i < sz; ++i, k += 8)
                {
                    drawRect = new Rect();

                    // Get first corner of selection quad
                    xpt.Value = quads[k];
                    ypt.Value = quads[k + 1];
                    mPDFView.ConvPagePtToScreenPt(xpt, ypt, pgnm);
                    drawRect.x1 = xpt.Value + sx;
                    drawRect.y1 = ypt.Value + sy;

                    // Get opposite corner of selection quad
                    xpt.Value = quads[k + 4];
                    ypt.Value = quads[k + 5];
                    mPDFView.ConvPagePtToScreenPt(xpt, ypt, pgnm);
                    drawRect.x2 = xpt.Value + sx;
                    drawRect.y2 = ypt.Value + sy;

                    drawRect.Normalize();

                    // draw rectangle on selected text
                    Rectangle rect = new Rectangle();
                    rect.SetValue(Canvas.LeftProperty, drawRect.x1);
                    rect.SetValue(Canvas.TopProperty, drawRect.y1);
                    rect.Width = drawRect.x2 - drawRect.x1;
                    rect.Height = drawRect.y2 - drawRect.y1;
                    rect.Fill = mSelectionRectangleBrush;
                    this.Children.Add(rect);
                }
            }
        }

        private void MarkupHandler()
        {
            // Store created markups by page, so that we can request all updates after we have created them all
            Dictionary<int, pdftron.PDF.Annots.ITextMarkup> textMarkupsToUpdate = new Dictionary<int, pdftron.PDF.Annots.ITextMarkup>();
            try
            {
                PDFDoc doc = mPDFView.GetDoc();
                mPDFView.DocLock(true);

                mSelectionStartPage = mPDFView.GetSelectionBeginPage();
                mSelectionEndPage = mPDFView.GetSelectionEndPage();

                for (int pgnm = mSelectionStartPage; pgnm <= mSelectionEndPage; pgnm++)
                {
                    if (!mPDFView.HasSelectionOnPage(pgnm))
                    {
                        continue;
                    }

                    double[] quads = mPDFView.GetSelection(pgnm).GetQuads();
                    int sz = quads.Length / 8;
                    if (sz == 0)
                    {
                        continue;
                    }

                    // for translating points
                    Point p1 = new Point();
                    Point p2 = new Point();
                    Point p3 = new Point();
                    Point p4 = new Point();

                    QuadPoint qp = new QuadPoint(p1, p2, p3, p4);
                    Rect bbox = new Rect(quads[0], quads[1], quads[4], quads[5]); //just use the first quad to temporarily populate the bbox
                    pdftron.PDF.Annots.ITextMarkup tm;

                    double opacity = 1; // default opacity

                    SettingsColor color = Settings.GetShapeColor(mToolMode, false);
                    opacity = Settings.GetOpacity(mToolMode);


                    if (mToolMode == ToolType.e_text_highlight)
                    {
                        tm = pdftron.PDF.Annots.Highlight.Create(doc.GetSDFDoc(), bbox);
                    }
                    else // Underline, Strikeout, and Squiggly share color and opacity settings
                    {
                        // figure out markup type
                        if (mToolMode == ToolType.e_text_underline)
                        {
                            tm = pdftron.PDF.Annots.Underline.Create(doc.GetSDFDoc(), bbox);
                        }
                        else if (mToolMode == ToolType.e_text_strikeout)
                        {
                            tm = pdftron.PDF.Annots.StrikeOut.Create(doc.GetSDFDoc(), bbox);
                        }
                        else // squiggly
                        {
                            tm = pdftron.PDF.Annots.Squiggly.Create(doc.GetSDFDoc(), bbox);
                        }
                    }

                    // Add the quads
                    int k = 0;
                    for (int i = 0; i < sz; ++i, k += 8)
                    {
                        p1.x = quads[k];
                        p1.y = quads[k + 1];

                        p2.x = quads[k + 2];
                        p2.y = quads[k + 3];

                        p3.x = quads[k + 4];
                        p3.y = quads[k + 5];

                        p4.x = quads[k + 6];
                        p4.y = quads[k + 7];


                        if (mToolManager.TextMarkupAdobeHack) // Create quad in a way that Adobe can handle.
                        {
                            qp.p1 = p4;
                            qp.p2 = p3;
                            qp.p3 = p1;
                            qp.p4 = p2;
                        }
                        else // according to PDF spec
                        {
                            qp.p1 = p1;
                            qp.p2 = p2;
                            qp.p3 = p3;
                            qp.p4 = p4;
                        }

                        tm.SetQuadPoint(i, qp);
                    }

                    // set color and opacity
                    ColorPt colorPt = new ColorPt(color.R / 255.0, color.G / 255.0, color.B / 255.0);
                    tm.SetColor(colorPt, 3);
                    tm.SetOpacity(opacity);

                    if (mToolMode != ToolType.e_text_highlight)
                    {
                        AnnotBorderStyle bs = tm.GetBorderStyle();
                        bs.width = Settings.GetThickness(mToolMode);
                        tm.SetBorderStyle(bs);
                    }

                    SetAuthor(tm);
                    tm.RefreshAppearance();

                    pdftron.PDF.Page page = doc.GetPage(pgnm);
                    page.AnnotPushBack(tm);

                    // add markup to dictionary for later update
                    textMarkupsToUpdate[pgnm] = tm;
                }

                mPDFView.ClearSelection();
            }
            catch (System.Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            // Now update the PDFViewCtrl to display our new text markup
            foreach (int pgnm in textMarkupsToUpdate.Keys)
            {
                mPDFView.UpdateWithAnnot(textMarkupsToUpdate[pgnm], pgnm);
                mToolManager.RaiseAnnotationAddedEvent(textMarkupsToUpdate[pgnm]);
            }

            //mViewerCanvas.Children.Remove(this);                
            mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
            mDelayedRemovalSet = true;
        }

    }
}

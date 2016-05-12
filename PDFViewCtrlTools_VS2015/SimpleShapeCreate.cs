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

using UIPoint = Windows.Foundation.Point;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using pdftron.PDF;
using pdftron.Common;


namespace pdftron.PDF.Tools
{
    /// <summary>
    /// This is the base class for simple shape creation tools
    /// </summary>
    public class SimpleShapeCreate : Tool
    {
        protected int START_DRAWING_THRESHOLD = 5;

        protected bool mStartDrawing = false;
        protected bool mPointerDown = false;
        protected Point m_cnPt1 = null;
        protected Point m_cnPt2 = null; 
        protected Canvas mViewerCanvas;
        protected bool mMirrorX = false;
        protected bool mMirrorY = false;
        protected CompositeTransform mRenderTransform;
        protected int mDownPageNumber = -1;
        protected Rect mPageCropOnClient;
        protected ScrollViewer mScrollViewer = null;
        protected int mPointerID = -1;

        protected bool mSimpleShapeCanceledAtPress = false; // lets the derived class know that the pointer press should be disregarded.
        protected bool mCancelledDrawing = false;

        // Visuals for shapes
        protected double mStrokeThickness = 1;
        protected double mZoomLevel = 1;
        protected double mDrawThickness;
        protected SolidColorBrush mStrokeBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
        protected bool mUseFill = false;
        protected bool mUseStroke = true;
        protected SolidColorBrush mFillBrush = new SolidColorBrush(Color.FromArgb(255, 0, 155, 155));
        protected double mOpacity = 1.0;

        // set to true if you have set yourself to delayed removal
        protected bool mDelayedRemovalSet = false;

        internal SimpleShapeCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
        }

        internal override void OnClose()
        {
            EnableScrolling();
            if (!mDelayedRemovalSet && mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Remove(this);
            }
            base.OnClose();
        }

        internal override bool OnScale()
        { // scaling is fine, since it can only happen before drawing has begun
            base.OnScale();
            return false;
        }

        internal override bool OnSize()
        {
            if (mPointerID >= 0)
            {
                EndCurrentTool(ToolType.e_pan);
            }
            return false;
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);
            DisableScrolling();
            mToolManager.ToolModeAfterEditing = ToolType.e_none;
            mCancelledDrawing = false;

            mSimpleShapeCanceledAtPress = false;

            if (mToolManager.ContactPoints.Count > 1)
            {
                mToolManager.EnableOneFingerScroll = false;
                mSimpleShapeCanceledAtPress = true;
                mCancelledDrawing = true;
                mStartDrawing = false;
                mPointerID = -1;
                if (mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
                {
                    this.Children.Clear();
                    mViewerCanvas.Children.Remove(this);
                }
                mToolManager.EnableScrollByManipulation = true;
                return false; // ignore additional pointer presses
            }

            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                mSimpleShapeCanceledAtPress = true;
                return false; // if mouse, only allow left button
            }
            mPointerID = (int)e.Pointer.PointerId;
            UIPoint screenPoint = e.GetCurrentPoint(mPDFView).Position;
            PDFDoc doc = mPDFView.GetDoc();
            if (doc == null)
            {
                mSimpleShapeCanceledAtPress = true;
                return false;
            }

            mViewerCanvas = mPDFView.GetAnnotationCanvas();

            // Ensure we're on a page
            mToolManager.EnableOneFingerScroll = false;
            mDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);
            if (mDownPageNumber < 1)
            {
                mPointerID = -1;
                mToolManager.EnableOneFingerScroll = true;
                mToolManager.EnableScrollByManipulation = true;
                mDownPageNumber = -1;
                mSimpleShapeCanceledAtPress = true;
                //EndCurrentTool(ToolType.e_pan);
                return false;
            }
            // Get this from mPDFViewCtrl
            mPage = doc.GetPage(mDownPageNumber);

            // Get the page's bounding box in canvas space
            mPageCropOnClient = BuildPageBoundBoxOnClient(mDownPageNumber);

            mPointerDown = true;

            SettingsColor color = Settings.GetShapeColor(this.ToolMode, false);
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;
            mUseStroke = true;
            mStrokeBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            //// Fetch style data from Settings
            //if (Settings.HasMarkupStrokeColor)
            //{
            //    SettingsColor color = Settings.MarkupStrokeColor;
            //    byte r = color.R;
            //    byte g = color.G;
            //    byte b = color.B;
            //    mUseStroke = color.Use;
            //    mStrokeBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            //}

            color = Settings.GetShapeColor(this.ToolMode, true);
            r = color.R;
            g = color.G;
            b = color.B;
            mUseFill = color.Use;
            mFillBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            //if (Settings.HasMarkupFillColor)
            //{
            //    SettingsColor color = Settings.MarkupFillColor;
            //    byte r = color.R;
            //    byte g = color.G;
            //    byte b = color.B;
            //    mUseFill = color.Use;
            //    mFillBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            //}

            mStrokeThickness = Settings.GetThickness(this.ToolMode);
            mOpacity = Settings.GetOpacity(this.ToolMode);
            //if (Settings.HasMarkupStrokeThickness)
            //{
            //    mStrokeThickness = Settings.MarkupStrokeThickness;
            //}

            //if (Settings.HasMarkupOpacity)
            //{
            //    mOpacity = Settings.MarkupOpacity;
            //}

            mZoomLevel = mPDFView.GetZoom();
            mDrawThickness = mStrokeThickness * mZoomLevel;

            UIPoint pp = e.GetCurrentPoint(mViewerCanvas).Position;
            m_cnPt1 = new Point(pp.X, pp.Y);
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
            m_cnPt2 = new Point(m_cnPt1.x, m_cnPt1.y);
            mViewerCanvas.CapturePointer(e.Pointer);
            return false;
        }


        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerDown)
            {
                if (e.Pointer.PointerId != mPointerID)
                {
                    return false;
                }
                UIPoint pp = e.GetCurrentPoint(mViewerCanvas).Position;

                m_cnPt2.x = pp.X;
                m_cnPt2.y = pp.Y;

                // want to make sure we're within our bounding box.
                if (mPageCropOnClient != null)
                {
                    if (m_cnPt2.x < mPageCropOnClient.x1)
                    {
                        m_cnPt2.x = mPageCropOnClient.x1;
                    }
                    if (m_cnPt2.x > mPageCropOnClient.x2)
                    {
                        m_cnPt2.x = mPageCropOnClient.x2;
                    }
                    if (m_cnPt2.y < mPageCropOnClient.y1)
                    {
                        m_cnPt2.y = mPageCropOnClient.y1;
                    }
                    if (m_cnPt2.y > mPageCropOnClient.y2)
                    {
                        m_cnPt2.y = mPageCropOnClient.y2;
                    }
                }

                // We don't want to start drawing until we have a bit of a distance
                if (!mStartDrawing) 
                {
                    // wait until you're at least START_DRAWING_THRESHOLD distance from the start
                    if ((Math.Abs(m_cnPt2.x - m_cnPt1.x) > START_DRAWING_THRESHOLD)
                        || (Math.Abs(m_cnPt2.y - m_cnPt1.y) > START_DRAWING_THRESHOLD))
                    {
                        mStartDrawing = true;
                        mViewerCanvas.Children.Add(this);
                        this.SetValue(Canvas.LeftProperty, m_cnPt1.x);
                        this.SetValue(Canvas.TopProperty, m_cnPt1.y);
                        mRenderTransform = new CompositeTransform();
                        this.RenderTransform = mRenderTransform;
                        mMirrorX = false;
                        mMirrorY = false;
                        this.Opacity = mOpacity;
                    }
                }

                // Size the canvas according to the two points
                if (mStartDrawing)
                {
                    this.Width = Math.Abs(m_cnPt2.x - m_cnPt1.x);
                    this.Height = Math.Abs(m_cnPt2.y - m_cnPt1.y);
                    if (m_cnPt2.x < m_cnPt1.x)
                    {
                        mMirrorX = true;
                        mRenderTransform.ScaleX = -1;
                    }
                    else
                    {
                        mMirrorX = false;
                        mRenderTransform.ScaleX = 1;
                    }
                    if (m_cnPt2.y < m_cnPt1.y)
                    {
                        mMirrorY = true;
                        mRenderTransform.ScaleY = -1;
                    }
                    else
                    {
                        mMirrorY = false;
                        mRenderTransform.ScaleY = 1;
                    }
                }
            }
            return false;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            // Remove contact from dictionary.
            RemovePointer(e.Pointer);
            if (mCancelledDrawing)
            {
                return false; // ignore any secondary pointer
            }
        
            mPointerDown = false;

            EnableScrolling();
            if (mStartDrawing) // don't want to add any annotations resulting from a tap
            {
                Finish();

                mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
                mToolManager.RaiseAnnotationAddedEvent(mAnnot);
                mDelayedRemovalSet = true;

                EndCurrentTool(ToolType.e_pan);
            }
            mPointerID = -1;
            if (mViewerCanvas != null)
            {
                mViewerCanvas.ReleasePointerCaptures();
            }
            return false;
        }

        internal override bool PointerCaptureLostHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mToolManager.ContactPoints.ContainsKey(e.Pointer.PointerId)) // We've lost capture of the active pointer
            {
                EndCurrentTool(ToolType.e_pan);
                mPointerID = -1;
            }
            RemovePointer(e.Pointer);
            return false;
        }

        internal override bool PointerCanceledHandler(object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            EndCurrentTool(ToolType.e_pan);
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
            else
            {
                EndCurrentTool(ToolType.e_pan);
            }
            return true;
        }

        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)
        {

            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                EndCurrentTool(ToolType.e_pan);
                return true;
            }

            return base.KeyDownAction(sender, e);

        }

        protected PDFRect GetShapeBBox()
        {
            double halfThickness = mDrawThickness / 2;

            //computes the bounding box of the rubber band in page space.
            PDFDouble x1 = new PDFDouble(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
            PDFDouble y1 = new PDFDouble(m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset());
            PDFDouble x2 = new PDFDouble(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
            PDFDouble y2 = new PDFDouble(m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset());

            mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);
            mPDFView.ConvScreenPtToPagePt(x2, y2, mDownPageNumber);

            pdftron.PDF.Rect rect;
            try
            {
                rect = new pdftron.PDF.Rect(x1.Value, y1.Value, x2.Value, y2.Value);
                rect.Normalize();
                return rect;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        ///  Must be implemented by the derived classes in order to create and push back the right type of annotation
        /// </summary>
        internal virtual void Finish()
        {

        }

        /// <summary>
        /// Sets the look of a markup annotation.
        /// </summary>
        /// <param name="annot">The annotation</param>
        /// <param name="hasFill">Whether the annotation has a fill color or not</param>
        protected void SetStyle(pdftron.PDF.Annots.IMarkup annot, bool hasFill = false)
        {
            double r = mStrokeBrush.Color.R / 255.0;
            double g = mStrokeBrush.Color.G / 255.0;
            double b = mStrokeBrush.Color.B / 255.0;
            ColorPt color = new ColorPt(r, g, b);
            if (!hasFill || mUseStroke)
            {
                annot.SetColor(color, 3);
            }
            else
            {
                annot.SetColor(color, 0); // 0 for transparent
            }
            if (hasFill)
            {
                r = mFillBrush.Color.R / 255.0;
                g = mFillBrush.Color.G / 255.0;
                b = mFillBrush.Color.B / 255.0;
                color = new ColorPt(r, g, b);
                if (mUseFill)
                {
                    annot.SetInteriorColor(color, 3);
                }
                else
                {
                    annot.SetInteriorColor(color, 0); // 0 for transparent
                }
            }
            AnnotBorderStyle bs = annot.GetBorderStyle();
            bs.width = mStrokeThickness;
            annot.SetBorderStyle(bs);
            annot.SetOpacity(mOpacity);

            SetAuthor(annot);
        }

    }
}

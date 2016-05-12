using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using pdftron.PDF;
using pdftron.Common;

namespace pdftron.PDF.Tools
{
    public class FreehandCreate : SimpleShapeCreate
    {
        public delegate void StrokeAddedDelegate();
        public delegate void StylusIsDetectedDelegate();

        protected bool mShapeHasBeenCreated = false;
        protected Path mShape;
        protected List<PDFPoint> mCurrentStrokeList = null; // A list of strokes
        protected List<List<PDFPoint>> mListOfStrokeLists = null; // Contains all the lists above
        protected List<List<PDFPoint>> mListOfUndoneStrokes;
        protected PathFigureCollection mPathPoints;
        protected List<PathFigure> mListOfUndonePathFigures;
        protected PathFigure mCurrentPathFigure;
        protected bool mFirstTime = false; // Whether or not a pointer has been pressed already.
        protected double mLeftmost, mRightmost, mTopmost, mBottommost;
        protected bool mMultiStrokeMode = false;
        protected UIPoint mLastPoint;
        protected PDFPoint mLastEndPoit;

        // for error correcting.
        private UIPoint m_cnCurrentPoint;
        private UIPoint mTwoPointsBack;
        private UIPoint mOnePointBack;
        private bool mStartedPathSmoothing;
        private bool mIsStylus = false;

        // for timing the ink when spontaneously creating it
        private DispatcherTimer mCompletionTimer;
        private bool mIsInTimedMode;
        private bool mShouldSaveInk;


        /// <summary>
        /// Sets whether the tool will push back each stroke once the pointer is released, 
        /// or if it should continue to accumulate strokes until the user explicitly says it's done
        /// 
        /// When done, call CommitAnnotation()
        /// </summary>
        public bool MultiStrokeMode
        {
            get
            {
                return mMultiStrokeMode;
            }
            set
            {
                mMultiStrokeMode = value;
            }
        }

        /// <summary>
        /// Gets or sets whether this is a stylus annotation
        /// </summary>
        public bool IsUsingSylus
        {
            get { return mIsStylus; }
            set { mIsStylus = value; }
        }

        /// <summary>
        /// This event is invoked when the Tool detects a Stylus and is not already in 
        /// Stylus mode (i.e. IsUsingSylus is false).
        /// </summary>
        public event StylusIsDetectedDelegate StylusIsDetected = delegate { };

        /// <summary>
        /// This even is invoked when a path has been drawn for the ink annotation.
        /// Note: It is not invoked after a call to RedoStroke()
        /// </summary>
        public event StrokeAddedDelegate StrokeAdded;

        public FreehandCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_ink_create;
            mToolMode = ToolType.e_ink_create;
            mListOfStrokeLists = new List<List<Point>>();

            mListOfUndoneStrokes = new List<List<PDFPoint>>();
            mListOfUndonePathFigures = new List<PathFigure>();
        }

        internal override void OnClose()
        {
            EndTimer();
            base.OnClose();
            mPDFView.RequestRendering();
        }

        internal override bool OnSize()
        {
            mNextToolMode = ToolType.e_pan;
            return base.OnSize();
        }

        //internal override void OnFinishedRendering()
        //{
        //    //base.OnFinishedRendering();
        //    //mPDFView.CancelRendering();
        //}

        internal override bool PointerPressedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);
            if (mToolManager.ContactPoints.Count > 1)
            {
                mPointerID = -1;
                mToolManager.EnableScrollByManipulation = true;
                if (mMultiStrokeMode && mCurrentPathFigure != null && !mIsStylus)
                {
                    mCurrentPathFigure = null;
                    mPathPoints.RemoveAt(mPathPoints.Count - 1);
                    mListOfStrokeLists.RemoveAt(mListOfStrokeLists.Count - 1);
                }
                if (mPageCropOnClient != null)
                {
                    mRectToKeepOnScreenWhileManipulating = new Rect(mPageCropOnClient.x1, mPageCropOnClient.y1, mPageCropOnClient.x2, mPageCropOnClient.y2);
                    mRectToKeepOnScreenWhileManipulating.Inflate(35);
                }
                return false;
            }
            if (IsUsingSylus && e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
            {
                if (mIsInTimedMode)
                {
                    EnableScrolling();
                    EndTimer();
                    mNextToolMode = ToolType.e_pan;
                    return true;
                }
                else 
                {
                    if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch)
                    {
                        mToolManager.EnableScrollByManipulation = true;
                        mToolManager.EnableOneFingerScroll = true;
                        if (mPageCropOnClient != null)
                        {
                            mRectToKeepOnScreenWhileManipulating = new Rect(mPageCropOnClient.x1, mPageCropOnClient.y1, mPageCropOnClient.x2, mPageCropOnClient.y2);
                            mRectToKeepOnScreenWhileManipulating.Inflate(35);
                        }
                    }
                    return true;
                }
            }
            PointerPoint pointerPoint = e.GetCurrentPoint(mPDFView);
            if (pointerPoint.Properties.IsEraser)
            {
                if (mIsInTimedMode)
                {
                    mNextToolMode = ToolType.e_ink_eraser;
                }
                return true;
            }
            if (!mIsStylus && pointerPoint.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                if (mJustSwitchedFromAnotherTool)
                {
                    mIsInTimedMode = true;
                    mShouldSaveInk = true;
                    mMultiStrokeMode = true;
                }
                mIsStylus = true;
                StylusIsDetected();
            }
            if (mIsInTimedMode)
            {
                StopTimer();
            }

            UIPoint screenPoint = pointerPoint.Position;
            if (mDownPageNumber > 0) // This means each free hand annotation has more than one stroke
            {
                
                int newDownPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);
                if (newDownPageNumber != mDownPageNumber)
                {
                    if (mIsInTimedMode)
                    {
                        mNextToolMode = ToolType.e_pan;
                    }
                    return false;
                }
            }

            base.PointerPressedHandler(sender, e); // gets us page, point, crop box and pointer capture
            if (mSimpleShapeCanceledAtPress)
            {
                return false;
            }


            if (mListOfStrokeLists.Count > 0 && mIsInTimedMode && mToolManager.INK_MARGIN_FOR_STARTING_NEW_ANNOTATION > 0)
            {
                // check distance of mouse down from current bounding box.
                PDFRect rect = new PDFRect(mLeftmost, mTopmost, mRightmost, mBottommost);
                rect.Inflate(mToolManager.INK_MARGIN_FOR_STARTING_NEW_ANNOTATION);
                if (!rect.Contains(m_cnPt1.x, m_cnPt1.y))
                {
                    mNextToolMode = ToolType.e_pan;
                    return true;
                }
            }

            if (!mIsStylus)
            {
                mPDFView.CancelRendering();
            }

            // Narrow the crop box to account for the thickness of the line we're drawing
            double halfThickness = mDrawThickness / 2;
            if (mPageCropOnClient != null)
            {
                if (m_cnPt1.x < mPageCropOnClient.x1 + halfThickness)
                {
                    m_cnPt1.x = mPageCropOnClient.x1 + halfThickness;
                }
                if (m_cnPt1.x > mPageCropOnClient.x2 - halfThickness)
                {
                    m_cnPt1.x = mPageCropOnClient.x2 - halfThickness;
                }
                if (m_cnPt1.y < mPageCropOnClient.y1 + halfThickness)
                {
                    m_cnPt1.y = mPageCropOnClient.y1 + halfThickness;
                }
                if (m_cnPt1.y > mPageCropOnClient.y2 - halfThickness)
                {
                    m_cnPt1.y = mPageCropOnClient.y2 - halfThickness;
                }
            }

            if (!mFirstTime)
            {
                mFirstTime = true;
                // set bounds
                mLeftmost = m_cnPt1.x;
                mRightmost = m_cnPt1.x;
                mTopmost = m_cnPt1.y;
                mBottommost = m_cnPt1.y;
                mPathPoints = new PathFigureCollection();
            }
            
            mCurrentPathFigure = new PathFigure();
            mCurrentPathFigure.StartPoint = new UIPoint(m_cnPt1.x - mPageCropOnClient.x1, m_cnPt1.y - mPageCropOnClient.y1);
            mPathPoints.Add(mCurrentPathFigure);
            mListOfStrokeLists.Add(new List<Point>());
            mCurrentStrokeList = mListOfStrokeLists[mListOfStrokeLists.Count() - 1];

            mStartedPathSmoothing = false;
            mTwoPointsBack = new UIPoint(m_cnPt1.x, m_cnPt1.y);

            mLastPoint = new UIPoint(m_cnPt1.x, m_cnPt1.y);
            m_cnCurrentPoint = new UIPoint(m_cnPt1.x, m_cnPt1.y);

            mCurrentStrokeList.Add(new Point(m_cnPt1.x - mPageCropOnClient.x1, m_cnPt1.y - mPageCropOnClient.y1));

            CheckIfShouldStartDrawing();
            if (mStartDrawing)
            {
                (mShape.Data as PathGeometry).Figures = mPathPoints;
            }

            return true;
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (IsUsingSylus && e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
            {
                return true;
            }
            if (mPointerDown)
            {
                if (e.Pointer.PointerId != mPointerID)
                {
                    return true;
                }
                // GetIntermediatePoints returns in REVERSE order.
                IList<PointerPoint> cnIntermediatePoints = e.GetIntermediatePoints(mViewerCanvas);
                for (int i = cnIntermediatePoints.Count - 1; i >= 0; i--)
                {
                    m_cnCurrentPoint = cnIntermediatePoints[i].Position;

                    // want to make sure we're within our bounding box.
                    m_cnPt2.x = m_cnCurrentPoint.X;
                    m_cnPt2.y = m_cnCurrentPoint.Y;
                    double halfThickness = mDrawThickness / 2;
                    if (mPageCropOnClient != null)
                    {
                        m_cnPt2.x = Math.Max(m_cnPt2.x, mPageCropOnClient.x1 + halfThickness);
                        m_cnPt2.x = Math.Min(m_cnPt2.x, mPageCropOnClient.x2 - halfThickness);
                        m_cnPt2.y = Math.Max(m_cnPt2.y, mPageCropOnClient.y1 + halfThickness);
                        m_cnPt2.y = Math.Min(m_cnPt2.y, mPageCropOnClient.y2 - halfThickness);
                    }

                    // Sometimes, we will get 2 point in the same, or at least a very similar position. Also, if we're out of bounds, this can happen.
                    if (Math.Abs(m_cnPt2.x - mLastPoint.X) < 0.1 && Math.Abs(m_cnPt2.y - mLastPoint.Y) < 0.1)
                    {
                        continue;
                    }

                    if (mIsStylus)
                    {
                        AddPointToPath(new UIPoint(m_cnPt2.x, m_cnPt2.y), false);
                    }
                    else 
                    {
                        if (!mStartedPathSmoothing)
                        {
                            mStartedPathSmoothing = true;
                            mOnePointBack = new UIPoint(m_cnPt2.x, m_cnPt2.y);
                        }
                        else
                        {
                            UIPoint newPoint = UtilityFunctions.GetPointCloserToLine(mTwoPointsBack, new UIPoint(m_cnPt2.x, m_cnPt2.y), mOnePointBack);
                            mTwoPointsBack.X = mOnePointBack.X;
                            mTwoPointsBack.Y = mOnePointBack.Y;
                            mOnePointBack.X = m_cnPt2.x;
                            mOnePointBack.Y = m_cnPt2.y;

                            AddPointToPath(newPoint);
                        }
                    }
                }

                if (!mStartDrawing) // We don't want to start drawing until we have a bit of a distance
                {
                    CheckIfShouldStartDrawing();
                }

                if (mStartDrawing)
                {
                    (mShape.Data as PathGeometry).Figures = mPathPoints;
                }
            }
            return false;
        }

        protected void CheckIfShouldStartDrawing()
        {
            // wait until you're at least START_DRAWING_THRESHOLD distance from the start
            // unless we're in multi stroke mode
            if (mMultiStrokeMode
                || (Math.Abs(m_cnPt2.x - m_cnPt1.x) > START_DRAWING_THRESHOLD)
                || (Math.Abs(m_cnPt2.y - m_cnPt1.y) > START_DRAWING_THRESHOLD))
            {
                mStartDrawing = true;
                if (!mShapeHasBeenCreated) // path is only created once. Subsequent draws add to the PathfigureCollection
                {
                    mShapeHasBeenCreated = true;

                    mViewerCanvas.Children.Add(this);
                    this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
                    this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
                    this.Width = Math.Abs(mPageCropOnClient.Width());
                    this.Height = Math.Abs(mPageCropOnClient.Height());
                    this.Opacity = mOpacity;

                    // create shape
                    PathGeometry geom = new PathGeometry();
                    mShape = new Path();
                    mShape.StrokeLineJoin = PenLineJoin.Round;
                    mShape.StrokeEndLineCap = PenLineCap.Round;
                    mShape.StrokeStartLineCap = PenLineCap.Round;
                    mShape.Data = geom;
                    mShape.StrokeThickness = mDrawThickness;
                    mShape.Stroke = mStrokeBrush;
                    this.Children.Add(mShape);
                }
            }
        }

        protected void AddPointToPath(UIPoint cnPoint, bool smooth = true)
        {
            // add to points going to PDF
            mCurrentStrokeList.Add(new Point(cnPoint.X - mPageCropOnClient.x1, cnPoint.Y - mPageCropOnClient.y1));

            if (smooth)
            {
                // Add a quadratic bezier curve to make for a smoother ink.
                QuadraticBezierSegment qbc = new QuadraticBezierSegment();
                double oX = mLastPoint.X - mPageCropOnClient.x1;
                double oY = mLastPoint.Y - mPageCropOnClient.y1;
                double nX = cnPoint.X - mPageCropOnClient.x1;
                double nY = cnPoint.Y - mPageCropOnClient.y1;

                qbc.Point1 = new UIPoint(oX, oY);
                qbc.Point2 = new UIPoint((oX + nX) / 2, (oY + nY) / 2);
                mCurrentPathFigure.Segments.Add(qbc);
            }
            else
            {
                LineSegment segment = new LineSegment();
                segment.Point = new UIPoint(cnPoint.X - mPageCropOnClient.x1, cnPoint.Y - mPageCropOnClient.y1);
                mCurrentPathFigure.Segments.Add(segment);
            }

            mLastPoint = new UIPoint(cnPoint.X, cnPoint.Y);

            // Update bounds
            if (cnPoint.X < mLeftmost)
            {
                mLeftmost = cnPoint.X;
            }
            if (cnPoint.X > mRightmost)
            {
                mRightmost = cnPoint.X;
            }
            if (cnPoint.Y < mTopmost)
            {
                mTopmost = cnPoint.Y;
            }
            if (cnPoint.Y > mBottommost)
            {
                mBottommost = cnPoint.Y;
            }
        }


        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            if (IsUsingSylus && e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
            {
                return true;
            }
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                return false; // ignore any secondary pointer
            }
            if (e.Pointer.PointerId == mPointerID)
            {
                mPointerDown = false;

                if (!mIsStylus)
                {
                    m_cnPt2.x = m_cnCurrentPoint.X;
                    m_cnPt2.y = m_cnCurrentPoint.Y;
                    double halfThickness = mDrawThickness / 2;
                    if (mPageCropOnClient != null)
                    {
                        m_cnPt2.x = Math.Max(m_cnPt2.x, mPageCropOnClient.x1 + halfThickness);
                        m_cnPt2.x = Math.Min(m_cnPt2.x, mPageCropOnClient.x2 - halfThickness);
                        m_cnPt2.y = Math.Max(m_cnPt2.y, mPageCropOnClient.y1 + halfThickness);
                        m_cnPt2.y = Math.Min(m_cnPt2.y, mPageCropOnClient.y2 - halfThickness);
                    }
                    if (!(Math.Abs(m_cnPt2.x - mLastPoint.X) < 0.1 && Math.Abs(m_cnPt2.y - mLastPoint.Y) < 0.1))
                    {
                        {
                            AddPointToPath(new UIPoint(m_cnPt2.x, m_cnPt2.y));
                        }
                    }
                    else
                    {
                        AddPointToPath(new UIPoint(m_cnPt2.x + 0.5, m_cnPt2.y + 0.5));
                    }
                }

                if (!mMultiStrokeMode)
                {
                    if (mStartDrawing) // don't want to add any annotations resulting from a tap
                    {
                        EnableScrolling();
                        Finish();

                        mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
                        mDelayedRemovalSet = true;

                        EndCurrentTool(ToolType.e_pan);
                    }
                }
                else
                {
                    if (StrokeAdded != null)
                    {
                        StrokeAdded();
                    }
                    mListOfUndoneStrokes.Clear();
                    mListOfUndonePathFigures.Clear();
                }
                mCurrentPathFigure = null;
                mPointerID = -1;
                mViewerCanvas.ReleasePointerCaptures();
                if (mIsInTimedMode)
                {
                    StartTimer();
                }
            }
            return false;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (!mMultiStrokeMode)
            {
                base.TappedHandler(sender, e);
            }
            return true;
        }


        internal override void Finish()
        {
            try
            {
                mPDFView.DocLock(true);
                double halfThickness = mDrawThickness / 2;
                m_cnPt1.x = mLeftmost - halfThickness;
                m_cnPt1.y = mTopmost - halfThickness;
                m_cnPt2.x = mRightmost + halfThickness;
                m_cnPt2.y = mBottommost + halfThickness;
                Rect rect = GetShapeBBox();
                if (rect != null)
                {
                    pdftron.PDF.Annots.Ink ink = pdftron.PDF.Annots.Ink.Create(mPDFView.GetDoc().GetSDFDoc(), rect);

                    AnnotBorderStyle bs = ink.GetBorderStyle();
                    bs.width = mStrokeThickness;
                    ink.SetBorderStyle(bs); 

                    // Shove the points into the Doc
                    int i = 0;
                    pdftron.Common.DoubleRef xpos = new pdftron.Common.DoubleRef(0);
                    pdftron.Common.DoubleRef ypos = new pdftron.Common.DoubleRef(0);
                    Point pdfp = new Point();
                    double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                    double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
                    foreach (List<Point> pointList in mListOfStrokeLists)
                    {
                        int j = 0;
                        foreach (Point p in pointList)
                        {
                            xpos.Value = p.x + mPageCropOnClient.x1 - sx;
                            ypos.Value = p.y + mPageCropOnClient.y1 - sy;
                            mPDFView.ConvScreenPtToPagePt(xpos, ypos, mDownPageNumber);
                            pdfp.x = xpos.Value;
                            pdfp.y = ypos.Value;
                            ink.SetPoint(i, j, pdfp);
                            j++;
                        }
                        i++;
                    }

                    SetStyle(ink);

                    if (mIsStylus)
                    {
                        ink.SetSmoothing(false);
                    }

                    ink.RefreshAppearance();

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    page.AnnotPushBack(ink);

                    mAnnotPageNum = mDownPageNumber;
                    mAnnot = ink;
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                    mToolManager.RaiseAnnotationAddedEvent(mAnnot);
                }
            }
            catch (System.Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }
        }


        #region MultiStrokeToolInterface

        /// <summary>
        /// Saves the ink annotation to the PDF
        /// </summary>
        public void CommitAnnotation()
        {
            if (mStartDrawing) // don't want to add any annotations resulting from a tap
            {
                EnableScrolling();
                Finish();
            }
            mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
            mDelayedRemovalSet = true;
            EndCurrentTool(ToolType.e_pan);
        }

        /// <summary>
        /// Undo the last stroke
        /// </summary>
        public void UndoStroke()
        {
            try
            {
                if (mPathPoints.Count > 0)
                {
                    mListOfUndonePathFigures.Add(mPathPoints[mPathPoints.Count - 1]);
                    mPathPoints.RemoveAt(mPathPoints.Count - 1);
                    (mShape.Data as PathGeometry).Figures = mPathPoints;
                }
                if (mListOfStrokeLists.Count > 0)
                {
                    mListOfUndoneStrokes.Add(mListOfStrokeLists[mListOfStrokeLists.Count - 1]);
                    mListOfStrokeLists.RemoveAt(mListOfStrokeLists.Count - 1);
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Error", "FreeHandCreate.cs->UndoStroke: " + e.ToString());
            }
        }

        /// <summary>
        /// Redo the last undone stroke
        /// </summary>
        public void RedoStroke()
        {
            try
            {
                if (mListOfUndonePathFigures.Count > 0)
                {
                    mPathPoints.Add(mListOfUndonePathFigures[mListOfUndonePathFigures.Count - 1]);
                    mListOfUndonePathFigures.RemoveAt(mListOfUndonePathFigures.Count - 1);
                    (mShape.Data as PathGeometry).Figures = mPathPoints;
                }
                if (mListOfUndoneStrokes.Count > 0)
                {
                    mListOfStrokeLists.Add(mListOfUndoneStrokes[mListOfUndoneStrokes.Count - 1]);
                    mListOfUndoneStrokes.RemoveAt(mListOfUndoneStrokes.Count - 1);
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Error", "FreeHandCreate.cs->RedoStroke: " + e.ToString());
            }
        }

        /// <summary>
        /// Returns true if it is possible to undo a stoke
        /// </summary>
        public bool CanUndoStroke()
        {
            return mPathPoints.Count > 0;
        }

        /// <summary>
        /// Returns true if it is possible to redo a stroke
        /// </summary>
        public bool CanRedoStroke()
        {
            return mListOfUndonePathFigures.Count > 0;
        }

        /// <summary>
        /// Lets you change the brush properties while between strokes, though it will change past strokes as well.
        /// </summary>
        /// <param name="thickness"></param>
        /// <param name="opacity"></param>
        /// <param name="color"></param>
        public void SetInkProperties(double thickness, double opacity, Windows.UI.Color color)
        {
            mStrokeThickness = thickness;
            mDrawThickness = mStrokeThickness * mZoomLevel;

            mOpacity = opacity;
            this.Opacity = opacity;

            mStrokeBrush = new SolidColorBrush(color);

            if (mShape != null)
            {
                mShape.StrokeThickness = mDrawThickness;
                mShape.Stroke = mStrokeBrush;
            }
        }

        #endregion MultiStrokeToolInterface



        #region Timer specific utilities

        private void StartTimer()
        {
            if (mCompletionTimer == null && mToolManager.INK_TIME_BEFORE_INK_SAVES_ANNOTATION_IN_MILLISECONDS > 0)
            {
                mCompletionTimer = new DispatcherTimer();
                mCompletionTimer.Interval = TimeSpan.FromMilliseconds(mToolManager.INK_TIME_BEFORE_INK_SAVES_ANNOTATION_IN_MILLISECONDS);
                mCompletionTimer.Tick += mCompletionTimer_Tick;
            }
            mCompletionTimer.Stop();
            mCompletionTimer.Start();
        }

        private void StopTimer()
        {
            if (mCompletionTimer != null)
            {
                mCompletionTimer.Stop();
            }
        }

        private void EndTimer()
        {
            if (mCompletionTimer != null)
            {
                mCompletionTimer.Stop();
                mCompletionTimer = null;

                if (mShouldSaveInk)
                {
                    Finish();
                }

                mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
                mDelayedRemovalSet = true;
            }
        }

        void mCompletionTimer_Tick(object sender, object e)
        {
            mToolManager.CreateTool(ToolType.e_pan, null);
        }



        #endregion Timer specific utilities

    }
}

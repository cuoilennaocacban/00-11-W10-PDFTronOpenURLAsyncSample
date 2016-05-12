using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    class Eraser : SimpleShapeCreate
    {
        private bool mShapeHasBeenCreated = false;
        Path mShape;

        protected bool mIsErasing = true;
        protected double mEraseHalfThickness = 10.0;
        protected Dictionary<SDF.Obj, Annots.Ink> mInkMap = null;
        protected List<PDFPoint> mEraseStrokeList = null;
        protected List<PDFPoint> mPointList = null;

        // update UI
        private Windows.UI.Color m_CurrentDrawingColor = Windows.UI.Colors.LightGray;
        protected UIPoint mLastPoint;

        private bool should_update = false;

        protected PathFigureCollection mPathPoints;
        protected PathFigure mCurrentPathFigure;
        protected bool mFirstTime = false;

        public Eraser(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_ink_eraser;
            mToolMode = ToolType.e_ink_eraser;

            mInkMap = new Dictionary<SDF.Obj, Annots.Ink>();
            mEraseStrokeList = new List<PDFPoint>();
            mPointList = new List<PDFPoint>();
        }

        public void SetEraserHalfThickness(double thickness)
        {
            mEraseHalfThickness = thickness;
        }

        public double GetEraserHalfThickness()
        {
            return mEraseHalfThickness;
        }

        internal override bool PointerPressedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(mPDFView);
            UIPoint screenPoint = pointerPoint.Position;

            base.PointerPressedHandler(sender, e);

            if (mSimpleShapeCanceledAtPress)
            {
                return false;
            }

            double halfThickness = mEraseHalfThickness * mPDFView.GetZoom();
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

            mEraseStrokeList.Add(new Point(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset()));
            mPointList.Add(new Point(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset()));

            // update UI
            if (!mFirstTime)
            {
                mFirstTime = true;
                mPathPoints = new PathFigureCollection();
            }
            mCurrentPathFigure = new PathFigure();
            mCurrentPathFigure.StartPoint = new UIPoint(m_cnPt1.x - mPageCropOnClient.x1, m_cnPt1.y - mPageCropOnClient.y1);
            mPathPoints.Add(mCurrentPathFigure);

            mLastPoint = new UIPoint(m_cnPt1.x, m_cnPt1.y);

            return true;
        }

        internal override bool PointerMovedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (mPointerDown)
            {
                if (e.Pointer.PointerId != mPointerID)
                {
                    return true;
                }

                // update PDF               

                IList<PointerPoint> intermediatePoints = e.GetIntermediatePoints(mViewerCanvas);
                for (int i = intermediatePoints.Count - 1; i >= 0; i--)
                {
                    UIPoint currentPoint = intermediatePoints[i].Position;

                    // want to make sure we're within our bounding box.
                    m_cnPt2.x = currentPoint.X;
                    m_cnPt2.y = currentPoint.Y;
                    double halfThickness = mEraseHalfThickness * mPDFView.GetZoom();
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

                    LineSegment segment = new LineSegment();
                    segment.Point = new UIPoint(m_cnPt2.x - mPageCropOnClient.x1, m_cnPt2.y - mPageCropOnClient.y1);
                    mCurrentPathFigure.Segments.Add(segment);

                    mEraseStrokeList.Add(new Point(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset()));
                    mPointList.Add(new Point(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset()));

                    mLastPoint = new UIPoint(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset(), m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset());
                }

                try
                {
                    mPDFView.DocLock(true);

                    for (int i = 0; i < mEraseStrokeList.Count - 1; ++i)
                    {
                        PDFPoint ppt1 = mEraseStrokeList[i];
                        PDFPoint ppt2 = mEraseStrokeList[i + 1];

                        Point pdfp1 = new Point();
                        Point pdfp2 = new Point();
                        pdftron.Common.DoubleRef xpos = new pdftron.Common.DoubleRef(0);
                        pdftron.Common.DoubleRef ypos = new pdftron.Common.DoubleRef(0);
                        xpos.Value = ppt1.x;
                        ypos.Value = ppt1.y;
                        mPDFView.ConvScreenPtToPagePt(xpos, ypos, mDownPageNumber);
                        pdfp1.x = xpos.Value;
                        pdfp1.y = ypos.Value;
                        xpos = new pdftron.Common.DoubleRef(0);
                        ypos = new pdftron.Common.DoubleRef(0);
                        xpos.Value = ppt2.x;
                        ypos.Value = ppt2.y;
                        mPDFView.ConvScreenPtToPagePt(xpos, ypos, mDownPageNumber);
                        pdfp2.x = xpos.Value;
                        pdfp2.y = ypos.Value;

                        Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                        int annot_num = page.GetNumAnnots();
                        for (int j = annot_num - 1; j >= 0; --j)
                        {
                            IAnnot annot = page.GetAnnot(j);
                            if (annot == null || !annot.IsValid()) continue;
                            if (annot.GetAnnotType() == AnnotType.e_Ink)
                            {
                                pdftron.PDF.Annots.Ink ink = new pdftron.PDF.Annots.Ink(annot.GetSDFObj());

                                if (ink.Erase(pdfp1, pdfp2, mEraseHalfThickness))
                                {
                                    var comparer = new ObjSpecialComparer();
                                    if (!mInkMap.Keys.Contains(ink.GetSDFObj(), comparer))
                                    {
                                        mInkMap.Add(ink.GetSDFObj(), ink);
                                    }
                                }
                            }
                        }

                    }

                }
                catch (System.Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
                PDFPoint prevPoint = mEraseStrokeList[mEraseStrokeList.Count - 1];
                mEraseStrokeList.Clear();
                mEraseStrokeList.Add(prevPoint);

                // update UI
                if (!mStartDrawing) // Simple Shape is ready to draw
                {
                    // wait until you're at least START_DRAWING_THRESHOLD distance from the start
                    if ((Math.Abs(m_cnPt2.x - m_cnPt1.x) > START_DRAWING_THRESHOLD)
                        || (Math.Abs(m_cnPt2.y - m_cnPt1.y) > START_DRAWING_THRESHOLD))
                    {
                        mStartDrawing = true;

                        var color = m_CurrentDrawingColor;
                        var size = mEraseHalfThickness * 2 * mPDFView.GetZoom();

                        if (!mShapeHasBeenCreated)
                        {
                            mShapeHasBeenCreated = true;

                            mViewerCanvas.Children.Add(this);
                            this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
                            this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
                            this.Width = Math.Abs(mPageCropOnClient.Width());
                            this.Height = Math.Abs(mPageCropOnClient.Height());

                            PathGeometry geom = new PathGeometry();
                            mShape = new Path();
                            mShape.StrokeLineJoin = PenLineJoin.Round;
                            mShape.StrokeEndLineCap = PenLineCap.Round;
                            mShape.StrokeStartLineCap = PenLineCap.Round;
                            mShape.Data = geom;
                            mShape.StrokeThickness = size;
                            mShape.Stroke = new SolidColorBrush(color);
                            mShape.Opacity = 0.7;                            
                            this.Children.Add(mShape);
                        }
                    }
                }

                if (mStartDrawing)
                {
                    (mShape.Data as PathGeometry).Figures = mPathPoints;
                }
            }

            return false;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                return false; // ignore any secondary pointer
            }

            if (e.Pointer.PointerId == mPointerID)
            {
                try
                {
                    mPDFView.DocLock(true);
                    Page page;
                    if (mPointList.Count == 1 && mEraseStrokeList.Count == 1)
                    {

                        PDFPoint ppt1 = mEraseStrokeList[0];

                        Point pdfp1 = new Point();
                        pdftron.Common.DoubleRef xpos = new pdftron.Common.DoubleRef(0);
                        pdftron.Common.DoubleRef ypos = new pdftron.Common.DoubleRef(0);
                        xpos.Value = ppt1.x;
                        ypos.Value = ppt1.y;
                        mPDFView.ConvScreenPtToPagePt(xpos, ypos, mDownPageNumber);
                        pdfp1.x = xpos.Value;
                        pdfp1.y = ypos.Value;

                        page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                        int annot_num = page.GetNumAnnots();
                        for (int j = annot_num - 1; j >= 0; --j)
                        {
                            IAnnot annot = page.GetAnnot(j);
                            if (annot == null || !annot.IsValid()) continue;
                            if (annot.GetAnnotType() == AnnotType.e_Ink)
                            {
                                pdftron.PDF.Annots.Ink ink = new pdftron.PDF.Annots.Ink(annot.GetSDFObj());

                                if (ink.Erase(pdfp1, pdfp1, mEraseHalfThickness))
                                {
                                    var comparer = new ObjSpecialComparer();
                                    if (!mInkMap.Keys.Contains(ink.GetSDFObj(), comparer))
                                    {
                                        mInkMap.Add(ink.GetSDFObj(), ink);
                                    }
                                }
                            }
                        }
                    }

                    // update PDF
                    page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    foreach (KeyValuePair<SDF.Obj, Annots.Ink> entry in mInkMap)
                    {

                        Annots.Ink ink = entry.Value;
                        Rect bbox = ink.GetRect();

                        Rect bbox_new = ConvertFromPageRectToScreenRect(bbox, mDownPageNumber);

                        bool annotRemoved = true;
                        for (int i = 0; i < ink.GetPathCount(); i++)
                        {
                            if (ink.GetPointCount(i) > 0)
                            {
                                annotRemoved = false;
                                break;
                            }
                        }
                        if (annotRemoved)
                        {
                            page.AnnotRemove(ink);
                            mToolManager.RaiseAnnotationRemovedEvent(ink);
                        }
                        else
                        {
                            try
                            {
                                ink.RefreshAppearance();
                                mToolManager.RaiseAnnotationEditedEvent(ink);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                            }
                        }

                        mPDFView.Update(bbox_new);
                        should_update = true;

                    }

                    mEraseStrokeList.Clear();
                    mInkMap.Clear();
                    mPointList.Clear();

                }
                catch (System.Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
            }

            mPointerDown = false;

            if (mStartDrawing) // don't want to add any annotations resulting from a tap
            {
                EnableScrolling();
                Finish();

                if (should_update)
                {
                    mToolManager.mDelayRemoveTimers.Add(new DelayRemoveTimer(mViewerCanvas, this, this, mPDFView, mToolManager));
                    mDelayedRemovalSet = true;
                    should_update = false;
                }

                EndCurrentTool(ToolType.e_pan);
            }


            mPointerID = -1;
            mViewerCanvas.ReleasePointerCaptures();
            return false;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            return true;
        }

        internal override void Finish()
        {

        }

        #region HelperFunctions

        public class ObjSpecialComparer : IEqualityComparer<SDF.Obj>
        {
            public bool Equals(SDF.Obj x, SDF.Obj y)
            {
                return x.IsEqual(y);
            }
            public int GetHashCode(SDF.Obj x)
            {
                return x.GetHashCode();
            }
        }

        private double Distance(double x1, double y1, double x2, double y2)
        {
            double d = 0;
            d = Math.Sqrt(Math.Pow((x2 - x1), 2) + Math.Pow((y2 - y1), 2));
            return d;
        }

        private Rect GetRectFromPoints(List<PDFPoint> pointsList)
        {
            double x1 = pointsList[0].x;
            double y1 = pointsList[0].y;
            double x2 = pointsList[0].x;
            double y2 = pointsList[0].y;

            foreach (PDFPoint pt in pointsList)
            {
                if (pt.x < x1 && pt.x > 0)
                {
                    x1 = pt.x;
                }
                if (pt.y < y1 && pt.y > 0)
                {
                    y1 = pt.y;
                }
                if (pt.x > x2 && pt.x > 0)
                {
                    x2 = pt.x;
                }
                if (pt.y > y2 && pt.y > 0)
                {
                    y2 = pt.y;
                }
            }
            Rect r = new Rect(x1, y1, x2, y2);
            return r;
        }
        #endregion

    }
}

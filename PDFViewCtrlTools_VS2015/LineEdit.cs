using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;
using UIPopup = Windows.UI.Xaml.Controls.Primitives.Popup;
using UILine = Windows.UI.Xaml.Shapes.Line;

using pdftron.PDF;
using pdftron.Common;
using pdftron.PDF.Annots;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using System.Diagnostics;


namespace pdftron.PDF.Tools
{
    class LineEdit : AnnotEdit
    {
        protected Path mBaseEllipse;
        protected Path mTipEllipse;
        protected Path mLineShape;

        protected TranslateTransform mBaseEllipseTransform;
        protected TranslateTransform mTipEllipseTransform;
        protected CompositeTransform mLineShapeTransform;

        protected const int E_BASE = 0;
        protected const int E_TIP = 1;
        protected const int E_LINE = 2;

        protected pdftron.PDF.Annots.Line mLine;

        protected PDFPoint mBasePoint;
        protected PDFPoint mTipPoint;
        protected PDFPoint mOldPoint;
        protected PDFPoint mOldBase;
        protected PDFPoint mOldTip;

        protected SolidColorBrush mWidgetBrush;
        protected double mLineHalfThickness;
        protected Rect mUpdateRect;

        protected double mStrokeThickness = 1;


        public LineEdit(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mToolMode = ToolType.e_line_edit;
            mNextToolMode = ToolType.e_line_edit;

            mWidgetBrush = new SolidColorBrush(Colors.Black);
            mCtrlPointRadius = 12.5;
        }


        internal override void OnCreate()
        {
            base.OnCreate();

            CONTROL_POINT_FOR_MOVING = 2;

            mLine = (pdftron.PDF.Annots.Line)mAnnot;

            // Get the thickness of the line
            AnnotBorderStyle bs = mLine.GetBorderStyle();
            mLineHalfThickness = bs.width / 2;
            mStrokeThickness = bs.width;
            mOldPoint = new PDFPoint(0, 0);
            mOldBase = new PDFPoint(0, 0);
            mOldTip = new PDFPoint(0, 0);     


            foreach (Path p in mControlPointShapes)
            {
                p.Visibility = Visibility.Collapsed;
            }
            mAnnotBBoxRectangle.Visibility = Visibility.Collapsed;

            mLineShape = new Path();
            mBaseEllipse = new Path();
            mTipEllipse = new Path();

            mBaseEllipseTransform = new TranslateTransform();
            mTipEllipseTransform = new TranslateTransform();
            mLineShapeTransform = new CompositeTransform();

            double strokeThickenss = 2;
            double circum = mCtrlPointRadius * 2 * Math.PI;
            double rawDashLength = circum / (13); // With 12, the fist and last merge for some reason
            double dashLength = rawDashLength / strokeThickenss;

            LineGeometry lineGeom = new LineGeometry();
            lineGeom = new LineGeometry();
            lineGeom.StartPoint = new UIPoint(0, 0);
            lineGeom.EndPoint = new UIPoint(1, 0);
            mLineShape.Data = lineGeom;
            mLineShape.StrokeLineJoin = PenLineJoin.Miter;
            mLineShape.StrokeMiterLimit = 1;
            mLineShape.StrokeThickness = 3;
            mLineShape.Stroke = mCtrlPointFillBrush;
            mLineShape.RenderTransform = mLineShapeTransform;
            mLineShape.StrokeDashArray = new DoubleCollection() { 2.0, 2.0 };
            mLineShape.Visibility = Visibility.Collapsed;
            this.Children.Add(mLineShape);

            EllipseGeometry geom = new EllipseGeometry();
            geom.RadiusX = mCtrlPointRadius;
            geom.RadiusY = mCtrlPointRadius;
            mBaseEllipse.Data = geom;
            mBaseEllipse.Stroke = mCtrlPointBorderBrush;
            mBaseEllipse.StrokeThickness = mCtrlPointBorderThickness;
            mBaseEllipse.Fill = mCtrlPointFillBrush;
            mBaseEllipse.RenderTransform = mBaseEllipseTransform;
            this.Children.Add(mBaseEllipse);

            geom = new EllipseGeometry();
            geom.RadiusX = mCtrlPointRadius;
            geom.RadiusY = mCtrlPointRadius;
            mTipEllipse.Data = geom;
            mTipEllipse.Stroke = mCtrlPointBorderBrush;
            mTipEllipse.StrokeThickness = mCtrlPointBorderThickness;
            mTipEllipse.Fill = mCtrlPointFillBrush;
            mTipEllipse.RenderTransform = mTipEllipseTransform;
            this.Children.Add(mTipEllipse);
        }


        protected override void CreateSelectionAppearance()
        {
            mBaseEllipseTransform.X = mBasePoint.x - mPageCropOnClient.x1;
            mBaseEllipseTransform.Y = mBasePoint.y - mPageCropOnClient.y1;

            mTipEllipseTransform.X = mTipPoint.x - mPageCropOnClient.x1;
            mTipEllipseTransform.Y = mTipPoint.y - mPageCropOnClient.y1;

            mLineShapeTransform.TranslateX = mBasePoint.x - mPageCropOnClient.x1;
            mLineShapeTransform.TranslateY = mBasePoint.y - mPageCropOnClient.y1;

            double length = Math.Sqrt((mBasePoint.x - mTipPoint.x) * (mBasePoint.x - mTipPoint.x) + (mBasePoint.y - mTipPoint.y) * (mBasePoint.y - mTipPoint.y));
            LineGeometry lg = mLineShape.Data as LineGeometry;
            lg.EndPoint = new UIPoint(length, 0);

            double xDist = (mTipPoint.x - mBasePoint.x);
            double yDist = (mTipPoint.y - mBasePoint.y);

            if (Math.Abs(xDist) <= 0.001)
            {
                if (yDist < 0)
                {
                    mLineShapeTransform.Rotation = 90;
                }
                else
                {
                    mLineShapeTransform.Rotation = -90;
                }
            }
            else
            {
                double angle = Math.Atan(yDist / xDist) * 180 / Math.PI;
                if (xDist < 0)
                {
                    angle += 180;
                }
                mLineShapeTransform.Rotation = angle;
            }
        }

        protected override void SetPosition()
        {
            base.SetPosition();
            mBasePoint = mLine.GetStartPoint();
            mTipPoint = mLine.GetEndPoint();

            PDFDouble x1 = new PDFDouble(mBasePoint.x);
            PDFDouble y1 = new PDFDouble(mBasePoint.y);
            PDFDouble x2 = new PDFDouble(mTipPoint.x);
            PDFDouble y2 = new PDFDouble(mTipPoint.y);

            mPDFView.ConvPagePtToScreenPt(x1, y1, mAnnotPageNum);
            mPDFView.ConvPagePtToScreenPt(x2, y2, mAnnotPageNum);

            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

            mBasePoint.x = x1.Value + sx;
            mBasePoint.y = y1.Value + sy;
            mTipPoint.x = x2.Value + sx;
            mTipPoint.y = y2.Value + sy;

            CreateSelectionAppearance();
        }

        protected override void GetControlPoint(UIPoint p)
        {
            double x = p.X;
            double y = p.Y;
            mEffectiveCtrlPoint = -1;

            // see if we're close to a ctrl point
            double thresh = Math.Pow(mCtrlPointRadius * 2.25, 2); // cheaper than sqrt
            double shortest_dist = thresh;

            double dist = (x - mBasePoint.x) * (x - mBasePoint.x) + (y - mBasePoint.y) * (y - mBasePoint.y);
            if (dist <= shortest_dist)
            {
                mEffectiveCtrlPoint = E_BASE;
                mOldPoint.x = mBasePoint.x;
                mOldPoint.y = mBasePoint.y;
            }

            dist = (x - mTipPoint.x) * (x - mTipPoint.x) + (y - mTipPoint.y) * (y - mTipPoint.y);
            if (dist <= shortest_dist)
            {
                mEffectiveCtrlPoint = E_TIP;
                mOldPoint.x = mTipPoint.x;
                mOldPoint.y = mTipPoint.y;
            }

            // Check if we have hit the line
            if (mEffectiveCtrlPoint < 0)
            {
                if (PointToLineDistance(x, y))
                {
                    mEffectiveCtrlPoint = E_LINE;
                    mOldTip.x = mTipPoint.x;
                    mOldTip.y = mTipPoint.y;
                    mOldBase.x = mBasePoint.x;
                    mOldBase.y = mBasePoint.y;
                }
            }

            // means we're manipulating the widget
            if (mEffectiveCtrlPoint >= 0)
            {
                mPt1.x = x;
                mPt1.y = y;
                mIsManipulated = true;
                mUpdateRect = new Rect(mBasePoint.x, mBasePoint.y, mTipPoint.x, mTipPoint.y);
            }


        }


        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            base.PointerMovedHandler(sender, e);

            if (!mIsManipulated || !mStartMoving)
            {
                return false;
            }

            mLineShape.Visibility = Visibility.Visible;
            switch (mEffectiveCtrlPoint)
            {
                case E_BASE:
                    mBasePoint.x = mOldPoint.x + mPt2.x - mPt1.x;
                    mBasePoint.y = mOldPoint.y + mPt2.y - mPt1.y;
                    CheckBounds(mBasePoint);
                    break;

                case E_TIP:
                    mTipPoint.x = mOldPoint.x + mPt2.x - mPt1.x;
                    mTipPoint.y = mOldPoint.y + mPt2.y - mPt1.y;
                    CheckBounds(mTipPoint);
                    break;

                case E_LINE:
                    mBasePoint.x = mOldBase.x + mPt2.x - mPt1.x;
                    mBasePoint.y = mOldBase.y + mPt2.y - mPt1.y;
                    mTipPoint.x = mOldTip.x + mPt2.x - mPt1.x;
                    mTipPoint.y = mOldTip.y + mPt2.y - mPt1.y;
                    CheckBounds();
                    break;
            }

            CreateSelectionAppearance();

            return true;
        }


        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            base.PointerReleasedHandler(sender, e);
            if (!mStartMoving)
            {
                if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
                {
                    PositionMenu(mCommandMenu);
                    ShowMenu(mCommandMenu);
                }
                return false;
            }

            mLineShape.Visibility = Visibility.Collapsed;
            try
            {
                mPDFView.DocLock(true);

                // Get the two endpoints in page space.
                double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

                PDFDouble x1 = new PDFDouble(mBasePoint.x - sx);
                PDFDouble y1 = new PDFDouble(mBasePoint.y - sy);
                PDFDouble x2 = new PDFDouble(mTipPoint.x - sx);
                PDFDouble y2 = new PDFDouble(mTipPoint.y - sy);

                mPDFView.ConvScreenPtToPagePt(x1, y1, mAnnotPageNum);
                mPDFView.ConvScreenPtToPagePt(x2, y2, mAnnotPageNum);

                // Create a rectangle that will fit the 2 end points, as well as any ending style.
                Rect newAnnotRect = new Rect(x1.Value, y1.Value, x2.Value, y2.Value);
                newAnnotRect.Normalize();
                newAnnotRect.Inflate(mStrokeThickness + GetLineEndingLength(mStrokeThickness));

                mAnnot.Resize(newAnnotRect);

                mLine.SetStartPoint(new PDFPoint(x1.Value, y1.Value));
                mLine.SetEndPoint(new PDFPoint(x2.Value, y2.Value));

                mAnnot.RefreshAppearance();
                mPDFView.UpdateWithAnnot(mLine, mAnnotPageNum);
                mToolManager.RaiseAnnotationEditedEvent(mLine);

                // mUpdateRect rect was set when we determined control point, inflate it like we do the bounding box
                mUpdateRect.Set(mUpdateRect.x1 - sx, mUpdateRect.y1 - sy, mUpdateRect.x2 - sx, mUpdateRect.y2 - sy);
                mUpdateRect.Normalize();
                mUpdateRect.Inflate((mStrokeThickness + GetLineEndingLength(mStrokeThickness)) * mPDFView.GetZoom());

                mPDFView.Update(mUpdateRect);
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            mIsShowingCommandMenu = true;
            PositionMenu(mCommandMenu);
            ShowMenu(mCommandMenu);

            mStartMoving = false;
            mIsManipulated = false;

            return true;
        }


        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool) // this should be the only entry point into the tool
            {
                mJustSwitchedFromAnotherTool = false;
                if (mAnnot == null)
                {
                    return true;
                }
                SetPosition();

                mIsShowingCommandMenu = true;
                PositionMenu(mCommandMenu);
                ShowMenu(mCommandMenu);
                return true;
            }

            if (mBoxPopup != null) // e.g. color picker is open
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                EnableScrolling();
            }
            else
            {
                UIPoint point = e.GetPosition(mViewerCanvas);
                if (PointToLineDistance(point.X, point.Y))
                {
                    mIsShowingCommandMenu = true;
                    PositionMenu(mCommandMenu);
                    ShowMenu(mCommandMenu);
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                }
            }
            return true;
        }

        /// <summary>
        /// Makes sure the point is within the page boundaries.
        /// </summary>
        /// <param name="point">The point we want to check</param>
        protected void CheckBounds(PDFPoint point)
        {
            if (point.x < mPageCropOnClient.x1 + mLineHalfThickness)
            {
                point.x = mPageCropOnClient.x1 + mLineHalfThickness;
            }
            if (point.y < mPageCropOnClient.y1 + mLineHalfThickness)
            {
                point.y = mPageCropOnClient.y1 + mLineHalfThickness;
            }
            if (point.x > mPageCropOnClient.x2 - mLineHalfThickness)
            {
                point.x = mPageCropOnClient.x2 - mLineHalfThickness;
            }
            if (point.y > mPageCropOnClient.y2 - mLineHalfThickness)
            {
                point.y = mPageCropOnClient.y2 - mLineHalfThickness;
            }
        }

        /// <summary>
        /// At this point, we assume the whole line is being dragged around.
        /// </summary>
        protected void CheckBounds()
        {
            double maxX = mBasePoint.x;
            double minX = mTipPoint.x;
            if (mTipPoint.x > mBasePoint.x)
            {
                maxX = mTipPoint.x;
                minX = mBasePoint.x;
            }
            double maxY = mBasePoint.y;
            double minY = mTipPoint.y;
            if (mTipPoint.y > mBasePoint.y)
            {
                maxY = mTipPoint.y;
                minY = mBasePoint.y;
            }

            double shiftX = 0;
            double shiftY = 0;
            if (minX < mPageCropOnClient.x1)
            {
                shiftX = mPageCropOnClient.x1 - minX;
            }
            if (minY < mPageCropOnClient.y1)
            {
                shiftY = mPageCropOnClient.y1 - minY;
            }
            if (maxX > mPageCropOnClient.x2)
            {
                shiftX = mPageCropOnClient.x2 - maxX;
            }
            if (maxY > mPageCropOnClient.y2)
            {
                shiftY = mPageCropOnClient.y2 - maxY;
            }
            mBasePoint.x += shiftX;
            mBasePoint.y += shiftY;
            mTipPoint.x += shiftX;
            mTipPoint.y += shiftY;

        }


        protected bool PointToLineDistance(double x, double y)
        {
            return UtilityFunctions.PointToLineDistance(mBasePoint, mTipPoint, new PDFPoint(x, y)) < (mCtrlPointRadius * mCtrlPointRadius);
        }

        internal override void PositionMenu(PopupCommandMenu m)
        {
            if (mCommandMenu != null)
            {
                PDFRect rect = new PDFRect(mBasePoint.x, mBasePoint.y, mTipPoint.x, mTipPoint.y);
                rect.Normalize();
                m.TargetSquare(rect.x1 - mPDFView.GetAnnotationCanvasHorizontalOffset(), rect.y1 - mPDFView.GetAnnotationCanvasVerticalOffset(), 
                    rect.x2 - mPDFView.GetAnnotationCanvasHorizontalOffset(), rect.y2 - mPDFView.GetAnnotationCanvasVerticalOffset());
            }
        }

        internal override UIRect GetBoxPopupRect()
        {
            UIRect rect = new UIRect();
            rect.X = Math.Min(mBasePoint.x, mTipPoint.x) - mPDFView.GetAnnotationCanvasHorizontalOffset();
            rect.Y = Math.Min(mBasePoint.y, mTipPoint.y) - mPDFView.GetAnnotationCanvasVerticalOffset();
            rect.Width = Math.Abs(mBasePoint.x - mTipPoint.x);
            rect.Height = Math.Abs(mBasePoint.y - mTipPoint.y);
            return rect;
        }
    }
}

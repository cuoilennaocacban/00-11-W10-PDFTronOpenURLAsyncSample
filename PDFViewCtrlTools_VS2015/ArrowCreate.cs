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
    class ArrowCreate : SimpleShapeCreate
    {
        private bool mShapeHasBeenCreated = false;
        private Path mShape;
        private UIPoint aPt1, aPt2, aPt3; // the end points of the shorter lines that form the arrow
        private double mArrowHeadLength;
        private double mCos, mSin;

        public ArrowCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_arrow_create;
            mToolMode = ToolType.e_arrow_create;
            mCos = Math.Cos(3.14159265 / 6); //30 degree
            mSin = Math.Sin(3.14159265 / 6);
            aPt1 = new UIPoint(0, 0);
            aPt2 = new UIPoint(0, 0);
            aPt3 = new UIPoint(0, 0);
        }

        internal override bool PointerPressedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            base.PointerPressedHandler(sender, e);
            if (mSimpleShapeCanceledAtPress)
            {
                return false;
            }
            mShapeHasBeenCreated = false;
            double halfThickness = mDrawThickness / 2;
            mArrowHeadLength = 4 * (Math.Sqrt(mStrokeThickness) + mStrokeThickness) * mZoomLevel; // matches the ones drawn by PDFNet
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
            return false;
        }


        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            // Canvas takes care of mirroring...
            base.PointerMovedHandler(sender, e);
            if (mStartDrawing) // Simple Shape is ready to draw
            {
                if (!mShapeHasBeenCreated)
                {
                    mShapeHasBeenCreated = true;
                    PathGeometry geom = new PathGeometry();
                    mShape = new Path();
                    mShape.StrokeLineJoin = PenLineJoin.Miter;
                    mShape.StrokeMiterLimit = 2;
                    mShape.Data = geom;
                    mShape.StrokeThickness = mDrawThickness;
                    mShape.Stroke = mStrokeBrush;
                    this.Children.Add(mShape);
                }

                // This ensures that the line stays within page bounds, since it can poke out outside the canvas
                double halfThickness = mDrawThickness / 2;
                if (mPageCropOnClient != null)
                {
                    if (m_cnPt2.x < mPageCropOnClient.x1 + halfThickness)
                    {
                        m_cnPt2.x = mPageCropOnClient.x1 + halfThickness;
                    }
                    if (m_cnPt2.x > mPageCropOnClient.x2 - halfThickness)
                    {
                        m_cnPt2.x = mPageCropOnClient.x2 - halfThickness;
                    }
                    if (m_cnPt2.y < mPageCropOnClient.y1 + halfThickness)
                    {
                        m_cnPt2.y = mPageCropOnClient.y1 + halfThickness;
                    }
                    if (m_cnPt2.y > mPageCropOnClient.y2 - halfThickness)
                    {
                        m_cnPt2.y = mPageCropOnClient.y2 - halfThickness;
                    }
                }
                this.Width = Math.Abs(m_cnPt2.x - m_cnPt1.x);
                this.Height = Math.Abs(m_cnPt2.y - m_cnPt1.y);

                CalcArrow(); // Get points of arrow
                UIPoint tip = new UIPoint(0, 0);
                UIPoint heel = new UIPoint(this.Width, this.Height);

                PathFigureCollection arrowPoints = new PathFigureCollection();

                // arrow head
                PathFigure a_head = new PathFigure();
                a_head.StartPoint = aPt1;
                a_head.Segments.Add(new LineSegment() { Point = tip });
                a_head.Segments.Add(new LineSegment() { Point = aPt2 });
                arrowPoints.Add(a_head);

                // arrow shaft
                PathFigure a_shaft = new PathFigure();
                a_shaft.StartPoint = aPt3;
                a_shaft.Segments.Add(new LineSegment() { Point = heel });
                arrowPoints.Add(a_shaft);

                (mShape.Data as PathGeometry).Figures = arrowPoints;
            }
            return false;
        }

        private void CalcArrow()
        {
            // aPt1 and aPt2 are the ends of the two lines forming the arrow head.
            aPt1.X = this.Width;
            aPt1.Y = this.Height;
            aPt2.X = this.Width;
            aPt2.Y = this.Height;
            double dx = this.Width;
            double dy = this.Height;
            double len = dx * dx + dy * dy;

            if (len != 0)
            {
                len = Math.Sqrt(len);
                dx /= len;
                dy /= len;

                double dx1 = dx * mCos - dy * mSin;
                double dy1 = dy * mCos + dx * mSin;
                aPt1.X = mArrowHeadLength * dx1;
                aPt1.Y = mArrowHeadLength * dy1;


                double dx2 = dx * mCos + dy * mSin;
                double dy2 = dy * mCos - dx * mSin;
                aPt2.X = mArrowHeadLength * dx2;
                aPt2.Y = mArrowHeadLength * dy2;

                // offset the top of the shaft by mDrawThickness, so that it's thickness doesn't blunt the tip.
                // mDrawThickness works because we've got exactly 30 degree offset for the arrows.
                aPt3.X = mDrawThickness * dx;
                aPt3.Y = mDrawThickness * dy;
            }
        }




        internal override void Finish()
        {
            try
            {
                mPDFView.DocLock(true);

                Rect rect = GetShapeBBox();
                if (rect != null)
                {

                    // The Rect is normalized, so we need to recalculate the points if we don't want it to start from 0,0
                    PDFDouble x1 = new PDFDouble(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                    PDFDouble y1 = new PDFDouble(m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset());
                    PDFDouble x2 = new PDFDouble(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                    PDFDouble y2 = new PDFDouble(m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset());

                    mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);
                    mPDFView.ConvScreenPtToPagePt(x2, y2, mDownPageNumber);

                    // inflate the rect to make sure we can fit the arrow tips
                    rect.Inflate(mDrawThickness + GetLineEndingLength(mStrokeThickness));

                    pdftron.PDF.Annots.Line line = pdftron.PDF.Annots.Line.Create(mPDFView.GetDoc().GetSDFDoc(), rect);
                    line.SetStartStyle(pdftron.PDF.Annots.LineEndingStyle.e_OpenArrow);

                    line.SetStartPoint(new Point(x1.Value, y1.Value));
                    line.SetEndPoint(new Point(x2.Value, y2.Value));

                    SetStyle(line);
                    line.RefreshAppearance();

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    page.AnnotPushBack(line);

                    mAnnotPageNum = mDownPageNumber;
                    mAnnot = line;
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);

                    pdftron.PDF.Annots.RubberStamp stmp = pdftron.PDF.Annots.RubberStamp.Create(mPDFView.GetDoc().GetSDFDoc(), rect);

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
    }
}

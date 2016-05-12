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
    class LineCreate : SimpleShapeCreate
    {
        private bool mShapeHasBeenCreated = false;
        Path mShape;

        public LineCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_line_create;
            mToolMode = ToolType.e_line_create;
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
            // we need to restrict the border further, since the line can stick out by halfthickness
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
                    LineGeometry geom = new LineGeometry();
                    geom.StartPoint = new UIPoint(0, 0);
                    mShape = new Path();
                    mShape.Data = geom;
                    mShape.StrokeThickness = mDrawThickness;
                    mShape.Stroke = mStrokeBrush;
                    this.Children.Add(mShape);
                }

                // This ensures that the line stays within page bounds, since the line can stick out by halfthickness
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

                (mShape.Data as LineGeometry).EndPoint = new UIPoint(this.Width, this.Height);
            }

            return false;
        }

        
        internal override void Finish()
        {
            try
            {
                mPDFView.DocLock(true);

                Rect rect = GetShapeBBox();
                if (rect != null)
                {
                    pdftron.PDF.Annots.Line line = pdftron.PDF.Annots.Line.Create(mPDFView.GetDoc().GetSDFDoc(), rect);

                    // The Rect is normalized, so we need to recalculate the points if we don't want it to start from 0,0
                    PDFDouble x1 = new PDFDouble(m_cnPt1.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                    PDFDouble y1 = new PDFDouble(m_cnPt1.y - mPDFView.GetAnnotationCanvasVerticalOffset());
                    PDFDouble x2 = new PDFDouble(m_cnPt2.x - mPDFView.GetAnnotationCanvasHorizontalOffset());
                    PDFDouble y2 = new PDFDouble(m_cnPt2.y - mPDFView.GetAnnotationCanvasVerticalOffset());

                    mPDFView.ConvScreenPtToPagePt(x1, y1, mDownPageNumber);
                    mPDFView.ConvScreenPtToPagePt(x2, y2, mDownPageNumber);

                    line.SetStartPoint(new Point(x1.Value, y1.Value));
                    line.SetEndPoint(new Point(x2.Value, y2.Value));

                    SetStyle(line);
                    line.RefreshAppearance();

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    page.AnnotPushBack(line);

                    mAnnotPageNum = mDownPageNumber;
                    mAnnot = line;
                    mPDFView.UpdateWithAnnot(line, mAnnotPageNum);
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

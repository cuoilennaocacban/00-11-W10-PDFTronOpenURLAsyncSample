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
    class OvalCreate : SimpleShapeCreate
    {
        private bool mShapeHasBeenCreated = false;
        Path mShape;

        public OvalCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_oval_create;
            mToolMode = ToolType.e_oval_create;
        }

        internal override bool PointerPressedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            base.PointerPressedHandler(sender, e);
            if (mSimpleShapeCanceledAtPress)
            {
                return false;
            }
            mShapeHasBeenCreated = false;
            return false;
        }


        internal override bool PointerMovedHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Canvas takes care of mirroring
            base.PointerMovedHandler(sender, e);
            if (mStartDrawing) // Simple Shape is ready to draw
            {
                if (!mShapeHasBeenCreated)
                {
                    mShapeHasBeenCreated = true;
                    EllipseGeometry geom = new EllipseGeometry();
                    mShape = new Path();
                    mShape.Data = geom;
                    if (mUseStroke)
                    {
                        mShape.Stroke = mStrokeBrush;
                    }
                    if (mUseFill)
                    {
                        mShape.Fill = mFillBrush;
                    }

                    this.Children.Add(mShape);
                }

                // Don't draw larger than canvas / 2, since it looks smoother
                double tempThickness = Math.Min(mDrawThickness, Math.Min(this.Width, this.Height) / 2);
                mShape.StrokeThickness = tempThickness;

                (mShape.Data as EllipseGeometry).Center = new UIPoint(this.Width / 2, this.Height / 2);
                (mShape.Data as EllipseGeometry).RadiusX = (this.Width / 2) - (tempThickness / 2);
                (mShape.Data as EllipseGeometry).RadiusY = (this.Height / 2) - (tempThickness / 2);
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
                    pdftron.PDF.Annots.Circle circle = pdftron.PDF.Annots.Circle.Create(mPDFView.GetDoc().GetSDFDoc(), rect);
                    SetStyle(circle, true);
                    circle.RefreshAppearance();

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    page.AnnotPushBack(circle);

                    mAnnotPageNum = mDownPageNumber;
                    mAnnot = circle;
                    mPDFView.UpdateWithAnnot(circle, mAnnotPageNum);
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

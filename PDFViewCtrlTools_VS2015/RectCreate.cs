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
using UIRect = Windows.Foundation.Rect;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using pdftron.PDF;
using pdftron.Common;

namespace pdftron.PDF.Tools
{
    class RectCreate : SimpleShapeCreate
    {
        private bool mShapeHasBeenCreated = false;
        Path mShape;

        public RectCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_rect_create;
            mToolMode = ToolType.e_rect_create;
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
                    RectangleGeometry geom = new RectangleGeometry();
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

                // Don't draw larger than canvas / 2, which gives a smoother look
                double tempThickness = Math.Min(mDrawThickness, Math.Min(this.Width, this.Height) / 2);
                mShape.StrokeThickness = tempThickness; 

                // place the two points on the canvas to make room for the thickness of the border.
                (mShape.Data as RectangleGeometry).Rect = new UIRect(tempThickness / 2, tempThickness / 2,
                    Math.Max(this.Width - tempThickness, 0), Math.Max(this.Height - tempThickness, 0));
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
                    pdftron.PDF.Annots.Square square = pdftron.PDF.Annots.Square.Create(mPDFView.GetDoc().GetSDFDoc(), rect);
                    SetStyle(square, true);
                    square.RefreshAppearance();

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(mDownPageNumber);
                    page.AnnotPushBack(square);

                    mAnnotPageNum = mDownPageNumber;
                    mAnnot = square;
                    mPDFView.UpdateWithAnnot(square, mAnnotPageNum);
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

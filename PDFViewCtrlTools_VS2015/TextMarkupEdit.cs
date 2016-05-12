using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

using PointerDeviceType = Windows.Devices.Input.PointerDeviceType;

using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using pdftron.PDF;
using pdftron.Common;
using pdftron.PDF.Annots;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef; 


namespace pdftron.PDF.Tools
{
    class TextMarkupEdit : TextSelect
    {
        protected ITextMarkup mSelectedTextMarkup;
        protected AnnotEdit mAnnotEdit;

        public TextMarkupEdit(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_annot_edit_text_markup;
            mToolMode = ToolType.e_annot_edit_text_markup;
        }

        internal override void OnClose()
        {
            base.OnClose();
            mAnnotEdit.AnnotPropertyChangedInHelperMode -= mAnnotEdit_AnnotPropertyChangedInHelperMode;
            mAnnotEdit.HelperModeHideMenu();
            mAnnotEdit.OnClose();
        }

        internal override void OnCreate()
        {
            mSelectedTextMarkup = (ITextMarkup)mAnnot;
            base.OnCreate();

            int qpCount = mSelectedTextMarkup.GetQuadPointCount();
            QuadPoint pg_qp1 = mSelectedTextMarkup.GetQuadPoint(0);
            QuadPoint pg_qp2 = mSelectedTextMarkup.GetQuadPoint(qpCount - 1);

            for (int i = 0; i < qpCount; ++i)
            {
                QuadPoint qqq = mSelectedTextMarkup.GetQuadPoint(i);
            }

            double minX = Math.Min(Math.Min(pg_qp1.p1.x, pg_qp1.p2.x), Math.Min(pg_qp1.p3.x, pg_qp1.p4.x));
            double minY = Math.Min(Math.Min(pg_qp1.p1.y, pg_qp1.p2.y), Math.Min(pg_qp1.p3.y, pg_qp1.p4.y));
            double maxY = Math.Max(Math.Max(pg_qp1.p1.y, pg_qp1.p2.y), Math.Max(pg_qp1.p3.y, pg_qp1.p4.y));
            double newX1 = minX;
            double newY1 = (minY + maxY) / 2;
            DoubleRef x1 = new PDFDouble(minX);
            DoubleRef y1 = new PDFDouble((minY + maxY) / 2);

            double maxX = Math.Max(Math.Max(pg_qp2.p1.x, pg_qp2.p2.x), Math.Max(pg_qp2.p3.x, pg_qp2.p4.x));
            minY = Math.Min(Math.Min(pg_qp2.p1.y, pg_qp2.p2.y), Math.Min(pg_qp2.p3.y, pg_qp2.p4.y));
            maxY = Math.Max(Math.Max(pg_qp2.p1.y, pg_qp2.p2.y), Math.Max(pg_qp2.p3.y, pg_qp2.p4.y));
            double newX2 = maxX;
            double newY2 = (minY + maxY) / 2;
            DoubleRef x2 = new PDFDouble(maxX);
            DoubleRef y2 = new PDFDouble((minY + maxY) / 2);

            mPDFView.ConvPagePtToScreenPt(x1, y1, mAnnotPageNum);
            mPDFView.ConvPagePtToScreenPt(x2, y2, mAnnotPageNum);

            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

            SetTextSelectionColor();

            mPDFView.SetTextSelectionMode(TextSelectionMode.e_structural);
            SelectAndDraw(new UIPoint(x1.Value + sx, y1.Value + sy), new UIPoint(x2.Value + sx, y2.Value + sy));
            PositionSelectionWidgets();

            mPageBoundingBoxRect = BuildPageBoundBoxOnClient(mAnnotPageNum);
            mLimitToPage = mAnnotPageNum;

            mForceControlPoints = true;

            mAnnotEdit = new AnnotEdit(mPDFView, mToolManager);
            mAnnotEdit.SetAnnotEditAsHelperTool(mAnnot, mAnnotPageNum);
            mAnnotEdit.AnnotPropertyChangedInHelperMode += mAnnotEdit_AnnotPropertyChangedInHelperMode;
        }

        void mAnnotEdit_AnnotPropertyChangedInHelperMode()
        {
            SetTextSelectionColor();
            DetachAllTextSelection();
            mPagesOnScreen.Clear();
            mSelectionCanvases.Clear();
            DrawSelection();
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool)
            {
                mJustSwitchedFromAnotherTool = false;

                ShowCommandMenu();
                return false;
            }

            if (IsPointInSelection(e.GetPosition(mPDFView), true))
            {
                return true;
            }
            else
            {
                mNextToolMode = ToolType.e_pan;
            }


            return false;
        }

        internal override bool RightTappedHandler(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            return false;
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            mAnnotEdit.HelperModeHideMenu();
            if (mJustSwitchedFromAnotherTool)
            {
                if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
                {
                    PointerPoint cn_downPoint = e.GetCurrentPoint(mViewerCanvas);
                    if (cn_downPoint.Properties.IsBarrelButtonPressed)
                    {
                        mPointerID = (int)e.Pointer.PointerId;
                        return false;
                    }
                }
                
            }
            return base.PointerPressedHandler(sender, e);
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                // do nothing
                return false;
            }

            int ctrlPoint = mCurrentWidget;

            base.PointerReleasedHandler(sender, e);

            if (ctrlPoint >= 0)
            {
                if (!mPDFView.HasSelectionOnPage(mAnnotPageNum))
                {
                    return true;
                }

                PDFRect sc_OldRect = null;

                try
                {
                    mPDFView.DocLock(true);
                    double[] quads = mPDFView.GetSelection(mAnnotPageNum).GetQuads();
                    int sz = quads.Length / 8;
                    if (sz == 0)
                    {
                        return true;
                    }

                    PDFRect pg_oldRect = mSelectedTextMarkup.GetRect();
                    sc_OldRect = ConvertFromPageRectToScreenRect(pg_oldRect, mAnnotPageNum);

                    // delete the old quad points and rect
                    mSelectedTextMarkup.GetSDFObj().Erase("QuadPoints");
                    mSelectedTextMarkup.GetSDFObj().Erase("Rect");

                    mSelectedTextMarkup.SetRect(new PDFRect(quads[0], quads[1], quads[4], quads[5]));

                    SetQuadPoints(mSelectedTextMarkup, quads);
                    mSelectedTextMarkup.RefreshAppearance();
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }

                mPDFView.UpdateWithAnnot(mSelectedTextMarkup, mAnnotPageNum);
                mToolManager.RaiseAnnotationEditedEvent(mSelectedTextMarkup);

                if (sc_OldRect != null)
                {
                    mPDFView.Update(sc_OldRect);
                }
            }
            if (ctrlPoint >= 0 || IsPointInSelection(e.GetCurrentPoint(mPDFView).Position, true))
            {
                
                ShowCommandMenu();
            }
            else
            {
                mNextToolMode = ToolType.e_pan;
            }

            return true;
        }

        protected void ShowCommandMenu()
        {
            PDFRect rect = ConvertFromPageRectToScreenRect(mAnnot.GetRect(), mAnnotPageNum);
            rect.Normalize();
            mAnnotEdit.HelperModeShowMenu(rect);
        }

        internal override bool CloseOpenDialog()
        {
            return mAnnotEdit.CloseOpenDialog();
        }

        protected void SetTextSelectionColor()
        {
            ColorPt color = mAnnot.GetColorAsRGB();
            double opacity = mSelectedTextMarkup.GetOpacity();
            Color col = new Color();
            double r = (int)(color.Get(0) * 255 + 0.5);
            double g = (int)(color.Get(1) * 255 + 0.5);
            double b = (int)(color.Get(2) * 255 + 0.5);
            col.R = (byte)r;
            col.G = (byte)g;
            col.B = (byte)b;
            col.A = (byte)(20 + (40 * opacity));
            mTextSelectionBrush = new SolidColorBrush(col);
        }
    }
}

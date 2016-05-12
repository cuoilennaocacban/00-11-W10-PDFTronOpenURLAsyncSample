using pdftron.PDF;
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


using PDFRect = pdftron.PDF.Rect;
using PDFDouble = pdftron.Common.DoubleRef;

namespace pdftron.PDF.Tools
{
    class LinkAction : Tool
    {
        private pdftron.PDF.Annots.Link mLink;
        private PDFRect mPageCropOnClient;
        private Canvas mViewerCanvas;

        public LinkAction(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_link_action;
            mToolMode = ToolType.e_link_action;
        }

        internal override void OnClose()
        {
            mPDFView.Focus(Windows.UI.Xaml.FocusState.Programmatic);
            base.OnClose();
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mAnnot != null)
            {
                try {
                    mPDFView.DocLockRead();
				    mLink = (pdftron.PDF.Annots.Link)mAnnot;
                    if (mLink != null)
                    {
                        ActivateLink();
                    }
			    } 
			    catch (Exception) {
			    }
			    finally {
				    mPDFView.DocUnlockRead();
			    }
		    }
            return false;
        }

        private async void ActivateLink()
        {
            // display square on top of link
            DrawRectangle();

            // wait for 0.5 secs
            await Task.Delay(500);


            mViewerCanvas.Children.Remove(this);

            // jump to location
            JumpToLink();
            mToolManager.CreateTool(ToolType.e_pan, this);
        }

        private void JumpToLink()
        {
            if (mLink != null)
            {
                pdftron.PDF.Action a;
                try
                {
                    mPDFView.DocLockRead();
                    a = mLink.GetAction();
                    if (a != null)
                    {
                        ActionType at = a.GetType();
                        if (at == ActionType.e_URI)
                        {
                            pdftron.SDF.Obj o = a.GetSDFObj();
                            o = o.FindObj("URI");
                            if (o != null)
                            {
                                String uristring = o.GetAsPDFText();

                                LaunchBrowser(uristring);
                            }
                        }
                        else if (at == ActionType.e_GoTo)
                        {
                            mPDFView.ExecuteAction(a); 
                        }
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlockRead();
                }
            }		
        }

        private void DrawRectangle()
        {
            try
            {
                // place canvas on page
                mPageCropOnClient = BuildPageBoundBoxOnClient(mAnnotPageNum);
                this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
                this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
                this.Width = Math.Abs(mPageCropOnClient.Width());
                this.Height = Math.Abs(mPageCropOnClient.Height());

                mViewerCanvas = mPDFView.GetAnnotationCanvas();
                mViewerCanvas.Children.Add(this);

                int qn = mLink.GetQuadPointCount();
                double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
                for (int i = 0; i < qn; ++i)
                {
                    // convert quad point to screen space
                    QuadPoint qp = mLink.GetQuadPoint(i);
                    PDFRect quadRect = new PDFRect();
                    PDFDouble x1 = new PDFDouble(Math.Min(Math.Min(Math.Min(qp.p1.x, qp.p2.x), qp.p3.x), qp.p4.x));
                    PDFDouble y2 = new PDFDouble(Math.Min(Math.Min(Math.Min(qp.p1.y, qp.p2.y), qp.p3.y), qp.p4.y));
                    PDFDouble x2 = new PDFDouble(Math.Max(Math.Max(Math.Max(qp.p1.x, qp.p2.x), qp.p3.x), qp.p4.x));
                    PDFDouble y1 = new PDFDouble(Math.Max(Math.Max(Math.Max(qp.p1.y, qp.p2.y), qp.p3.y), qp.p4.y));
                    mPDFView.ConvPagePtToScreenPt(x1, y1, mAnnotPageNum);
                    quadRect.x1 = x1.Value + sx;
                    quadRect.y1 = y1.Value + sy;
                    mPDFView.ConvPagePtToScreenPt(x2, y2, mAnnotPageNum);
                    quadRect.x2 = x2.Value + sx;
                    quadRect.y2 = y2.Value + sy;

                    Rectangle drawRect = new Rectangle();
                    drawRect.Fill = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
                    drawRect.SetValue(Canvas.LeftProperty, quadRect.x1 - mPageCropOnClient.x1);
                    drawRect.SetValue(Canvas.TopProperty, quadRect.y1 - mPageCropOnClient.y1);
                    drawRect.Width = quadRect.x2 - quadRect.x1;
                    drawRect.Height = quadRect.y2 - quadRect.y1;
                    this.Children.Add(drawRect);
                }
            }
            catch (Exception) { }
        }
    }
}

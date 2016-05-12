using pdftron.Common;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using PDFRect = pdftron.PDF.Rect;
using Windows.Foundation;

namespace pdftron.PDF.Tools.Controls
{
    /// <summary>
    /// This class will highlight every instance of a particular string on all visible pages.
    /// </summary>
    public class TextHighlighter
    {
        public class PageHighlights
        {
            public bool IsAttached;
            public double PageWidth;
            public double PageHeight;
            public List<PDFRect> Quads;
            public Canvas Canvas;

            public PageHighlights()
            {
                Quads = new List<PDFRect>();
                Canvas = new Canvas();
            }
        }

        public struct TextHighlighterSettings
        {
            public bool MatchCase;
            public bool MatchWholeWords;
            public bool UseRegularExpressions;

            public TextHighlighterSettings(bool matchCase, bool matchWholeWords, bool useRegularExpressions)
            {
                MatchCase = matchCase;
                MatchWholeWords = matchWholeWords;
                UseRegularExpressions = useRegularExpressions;
            }
        }
        
        private PDFViewCtrl _PDFViewCtrl;
        private string _SearchString;
        private TextHighlighterSettings _Settings;

        private IList<int> _PagesOnScreen;
        private Dictionary<int, PageHighlights> _HighlightCanvases;
        private Dictionary<int, Canvas> _AttachedCanvases;

        private SolidColorBrush _TextSelectionBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 0));

        public Windows.UI.Color HighlightColor { set { _TextSelectionBrush = new SolidColorBrush(value); } }

        /// <summary>
        /// Creates a new TextHighlighter associated with a PDFViewCtrl, highlighting every occurence of searchString
        /// </summary>
        /// <param name="pdfViewCtrl"></param>
        /// <param name="searchString"></param>
        public TextHighlighter(PDFViewCtrl pdfViewCtrl, string searchString)
        {
            _Settings = new TextHighlighterSettings(false, false, false);
            Init(pdfViewCtrl, searchString);
        }

        public TextHighlighter(PDFViewCtrl pdfViewCtrl, string searchString, TextHighlighterSettings settings)
        {
            _Settings = settings;
            Init(pdfViewCtrl, searchString);
        }

        private void Init(PDFViewCtrl pdfViewCtrl, string searchString)
        {
            _PDFViewCtrl = pdfViewCtrl;
            _SearchString = searchString;

            _HighlightCanvases = new Dictionary<int, PageHighlights>();
            _AttachedCanvases = new Dictionary<int, Canvas>();
            _PagesOnScreen = new List<int>();

            _PDFViewCtrl.OnLayoutChanged += _PDFViewCtrl_OnLayoutChanged;
            _PDFViewCtrl.OnViewChanged += _PDFViewCtrl_OnViewChanged;
            _PDFViewCtrl.OnScale += _PDFViewCtrl_OnScale;
            _PDFViewCtrl.OnSize += _PDFViewCtrl_OnSize;
            _PDFViewCtrl.OnPageNumberChanged += _PDFViewCtrl_OnPageNumberChanged;
            if (_PDFViewCtrl.GetDoc() != null)
            {
                DrawCurrentPages();
            }
        }



        /// <summary>
        /// This must be called before the text highlighter goes out of scope. Otherwise, it will hang on to the PDFViewCtrl
        /// </summary>
        public void Detach()
        {
            _PDFViewCtrl.OnLayoutChanged -= _PDFViewCtrl_OnLayoutChanged;
            _PDFViewCtrl.OnViewChanged -= _PDFViewCtrl_OnViewChanged;
            _PDFViewCtrl.OnScale -= _PDFViewCtrl_OnScale;
            _PDFViewCtrl.OnSize -= _PDFViewCtrl_OnSize;
            _PDFViewCtrl.OnPageNumberChanged -= _PDFViewCtrl_OnPageNumberChanged;
            foreach (Canvas canvas in _AttachedCanvases.Values)
            {
                Canvas parent = canvas.Parent as Canvas;
                if (parent != null)
                {
                    parent.Children.Remove(canvas);
                }
            }
        }


        void _PDFViewCtrl_OnLayoutChanged()
        {
            // all annotation canvases will be detached in the PDFViewCtrl, so we might as well clear it all.
            _PagesOnScreen.Clear();
            _HighlightCanvases.Clear();

            DrawCurrentPages();
        }

        void _PDFViewCtrl_OnViewChanged(object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            DrawCurrentPages();
        }


        void _PDFViewCtrl_OnScale()
        {
            DrawCurrentPages(true);
        }

        void _PDFViewCtrl_OnPageNumberChanged(int current_page, int num_pages)
        {
            DrawCurrentPages();
        }

        void _PDFViewCtrl_OnSize()
        {
            DrawCurrentPages(true);
        }

        /// <summary>
        /// Draws the quads around the selected text.
        /// If a page hasn't been searched yet, we do so here.
        /// </summary>
        /// <param name="reposition"></param>
        private async void DrawCurrentPages(bool reposition = false)
        {
            IList<int> pagesOnScreen = _PDFViewCtrl.GetVisiblePages();
            IList<int> addedPages = GetPageDifference(pagesOnScreen, _PagesOnScreen);
            IList<int> removedPages = GetPageDifference(_PagesOnScreen, pagesOnScreen);
            _PagesOnScreen = pagesOnScreen;

            // Pages removed from screen should be detached
            foreach (int pgnm in removedPages)
            {
                if (_HighlightCanvases.ContainsKey(pgnm) && _HighlightCanvases[pgnm].IsAttached)
                {
                    PageHighlights sd = _HighlightCanvases[pgnm];
                    sd.IsAttached = false;
                    _AttachedCanvases.Remove(pgnm);
                    if (sd.Canvas.Parent != null)
                    {
                        Canvas parent = sd.Canvas.Parent as Canvas;
                        if (parent.Children.Contains(sd.Canvas))
                        {
                            parent.Children.Remove(sd.Canvas);
                        }
                    }
                }
            }

            foreach (int pgnm in addedPages)
            {
                if (_HighlightCanvases.ContainsKey(pgnm))
                {
                    PageHighlights sd = _HighlightCanvases[pgnm];
                    sd.IsAttached = true;
                    _AttachedCanvases[pgnm] = sd.Canvas;
                    Canvas annotCanvas = _PDFViewCtrl.GetAnnotationCanvas();
                    if (!annotCanvas.Children.Contains(sd.Canvas))
                    {
                        annotCanvas.Children.Insert(0, sd.Canvas);
                    }
                    PositionPageCanvas(pgnm, _HighlightCanvases[pgnm].Canvas);
                    continue;
                }

                PageHighlights drawing = new PageHighlights();
                _HighlightCanvases.Add(pgnm, drawing);
                await GetAllMatchesOnPageAsync(pgnm);

                foreach (PDFRect drawRect in drawing.Quads)
                {
                    // draw rectangle on selected text
                    Rectangle rect = new Rectangle();
                    rect.SetValue(Canvas.LeftProperty, drawRect.x1);
                    rect.SetValue(Canvas.TopProperty, drawRect.y1);
                    rect.Width = drawRect.x2 - drawRect.x1;
                    rect.Height = drawRect.y2 - drawRect.y1;
                    rect.Fill = _TextSelectionBrush;

                    // This will add the rectangle to the screen
                    drawing.Canvas.Children.Add(rect);
                }

                if (_PagesOnScreen.Contains(pgnm))
                {
                    // this can happen if we navigate back before the previous page has been added.
                    if (!_PDFViewCtrl.GetAnnotationCanvas().Children.Contains(drawing.Canvas))
                    {
                        _PDFViewCtrl.GetAnnotationCanvas().Children.Insert(0, drawing.Canvas);
                        drawing.IsAttached = true;
                        _AttachedCanvases[pgnm] = drawing.Canvas;
                    }
                }
                
                drawing.Canvas.Width = drawing.PageWidth;
                drawing.Canvas.Height = drawing.PageHeight;
                
                PositionPageCanvas(pgnm, drawing.Canvas);
            }
            // We need to reposition pages that remained on the screen
            if (reposition)
            {
                foreach (int pgnm in pagesOnScreen)
                {
                    if (_HighlightCanvases.ContainsKey(pgnm) && !addedPages.Contains(pgnm))
                    {
                        PositionPageCanvas(pgnm, _HighlightCanvases[pgnm].Canvas);
                    }
                }
            }
        }


        private IAsyncAction GetAllMatchesOnPageAsync(int pageNumber)
        {
            Task t = new Task(() =>
            {
                GetAllMatchesOnPage(pageNumber);
            });
            t.Start();
            return t.AsAsyncAction();
        }

        private void GetAllMatchesOnPage(int pageNumber)
        {
            PageHighlights drawing = _HighlightCanvases[pageNumber];

            TextSearch searcher = new TextSearch();
               
            int mode = (int)(TextSearchSearchMode.e_page_stop | TextSearchSearchMode.e_highlight);
            if (_Settings.MatchCase)
            {
                mode |= (int)TextSearchSearchMode.e_case_sensitive;
            }
            if (_Settings.MatchWholeWords)
            {
                mode |= (int)TextSearchSearchMode.e_whole_word;
            }
            if (_Settings.UseRegularExpressions)
            {
                mode |= (int)TextSearchSearchMode.e_reg_expression;
            }
            
            
            // needed for the search
            pdftron.Common.Int32Ref pageNum = new pdftron.Common.Int32Ref(0);
            pdftron.Common.StringRef resultString = new pdftron.Common.StringRef();
            pdftron.Common.StringRef ambientString = new pdftron.Common.StringRef();
            Highlights highlights = new Highlights();

            try
            {
                _PDFViewCtrl.DocLockRead();
                PDFDoc doc = _PDFViewCtrl.GetDoc();
                Page page = doc.GetPage(pageNumber);
                drawing.PageWidth = page.GetCropBox().Width();
                drawing.PageHeight = page.GetCropBox().Height();
                searcher.Begin(doc, _SearchString, mode, pageNumber, pageNumber);

                while (true)
                {
                    TextSearchResultCode code = searcher.Run(pageNum, resultString, ambientString, highlights);

                    if (code == TextSearchResultCode.e_found)
                    {
                        highlights.Begin(doc);
                        while (highlights.HasNext())
                        {
                            double[] quads = highlights.GetCurrentQuads();
                            for (int i = 0; i < quads.Length; i += 8)
                            {

                                PDFRect rect = new PDFRect(quads[i], quads[i + 1], quads[i + 4], quads[i + 5]);
                                rect.y1 = drawing.PageHeight - rect.y1;
                                rect.y2 = drawing.PageHeight - rect.y2;
                                rect.Normalize();
                                drawing.Quads.Add(rect);
                            }
                            highlights.Next();
                        }
                    }
                    if (code == TextSearchResultCode.e_done || code == TextSearchResultCode.e_page)
                    { 
                        return;
                    }
                }

            }
            catch (System.Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Errors: " + e.Message);
            }
            finally
            {
                _PDFViewCtrl.DocUnlockRead();
            }

        }

        private void PositionPageCanvas(int pageNumber, Canvas canvas)
        {
            TransformGroup transGroup = new TransformGroup();

            pdftron.PDF.Page page = _PDFViewCtrl.GetDoc().GetPage(pageNumber);
            DoubleRef x = new DoubleRef(0);
            DoubleRef y = new DoubleRef(page.GetCropBox().Height());
            int rotation = ((int)page.GetRotation() + (int)_PDFViewCtrl.GetRotation()) % 4;

            _PDFViewCtrl.ConvPagePtToScreenPt(x, y, pageNumber);
            canvas.SetValue(Canvas.LeftProperty, x.Value + _PDFViewCtrl.GetAnnotationCanvasHorizontalOffset());
            canvas.SetValue(Canvas.TopProperty, y.Value + _PDFViewCtrl.GetAnnotationCanvasVerticalOffset());

            CompositeTransform ct = new CompositeTransform();
            ct.ScaleX = _PDFViewCtrl.GetZoom();
            ct.ScaleY = ct.ScaleX;
            ct.Rotation = rotation * 90;

            canvas.RenderTransform = ct;
        }

        private IList<int> GetPageDifference(IList<int> pageList1, IList<int> pageList2)
        {
            List<int> difference = new List<int>();

            foreach (int page in pageList1)
            {
                if (!pageList2.Contains(page))
                {
                    difference.Add(page);
                }
            }
            return difference;
        }

    }
}

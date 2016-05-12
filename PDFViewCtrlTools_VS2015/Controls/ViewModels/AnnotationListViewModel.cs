using pdftron.PDF.Tools.Controls.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using PDFDouble = pdftron.Common.DoubleRef;

namespace pdftron.PDF.Tools.Controls.ViewModels
{
    public class AnnotationItem
    {
        public string Author { get; set; }
        public string Content { get; set; }
        public string AuthorAndContent
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Author))
                {
                    return Author + ": " + Content;
                }
                else if (!string.IsNullOrWhiteSpace(Content))
                {
                    return Content;
                }
                return string.Empty;
            }
        }
        public int PageNumber;
        public Rect AnnotRect { get; set; }
        private string AdditionalInformation;
        public pdftron.PDF.AnnotType AnnotationType { get; set; }
        public string AnnotationSymbol
        {
            get
            {
                string annotGlyph = new string((char)0x270F, 1); // default
                switch (AnnotationType)
                {
                    case AnnotType.e_Line:
                        annotGlyph = new string((char)0xE098, 1);
                        if (!string.IsNullOrWhiteSpace(AdditionalInformation) && AdditionalInformation.Equals("Arrow", StringComparison.OrdinalIgnoreCase))
                        {
                            annotGlyph = new string((char)0x006F, 1);
                        }
                        break;
                    //caret 0049
                    case AnnotType.e_Square:
                        annotGlyph = new string((char)0xE095, 1);
                        break;
                    case AnnotType.e_Circle:
                        annotGlyph = new string((char)0xE096, 1);
                        break;
                    case AnnotType.e_Ink:
                        annotGlyph = new string((char)0xE235, 1);
                        break;
                    case AnnotType.e_Polygon:
                        annotGlyph = new string((char)0xE097, 1);
                        break;
                    case AnnotType.e_Polyline:
                        annotGlyph = new string((char)0x004E, 1);
                        break;
                    case AnnotType.e_FreeText:
                        annotGlyph = new string((char)0x0050, 1);
                        if (!string.IsNullOrWhiteSpace(AdditionalInformation) && AdditionalInformation.Equals("Callout", StringComparison.OrdinalIgnoreCase))
                        {
                            annotGlyph = new string((char)0x0049, 1);
                        }
                        break;
                    case AnnotType.e_Text:
                        annotGlyph = new string((char)0xE310, 1);
                        break;
                    case AnnotType.e_Highlight:
                        annotGlyph = new string((char)0x0057, 1);
                        break;
                    case AnnotType.e_Underline:
                        annotGlyph = new string((char)0xE104, 1);
                        break;
                    case AnnotType.e_StrikeOut:
                        annotGlyph = new string((char)0xE105, 1);
                        break;
                    case AnnotType.e_Squiggly:
                        annotGlyph = new string((char)0x0070, 1);
                        break;
                    case AnnotType.e_Caret:
                        annotGlyph = new string((char)0x004F, 1);
                        break;
                    case AnnotType.e_Stamp:
                        annotGlyph = new string((char)0x004A, 1);
                        if (!string.IsNullOrWhiteSpace(AdditionalInformation) && AdditionalInformation.Equals("Signature", StringComparison.OrdinalIgnoreCase))
                        {
                            annotGlyph = new string((char)0x004D, 1);
                        }
                        break;
                    case AnnotType.e_Redact:
                        annotGlyph = new string((char)0xE208, 1);
                        break;
                }


                return annotGlyph;
            }
        }

        public AnnotationItem(string author, string content, AnnotType annotationSymbol, int pageNumber, Rect annotRect, string additionalInformation = null)
        {
            Author = author;
            Content = content;
            AnnotationType = annotationSymbol;
            PageNumber = pageNumber;
            AnnotRect = annotRect;
            AdditionalInformation = additionalInformation;
        }
    }

    public class PageHeaderItem
    {
        public int PageNumber { get; set; }
        public string PageHeader
        {
            get
            {
                return string.Format(ResourceHandler.GetString("AnnotationList_Page"), PageNumber);
            }
        }

        public ObservableCollection<AnnotationItem> Annotations { get; set; }

        public PageHeaderItem(IList<AnnotationItem> items)
        {
            PageNumber = 0;
            if (items != null && items.Count > 0)
            {
                PageNumber = items[0].PageNumber;
            }
            Annotations = new ObservableCollection<AnnotationItem>();
            foreach (AnnotationItem item in items)
            {
                Annotations.Add(item);
            }
        }
    }

    public class AnnotationListViewModel : ViewModelBase
    {
        private static List<AnnotType> WORKABLE_ANNOTATIONS = new List<AnnotType>()
        {
            AnnotType.e_Circle,
            AnnotType.e_Square,
            AnnotType.e_Text,
            AnnotType.e_Line,
            AnnotType.e_Polygon, 
            AnnotType.e_Underline, 
            AnnotType.e_StrikeOut,
            AnnotType.e_Ink,
            AnnotType.e_Highlight,
            AnnotType.e_FreeText,
            AnnotType.e_Squiggly,
            AnnotType.e_Stamp,
            AnnotType.e_Caret,
            AnnotType.e_Polyline,
            AnnotType.e_Redact
        };

        private PDFViewCtrl _PDFViewCtrl;
        public PDFViewCtrl PDFViewCtrl
        {
            get { return _PDFViewCtrl; }
            set 
            { 
                _PDFViewCtrl = value;
                Init();
            }
        }

        public AnnotationListViewModel()
        {
            AnnotationGroups = new ObservableCollection<PageHeaderItem>();
            InitInteraction();
        }

        public AnnotationListViewModel(PDFViewCtrl ctrl)
        {
            AnnotationGroups = new ObservableCollection<PageHeaderItem>();
            InitInteraction();
            PDFViewCtrl = ctrl;
        }

        private void Init()
        {
            CreateAnnotationList();
        }

        private bool _Cancel = false;


        #region Properties
        public ObservableCollection<PageHeaderItem> AnnotationGroups { get; private set; }

        private bool _FoundAnnotations = true;
        public bool FoundAnnotations
        {
            get { return _FoundAnnotations; }
            set
            {
                if (_FoundAnnotations != value)
                {
                    _FoundAnnotations = value;
                    RaisePropertyChanged();
                }
            }
        }

        private Rectangle _FlashingRectangle;

        private Brush _FlashBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        public Brush FlashBrush
        {
            get { return _FlashBrush; }
            set
            {
                _FlashBrush = value;
                if (_FlashingRectangle != null)
                {
                    _FlashingRectangle.Fill = value;
                }
            }
        }

        private Storyboard _FlashAnimation = null;
        public Storyboard FlashAnimation
        {
            get { return _FlashAnimation; }
            set 
            { 
                _FlashAnimation = value;
                Storyboard.SetTarget(FlashAnimation, _FlashingRectangle);
                Storyboard.SetTargetName(FlashAnimation, _FlashingRectangle.Name);
                _FlashAnimation.Completed += FlashAnimation_Completed;
            }
        }

        #endregion Properties


        private async void CreateAnnotationList()
        {
            PDFDoc doc = _PDFViewCtrl.GetDoc();
            if (doc != null)
            {
                try
                {
                    bool gotItems = false;
                    _PDFViewCtrl.DocLockRead();
                    PageIterator pageIter = doc.GetPageIterator();
                    IList<AnnotationItem> items = await GetNextPageWithAnnotsAsync(pageIter);
                    while (items != null)
                    {
                        gotItems = true;
                        PageHeaderItem header = new PageHeaderItem(items);
                        AnnotationGroups.Add(header);
                        items = await GetNextPageWithAnnotsAsync(pageIter);
                    }
                    if (!gotItems)
                    {
                        FoundAnnotations = false;
                    }
                }
                catch (Exception) { }
                finally
                {
                    _PDFViewCtrl.DocUnlockRead();
                }
            }
        }

        private IAsyncOperation<IList<AnnotationItem>> GetNextPageWithAnnotsAsync(PageIterator pageIter)
        {
            Task<IList<AnnotationItem>> t = new Task<IList<AnnotationItem>>(() =>
            {
                return GetNextPageWithAnnot(pageIter);
            });
            t.Start();
            return t.AsAsyncOperation<IList<AnnotationItem>>();
        }

        private IList<AnnotationItem> GetNextPageWithAnnot(PageIterator pageIter)
        {
            while (pageIter.HasNext() && !_Cancel)
            {
                pdftron.PDF.Page page = pageIter.Current();
                int numAnnots = page.GetNumAnnots();
                if (numAnnots > 0)
                {
                    IList<AnnotationItem> items = GetAnnotationItemsOnPage(page);
                    if (items.Count > 0)
                    {
                        pageIter.Next();
                        return items;
                    }
                }

                pageIter.Next();
            }
            return null;
        }


        private IList<AnnotationItem> GetAnnotationItemsOnPage(pdftron.PDF.Page page)
        {
            List<AnnotationItem> items = new List<AnnotationItem>();
            int numAnnots = page.GetNumAnnots();
            for (int i = 0; i < numAnnots; i++)
            {
                IAnnot annot = page.GetAnnot(i);
                AnnotType annotType = annot.GetAnnotType();
                if (WORKABLE_ANNOTATIONS.Contains(annotType) && annot.IsMarkup())
                {
                    string author = "";
                    string contents = "";
                    string additonalInfo = "";

                    if (annot is pdftron.PDF.Annots.IMarkup)
                    {
                        Annots.IMarkup markup = (Annots.IMarkup)annot;

                        author = markup.GetTitle();

                        pdftron.PDF.Annots.Popup popup = markup.GetPopup();
                        if (popup != null && popup.IsValid())
                        {
                            contents = popup.GetContents();
                        }
                        else
                        {
                            contents = markup.GetContents();
                        }

                        if (string.IsNullOrWhiteSpace(contents) && annot is Annots.ITextMarkup)
                        {
                            pdftron.PDF.TextExtractor textExtractor = new pdftron.PDF.TextExtractor();

                            textExtractor.Begin(page);
                            String text = textExtractor.GetTextUnderAnnot(annot);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                contents = text;
                            }
                        }
                    }

                    // if stamp, see if it's a signature
                    if (annotType == AnnotType.e_Stamp)
                    {
                        pdftron.SDF.Obj obj = annot.GetSDFObj();
                        obj = obj.FindObj(ToolManager.SignatureAnnotationIdentifyingString);
                        if (obj != null)
                        {
                            additonalInfo = "Signature";
                        }
                    }

                    // if a line, see if it is an arrow
                    if (annotType == AnnotType.e_Line)
                    {
                        Annots.Line line = (Annots.Line)annot;
                        if (UtilityFunctions.IsArrow(line))
                        {
                            additonalInfo = "arrow";
                        }
                    }

                    // if a free text, see if it's a callout
                    if (annotType == AnnotType.e_FreeText)
                    {
                        pdftron.SDF.Obj obj = annot.GetSDFObj();
                        obj = obj.FindObj("CL");
                        if (obj != null)
                        {
                            additonalInfo = "Callout";
                        }
                    }

                    AnnotationItem item = new AnnotationItem(author, contents, annotType, page.GetIndex(), annot.GetRect(), additonalInfo);

                    items.Add(item);
                }
            }

            return items;
        }

        #region Interaction

        private void InitInteraction()
        {
            ItemClickCommand = new RelayCommand(ItemClickCommandImpl);
            _FlashingRectangle = new Rectangle();
            _FlashingRectangle.Opacity = 0;
            _FlashingRectangle.Fill = FlashBrush;
        }

        public RelayCommand ItemClickCommand { get; set; }

        private void ItemClickCommandImpl(object param)
        {
            ItemClickEventArgs args = param as ItemClickEventArgs;
            if (args != null)
            {
                AnnotationItem item = args.ClickedItem as AnnotationItem;
                if (item != null)
                {
                    SwitchPageIfNecessary(item.PageNumber);

                    // Get rectangle in canvas coordinates
                    double sx = _PDFViewCtrl.GetAnnotationCanvasHorizontalOffset();
                    double sy = _PDFViewCtrl.GetAnnotationCanvasVerticalOffset();

                    PDFDouble x1 = new PDFDouble(item.AnnotRect.x1);
                    PDFDouble y1 = new PDFDouble(item.AnnotRect.y1);
                    PDFDouble x2 = new PDFDouble(item.AnnotRect.x2);
                    PDFDouble y2 = new PDFDouble(item.AnnotRect.y2);

                    _PDFViewCtrl.ConvPagePtToScreenPt(x1, y1, item.PageNumber);
                    _PDFViewCtrl.ConvPagePtToScreenPt(x2, y2, item.PageNumber);

                    Rect canvasRect = new Rect(x1.Value + sx, y1.Value + sy, x2.Value + sx, y2.Value + sy);
                    canvasRect.Normalize();

                    ScrollRectIntoView(item.PageNumber, canvasRect);

                    if (FlashAnimation != null)
                    {
                        FlashAnnotation(item.PageNumber, canvasRect);
                    }
                }
            }
        }

        /// <summary>
        /// Will switch page if the current page can not be scrolled into view.
        /// </summary>
        /// <param name="pageNumber"></param>
        public void SwitchPageIfNecessary(int pageNumber)
        {
            // If in a non-continuous mode we want to first go to the page.
            PDFViewCtrlPagePresentationMode mode = this.PDFViewCtrl.GetPagePresentationMode();
            if (mode == PDFViewCtrlPagePresentationMode.e_single_page ||
                mode == PDFViewCtrlPagePresentationMode.e_facing ||
                mode == PDFViewCtrlPagePresentationMode.e_facing_cover)
            {
                int currentPage = PDFViewCtrl.GetCurrentPage();
                if (currentPage == pageNumber)
                {
                    return;
                }

                if (mode == PDFViewCtrlPagePresentationMode.e_facing && ((currentPage + 1) / 2 == (pageNumber + 1) / 2))
                {
                    return;
                }
                else if (mode == PDFViewCtrlPagePresentationMode.e_facing_cover && (currentPage / 2 == pageNumber / 2))
                {
                    return;
                }
                this.PDFViewCtrl.SetCurrentPage(pageNumber);
            }
        }

        public void ScrollRectIntoView(int pageNumber, Rect rect)
        {
            // Now, compare the view's position on the canvas to that of the annotation
            double sx = this.PDFViewCtrl.GetAnnotationCanvasHorizontalOffset();
            double sy = this.PDFViewCtrl.GetAnnotationCanvasVerticalOffset();

            Rect viewRect = new Rect(sx, sy, sx + this.PDFViewCtrl.ActualWidth, sy + this.PDFViewCtrl.ActualHeight);
            Rect intersectRect = new Rect();
            bool intersect = intersectRect.IntersectRect(viewRect, rect);

            // If we don't intersect enough, scroll it into view.
            if (!intersect ||
                (intersectRect.Width() < 100 && rect.Width() > 100) ||
                (intersectRect.Height() < 100 && rect.Height() > 100))
            {
                double x = rect.x1 - ((this.PDFViewCtrl.ActualWidth - rect.Width()) / 2);
                double y = rect.y1 - ((this.PDFViewCtrl.ActualHeight - rect.Height()) / 2);
                if (x < 0)
                {
                    x = 0;
                }
                if (y < 0)
                {
                    y = 0;
                }
                this.PDFViewCtrl.SetHScrollPos(x);
                this.PDFViewCtrl.SetVScrollPos(y);
            }
        }

        private void FlashAnnotation(int pagenumber, Rect rect)
        {
            DetachFlashinfRectangle();

            Canvas canvas = _PDFViewCtrl.GetAnnotationCanvas();
            canvas.Children.Add(_FlashingRectangle);
            _FlashingRectangle.Tag = canvas;
            _FlashingRectangle.Opacity = 0;
            _FlashingRectangle.Width = rect.Width();
            _FlashingRectangle.Height = rect.Height();
            _FlashingRectangle.SetValue(Canvas.LeftProperty, rect.x1);
            _FlashingRectangle.SetValue(Canvas.TopProperty, rect.y1);

            FlashAnimation.Begin();
        }

        private void FlashingAnimationCompleted(object sender, object e)
        {
            DetachFlashinfRectangle();
        }

        private void DetachFlashinfRectangle()
        {
            Canvas parent = _FlashingRectangle.Tag as Canvas;
            if (parent != null)
            {
                parent.Children.Remove(_FlashingRectangle);
            }
        }


        void FlashAnimation_Completed(object sender, object e)
        {
            DetachFlashinfRectangle();
        }

        #endregion Interaction

        #region Back Key
        public bool GoBack()
        {

            return false;
        }

        #endregion Back Key
    }
}

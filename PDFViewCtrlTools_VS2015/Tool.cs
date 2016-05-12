using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PointerDeviceType = Windows.Devices.Input.PointerDeviceType;


using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

using UIPoint = Windows.Foundation.Point;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using pdftron.PDF;
using pdftron.Common;
using Windows.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using System.Text.RegularExpressions;



namespace pdftron.PDF.Tools
{

    public class Tool : Canvas
    {
        /// <summary>
        /// A convenience structure that represents all the text selection on a particular page.
        /// </summary>
        public struct SelectionDrawing
        {
            public bool IsAttached;
            public double PageHeight;
            public List<PDFRect> Quads;
            public Canvas Canvas;
        }

        protected PDFViewCtrl mPDFView;
        protected ToolManager mToolManager;
        protected PDFPage mPage;


        // behavior affecting variables
        internal bool mReturnToPanModeWhenFinished = true; // This variable can be changed if you want the tool to 
        internal bool ReturnToPanModeWhenFinished
        {
            get
            {
                return mReturnToPanModeWhenFinished;
            }
            set
            {
                mReturnToPanModeWhenFinished = value;
            }
        }
        internal bool mPanBringUpMenuOnTap = false;
        internal bool PanBringUpMenuOnTap
        {
            get
            {
                return mPanBringUpMenuOnTap;
            }
            set
            {
                mPanBringUpMenuOnTap = value;
            }
        }

        protected IAnnot mAnnot = null;
        protected int mAnnotPageNum;
        protected PDFRect mAnnotBBox;		//in page space
        protected bool mJustSwitchedFromAnotherTool = false;
        public bool JustSwitchedFromAnotherTool { set { mJustSwitchedFromAnotherTool = value; } }
        protected bool mIsUsingMouse = false;
        protected bool mWasFormFillTool = false;
        protected bool mIsInSnappedView = false;
 
        protected PopupCommandMenu mCommandMenu = null; // The current popup window
        protected bool mIsShowingCommandMenu = false;

        protected const int ArrowScrollDistance = 70; 
        protected const int PageUpDownScrollDistanceMargin = 100; // scroll screen height - this

        // keyboard events
        protected bool mIsCtrlDown = false;
        protected bool mIsShiftDown = false;
        protected bool mIsAltDown = false;
        protected bool mIsModifierKeyDown = false; // aggregate to simplify
        protected bool mIsContinuousMode = false;

        protected Rect mRectToKeepOnScreenWhileManipulating = null; // if this rectangle is not null, the tools will use it to limit the range of 2 finger scrolling

        // Text Selection
        protected int mSelectionStartPage;
        protected int mSelectionEndPage;
        protected List<PDFRect> mSelectedAreasForHitTest;
        protected bool mAllowTextSelectionOptions = true;

        protected IList<int> mPagesOnScreen;
        protected Dictionary<int, SelectionDrawing> mSelectionCanvases;
        protected Dictionary<int, PDFRect> mSelectionRectangles;
        protected double CumulativeRotation = 0;

        protected SolidColorBrush mTextSelectionBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(100, 80, 110, 200));
        protected double mTextSelectFirstPointX;
        protected double mTextSelectFirstPointY;
        protected double mTextSelectLastPointX;
        protected double mTextSelectLastPointY;
        protected double mTextSelectFirstQuadHeight;
        protected double mTextSelectLastQuadHeight;

        // Author dialog
        private Windows.UI.Xaml.Controls.Primitives.Popup mAuthorDialogPopup;
        private Utilities.AuthorInputDialog mAuthorInputDialog;
        pdftron.PDF.Tools.Controls.ViewModels.AuthorDialogViewModel mAuthorVM = null;
        private Annots.IMarkup mMarkupToAuthor;

        internal bool mIsInHelperMode = false;

        protected ToolType mToolMode;
        /// <summary>
        /// The current Tool Mode
        /// </summary>
        public ToolType ToolMode
        {
            get
            {
                return mToolMode;
            }
        }
        protected ToolType mNextToolMode;
        /// <summary>
        /// The Next Tool Mode
        /// </summary>
        public ToolType NextToolMode
        {
            get
            {
                return mNextToolMode;
            }
        }

        internal IAnnot CurrentAnnot { get { return mAnnot; } }
        internal int CurrentAnnotPageNumber { get { return mAnnotPageNum; } }
        internal Rect CurrentAnnotBBox { get { return mAnnotBBox; } }

        internal Tool(PDFViewCtrl ctrl, ToolManager tMan)
        {
            mPDFView = ctrl;
            mToolManager = tMan;
            mNextToolMode = ToolType.e_pan;
            mToolMode = ToolType.e_none;
            mAnnotBBox = new PDFRect();
            TextBlock tb = new TextBlock();

            mSelectionCanvases = new Dictionary<int, SelectionDrawing>();
            mPagesOnScreen = new List<int>();
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
        }

        /// <summary>
        /// Transfers information from the old tool to the new tool.
        /// Used when the ToolManager switches tool.
        /// </summary>
        /// <param name="ot"></param>
        internal virtual void Transfer(Tool ot)
        {
            mAnnot = ot.mAnnot;
            mAnnotBBox = ot.mAnnotBBox;
            mAnnotPageNum = ot.mAnnotPageNum;
            if (this.ToolMode != ot.ToolMode)
            {
                mJustSwitchedFromAnotherTool = true;
            }
            //if (ot.ToolMode == ToolType.e_form_fill)
            //{
            //    mWasFormFillTool = true;
            //}
            mIsUsingMouse = ot.mIsUsingMouse;
            mPanBringUpMenuOnTap = ot.mPanBringUpMenuOnTap;
            mIsInSnappedView = ot.mIsInSnappedView;
        }

        internal virtual ToolType GetNextToolMode()
        {
            return mNextToolMode;
        }

        /// <summary>
        /// This happens after the tool is created and the transfer of information is complete.
        /// Here we can initialize things based on the transferred information.
        /// </summary>
        internal virtual void OnCreate()
        {
        }

        /// <summary>
        /// This will let the tool close down gracefully, including removing menus and widgets from the screen.
        /// </summary>
        internal virtual void OnClose()
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mIsShowingCommandMenu = false;
            }
        }

        internal virtual bool OnScale()
        {
            mToolManager.ClearDelayRemoveList();
            DrawSelection(true);
            return false;
        }

        internal virtual bool OnSize()
        {
            DrawSelection(true);
            return false;
        }

        internal virtual void OnFinishedRendering()
        {
            mToolManager.ClearDelayRemoveList();
        }

        internal virtual bool OnViewChanged(Object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            DrawSelection();
            mToolManager.mPageNumberIndicator.Show();
            return false;
        }

        internal virtual bool OnPageNumberChanged(int current_page, int num_pages)
        {
            UpdatePageNumber(current_page, num_pages);

            PDFViewCtrlPagePresentationMode mode = mPDFView.GetPagePresentationMode();
            if (mode == PDFViewCtrlPagePresentationMode.e_single_page ||
                        mode == PDFViewCtrlPagePresentationMode.e_facing ||
                        mode == PDFViewCtrlPagePresentationMode.e_facing_cover)

            {
                mToolManager.ClearDelayRemoveList();
            }

            return false;
        }

        internal virtual bool PointerPressedHandler(Object sender, PointerRoutedEventArgs e)
        {
            AddPointer(e.Pointer);
            if (mToolManager.ContactPoints.Count > 1)
            {
                mToolManager.EnableScrollByManipulation = true;
            }
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                mIsUsingMouse = true;
            }
            else
            {
                mIsUsingMouse = false;
            }
            if (mCommandMenu != null)
            {
                mCommandMenu.Hide();
                mIsShowingCommandMenu = false;
                mCommandMenu = null;
            }
            return false;
        }

        internal virtual bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool PointerReleasedHandler(Object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            return false;
        }

        internal virtual bool PointerCanceledHandler(Object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            return false;
        }

        internal virtual bool PointerWheelChangedHandler(Object sender, PointerRoutedEventArgs e)
        {
            return false;
        }

		internal virtual bool PointerCaptureLostHandler(Object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            return false;
        }

		internal virtual bool PointerEnteredHandler(Object sender, PointerRoutedEventArgs e)
        {
            return false;
        }

		internal virtual bool PointerExitedHandler(Object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            return false;
        }

        internal virtual bool OnSetDoc()
        {
            UpdatePageNumber(mPDFView.GetCurrentPage(),mPDFView.GetPageCount());
            mPDFView.IsEnabled = true;
            return false;
        }

        internal virtual bool ManipulationStartedEventHandler(Object sender, ManipulationStartedRoutedEventArgs e)
        {
            mToolManager.ManipulationTouch = true;
            return false;
        }

		internal virtual bool ManipulationCompletedEventHandler(Object sender, ManipulationCompletedRoutedEventArgs e)
        {
            mToolManager.ManipulationTouch = false;
            mToolManager.EnableOneFingerScroll = false;
            return false;
        }

        internal virtual bool ManipulationDeltaEventHandler(Object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // we let the scrollviewer handle inertial scrolling. This here is just for temporary scrolling while creating annotations.
            if (e.IsInertial)
            {
                e.Complete();
                return false;
            }
            if (mToolManager.EnableScrollByManipulation && mToolManager.ManipulationTouch && 
                (mToolManager.TouchPoints > 1
                || (mToolManager.TouchPoints >= 1 && mToolManager.EnableOneFingerScroll)))
            {
                double xTranslation = -e.Delta.Translation.X;
                double yTranslation = -e.Delta.Translation.Y;
                if (mRectToKeepOnScreenWhileManipulating != null)
                {
                    double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                    double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
                    // this means the rect is larger than the screen, so we want to make sure it always covers the screen
                    if (mRectToKeepOnScreenWhileManipulating.Height() > mPDFView.ActualHeight) 
                    {
                        if (yTranslation < 0 && yTranslation < mRectToKeepOnScreenWhileManipulating.y1 - sy)
                        {
                            yTranslation = Math.Min(mRectToKeepOnScreenWhileManipulating.y1 - sy, 0);
                        }
                        if (yTranslation > 0 && yTranslation > mRectToKeepOnScreenWhileManipulating.y2 - sy - mPDFView.ActualHeight)
                        {
                            yTranslation = Math.Max(mRectToKeepOnScreenWhileManipulating.y2 - sy - mPDFView.ActualHeight, 0);
                        }
                    }
                    else
                    {
                        if (yTranslation < 0 && yTranslation < mRectToKeepOnScreenWhileManipulating.y2 - sy - mPDFView.ActualHeight)
                        {
                            yTranslation = Math.Min(mRectToKeepOnScreenWhileManipulating.y2 - sy - mPDFView.ActualHeight, 0);
                        }
                        if (yTranslation > 0 && yTranslation > mRectToKeepOnScreenWhileManipulating.y1 - sy)
                        {
                            yTranslation = Math.Max(mRectToKeepOnScreenWhileManipulating.y1 - sy, 0);
                        }
                    }

                    if (mRectToKeepOnScreenWhileManipulating.Width() > mPDFView.ActualWidth)
                    {
                        if (xTranslation < 0 && xTranslation < mRectToKeepOnScreenWhileManipulating.x1 - sx)
                        {
                            xTranslation = Math.Min(mRectToKeepOnScreenWhileManipulating.x1 - sx, 0);
                        }
                        if (xTranslation > 0 && xTranslation > mRectToKeepOnScreenWhileManipulating.x2 - sx - mPDFView.ActualWidth)
                        {
                            xTranslation = Math.Max(mRectToKeepOnScreenWhileManipulating.x2 - sx - mPDFView.ActualWidth, 0);
                        }
                    }
                    else
                    {
                        if (xTranslation < 0 && xTranslation < mRectToKeepOnScreenWhileManipulating.x2 - sx - mPDFView.ActualWidth)
                        {
                            xTranslation = Math.Min(mRectToKeepOnScreenWhileManipulating.x2 - sx - mPDFView.ActualWidth, 0);
                        }
                        if (xTranslation > 0 && xTranslation > mRectToKeepOnScreenWhileManipulating.x1 - sx)
                        {
                            xTranslation = Math.Max(mRectToKeepOnScreenWhileManipulating.x1 - sx, 0);
                        }
                    }
                }

                mPDFView.SetHScrollPos(mPDFView.GetHScrollPos() + xTranslation);
                mPDFView.SetVScrollPos(mPDFView.GetVScrollPos() + yTranslation);
            }
            return false;
        }

        internal virtual bool ManipulationInertiaStartingEventHandler(Object sender, ManipulationInertiaStartingRoutedEventArgs e)
        {
            mToolManager.ManipulationTouch = false;
            mToolManager.EnableOneFingerScroll = false;
            return false;
        }

        internal virtual bool ManipulationStartingEventHandler(Object sender, ManipulationStartingRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool TappedHandler(Object sender, TappedRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool HoldingHandler(Object sender, HoldingRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool RightTappedHandler(Object sender, RightTappedRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool DoubleTappedHandler(object sender, DoubleTappedRoutedEventArgs e)
        {
            return false;
        }

        internal virtual bool DoubleClickedHandler(Object sender, PointerRoutedEventArgs e)
        {
            return true;
        }

        public bool KeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            // begin by detecting with modifier keys are pressed

            Windows.UI.Core.CoreVirtualKeyStates state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            mIsCtrlDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);
            mIsShiftDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Menu);
            mIsAltDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            PDFViewCtrlPagePresentationMode presentationMode = mPDFView.GetPagePresentationMode();
            mIsContinuousMode = (presentationMode == PDFViewCtrlPagePresentationMode.e_facing_continuous ||
                        presentationMode == PDFViewCtrlPagePresentationMode.e_facing_continuous_cover ||
                        presentationMode == PDFViewCtrlPagePresentationMode.e_single_continuous);

            mIsModifierKeyDown = mIsCtrlDown || mIsShiftDown || mIsAltDown;

            //return false;
            return KeyDownAction(sender, e);
        }

        public bool KeyUpHandler(object sender, KeyRoutedEventArgs e)
        {
            // begin by detecting with modifier keys are pressed
            Windows.UI.Core.CoreVirtualKeyStates state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            mIsCtrlDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);
            mIsShiftDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            state = Windows.UI.Core.CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Menu);
            mIsAltDown = (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            PDFViewCtrlPagePresentationMode presentationMode = mPDFView.GetPagePresentationMode();
            bool mIsContinuousMode = (presentationMode == PDFViewCtrlPagePresentationMode.e_facing_continuous ||
                        presentationMode == PDFViewCtrlPagePresentationMode.e_facing_continuous_cover ||
                        presentationMode == PDFViewCtrlPagePresentationMode.e_single_continuous);

            mIsModifierKeyDown = mIsCtrlDown || mIsShiftDown || mIsAltDown;

            return false;
            //return KeyUpAction(sender, e);
        }

        internal virtual bool KeyDownAction(object sender, KeyRoutedEventArgs e)
        {
            mJustSwitchedFromAnotherTool = false;
            if (mIsAltDown)
            {
                return false;
            }
            Canvas ViewerCanvas = mPDFView.GetAnnotationCanvas();

             // Start with things we always want to do.
            if (mIsCtrlDown && mIsShiftDown && !mIsAltDown)
            {
                if (e.Key == VirtualKey.Up || e.Key == VirtualKey.PageUp)
                {
                    mPDFView.GotoFirstPage();
                    return true;
                }
                if (e.Key == VirtualKey.Down || e.Key == VirtualKey.PageDown)
                {
                    mPDFView.GotoLastPage();
                    return true;
                }
            }
            if (mIsCtrlDown && !mIsShiftDown && !mIsAltDown)
            {
                if (e.Key == VirtualKey.PageUp)
                {
                    mPDFView.GotoPreviousPage();
                    return true;
                }
                if (e.Key == VirtualKey.PageDown)
                {
                    mPDFView.GotoNextPage();
                    return true;
                }
            }
            if (!mIsModifierKeyDown && !mIsContinuousMode)
            {
                if (e.Key == VirtualKey.Home)
                {
                    mPDFView.GotoFirstPage();
                    return true;
                }
                if (e.Key == VirtualKey.End)
                {
                    mPDFView.GotoLastPage();
                    return true;
                }
            }
            if (!mIsCtrlDown && !mIsAltDown)
            {
                if (!mIsContinuousMode)
                {
                    if (mPDFView.ActualHeight >= ViewerCanvas.ActualHeight)
                    {
                        if (e.Key == VirtualKey.Up || e.Key == VirtualKey.PageUp)
                        {
                            mPDFView.GotoPreviousPage();
                            return true;
                        }
                        if (e.Key == VirtualKey.Down || e.Key == VirtualKey.PageDown)
                        {
                            mPDFView.GotoNextPage();
                            return true;
                        }
                    }
                    if (mPDFView.ActualWidth >= ViewerCanvas.ActualWidth)
                    {
                        if (e.Key == VirtualKey.Left)
                        {
                            mPDFView.GotoPreviousPage();
                            return true;
                        }
                        if (e.Key == VirtualKey.Right)
                        {
                            mPDFView.GotoNextPage();
                            return true;
                        }
                    }
                }
                else
                {
                    if (e.Key == VirtualKey.Left)
                    {
                        if (ViewerCanvas.ActualWidth <= mPDFView.ActualWidth)
                        {
                            mPDFView.GotoPreviousPage();
                            return true;
                        }
                    }
                    if (e.Key == VirtualKey.Right)
                    {
                        if (ViewerCanvas.ActualWidth <= mPDFView.ActualWidth)
                        {
                            mPDFView.GotoNextPage();
                            return true;
                        }
                    }
                }
            }

            if (e.Handled)
            {
                return false;
            }

            if (!mIsCtrlDown && !mIsAltDown)
            {
                switch (e.Key)
                {
                    case VirtualKey.Enter:
                        if (mIsShiftDown)
                        {
                            PageUpScroll();
                        }
                        else
                        {
                            PageDownScroll();
                        }
                        return true;
                    case VirtualKey.Escape:
                        if (mIsShowingCommandMenu)
                        {
                            mCommandMenu.Hide();
                            mCommandMenu = null;
                            mIsShowingCommandMenu = false;
                            return true;
                        }
                        return false;
                    default: // we didn't do anything
                        return false;
                }
            }
            else if (mIsCtrlDown && e.Key != VirtualKey.Control)
            {
                switch (e.Key)
                {
                    case VirtualKey.PageDown:
                        mPDFView.GotoNextPage();
                        return true;
                    case VirtualKey.PageUp:
                        mPDFView.GotoPreviousPage();
                        return true;
                    case VirtualKey.Down:
                        mPDFView.GotoNextPage();
                        return true;
                    case VirtualKey.Up:
                        mPDFView.GotoPreviousPage();
                        return true;
                    case VirtualKey.Left:
                        mPDFView.GotoPreviousPage();
                        return true;
                    case VirtualKey.Right:
                        mPDFView.GotoNextPage();
                        return true;
                    case VirtualKey.Home:
                        mPDFView.GotoFirstPage();
                        return true;
                    case VirtualKey.End:
                        mPDFView.GotoLastPage();
                        return true;
                    case VirtualKey.Enter:
                        PageUpScroll();
                        return true;
                    case VirtualKey.Subtract:
                        mPDFView.SetZoom(mPDFView.GetZoom() * 0.9);
                        return true;
                    case VirtualKey.Add:
                        mPDFView.SetZoom(mPDFView.GetZoom() * 1.1);
                        return true;
                    default: // we didn't do anything, We also need to check keys we don't have in the enum.
                        if ((int)e.Key == 189)
                        {
                            mPDFView.SetZoom(mPDFView.GetZoom() * 0.9);
                            return true;
                        }
                        if ((int)e.Key == 187)
                        {
                            mPDFView.SetZoom(mPDFView.GetZoom() * 1.1);
                            return true;
                        }
                        return false;
                }
            }
            return false;
        }

        internal virtual bool KeyUpAction(object sender, KeyRoutedEventArgs e)
        {
            return false;
        }

        internal List<string> ToolCreationOptions()
        {
            return new List<string>() { "Rectangle", "Ellipse", "Line", "Arrow", "Sticky Note", "Free Hand", "Free Text" };
        }

        internal void HandleToolCreationOption(string title, bool toolIsPersistent = false)
        {
            if (title.Equals("line", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_line_create, this, toolIsPersistent);
            }
            else if (title.Equals("arrow", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_arrow_create, this, toolIsPersistent);
            }
            else if (title.Equals("rectangle", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_rect_create, this, toolIsPersistent);
            }
            else if (title.Equals("ellipse", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_oval_create, this, toolIsPersistent);
            }
            else if (title.Equals("sticky note", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_sticky_note_create, this, toolIsPersistent);
            }
            else if (title.Equals("free hand", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_ink_create, this, toolIsPersistent);
            }
            else if (title.Equals("free text", StringComparison.OrdinalIgnoreCase))
            {
                mToolManager.CreateTool(ToolType.e_text_annot_create, this, toolIsPersistent);
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }
        }

        internal virtual void OnCommandMenuClicked(string title)
        {

        }

        internal virtual bool CloseOpenDialog()
        {
            return false;
        }

        /// <summary>
        /// Will either return to pan or create a new tool of the same type, depending on the flag mReturnToPanModeWhenFinished
        /// </summary>
        /// <param name="nextToolMode"></param>
        protected void EndCurrentTool(ToolType nextToolMode)
        {
            if (mReturnToPanModeWhenFinished)
            {
                mNextToolMode = nextToolMode;
            }
            else
            {
                mToolManager.CreateTool(mToolMode, this, true);
            }
        }

        /// <summary>
        /// Creates a new tool, but first makes sure the ToolManager knows it's from inside the tools code.
        /// </summary>
        /// <param name="newToolMode"></param>
        protected void CreateNewTool(ToolType newToolMode)
        {
            mToolManager.ToolChangedInternally = true;
            mToolManager.CreateTool(newToolMode, this);
            mToolManager.ToolChangedInternally = false;
        } 


        #region UtilityFunctions

        internal void UpdatePageNumber(int currentPage, int numPages)
        {
            if (mToolManager.UseSmallPageNumberIndicator)
            {
                mToolManager.mPageNumberIndicator.Text = string.Format(ResourceHandler.GetString("PageNumberIndocator_FormatWithoutTotal"), currentPage);
            }
            else
            {
                string formatstring = ResourceHandler.GetString("PageNumberIndocator_FormatWithTotal");
                mToolManager.mPageNumberIndicator.Text = string.Format(ResourceHandler.GetString("PageNumberIndocator_FormatWithTotal"), currentPage, numPages);
            }
        }

        internal void DisableScrolling()
        {
            mPDFView.SetZoomEnabled(false);
            mPDFView.SetScrollEnabled(false);
        }

        internal void EnableScrolling()
        {
            mPDFView.SetZoomEnabled(true);
            mPDFView.SetScrollEnabled(true);
        }

        protected PDFPoint GetPageCoordinates(Windows.UI.Input.PointerPoint pp, int pageNumber)
        {
            DoubleRef ptX = new DoubleRef(pp.Position.X);
            DoubleRef ptY = new DoubleRef(pp.Position.Y);
            mPDFView.ConvScreenPtToPagePt(ptX, ptY, pageNumber);
            return new PDFPoint(ptX.Value, ptY.Value);
        }


        /// <summary>
        /// Computes and returns a rectangle with the bounding box of the page indicated by page_num
        /// </summary>
        /// <param name="page_num">The page number of the page whose bounding box is requested</param>
        /// <returns>A Rect containing the page bounding box</returns>
        protected PDFRect BuildPageBoundBoxOnClient(int page_num)
        {
            PDFRect rect = null;
            if (page_num >= 1)
            {
                try
                {
                    mPDFView.DocLockRead();
                    if (mPage == null)
                    {
                        mPage = mPDFView.GetDoc().GetPage(page_num);
                    }
                    if (mPage != null)
                    {
                        rect = new PDFRect();
                        PDFRect r = mPage.GetCropBox();

                        rect = ConvertFromPageRectToCanvasRect(r, page_num);
                        rect.Normalize();

                        //PDFDouble x1 = new PDFDouble(r.x1);
                        //PDFDouble y1 = new PDFDouble(r.y1);
                        //PDFDouble x2 = new PDFDouble(r.x2);
                        //PDFDouble y2 = new PDFDouble(r.y2);

                        //// Get coordinates of two opposite points in screen space
                        //mPDFView.ConvPagePtToScreenPt(x1, y1, page_num);
                        //mPDFView.ConvPagePtToScreenPt(x2, y2, page_num);

                        //// Get min and max, since page can be rotated
                        //double min_x = Math.Min(x1.Value, x2.Value);
                        //double max_x = Math.Max(x1.Value, x2.Value);
                        //double min_y = Math.Min(y1.Value, y2.Value);
                        //double max_y = Math.Max(y1.Value, y2.Value);

                        //double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                        //double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

                        //rect.Set(min_x + sx, min_y + sy, max_x + sx, max_y + sy);
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
            return rect;
        }


        /// <summary>
        /// Build the bounding box for the annotation (mAnnotBBox)
        /// The rectangle is in page space.
        /// </summary>
        protected void BuildAnnotBBox()
        {
            if (mAnnot != null)
            {
                mAnnotBBox.Set(0, 0, 0, 0);
                try
                {
                    PDFRect r = mAnnot.GetRect();
                    mAnnotBBox.Set(r.x1, r.y1, r.x2, r.y2);
                    mAnnotBBox.Normalize();
                }
                catch (Exception)
                {
                }
            }
        }

        protected bool IsPointInsideAnnot(double viewCtrlX, double viewCtrlY)
        {
            if (mAnnot != null)
            {
                PDFDouble x = new PDFDouble(viewCtrlX);
                PDFDouble y = new PDFDouble(viewCtrlY);
                mPDFView.ConvScreenPtToPagePt(x, y, mAnnotPageNum);
                if (mAnnotBBox.Contains(x.Value, y.Value))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds an annotation (if available) that is in screen space (x, y)
        /// And sets mAnnot, mAnnotBBox, and mAnnotPageNum accordingly
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        protected void SelectAnnot(int sc_x, int sc_y, double distanceThreshold = -1, double minimumLineWeight = -1)
        {
            mAnnot = null;
            mAnnotPageNum = 0;

            PDFDoc doc = mPDFView.GetDoc();
            if (doc != null)
            {
                try
                {
                    IAnnot a = null;
                    if (distanceThreshold < 0)
                    {
                        a = mPDFView.GetAnnotAt(sc_x, sc_y);
                    }
                    else
                    {
                        a = mPDFView.GetAnnotAt(sc_x, sc_y, distanceThreshold, minimumLineWeight);
                    }
                    if (a != null && a.IsValid())
                    {
                        mAnnot = a;
                        BuildAnnotBBox();
                        mAnnotPageNum = mPDFView.GetPageNumberFromScreenPoint(sc_x, sc_y);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    //mPDFView.DocUnlock();
                }
            }
        }

        internal PDFRect ConvertFromScreenRectToPageRect(PDFRect rect, int pageNum)
        {
            PDFDouble x1 = new PDFDouble(rect.x1);
            PDFDouble y1 = new PDFDouble(rect.y1);
            PDFDouble x2 = new PDFDouble(rect.x2);
            PDFDouble y2 = new PDFDouble(rect.y2);

            mPDFView.ConvScreenPtToPagePt(x1, y1, pageNum);
            mPDFView.ConvScreenPtToPagePt(x2, y2, pageNum);

            PDFRect retRect = new PDFRect(x1.Value, y1.Value, x2.Value, y2.Value);

            return retRect;
        }

        internal PDFRect ConvertFromPageRectToScreenRect(PDFRect rect, int pageNum)
        {
            PDFDouble x1 = new PDFDouble(rect.x1);
            PDFDouble y1 = new PDFDouble(rect.y1);
            PDFDouble x2 = new PDFDouble(rect.x2);
            PDFDouble y2 = new PDFDouble(rect.y2);

            mPDFView.ConvPagePtToScreenPt(x1, y1, pageNum);
            mPDFView.ConvPagePtToScreenPt(x2, y2, pageNum);

            PDFRect retRect = new PDFRect(x1.Value, y1.Value, x2.Value, y2.Value);

            return retRect;
        }

        internal PDFRect ConvertFromCanvasRectToPageRect(PDFRect rect, int pageNum)
        {
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

            PDFDouble x1 = new PDFDouble(rect.x1 - sx);
            PDFDouble y1 = new PDFDouble(rect.y1 - sy);
            PDFDouble x2 = new PDFDouble(rect.x2 - sx);
            PDFDouble y2 = new PDFDouble(rect.y2 - sy);

            mPDFView.ConvScreenPtToPagePt(x1, y1, pageNum);
            mPDFView.ConvScreenPtToPagePt(x2, y2, pageNum);

            PDFRect retRect = new PDFRect(x1.Value, y1.Value, x2.Value, y2.Value);
            retRect.Normalize();

            return retRect;
        }

        internal PDFRect ConvertFromPageRectToCanvasRect(PDFRect rect, int pageNum)
        {
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();

            PDFDouble x1 = new PDFDouble(rect.x1);
            PDFDouble y1 = new PDFDouble(rect.y1);
            PDFDouble x2 = new PDFDouble(rect.x2);
            PDFDouble y2 = new PDFDouble(rect.y2);

            mPDFView.ConvPagePtToScreenPt(x1, y1, pageNum);
            mPDFView.ConvPagePtToScreenPt(x2, y2, pageNum);

            PDFRect retRect = new PDFRect(x1.Value + sx, y1.Value + sy, x2.Value + sx, y2.Value + sy);
            
            return retRect;
        }


        /// <summary>
        /// Creates a one time TranslateAnimation with parameters that matches the virtual keyboard
        /// </summary>
        /// <param name="_target">FrameworkElement to animate</param>
        /// <param name="_tX">Starting Y offset of animation.</param>
        /// <param name="_tY">Ending Y offset of animation.</param>
        /// <param name="_duration">Duration of the animation</param>
        /// <returns>Completed Storyboard</returns>
        public static Storyboard KeyboardTranslate(FrameworkElement _target, Double _fY, Double _tY, TimeSpan _duration)
        {
            Storyboard sb = new Storyboard();

            _target.RenderTransform = new TranslateTransform();

            DoubleAnimationUsingKeyFrames ttY = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(_duration),
                RepeatBehavior = new RepeatBehavior(1),
            };
            ttY.KeyFrames.Add(new DiscreteDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0)),
                Value = _fY,
            }
            );
            ttY.KeyFrames.Add(new SplineDoubleKeyFrame
            {
                KeySpline = new KeySpline
                {
                    ControlPoint1 = new UIPoint(0.10, 0.90),
                    ControlPoint2 = new UIPoint(0.20, 1),
                },
                KeyTime = KeyTime.FromTimeSpan(_duration),
                Value = _tY,
            });

            Storyboard.SetTarget(ttY, _target);
            Storyboard.SetTargetName(ttY, _target.Name);

            Storyboard.SetTargetProperty(ttY, "(UIElement.RenderTransform).(TranslateTransform.Y)");

            sb.Children.Add(ttY);
            return sb;
        }


        protected void PageDownScroll()
        {
            mPDFView.SetVScrollPos(mPDFView.GetAnnotationCanvasVerticalOffset() + mPDFView.ActualHeight - PageUpDownScrollDistanceMargin);
        }

        protected void PageUpScroll()
        {
            mPDFView.SetVScrollPos(mPDFView.GetAnnotationCanvasVerticalOffset() - mPDFView.ActualHeight + PageUpDownScrollDistanceMargin);
        }


        /// <summary>
        /// Returns the length of the line ending for the open arrow based on the stroke thickness, assuming a scale factor of 1.
        /// </summary>
        /// <param name="strokThickenss">The stroke thickenss for the arrow to draw.</param>
        /// <returns>The required length for the arrow head</returns>
        protected double GetLineEndingLength(double strokThickenss)
        {
            return 8 * (Math.Sqrt(strokThickenss) + strokThickenss);
        }

        protected async void LaunchBrowser(string uristring)
        {
            if (string.IsNullOrWhiteSpace(uristring))
            {
                return;
            }

            try
            {
                uristring = Annots.Link.GetNormalizedURL(uristring);

                // for now, we ignore emails.
                if (IsEmail(uristring))
                {
                    return;
                }

                var uri = new Uri(uristring);
                var success = await Windows.System.Launcher.LaunchUriAsync(uri);
                if (!success)
                {
                    // Take appropriate action if desirable
                }
            }
            catch (Exception)
            {
                // Take appropriate action if desirable
            }

        }

        // This was found here: http://haacked.com/archive/2007/08/21/i-knew-how-to-validate-an-email-address-until-i.aspx/
        public bool IsEmail(string url)
        {
            string pattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|"
              + @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)"
              + @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";

            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(url);
        }

        #endregion Utility Functions


        #region Manipulation Utilities

        internal void AddPointer(Pointer ptr)
        {
            if (!mToolManager.ContactPoints.ContainsKey(ptr.PointerId))
            {
                if (ptr.PointerDeviceType == PointerDeviceType.Touch)
                {
                    ++mToolManager.TouchPoints;
                }
            }
            mToolManager.ContactPoints[ptr.PointerId] = ptr;

        }

        internal void RemovePointer(Pointer ptr)
        {
            if (mToolManager.ContactPoints.ContainsKey(ptr.PointerId))
            {
                mToolManager.ContactPoints[ptr.PointerId] = null;
                mToolManager.ContactPoints.Remove(ptr.PointerId);
                if (ptr.PointerDeviceType == PointerDeviceType.Touch)
                {
                    --mToolManager.TouchPoints;
                }
            }
            if (mToolManager.ContactPoints.Count == 0)
            {
                mToolManager.EnableScrollByManipulation = false;
                mToolManager.EnableOneFingerScroll = false;
                mRectToKeepOnScreenWhileManipulating = null;
            }
        }

        #endregion ManipulationUtilities


        #region Handle Selected Text



        /// <summary>
        /// Call this Function when a tool is created to clear text selection and suppress any context menu options related to it.
        /// </summary>
        protected void DisallowTextSelection()
        {
            DeselectAllText();
            mAllowTextSelectionOptions = false;
        }


        /// <summary>
        /// This functions draws the current text selection to the screen.
        /// To only include text selection where it is visible, we draw all quads for a
        /// specific page on one canvas in page space. This canvas can then be positioned,
        /// rotated, scaled, and added or removed based on the state of the viewer.
        /// </summary>
        /// <param name="reposition">If true, everything will be repositoned. Set to true after zoom or changing view 
        /// mode when every canvas has to be positioned</param>
        protected virtual void DrawSelection(bool reposition = false)
        {
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
            mSelectionStartPage = mPDFView.GetSelectionBeginPage();
            mSelectionEndPage = mPDFView.GetSelectionEndPage();
            IList<int> pagesOnScreen = mPDFView.GetVisiblePages();
            List<int> addedPages = GetPageDifference(pagesOnScreen, mPagesOnScreen);
            List<int> removedPages = GetPageDifference(mPagesOnScreen, pagesOnScreen);

            mPagesOnScreen = pagesOnScreen;
            if (mSelectionStartPage == -1 || mSelectionStartPage == -1)
            {
                return;
            }

            // Pages removed from screen should be detached
            foreach (int pgnm in removedPages)
            {
                if (mSelectionCanvases.ContainsKey(pgnm) && mSelectionCanvases[pgnm].IsAttached)
                {
                    SelectionDrawing sd = mSelectionCanvases[pgnm];
                    sd.IsAttached = false;
                    DetachSelectionDrawing(sd);
                    //mToolManager.TextSelectionCanvas.Children.Remove(mSelectionCanvases[pgnm].Canvas);
                }
            }

            try
            {
                mPDFView.DocLockRead();
                foreach (int pgnm in addedPages)
                {
                    if (!mPDFView.HasSelectionOnPage(pgnm))
                    {
                        continue;
                    }

                    // if we already have the page set up, we just need to stick it back in and position it.
                    if (mSelectionCanvases.ContainsKey(pgnm))
                    {
                        SelectionDrawing sd = mSelectionCanvases[pgnm];
                        sd.IsAttached = true;
                        AttachSelectionDrawing(mSelectionCanvases[pgnm], mPDFView.GetAnnotationCanvas());
                        PositionPageCanvas(pgnm, mSelectionCanvases[pgnm].Canvas);
                        continue;
                    }

                    PDFViewCtrlSelection sel = mPDFView.GetSelection(pgnm);

                    double[] quads = sel.GetQuads();
                    if (quads == null)
                    {
                        continue;
                    }

                    // create a new page for selection, at normalized scale
                    SelectionDrawing selDrawing = new SelectionDrawing();
                    selDrawing.IsAttached = true;
                    selDrawing.Canvas = new Canvas();
                    selDrawing.Canvas.IsHitTestVisible = false;

                    pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(pgnm);
                    selDrawing.Canvas.Width = page.GetCropBox().Width();
                    selDrawing.Canvas.Height = page.GetCropBox().Height();
                    
                    selDrawing.PageHeight = page.GetCropBox().Height();
                    selDrawing.Quads = new List<PDFRect>();

                    mSelectionCanvases[pgnm] = selDrawing;
                    AttachSelectionDrawing(selDrawing, mPDFView.GetAnnotationCanvas());
                    PositionPageCanvas(pgnm, selDrawing.Canvas);

                    int sz = quads.Length / 8;

                    int k = 0;
                    PDFRect drawRect;

                    // each quad consists of 8 consecutive points
                    for (int i = 0; i < sz; ++i, k += 8)
                    {
                        drawRect = new PDFRect(quads[k], selDrawing.PageHeight - quads[k + 1], quads[k + 4], selDrawing.PageHeight - quads[k + 5]);
                        drawRect.Normalize();

                        // draw rectangle on selected text
                        Rectangle rect = new Rectangle();
                        rect.SetValue(Canvas.LeftProperty, drawRect.x1);
                        rect.SetValue(Canvas.TopProperty, drawRect.y1);
                        rect.Width = drawRect.x2 - drawRect.x1;
                        rect.Height = drawRect.y2 - drawRect.y1;
                        rect.Fill = mTextSelectionBrush;

                        // This will add the rectangle to the screen
                        selDrawing.Canvas.Children.Add(rect);
                        selDrawing.Quads.Add(drawRect);
                    }

                }

                // We need to reposition pages that remained on the screen
                if (reposition)
                {
                    foreach (int pgnm in pagesOnScreen)
                    {
                        if (mSelectionCanvases.ContainsKey(pgnm) && !addedPages.Contains(pgnm))
                        {
                            PositionPageCanvas(pgnm, mSelectionCanvases[pgnm].Canvas);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                mPDFView.DocUnlockRead();
            }
        }

        /// <summary>
        /// Positions the page canvas on the page at the correct scale.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="canvas"></param>
        private void PositionPageCanvas(int pageNumber, Canvas canvas)
        {
            TransformGroup transGroup = new TransformGroup();

            pdftron.PDF.Page page = mPDFView.GetDoc().GetPage(pageNumber);
            DoubleRef x = new DoubleRef(0);
            DoubleRef y = new DoubleRef(page.GetCropBox().Height());
            int rotation = ((int)page.GetRotation() + (int)mPDFView.GetRotation()) % 4;

            mPDFView.ConvPagePtToScreenPt(x, y, pageNumber);
            canvas.SetValue(Canvas.LeftProperty, x.Value + mPDFView.GetAnnotationCanvasHorizontalOffset());
            canvas.SetValue(Canvas.TopProperty, y.Value + mPDFView.GetAnnotationCanvasVerticalOffset());

            CompositeTransform ct = new CompositeTransform();
            ct.ScaleX = mPDFView.GetZoom();
            ct.ScaleY = ct.ScaleX;
            ct.Rotation = rotation * 90;
               
            canvas.RenderTransform = ct;
        }


        /// <summary>
        /// Figures out if the point is inside one of the rectangles representing our selection
        /// </summary>
        /// <param name="p">The point we're looking for in PDFViewCtrl space</param>
        /// <returns>True if the point is inside one of the rectangles</returns>
        protected bool IsPointInSelection(UIPoint p)
        {
            int pgnm = mPDFView.GetPageNumberFromScreenPoint(p.X, p.Y);
            if (mSelectionCanvases.ContainsKey(pgnm))
            {
                DoubleRef x = new DoubleRef(p.X);
                DoubleRef y = new DoubleRef(p.Y);
                mPDFView.ConvScreenPtToPagePt(x, y, pgnm);

                double xp = x.Value;
                double yp = mSelectionCanvases[pgnm].PageHeight - y.Value;
                foreach (PDFRect rect in mSelectionCanvases[pgnm].Quads)
                {
                    if (rect.Contains(xp, yp))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Figures out if the point is inside one of the rectangles representing our selection
        /// </summary>
        /// <param name="p">The point we're looking for in PDFViewCtrl space</param>
        /// <param name="inflate">If the rectangles should be inflated for more lenient hit testing</param>
        /// <returns>True if the point is inside one of the [inflated] rectangles</returns>
        protected bool IsPointInSelection(UIPoint p, bool inflate)
        {
            if (!inflate)
            {
                return IsPointInSelection(p);
            }
            int pgnm = mPDFView.GetPageNumberFromScreenPoint(p.X, p.Y);
            if (mSelectionCanvases.ContainsKey(pgnm))
            {
                DoubleRef x = new DoubleRef(p.X);
                DoubleRef y = new DoubleRef(p.Y);
                mPDFView.ConvScreenPtToPagePt(x, y, pgnm);

                double xp = x.Value;
                double yp = mSelectionCanvases[pgnm].PageHeight - y.Value;
                foreach (PDFRect rect in mSelectionCanvases[pgnm].Quads)
                {
                    PDFRect r = new PDFRect(rect.x1, rect.y1, rect.x2, rect.y2);
                    r.Inflate(5);
                    if (r.Contains(xp, yp))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a list of all integers in pageList1 that are not in pageList2
        /// </summary>
        /// <param name="pageList1"></param>
        /// <param name="pageList2"></param>
        /// <returns></returns>
        protected List<int> GetPageDifference(IList<int> pageList1, IList<int> pageList2)
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

        protected void CopySelectedTextToClipBoard()
        {
            string text = "";

            // Extract selected text
            if (mPDFView.HasSelection())
            {
                mSelectionStartPage = mPDFView.GetSelectionBeginPage();
                mSelectionEndPage = mPDFView.GetSelectionEndPage();
                if (mSelectionStartPage > 0)
                {
                    for (int pgnm = mSelectionStartPage; pgnm <= mSelectionEndPage; pgnm++)
                    {
                        if (mPDFView.HasSelectionOnPage(pgnm))
                        {
                            text += mPDFView.GetSelection(pgnm).GetAsUnicode();
                        }
                    }
                }
            }

            UtilityFunctions.CopySelectedTextToClipBoard(text);
        }


        /// <summary>
        /// Helper function to select all text.
        /// </summary>
        protected void SelectAllText()
        {
            mPDFView.SelectAll();
            DetachAllTextSelection();
            mPagesOnScreen.Clear();
            mSelectionCanvases.Clear();
            DrawSelection();
        }

        /// <summary>
        /// Helper function to deselct all text.
        /// </summary>
        protected void DeselectAllText()
        {
            if (mPDFView.HasSelection())
            {
                mPDFView.ClearSelection();
                DetachAllTextSelection();
                mPagesOnScreen.Clear();
                mSelectionCanvases.Clear();
                DrawSelection();
            }
        }

        protected void DetachAllTextSelection()
        {
            if (mSelectionCanvases == null)
            {
                return;
            }
            foreach (SelectionDrawing sd in mSelectionCanvases.Values)
            {
                DetachSelectionDrawing(sd);
            }
        }

        /// <summary>
        /// Attaches the SelectionDrawing to its parent canvas.
        /// </summary>
        /// <param name="sd"></param>
        /// <param name="parent"></param>
        private void AttachSelectionDrawing(SelectionDrawing sd, Canvas parent)
        {
            parent.Children.Insert(0, sd.Canvas);
            sd.Canvas.Tag = parent;
        }

        /// <summary>
        /// Detaches the SelectionDrawing from its parent canvas.
        /// </summary>
        /// <param name="sd"></param>
        protected void DetachSelectionDrawing(SelectionDrawing sd)
        {
            Canvas parent = sd.Canvas.Tag as Canvas;
            if (parent != null)
            {
                sd.Canvas.Tag = null;
                parent.Children.Remove(sd.Canvas);
            }
        }

        /// <summary>
        /// Tries to select text at the point
        /// point is in screen coordinates
        /// </summary>
        /// <param name="point">The point where you want to check for text</param>
        /// <param name="expansion">How much you want to expand that point, to be more "generous"</param>
        /// <returns></returns>
        protected bool SelectTextAtPoint(UIPoint point, double expansion = 0)
        {
            bool foundText = false;

            try
            {
                mPDFView.DocLockRead();
                double zoomFactor = mPDFView.GetZoom();
                double x1 = point.X;
                double y1 = point.Y;
                double diff = 0.05 * zoomFactor;
                if (expansion > 0)
                {
                    diff = expansion * zoomFactor;
                }
                x1 = Math.Max(x1 - diff, 0);
                y1 = Math.Max(y1 - diff, 0);
                diff *= 2;
                double x2 = x1 + diff;
                double y2 = y1 + diff;
                mPDFView.ClearSelection();
                mPDFView.SetTextSelectionMode(TextSelectionMode.e_rectangular);
                if (mPDFView.Select(x1, y1, x2, y2))
                {
                    foundText = true;
                }
                mPDFView.SetTextSelectionMode(TextSelectionMode.e_structural);
            }
            catch (Exception)
            {

            }
            finally
            {
                mPDFView.DocUnlockRead();
            }

            return foundText;
        }


        #endregion Handle Selected Text


        #region Author

        

        public void SetAuthor(Annots.IMarkup markup)
        {          
            if (!mToolManager.AddAuthorToAnnotations)
            {
                return;
            }

            string author = Settings.AnnotationAutor;
            if (!string.IsNullOrEmpty(author))
            {
                SetAuthor(markup, author);
                return;
            }

            if (!Settings.AnnotationAuthorHasBeenAsked)
            {
                Settings.AnnotationAuthorHasBeenAsked = true;
                mMarkupToAuthor = markup;
                GetAuthorFromDialog();
            }

            return;
        }

        private void SetAuthor(Annots.IMarkup markup, string name)
        {
            try
            {
                mPDFView.DocLock(true);
                markup.SetTitle(name);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("An error occurred when trying to set the author: " + e.Message);
            }
            finally
            {
                mPDFView.DocUnlock();
            }
        }

        protected void GetAuthorFromDialog()
        {
            mAuthorVM = new Controls.ViewModels.AuthorDialogViewModel();
            if (mToolManager.AuthorDialog == null)
            {
                mAuthorDialogPopup = new Windows.UI.Xaml.Controls.Primitives.Popup();
                mAuthorInputDialog = new Utilities.AuthorInputDialog();

                mAuthorInputDialog.Width = Window.Current.Bounds.Width;
                mAuthorInputDialog.Height = Window.Current.Bounds.Height;

                mAuthorDialogPopup.IsOpen = true;
                Window.Current.SizeChanged += Current_SizeChanged;

                mAuthorDialogPopup.Child = mAuthorInputDialog;
                mAuthorInputDialog.SetAuthorViewModel(mAuthorVM);
            }
            else
            {
                mToolManager.AuthorDialog.Show();
                mToolManager.AuthorDialog.SetAuthorViewModel(mAuthorVM);
            }
            mAuthorVM.AuthorDialogFinished += AuthorViewModel_AuthorDialogFinished;
            mToolManager.mOpenAuthorDialogViewModel = mAuthorVM;

            mPDFView.IsEnabled = false;

            
        }

        void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            mAuthorInputDialog.Width = e.Size.Width;
            mAuthorInputDialog.Height = e.Size.Height;
        }

        void AuthorViewModel_AuthorDialogFinished(string authorName)
        {
            if (authorName != null)
            {
                SetAuthor(mMarkupToAuthor, authorName);

                Windows.Storage.ApplicationDataContainer roamingSettings =
                    Windows.Storage.ApplicationData.Current.RoamingSettings;
                Settings.AnnotationAutor = authorName;
            }
            mToolManager.mOpenAuthorDialogViewModel = null;
            mAuthorVM.AuthorDialogFinished -= AuthorViewModel_AuthorDialogFinished;
            if (mAuthorDialogPopup != null)
            {
                mAuthorDialogPopup.IsOpen = false;
            }
            else if (mToolManager.AuthorDialog != null)
            {
                mToolManager.AuthorDialog.Hide();
            }
            mPDFView.IsEnabled = true;
        }


        #endregion Author
        

    }
}

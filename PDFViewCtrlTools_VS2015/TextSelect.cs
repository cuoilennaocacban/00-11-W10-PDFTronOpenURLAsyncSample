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
    class TextSelect : Tool
    {
        protected class SelectionWidget
        {
            public double QuadHeight
            {
                get;
                set;
            }
            public PDFPoint BottomPoint // top of the shape
            {
                get;
                set;
            }
            public SelectionWidget()
            {
                BottomPoint = new PDFPoint();
            }
        }

        protected Canvas mViewerCanvas;
        protected SelectionWidget[] mSelectionWidgets;
        protected PDFPoint mPointerDownPoint; // the position where the pointer went down
        protected PDFPoint mCurrentPointerPoint; // the current pointer position
        protected PDFPoint mOppositePoint; // the point to select from that is not moved (i.e. the other widget)
        protected PDFPoint mCurrentPointOffset; // the offset from the mouse down to the selection of text.

        protected double mWidgetHitRadius = 30;
        protected int mCurrentWidget = -1;
        protected int mPointerID = -1;
        protected const int mSelectedTextTappingErrorMargin = 5;
        protected bool mSelectingByMouse = false;
        protected bool mHadSelections = false; // this flag is set to true if the tool had selections by the time a mouse press happens
        protected bool mHasMoved = false;
        protected bool mPointerReleaseDismissedMenu = false; // This lets the (right) tapped event know if a menu was dismissed by releasing a pointer
        protected bool mTouchScrolling = false;
        protected bool mCtrlIsDown = false; // Used for hotkeys

        protected bool mForceControlPoints = false;
        protected int mLimitToPage = -1; // set to one to force selection to stay on the current page
        protected PDFRect mPageBoundingBoxRect;

        // graphics properties
        protected double mWidgetBorderThickness = 2;
        protected double mWidgetRadius = 10;
        protected Path[] mSelectionEllipses;
        protected TranslateTransform[] mSelectionEllipseTranslations; // positions the selection ellipses
        protected SolidColorBrush mWidgetBorderBrush;
        protected SolidColorBrush mWidgetFillBrush;
        protected SolidColorBrush mSelectionRectangleBrush;

        // double and shift click properties
        UIPoint m_cnAnchorPoint;
        bool m_HasAnchorPoint = false;
        PDFRect m_cnAnchorRect;
        bool m_HasAnchorRect = false;
        bool m_IsSelectingWithShift = false;
        protected double mDoubleClickSelectionQuadLength = 0;

        // automatic scrolling
        protected bool mCanScroll = false;
        protected UIPoint m_scDownPoint;
        protected DispatcherTimer _AutomaticScrollTimer;
        protected double m_ScrollSpeedX;
        protected double m_ScrollSpeedY;
        protected UIPoint m_scScrollPoint;

        // This is for delaying the drawing so that we don't draw on each PointerMoved event, as that doesn't let slower devices actually display the selection.
        protected DateTime mLastDraw = DateTime.MinValue;
        protected TimeSpan mDrawingInterval = TimeSpan.FromSeconds(0.07);

        public TextSelect(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_text_select;
            mToolMode = ToolType.e_text_select;

            mPointerDownPoint = new PDFPoint();
            mCurrentPointerPoint = new PDFPoint();
            mOppositePoint = new PDFPoint();
            mCurrentPointOffset = new PDFPoint();

            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            mViewerCanvas.Children.Add(this);
            
            this.Width = mViewerCanvas.ActualWidth;
            this.Height = mViewerCanvas.ActualHeight;
            this.IsHitTestVisible = true;
            mSelectionWidgets = new SelectionWidget[2];
            mSelectionWidgets[0] = new SelectionWidget();
            mSelectionWidgets[1] = new SelectionWidget();
            mSelectedAreasForHitTest = new List<PDFRect>();

            mSelectionRectangleBrush = new SolidColorBrush(Color.FromArgb(80, 40, 70, 200));

            // create Widgets
            mWidgetBorderBrush = new SolidColorBrush(Colors.Black);
            mWidgetFillBrush = new SolidColorBrush(Colors.White);
            mSelectionEllipseTranslations = new TranslateTransform[2];
            mSelectionEllipses = new Path[2];
            
            for (int i = 0; i < 2; i++)
            {
                mSelectionEllipseTranslations[i] = new TranslateTransform();

                EllipseGeometry geom = new EllipseGeometry();
                geom.RadiusX = mWidgetRadius;
                geom.RadiusY = mWidgetRadius;
                mSelectionEllipses[i] = new Path();
                mSelectionEllipses[i].Data = geom;
                mSelectionEllipses[i].Stroke = mWidgetBorderBrush;
                mSelectionEllipses[i].StrokeThickness = mWidgetBorderThickness;
                mSelectionEllipses[i].Fill = mWidgetFillBrush;
                mSelectionEllipses[i].RenderTransform = mSelectionEllipseTranslations[i];
            }
            this.Children.Add(mSelectionEllipses[0]);
            this.Children.Add(mSelectionEllipses[1]);
            mSelectionEllipses[0].Opacity = 0;
            mSelectionEllipses[1].Opacity = 0;
        }

        internal override void OnClose()
        {
            DetachAllTextSelection();
            if (this.Children.Contains(mSelectionEllipses[0]))
            {
                this.Children.Remove(mSelectionEllipses[0]);
                this.Children.Remove(mSelectionEllipses[1]);
            }
            mPDFView.ClearSelection();
            EnableScrolling();

            TurnOffScrollTimer();

            if (mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Remove(this);
            }

            base.OnClose();
        }

        internal override bool OnScale()
        {
            DrawSelection(true); // redraw selection
            if (!mIsUsingMouse)
            {
                PositionSelectionWidgets();
            }
            base.OnScale();
            return false;
        }

        internal override bool OnSize()
        {
            DeselectAllText();
            mNextToolMode = ToolType.e_pan;
            return false;
        }

        internal override bool OnViewChanged(Object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            if (mIsShowingCommandMenu)
            {
                HideMenu(mCommandMenu);
                mIsShowingCommandMenu = false;
            }
            base.OnViewChanged(sender, e);
            return false;
        }
        internal override bool OnPageNumberChanged(int current_page, int num_pages)
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mIsShowingCommandMenu = false;
            }
            base.OnPageNumberChanged(current_page, num_pages);
            return false;
        }




        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID >= 0)        
            {
                // Do nothing
                return false;
            }
            if (mPointerID < 0)
            {
                mPointerID = (int)e.Pointer.PointerId;
            }

            UIPoint pp = e.GetCurrentPoint(mViewerCanvas).Position;

            double x = pp.X;
            double y = pp.Y;

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !mForceControlPoints)
            {
                MouseDownHandler(x, y, e);
            }
            else // touch or stylus
            {
                TouchDownHandler(x, y, e);
            }
            mJustSwitchedFromAnotherTool = false;
            return false;
        }

        private void MouseDownHandler(double x, double y, PointerRoutedEventArgs e)
        {
            mCanScroll = false;
            mIsUsingMouse = true;
            mHasMoved = false;
            m_scDownPoint = e.GetCurrentPoint(mPDFView).Position;
            m_IsSelectingWithShift = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) == Windows.System.VirtualKeyModifiers.Shift;
            mViewerCanvas.CapturePointer(e.Pointer);

            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && !mIsShowingCommandMenu)
            {
                mSelectingByMouse = true;
                if ((!m_HasAnchorPoint || !m_IsSelectingWithShift))
                {
                    m_cnAnchorPoint.X = x;
                    m_cnAnchorPoint.Y = y;
                    m_HasAnchorPoint = true;
                }


                if (m_IsSelectingWithShift)
                {
                    UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);
                    MouseMovedHandler(e);
                    mHasMoved = true; // if shift clicking, we don't want to enforce click and drag
                }
                else
                {
                    m_HasAnchorRect = false;
                }
            }
        }

        private void TouchDownHandler(double x, double y, PointerRoutedEventArgs e)
        {
            // see if we're hitting one of the widgets
            mCurrentWidget = -1;
            double thresh = mWidgetHitRadius * mWidgetHitRadius;
            double dist0 = Math.Pow((x - mSelectionWidgets[0].BottomPoint.x), 2) + Math.Pow((y - (mSelectionWidgets[0].BottomPoint.y + mWidgetRadius)), 2);
            double dist1 = Math.Pow((x - mSelectionWidgets[1].BottomPoint.x), 2) + Math.Pow((y - (mSelectionWidgets[1].BottomPoint.y + mWidgetRadius)), 2);
            if (dist0 < dist1 && dist0 < thresh)
            {
                mCurrentWidget = 0;
            }
            else if (dist1 < dist0 && dist1 < thresh)
            {
                mCurrentWidget = 1;
            }

            // If we've hit a widget
            if (mCurrentWidget >= 0)
            {
                DisableScrolling();
                mTouchScrolling = false;
                mViewerCanvas.CapturePointer(e.Pointer);

                mPointerDownPoint.x = x;
                mPointerDownPoint.y = y;
                mCurrentPointerPoint.x = x;
                mCurrentPointerPoint.y = y;

                // figure out the opposite point
                mOppositePoint.x = mSelectionWidgets[1 - mCurrentWidget].BottomPoint.x;
                mOppositePoint.y = mSelectionWidgets[1 - mCurrentWidget].BottomPoint.y - (mSelectionWidgets[1 - mCurrentWidget].QuadHeight / 2);

                // figure out the offset from touch location to selection location
                mCurrentPointOffset.x = mSelectionWidgets[mCurrentWidget].BottomPoint.x - x;
                mCurrentPointOffset.y = (mSelectionWidgets[mCurrentWidget].BottomPoint.y - (mSelectionWidgets[mCurrentWidget].QuadHeight / 2)) - y;

                mSelectionEllipses[0].Opacity = 0;
                mSelectionEllipses[1].Opacity = 0;
            }
            else
            {
                mTouchScrolling = true; // tells PointerCaptureLost that we're expecting it since we didn't hit a widget
            }
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == mPointerID)
            {
                // This clump is needed because the mouse doesn't lose capture when it goes outside the app's boundaries.
                if ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) // mouse not pressed
                {
                    mPointerID = -1;
                    mViewerCanvas.ReleasePointerCaptures();
                    UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
                    return false;
                }


                if (mIsUsingMouse && mSelectingByMouse && !mForceControlPoints)
                {
                    MouseMovedHandler(e);
                }
                else // Using touch or stylus
                {
                    DateTime currTime = DateTime.Now;
                    if (currTime - mLastDraw > mDrawingInterval)
                    {
                        mLastDraw = currTime;
                        TouchMovedHandler(e);
                    }
                }
            }
            return true;
        }

        private void MouseMovedHandler(PointerRoutedEventArgs e)
        {
            PointerPoint cnPP = e.GetCurrentPoint(mViewerCanvas);
            if (m_HasAnchorRect)
            {
                SelectAndDraw(m_cnAnchorRect, cnPP.Position);
                UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);
            }
            else
            {
                SelectAndDraw(m_cnAnchorPoint, cnPP.Position);
                if (!mHasMoved && // make sure we move at least 4 points in some direction
                    (Math.Abs(cnPP.Position.X - m_cnAnchorPoint.X) > 4 || Math.Abs(cnPP.Position.Y - m_cnAnchorPoint.Y) > 4))
                {
                    mHasMoved = true;
                    UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);
                }
            }
            ScrollingHandler(e.GetCurrentPoint(mPDFView).Position);
            return;
        }

        private void TouchMovedHandler(PointerRoutedEventArgs e)
        {
            if (mCurrentWidget >= 0)
            {
                UIPoint pp = e.GetCurrentPoint(mViewerCanvas).Position;
                // calculate position of pointer
                double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
                double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
                mCurrentPointerPoint.x = pp.X;
                mCurrentPointerPoint.y = pp.Y;


                double p1x = mOppositePoint.x - sx;
                double p1y = mOppositePoint.y - sy;

                // need to take into account the offset from where the widget was pressed to where we're selecting
                double p2x = mCurrentPointerPoint.x + mCurrentPointOffset.x;
                double p2y = mCurrentPointerPoint.y + mCurrentPointOffset.y;

                if (mLimitToPage > 0)
                {
                    if (p2x < mPageBoundingBoxRect.x1)
                    {
                        p2x = mPageBoundingBoxRect.x1;
                    }
                    if (p2y < mPageBoundingBoxRect.y1)
                    {
                        p2y = mPageBoundingBoxRect.y1;
                    }
                    if (p2x > mPageBoundingBoxRect.x2)
                    {
                        p2x = mPageBoundingBoxRect.x2;
                    }
                    if (p2y > mPageBoundingBoxRect.y2)
                    {
                        p2y = mPageBoundingBoxRect.y2;
                    }
                }

                // subtract scroll position to get screen space
                p2x -= sx;
                p2y -= sy;


                mPDFView.ClearSelection();
                DetachAllTextSelection();

                mPagesOnScreen.Clear();
                mSelectionCanvases.Clear();

                mPDFView.SetTextSelectionMode(TextSelectionMode.e_structural);
                mPDFView.Select(p1x, p1y, p2x, p2y);

                DrawSelection();
            }
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                // do nothing
                return false;
            }

            TurnOffScrollTimer();

            mPointerID = -1;
            mViewerCanvas.ReleasePointerCaptures();
            // dismiss menu if open
            mPointerReleaseDismissedMenu = false;
            if (mIsShowingCommandMenu)
            {
                HideMenu(mCommandMenu);
                mIsShowingCommandMenu = false;
                mPointerReleaseDismissedMenu = true;
                EnableScrolling();
            }
            if (mSelectingByMouse && !mForceControlPoints)
            {
                MouseReleasedHandler();
            }
            else if (mCurrentWidget >= 0) // Using touch or stylus
            {
                TouchMovedHandler(e);
                TouchReleaseHandler();
            }
            return true;
        }

        private void MouseReleasedHandler()
        {
            // return cursor to arrow
            UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
            bool hasSelection = mPDFView.HasSelection();
            if (hasSelection && mHasMoved)
            {
                mSelectingByMouse = false;
            }
            else
            {
                if (!mHadSelections)
                {
                    // This means that we didn't have any selection even before the click and drag.
                    // This way, if a tapped event is invoked by this release, pan tool can treat it as if it never was in this toolmode.
                    mNextToolMode = ToolType.e_pan;
                }
            }
        }

        private void TouchReleaseHandler()
        {
            PositionSelectionWidgets();
            EnableScrolling();
            mCurrentWidget = -1;
        }

        internal override bool PointerCaptureLostHandler(object sender, PointerRoutedEventArgs e)
        {
            TurnOffScrollTimer();
            if (mTouchScrolling) // we've lost capture because we started scroll, this is fine
            {
                mPointerID = -1;
            }
            if (mPointerID >= 0) // we were selecting, not good
            {
                mNextToolMode = ToolType.e_pan;
                return true;
            }
            return false;
        }

        internal override bool PointerCanceledHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mNextToolMode = ToolType.e_pan;
            return true;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            // If just created, we should already have a selection. This can't happen with mouse
            if (mJustSwitchedFromAnotherTool)
            {
                try
                {
                    mPDFView.DocLockRead();
                    double x1 = e.GetPosition(mPDFView).X;
                    double y1 = e.GetPosition(mPDFView).Y;
                    double diff = 0.05;
                    x1 = Math.Max(x1 - diff, 0);
                    y1 = Math.Max(y1 - diff, 0);
                    diff *= 2;
                    double x2 = x1 + diff;
                    double y2 = y1 + diff;
                    mPDFView.ClearSelection();
                    mPDFView.SetTextSelectionMode(TextSelectionMode.e_rectangular);
                    if (mPDFView.Select(x1, y1, x2, y2))
                    {
                        DrawSelection();
                        PositionSelectionWidgets();
                        
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("" + ex.Message);
                }
                finally
                {
                    mPDFView.DocUnlockRead();
                }
                mIsUsingMouse = false; 
            }
            else 
            {
                UIPoint pp = e.GetPosition(mPDFView);


                if (mIsUsingMouse)
                {
                    if (!mPointerReleaseDismissedMenu && !m_IsSelectingWithShift)
                    {
                        // if pointer release did not dismiss menu, we should switch to pan tool
                        mNextToolMode = ToolType.e_pan;
                        mPDFView.ClearSelection();
                    }
                }
                else // using touch or stylus
                {
                    if (!mPointerReleaseDismissedMenu) 
                    {
                        // if pointer release did not dismiss menu, we should display menu if we hit selection, or go to pan if not
                        if (IsPointInSelection(pp, true)) // Are we hitting a selection?  && mToolManager.IsPopupMenuEnabled
                        {
                            if (mToolManager.IsPopupMenuEnabled)
                            {
                                if (mCommandMenu == null)
                                {
                                    if (mIsInSnappedView)
                                    {
                                        Dictionary<string, string> textSelectOptionsDict = new Dictionary<string, string>();
                                        textSelectOptionsDict["copy"] = ResourceHandler.GetString("TextSelect_Option_Copy");
                                        mCommandMenu = new PopupCommandMenu(mPDFView, textSelectOptionsDict, OnCommandMenuClicked);
                                    }
                                    else
                                    {
                                        Dictionary<string, string> textSelectOptionsDict = new Dictionary<string, string>();
                                        textSelectOptionsDict["copy"] = ResourceHandler.GetString("TextSelect_Option_Copy");
                                        textSelectOptionsDict["highlight"] = ResourceHandler.GetString("TextSelect_Option_Highlight");
                                        textSelectOptionsDict["strikeout"] = ResourceHandler.GetString("TextSelect_Option_Strikeout");
                                        textSelectOptionsDict["underline"] = ResourceHandler.GetString("TextSelect_Option_Underline");
                                        textSelectOptionsDict["squiggly"] = ResourceHandler.GetString("TextSelect_Option_Squiggly");
                                        mCommandMenu = new PopupCommandMenu(mPDFView, textSelectOptionsDict, OnCommandMenuClicked);
                                    }

                                }
                                mCommandMenu.UseFadeAnimations(true);
                                // position and show the menu
                                PositionMenu(mCommandMenu, e.GetPosition(null));
                                ShowMenu(mCommandMenu);
                                DisableScrolling();
                                mIsShowingCommandMenu = true;
                            }
                        }
                        else
                        {
                            mNextToolMode = ToolType.e_pan;
                            mPDFView.ClearSelection();
                        }
                    }
                }
            }
            mJustSwitchedFromAnotherTool = false;
            return true;
        }

        internal override bool RightTappedHandler(object sender, RightTappedRoutedEventArgs e)
        {
            // check if mouse, so that we don't fire this when releasing a long press
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                mIsUsingMouse = true;
                UIPoint mousePoint = e.GetPosition(mPDFView);

                // if pointer release dismissed menu, we shouldn't do anything.
                if (mPointerReleaseDismissedMenu)
                {
                    return true;
                }
                else if (IsPointInSelection(mousePoint) && !mPointerReleaseDismissedMenu && mToolManager.IsPopupMenuEnabled)
                {   // if pointer release did not dismiss menu, we should display menu if we hit selection
                    if (mCommandMenu == null)
                    {
                        if (mIsInSnappedView)
                        {
                            Dictionary<string, string> textSelectOptionsDict = new Dictionary<string, string>();
                            textSelectOptionsDict["copy"] = ResourceHandler.GetString("TextSelect_Option_Copy");
                            mCommandMenu = new PopupCommandMenu(mPDFView, textSelectOptionsDict, OnCommandMenuClicked);
                        }
                        else
                        {
                            Dictionary<string, string> textSelectOptionsDict = new Dictionary<string, string>();
                            textSelectOptionsDict["copy"] = ResourceHandler.GetString("TextSelect_Option_Copy");
                            textSelectOptionsDict["highlight"] = ResourceHandler.GetString("TextSelect_Option_Highlight");
                            textSelectOptionsDict["strikeout"] = ResourceHandler.GetString("TextSelect_Option_Strikeout");
                            textSelectOptionsDict["underline"] = ResourceHandler.GetString("TextSelect_Option_Underline");
                            textSelectOptionsDict["squiggly"] = ResourceHandler.GetString("TextSelect_Option_Squiggly");
                            mCommandMenu = new PopupCommandMenu(mPDFView, textSelectOptionsDict, OnCommandMenuClicked);
                        }
                    }
                    mCommandMenu.UseFadeAnimations(true);
                    // position and show menu
                    PositionMenu(mCommandMenu, e.GetPosition(null));
                    ShowMenu(mCommandMenu);
                    mIsShowingCommandMenu = true;
                    UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.Arrow);
                    return true;
                }
            }
            return false;
        }


        internal override bool DoubleClickedHandler(object sender, PointerRoutedEventArgs e)
        {
            m_scDownPoint = e.GetCurrentPoint(mPDFView).Position;
            m_IsSelectingWithShift = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) == Windows.System.VirtualKeyModifiers.Shift;
            if (!m_IsSelectingWithShift)
            {
                try
                {
                    mPDFView.DocLockRead();
                    UIPoint downPoint = e.GetCurrentPoint(mPDFView).Position;
                    double x1 = downPoint.X - 0.5;
                    double x2 = downPoint.X + 0.5;
                    double y1 = downPoint.Y - 0.5;
                    double y2 = downPoint.Y + 0.5;
                    CleanSelectionState();
                    mPDFView.SetTextSelectionMode(TextSelectionMode.e_rectangular);
                    mPDFView.Select(x1, y1, x2, y2);
                    mPDFView.SetTextSelectionMode(TextSelectionMode.e_structural);
                    DrawSelection(true);

                    // create anchor points if applicable
                    if (mPDFView.HasSelection())
                    {
                        int pgNum = mPDFView.GetSelectionBeginPage();
                        if (pgNum > 0)
                        {
                            PDFViewCtrlSelection selection = mPDFView.GetSelection(pgNum);
                            double[] quads = selection.GetQuads();
                            if (quads.Length >= 8)
                            {
                                if (mPointerID >= 0)
                                {
                                    // Do nothing
                                    return false;
                                }
                                if (mPointerID < 0)
                                {
                                    mPointerID = (int)e.Pointer.PointerId;
                                }


                                // we only care about the first quad.
                                PDFRect selRect = new PDFRect(quads[0], quads[1], quads[4], quads[5]);
                                mDoubleClickSelectionQuadLength = selRect.Width();
                                selRect = ConvertFromPageRectToCanvasRect(selRect, pgNum);
                                selRect.Normalize();
                                m_cnAnchorRect = selRect;
                                m_HasAnchorRect = true;
                                m_IsSelectingWithShift = true;

                                mSelectingByMouse = true; 
                                mIsUsingMouse = true;
                                mHasMoved = true;
                            }

                        }
                    }

                }
                catch (System.Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlockRead();
                }
            }

            return true;
        }


        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)

        {
            bool continuousPresentationMode = (mPDFView.GetPagePresentationMode() == PDFViewCtrlPagePresentationMode.e_facing_continuous ||
            mPDFView.GetPagePresentationMode() == PDFViewCtrlPagePresentationMode.e_facing_continuous_cover ||
            mPDFView.GetPagePresentationMode() == PDFViewCtrlPagePresentationMode.e_single_continuous);
            if (mPointerID >= 0)
            {
                return true;
            }
            if (mIsCtrlDown)
            {
                if (HotKey_Pressed(e.Key))
                {
                    return true;
                }
                return base.KeyDownAction(sender, e);
            }
            if (mIsModifierKeyDown)
            {
                return base.KeyDownAction(sender, e);
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (mIsShowingCommandMenu)
                {
                    HideMenu(mCommandMenu);
                    mIsShowingCommandMenu = false;
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                }
                return true;
            }
            return true;
        }

        private bool HotKey_Pressed(Windows.System.VirtualKey key)
        {
            switch (key)
            {
                case Windows.System.VirtualKey.C:
                    HandleCopy();
                    return true;
            }
            return false;
        }

        #region Command Menu and Handlers

        internal override void OnCommandMenuClicked(string title)
        {
            EnableScrolling();
            Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("QuickMenu Tool", title + " selected");

            if (title.Equals("copy", StringComparison.OrdinalIgnoreCase))
            {
                HandleCopy();
            }
            else if (title.Equals("highlight", StringComparison.OrdinalIgnoreCase))
            {
                MarkupHandler(title);
            }
            else if (title.Equals("underline", StringComparison.OrdinalIgnoreCase))
            {
                MarkupHandler(title);
            }
            else if (title.Equals("strikeout", StringComparison.OrdinalIgnoreCase))
            {
                MarkupHandler(title);
            }
            else if (title.Equals("squiggly", StringComparison.OrdinalIgnoreCase))
            {
                MarkupHandler(title);
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }
            mIsShowingCommandMenu = false;
            HideMenu(mCommandMenu);
        }

        private void HandleCopy()
        {
            string text = mToolManager.GetSelectedText();
            // Copy text to clipboard
            UtilityFunctions.CopySelectedTextToClipBoard(text);
        }



        private void MarkupHandler(string markupType)
        {
            // Store created markups by page, so that we can request all updates after we have created them all
            Dictionary<int, ITextMarkup> textMarkupsToUpdate = new Dictionary<int, ITextMarkup>();
            try
            {
                PDFDoc doc = mPDFView.GetDoc();
                mPDFView.DocLock(true);

                mSelectionStartPage = mPDFView.GetSelectionBeginPage();
                mSelectionEndPage = mPDFView.GetSelectionEndPage();

                for (int pgnm = mSelectionStartPage; pgnm <= mSelectionEndPage; pgnm++)
                {
                    if (!mPDFView.HasSelectionOnPage(pgnm))
                    {
                        continue;
                    }

                    double[] quads = mPDFView.GetSelection(pgnm).GetQuads();
                    int sz = quads.Length / 8;
                    if (sz == 0)
                    {
                        continue;
                    }

                    PDFRect bbox = new PDFRect(quads[0], quads[1], quads[4], quads[5]); //just use the first quad to temporarily populate the bbox
                    ITextMarkup tm;

                    SettingsColor color = new SettingsColor(255, 0, 0, true); // default color
                    double opacity = 1; // default opacity
                    double thickness = 1; // default thickness

                    if (markupType.Equals("highlight", StringComparison.OrdinalIgnoreCase))
                    {
                        tm = Highlight.Create(doc.GetSDFDoc(), bbox);
                        color = Settings.GetShapeColor(ToolType.e_text_highlight, false);
                        opacity = Settings.GetOpacity(ToolType.e_text_highlight);
                    }
                    else if (markupType.Equals("underline", StringComparison.OrdinalIgnoreCase))
                    {
                        tm = Underline.Create(doc.GetSDFDoc(), bbox);
                        color = Settings.GetShapeColor(ToolType.e_text_underline, false);
                        opacity = Settings.GetOpacity(ToolType.e_text_underline);
                        thickness = Settings.GetThickness(ToolType.e_text_underline);
                    }
                    else if (markupType.Equals("strikeout", StringComparison.OrdinalIgnoreCase))
                    {
                        tm = StrikeOut.Create(doc.GetSDFDoc(), bbox);
                        color = Settings.GetShapeColor(ToolType.e_text_strikeout, false);
                        opacity = Settings.GetOpacity(ToolType.e_text_strikeout);
                        thickness = Settings.GetThickness(ToolType.e_text_strikeout);
                    }
                    else // squiggly
                    {
                        tm = Squiggly.Create(doc.GetSDFDoc(), bbox);
                        color = Settings.GetShapeColor(ToolType.e_text_squiggly, false);
                        opacity = Settings.GetOpacity(ToolType.e_text_squiggly);
                        thickness = Settings.GetThickness(ToolType.e_text_squiggly);
                    }

                    SetQuadPoints(tm, quads);

                    // set color and opacity
                    ColorPt colorPt = new ColorPt(color.R / 255.0, color.G / 255.0, color.B / 255.0);
                    tm.SetColor(colorPt, 3);
                    tm.SetOpacity(opacity);

                    if (!(tm is Annots.Highlight))
                    {
                        AnnotBorderStyle bs = tm.GetBorderStyle();
                        bs.width = thickness;
                        tm.SetBorderStyle(bs);
                    }

                    SetAuthor(tm);

                    tm.RefreshAppearance();

                    PDFPage page = doc.GetPage(pgnm);
                    page.AnnotPushBack(tm);

                    // add markup to dictionary for later update
                    textMarkupsToUpdate[pgnm] = tm;
                }

                // clear selection
                mCurrentWidget = -1;
                mPDFView.ClearSelection();
               

            }
            catch (System.Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            // Now update the PDFViewCtrl to display our new text markup
            foreach (int pgnm in textMarkupsToUpdate.Keys)
            {
                mPDFView.UpdateWithAnnot(textMarkupsToUpdate[pgnm], pgnm);
                mToolManager.RaiseAnnotationAddedEvent(textMarkupsToUpdate[pgnm]);
            }

       
            mViewerCanvas.Children.Remove(this);
            mToolManager.CreateTool(ToolType.e_pan, this);
        }


        #endregion Command Menu and Handlers


        #region Utility Functions

        /// <summary>
        /// Sets the quad points for the annotation.
        /// </summary>
        /// <param name="tm"></param>
        /// <param name="quads"></param>
        protected void SetQuadPoints(ITextMarkup tm, double[] quads)
        {
            int sz = quads.Length / 8;

            PDFPoint p1 = new PDFPoint();
            PDFPoint p2 = new PDFPoint();
            PDFPoint p3 = new PDFPoint();
            PDFPoint p4 = new PDFPoint();

            QuadPoint qp = new QuadPoint(p1, p2, p3, p4);

            // Add the quads
            int k = 0;
            for (int i = 0; i < sz; ++i, k += 8)
            {
                p1.x = quads[k];
                p1.y = quads[k + 1];

                p2.x = quads[k + 2];
                p2.y = quads[k + 3];

                p3.x = quads[k + 4];
                p3.y = quads[k + 5];

                p4.x = quads[k + 6];
                p4.y = quads[k + 7];


                if (mToolManager.TextMarkupAdobeHack) // Create quad in a way that Adobe can handle.
                {
                    qp.p1 = p4;
                    qp.p2 = p3;
                    qp.p3 = p1;
                    qp.p4 = p2;
                }
                else // according to PDF spec
                {
                    qp.p1 = p1;
                    qp.p2 = p2;
                    qp.p3 = p3;
                    qp.p4 = p4;
                }

                tm.SetQuadPoint(i, qp);
            }
        }

        /// <summary>
        /// Will position the Selection Widgets once selection is finished.
        /// </summary>
        protected void PositionSelectionWidgets()
        {
            // first, get first quad
            PDFRect firstQuad = GetFirstQuad();

            // get second quad
            PDFRect lastQuad = GetLastQuad();

            if (firstQuad == null || lastQuad == null)
            {
                return;
            }

            firstQuad = ConvertFromPageRectToCanvasRect(firstQuad, mSelectionStartPage);
            lastQuad = ConvertFromPageRectToCanvasRect(lastQuad, mSelectionEndPage);
            firstQuad.Normalize();
            lastQuad.Normalize();

            mSelectionWidgets[0].QuadHeight = firstQuad.Height();
            mSelectionWidgets[1].QuadHeight = lastQuad.Height();

            mSelectionWidgets[0].BottomPoint.x = firstQuad.x1;
            mSelectionWidgets[0].BottomPoint.y = firstQuad.y2;
            mSelectionWidgets[1].BottomPoint.x = lastQuad.x2;
            mSelectionWidgets[1].BottomPoint.y = lastQuad.y2;

            mSelectionEllipseTranslations[0].X = mSelectionWidgets[0].BottomPoint.x;
            mSelectionEllipseTranslations[0].Y = mSelectionWidgets[0].BottomPoint.y + mWidgetRadius;
            mSelectionEllipseTranslations[1].X = mSelectionWidgets[1].BottomPoint.x;
            mSelectionEllipseTranslations[1].Y = mSelectionWidgets[1].BottomPoint.y + mWidgetRadius;

            mSelectionEllipses[0].Opacity = 1;
            mSelectionEllipses[1].Opacity = 1;
        }

        /// <summary>
        /// Gets the first quad in the selection
        /// </summary>
        /// <returns></returns>

        protected PDFRect GetFirstQuad()
        {
            // loop through selections on pages until we find a quad
            for (int i = mSelectionStartPage; i <= mSelectionEndPage; i++)
            {
                PDFViewCtrlSelection sel = mPDFView.GetSelection(i);
                if (sel != null)
                {
                    double[] quads = sel.GetQuads();
                    if (quads != null && quads.Length > 0)
                    {
                        return new PDFRect(quads[0], quads[1], quads[4], quads[5]);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the last quad in the selection
        /// </summary>
        /// <returns></returns>
        protected PDFRect GetLastQuad()
        {
            // look at selections on pages in reverse order until we find a quad
            for (int i = mSelectionEndPage; i >= mSelectionStartPage; i--)
            {
                PDFViewCtrlSelection sel = mPDFView.GetSelection(i);
                if (sel != null)
                {
                    double[] quads = sel.GetQuads();
                    if (quads != null && quads.Length > 0)
                    {
                        // we want the last quad
                        return new PDFRect(quads[quads.Length - 8], quads[quads.Length - 7], quads[quads.Length - 4], quads[quads.Length - 3]);
                    }
                }
            }
            return null;
        }

        private void ShowMenu(PopupCommandMenu m)
        {
            if (m != null)
            {
                m.Show();
            }
        }
        private void HideMenu(PopupCommandMenu m)
        {
            if (m != null)
            {
                m.Hide();
            }
        }
        private void PositionMenu(PopupCommandMenu m, UIPoint p)
        {
            if (m != null)
            {
                m.TargetPoint(p);
            }
        }

        #endregion Utility Functions



        #region Text Selection Utilities



        /// <summary>
        /// Takes two points in the space of mViewerCanvas, selects in the PDFViewWPF with these points
        /// and then draws the selection as an overlay over the PDFViewWPF.
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        protected void SelectAndDraw(UIPoint cnP1, UIPoint cnP2)
        {
            Select(cnP1, cnP2);
            CleanSelectionState();
            DrawSelection();
        }

        /// <summary>
        /// Takes a point and a rectangle in the space of mViewerCanvas, selects in the PDFViewWPF with the point 
        /// and the corner of the rectangle that gives the largest selection. 
        /// Then draws the selection as an overlay over the PDFViewWPF.
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="p2"></param>
        protected void SelectAndDraw(PDFRect rect, UIPoint p2)
        {
            UIPoint anchorPoint1 = new UIPoint(rect.x1, rect.y2);
            UIPoint anchorPoint2 = new UIPoint(rect.x2, rect.y1);
            //if (mToolManager.IsRightToLeftLanguage)
            //{
            //    anchorPoint1.X = rect.x2;
            //    anchorPoint2.X = rect.x1;
            //}

            // see if point is in the original selection rect.
            if (rect.Contains(p2.X, p2.Y))
            {
                SelectAndDraw(anchorPoint1, anchorPoint2);
                return;
            }

            int sel1PgNum = 0;
            int sel2PgNum = 0;
            int sel1QdNum = 0;
            int sel2QdNum = 0;
            double sel1StartQuadLength = 0;
            double sel1EndQuadLength = 0;
            double sel2StartQuadLength = 0;
            double sel2EndQuadLength = 0;

            GetSelectionData(anchorPoint1, p2, ref sel1PgNum, ref sel1QdNum, ref sel1StartQuadLength, ref sel1EndQuadLength);
            GetSelectionData(anchorPoint2, p2, ref sel2PgNum, ref sel2QdNum, ref sel2StartQuadLength, ref sel2EndQuadLength);

            bool useFirstPoint = true;
            if (sel2PgNum > sel1PgNum)
            {
                useFirstPoint = false;
            }
            else if (sel2PgNum == sel1PgNum)
            {
                if (sel2QdNum > sel1QdNum)
                {
                    useFirstPoint = false;
                }
                else if (sel2QdNum == sel1QdNum)
                {
                    if (sel2EndQuadLength + sel2StartQuadLength > sel1EndQuadLength + sel1StartQuadLength)
                    {
                        useFirstPoint = false;
                    }
                }
            }

            if (Math.Max(sel1PgNum, sel2PgNum) == 1 && Math.Max(sel1QdNum, sel2QdNum) == 16
                && Math.Max(sel2EndQuadLength, sel1EndQuadLength) < mDoubleClickSelectionQuadLength)
            {
                SelectAndDraw(anchorPoint1, anchorPoint2);
            }
            else if (useFirstPoint)
            {
                SelectAndDraw(anchorPoint1, p2);
            }
            else
            {
                SelectAndDraw(anchorPoint2, p2);
            }
        }


        protected void Select(UIPoint p1, UIPoint p2)
        {
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
            mPDFView.Select(p1.X - sx, p1.Y - sy, p2.X - sx, p2.Y - sy);
        }

        protected void CleanSelectionState()
        {
            DetachAllTextSelection();
            mPagesOnScreen.Clear();
            mSelectionCanvases.Clear();
        }

        /// <summary>
        /// Selects text between the two points in mAnnotationCanvas space and then measures the data
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="numPages"></param>
        /// <param name="numQuads"></param>
        /// <param name="startQuadlength"></param>
        /// <param name="endQuadlength"></param>
        protected void GetSelectionData(UIPoint p1, UIPoint p2, ref int numPages, ref int numQuads, ref double startQuadLength, ref double endQuadLength)
        {
            numPages = 0;
            numQuads = 0;
            startQuadLength = 0;
            endQuadLength = 0;

            mPDFView.ClearSelection();
            Select(p1, p2);
            int selBegin = mPDFView.GetSelectionBeginPage();
            int selEnd = mPDFView.GetSelectionEndPage();
            if (selBegin < 1)
            {
                return;
            }
            numPages = selEnd - selBegin + 1;
            pdftron.PDF.PDFViewCtrlSelection selection = mPDFView.GetSelection(selBegin);
            double[] startQuads = selection.GetQuads();
            selection = mPDFView.GetSelection(selEnd);
            double[] endQuads = selection.GetQuads();

            if (startQuads != null)
            {
                numQuads += startQuads.Length;
                startQuadLength = Math.Abs(startQuads[0] - startQuads[4]);
            }
            if (endQuads != null)
            {
                numQuads += endQuads.Length;
                endQuadLength = Math.Abs(endQuads[endQuads.Length - 8] - endQuads[endQuads.Length - 4]);
            }
            return;
        }

        protected void ScrollingHandler(UIPoint currentPoint)
        {
            if (!mCanScroll)
            {
                double xDist = currentPoint.X - m_scDownPoint.X;
                double yDist = currentPoint.Y - m_scDownPoint.Y;
                if ((xDist * xDist) + (yDist * yDist) > mToolManager.MOVE_THRESHOLD_TO_START_SCROLL)
                {
                    mCanScroll = true;
                }
            }
            else
            {
                bool isScrolling = false;
                // x scrolling
                double xSpeed = 0;
                if (currentPoint.X < mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X)
                {
                    double xRatio = (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X - currentPoint.X)
                        / (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X - mToolManager.TEXT_SELECT_SCROLL_HARD_MARGIN_X);
                    if (xRatio > 1)
                    {
                        xRatio = mToolManager.TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_X;
                    }
                    xSpeed = xRatio * -mToolManager.TEXT_SELECT_SCROLL_SPEED_X;
                }
                if (currentPoint.X > mPDFView.ActualWidth - mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X)
                {
                    double xRatio = (currentPoint.X - ( mPDFView.ActualWidth - mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X)) 
                        / (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_X - mToolManager.TEXT_SELECT_SCROLL_HARD_MARGIN_X);
                    if (xRatio > 1)
                    {
                        xRatio = mToolManager.TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_X;
                    }
                    xSpeed = xRatio * mToolManager.TEXT_SELECT_SCROLL_SPEED_X;
                }
                if (xSpeed != 0)
                {
                    mPDFView.SetHScrollPos(mPDFView.GetAnnotationCanvasHorizontalOffset() + xSpeed);
                    m_ScrollSpeedX = xSpeed;
                    isScrolling = true;
                }

                // y scrolling
                double ySpeed = 0;
                if (currentPoint.Y < mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y)
                {
                    double yRatio = (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y - currentPoint.Y) 
                        / (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y - mToolManager.TEXT_SELECT_SCROLL_HARD_MARGIN_Y);
                    if (yRatio > 1)
                    {
                        yRatio = mToolManager.TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_Y;
                    }
                    ySpeed = yRatio * -mToolManager.TEXT_SELECT_SCROLL_SPEED_Y;
                }
                if (currentPoint.Y > mPDFView.ActualHeight - mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y)
                {
                    double yRatio = (currentPoint.Y - (mPDFView.ActualHeight - mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y)) 
                        / (mToolManager.TEXT_SELECT_SCROLL_SOFT_MARGIN_Y - mToolManager.TEXT_SELECT_SCROLL_HARD_MARGIN_Y);
                    if (yRatio > 1)
                    {
                        yRatio = mToolManager.TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_Y;
                    }
                    ySpeed = yRatio * mToolManager.TEXT_SELECT_SCROLL_SPEED_Y;
                }
                if (ySpeed != 0)
                {
                    mPDFView.SetVScrollPos(mPDFView.GetAnnotationCanvasVerticalOffset() + ySpeed);
                    m_ScrollSpeedY = ySpeed;
                    isScrolling = true;
                }

                if (isScrolling)
                {
                    m_scScrollPoint.X = currentPoint.X;
                    m_scScrollPoint.Y = currentPoint.Y;
                    TurnOnScrollTimer();
                }
                else
                {
                    TurnOffScrollTimer();
                }
            }

        }

        private void TurnOnScrollTimer()
        {
            if (_AutomaticScrollTimer == null)
            {
                _AutomaticScrollTimer = new DispatcherTimer();
                _AutomaticScrollTimer.Interval = TimeSpan.FromMilliseconds(100);
                _AutomaticScrollTimer.Tick += _AutomaticScrollTimer_Tick;
                _AutomaticScrollTimer.Start();
            }
        }

        void _AutomaticScrollTimer_Tick(object sender, object e)
        {
            UIPoint cnDragPoint = new UIPoint(m_scScrollPoint.X + mPDFView.GetAnnotationCanvasHorizontalOffset(), m_scScrollPoint.Y + mPDFView.GetAnnotationCanvasVerticalOffset());
            if (m_HasAnchorRect)
            {
                SelectAndDraw(m_cnAnchorRect, cnDragPoint);
                UtilityFunctions.SetCursor(Windows.UI.Core.CoreCursorType.IBeam);
            }
            else
            {
                SelectAndDraw(m_cnAnchorPoint, cnDragPoint);
            }
            mPDFView.SetHScrollPos(mPDFView.GetAnnotationCanvasHorizontalOffset() + m_ScrollSpeedX);
            mPDFView.SetVScrollPos(mPDFView.GetAnnotationCanvasVerticalOffset() + m_ScrollSpeedY);
        }

        private void TurnOffScrollTimer()
        {
            if (_AutomaticScrollTimer != null)
            {
                _AutomaticScrollTimer.Stop();
            }
            _AutomaticScrollTimer = null;
        }

        #endregion Text Selection Utilities

    }
}

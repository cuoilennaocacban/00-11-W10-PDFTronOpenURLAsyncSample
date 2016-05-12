using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.Devices.Input;

using UIPoint = Windows.Foundation.Point;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef; 
using pdftron.PDF;
using pdftron.Common;
using Windows.UI.Input;

namespace pdftron.PDF.Tools
{
    internal class Pan : Tool
    {
        internal bool mSwitchToTextSelect = false;
        bool mShouldSmartZoom = false;

        protected UIPoint mPopupTargetPoint;

        // For grabbing and scrolling
        protected int mDragPointerID = -1;
        protected UIPoint mLastPoint;
        public Pan(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_pan;
            mToolMode = ToolType.e_pan;
        }

        internal override bool OnScale()
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mCommandMenu = null;
                mIsShowingCommandMenu = false;
            }
            mJustSwitchedFromAnotherTool = false;
            base.OnScale();
            return false;
        }

        internal override bool OnSize()
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mCommandMenu = null;
                mIsShowingCommandMenu = false;
            }
            mJustSwitchedFromAnotherTool = false;
            base.OnSize();
            EnableScrolling();
            return false;
        }

        internal override bool OnViewChanged(Object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mCommandMenu = null;
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
                mCommandMenu = null;
                mIsShowingCommandMenu = false;
            }
            base.OnPageNumberChanged(current_page, num_pages);
            return false;
        }
        
        internal override bool HoldingHandler(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started && mToolManager.EnablePopupMenuOnLongPress && ToolMode == ToolType.e_pan)
            {
                UIPoint holdPoint = e.GetPosition(mPDFView);
                if (mPDFView.GetPageNumberFromScreenPoint(holdPoint.X, holdPoint.Y) > 0)
                {
                    Dictionary<string, string> creationOptionsDict = new Dictionary<string, string>();
                    creationOptionsDict["note"] = ResourceHandler.GetString("CreateOption_Note");
                    creationOptionsDict["signature"] = ResourceHandler.GetString("CreateOption_Signature");
                    creationOptionsDict["ink"] = ResourceHandler.GetString("CreateOption_Ink");
                    creationOptionsDict["text"] = ResourceHandler.GetString("CreateOption_Text");
                    creationOptionsDict["arrow"] = ResourceHandler.GetString("CreateOption_Arrow");
                    creationOptionsDict["line"] = ResourceHandler.GetString("CreateOption_Line");
                    creationOptionsDict["rectangle"] = ResourceHandler.GetString("CreateOption_Rectangle");
                    creationOptionsDict["ellipse"] = ResourceHandler.GetString("CreateOption_Ellipse");

                    mCommandMenu = new PopupCommandMenu(mPDFView, creationOptionsDict, OnCommandMenuClicked);
                }
                else
                {
                    return false;
                    // We could also eliminate the options that use the position of this long press
                    //mCommandMenu = new PopupCommandMenu(mPDFView, new List<string> { "Sticky Note", "Free Hand", "Free Text", 
                    //    "Arrow", "Line", "Rectangle", "Ellipse"}, OnCommandMenuClicked);
                }
                mCommandMenu.UseFadeAnimations(true);
                mPopupTargetPoint = e.GetPosition(mPDFView);
                mCommandMenu.TargetPoint(mPopupTargetPoint);
                mIsShowingCommandMenu = true;
                mCommandMenu.Show();
            }
            return false;
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            mShouldSmartZoom = false;
            mDragPointerID = -1;

            if (mPDFView.GetDoc() == null)
            {
                return false;
            }
            if (mIsShowingCommandMenu && !mJustSwitchedFromAnotherTool)
            {
                mCommandMenu.Hide();
                mCommandMenu = null;
                mIsShowingCommandMenu = false;
                mToolManager.SuppressTap = true;
            }

            PointerPoint sc_pointerPoint = e.GetCurrentPoint(mPDFView);
            UIPoint sc_downPoint = sc_pointerPoint.Position;
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                bool shouldPan = false;
                if (mToolManager.PanToolTextSelectionMode == ToolManager.TextSelectionBehaviour.AlwaysSelect)
                {
                    mIsUsingMouse = true;
                    mNextToolMode = ToolType.e_text_select;
                }
                else if (mToolManager.PanToolTextSelectionMode == ToolManager.TextSelectionBehaviour.AlwaysPan)
                {
                    shouldPan = true;
                }
                else // mixed mode
                {
                    if (SelectTextAtPoint(sc_downPoint, 5))
                    {
                        mIsUsingMouse = true;
                        mNextToolMode = ToolType.e_text_select;
                    }
                    else
                    {
                        shouldPan = true;
                    }
                }

                if (shouldPan)
                {
                    mDragPointerID = (int)e.Pointer.PointerId;
                    mLastPoint = e.GetCurrentPoint(mPDFView).Position;
                    mPDFView.GetAnnotationCanvas().CapturePointer(e.Pointer);
                }
            }
            else if (e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
            {
                int pgNum = mPDFView.GetPageNumberFromScreenPoint(sc_downPoint.X, sc_downPoint.Y);
                if (pgNum > 0)
                {
                    if (sc_pointerPoint.Properties.IsEraser) // we want to erase ink
                    {
                        mNextToolMode = ToolType.e_ink_eraser;
                        mIsUsingMouse = false;
                    }
                    else if (sc_pointerPoint.Properties.IsBarrelButtonPressed)
                    {
                        try
                        {
                            mPDFView.DocLockRead();

                            SelectAnnot((int)sc_downPoint.X, (int)sc_downPoint.Y, 11, 7);
                            if (mAnnot != null)
                            {
                                mAnnotPageNum = mPDFView.GetPageNumberFromScreenPoint(sc_downPoint.X, sc_downPoint.Y);
                                mNextToolMode = ToolType.e_annot_edit;
                                if (mAnnot.GetAnnotType() == AnnotType.e_Line)
                                {
                                    mNextToolMode = ToolType.e_line_edit;
                                }
                                if (mAnnot is Annots.ITextMarkup)
                                {
                                    mNextToolMode = ToolType.e_annot_edit_text_markup;
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
                    else // we are not erasing or selecting, so we draw ink if we're on a particular page.
                    {
                        mNextToolMode = ToolType.e_ink_create;
                        mIsUsingMouse = false;
                    }
                    
                }
            }
            mJustSwitchedFromAnotherTool = false;
            return false;
        }


        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == mDragPointerID)
            {
                UIPoint dragPoint = e.GetCurrentPoint(mPDFView).Position;
                mPDFView.SetHScrollPos(mPDFView.GetHScrollPos() - dragPoint.X + mLastPoint.X);
                mPDFView.SetVScrollPos(mPDFView.GetVScrollPos() - dragPoint.Y + mLastPoint.Y);
                mLastPoint = dragPoint;
                return true;
            }
            return false;
        }


        internal override bool PointerCanceledHandler(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == mDragPointerID)
            {
                mDragPointerID = -1;
                mPDFView.GetAnnotationCanvas().ReleasePointerCaptures();
            }
            return true;
        }

        internal override bool PointerCaptureLostHandler(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == mDragPointerID)
            {
                mDragPointerID = -1;
            }
            return true;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            // We want this to happen when the finger is released, as opposed to pressed.
            if (mShouldSmartZoom)
            {
                UIPoint tapPoint = e.GetCurrentPoint(mPDFView).Position;

                // We need to marshal this call. At this point in time, the scroll viewer is still being "manipulated" and will behave oddly if we 
                // resize it's contents here. So we create a task that the UI thread can execute after it's done finalizing manipulation.
                CheckForSmartZoom(tapPoint);
                return false;
            }
            else if (mDragPointerID == e.Pointer.PointerId)
            {
                mDragPointerID = -1;
                mPDFView.GetAnnotationCanvas().ReleasePointerCaptures();
            }

            EnableScrolling();
            mJustSwitchedFromAnotherTool = false;
            return false;
        }

        private async void CheckForSmartZoom(UIPoint tapPoint)
        {
            try
            {
                // We need to marshal this call. At this point in time, the scroll viewer is still being "manipulated" and will behave oddly if we 
                // resize it's contents here. So we create a task that the UI thread can execute after it's done finalizing manipulation.
                await Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    mShouldSmartZoom = false;
                    {
                        PDFViewCtrlPageViewMode mode = mPDFView.GetPageViewMode();
                        PDFViewCtrlPageViewMode refViewMode = mPDFView.GetPageRefViewMode();
                        PDFViewCtrlPagePresentationMode presentationMode = mPDFView.GetPagePresentationMode();

                        if (mode == refViewMode) 
                        {
                            if (!mPDFView.SmartZoom((int)tapPoint.X, (int)tapPoint.Y))
                            {
                                mPDFView.SetZoom((int)tapPoint.X, (int)tapPoint.Y, 2.5 * mPDFView.GetZoom());
                                //mPDFView.SetZoom(2.5 * mPDFView.GetZoom());
                            }
                        }
                        else
                        {
                            mPDFView.SetPageViewMode(refViewMode);
                        }
                    }
                });
            }
            catch (Exception)
            {

            }
        }



        internal override bool DoubleTappedHandler(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Mouse)
            {
                mJustSwitchedFromAnotherTool = false;
                mSwitchToTextSelect = false;
                mShouldSmartZoom = true;
            }
            return false;
        }

        internal override bool DoubleClickedHandler(object sender, PointerRoutedEventArgs e)
        {
            // We're still in pan mode -> we did not select any annotation (we would have been in annot edit mode
            mNextToolMode = ToolType.e_text_select;

            return true;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool && !mIsInHelperMode)
            {
                mJustSwitchedFromAnotherTool = false;
                return true;
            }
            if (mPDFView.GetDoc() == null)
            {
                return false;
            }
            if (!mPDFView.IsEnabled)
            {
                return false;
            }
            
            int x = (int)(e.GetPosition(mPDFView).X + 0.5);
            int y = (int)(e.GetPosition(mPDFView).Y + 0.5);

            mAnnot = null;
            bool foundSomething = false;
            mPDFView.DocLockRead();
            try
            {
                if (!mIsInSnappedView)
                {
                    if (e.PointerDeviceType == PointerDeviceType.Mouse)
                    {
                        SelectAnnot(x, y, 6, 3);
                    }
                    else if (e.PointerDeviceType == PointerDeviceType.Pen)
                    {
                        SelectAnnot(x, y, 11, 7);
                    }
                    else
                    {
                        SelectAnnot(x, y, 17, 11);
                    }

                    if (mAnnot != null)
                    {
                        foundSomething = true;
                        try
                        {
                            if (mAnnot.GetAnnotType() == AnnotType.e_Link)
                            {
                                //link navigation
                                mNextToolMode = ToolType.e_link_action;
                            }
                            else if (mAnnot.GetAnnotType() == AnnotType.e_Widget)
                            {
                                //form filling
                                mNextToolMode = ToolType.e_form_fill;
                            }
                            else if (mAnnot.GetAnnotType() == AnnotType.e_RichMedia)
                            {
                                // Movie annotation
                                mNextToolMode = ToolType.e_rich_media;
                            }
                            else
                            {

                                //Annotation editing
                                mNextToolMode = ToolType.e_annot_edit;
                                if (mAnnot.GetAnnotType() == AnnotType.e_Line)
                                {
                                    mNextToolMode = ToolType.e_line_edit;
                                }
                                if (mAnnot is Annots.ITextMarkup)
                                {
                                    mNextToolMode = ToolType.e_annot_edit_text_markup;
                                }
                            }
                            mAnnotPageNum = mPDFView.GetPageNumberFromScreenPoint(x, y);
                        }
                        catch (Exception)
                        {
                        }
                        finally
                        {
                        }
                        return true;
                    }
                    else if (mIsInHelperMode)
                    {
                        return true;
                    }
                    else
                    {
                        PDFViewCtrlLinkInfo linkInfo = mPDFView.GetLinkAt(x, y);
                        if (linkInfo != null)
                        {
                            foundSomething = true;
                            string uriString = linkInfo.GetUrl();
                            LaunchBrowser(uriString);
                        }
                    }
                }


                if (!foundSomething && e.PointerDeviceType != PointerDeviceType.Mouse)
                {
                    //if hit text, run text select tool
                    double zoomFactor = 1;
                    double x1 = e.GetPosition(mPDFView).X;
                    double y1 = e.GetPosition(mPDFView).Y;
                    double diff = 0.05 * zoomFactor;
                    x1 = Math.Max(x1 - diff, 0);
                    y1 = Math.Max(y1 - diff, 0);
                    diff *= 2;
                    double x2 = x1 + diff;
                    double y2 = y1 + diff;
                    mPDFView.ClearSelection();
                    mPDFView.SetTextSelectionMode(TextSelectionMode.e_rectangular);
                    if (mPDFView.Select(x1, y1, x2, y2) && !mIsUsingMouse)
                    {
                        mPDFView.ClearSelection();
                        DelayTextSelect(e);
                        return true;
                    }
                    else
                    {
                        mToolManager.PageNumberIndicator.Show();
                    }
                }
                else
                {
                    mIsShowingCommandMenu = false;
                    mToolManager.PageNumberIndicator.Show();
                }
            }
            catch (Exception) { }
            finally
            {
                mPDFView.DocUnlockRead();
            }

            return false;
        }

        internal async void DelayTextSelect(TappedRoutedEventArgs e)
        {
            mSwitchToTextSelect = true;
            await Task.Delay(300);
            if (mSwitchToTextSelect)
            {
                mSwitchToTextSelect = false;
                mToolManager.CreateTool(ToolType.e_text_select, this);
                mToolManager.CurrentTool.TappedHandler(this, e);
            }
        }

        internal override bool RightTappedHandler(object sender, RightTappedRoutedEventArgs e)
        {
            return false;
        }

        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)

        {
            mJustSwitchedFromAnotherTool = false;
            if (!mIsModifierKeyDown)
            {
                switch (e.Key)
                {
                        
                    case Windows.System.VirtualKey.Escape:
                        if (mIsShowingCommandMenu)
                        {
                            mCommandMenu.Hide();
                            mCommandMenu = null;
                            mIsShowingCommandMenu = false;
                        }
                        return true;
                }
            }
            return base.KeyDownAction(sender, e);
        }

        #region Handlers For Popup Menu

        internal override void OnCommandMenuClicked(string title)
        {

            Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("QuickMenu Tool", title + " selected");

            if (title.Equals("line", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_line_create;
            }
            else if (title.Equals("arrow", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_arrow_create;
            }
            else if (title.Equals("rectangle", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_rect_create;
            }
            else if (title.Equals("ellipse", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_oval_create;
            }
            else if (title.Equals("note", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_sticky_note_create;
                mToolManager.CreateTool(ToolType.e_sticky_note_create, this);
                StickyNoteCreate stickyTool = mToolManager.CurrentTool as StickyNoteCreate;
                stickyTool.SetTargetPoint(mPopupTargetPoint);
            }
            else if (title.Equals("ink", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_ink_create;
                mToolManager.CreateTool(ToolType.e_ink_create, this);
            }
            else if (title.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_text_annot_create;
                mToolManager.CreateTool(ToolType.e_text_annot_create, this);
                FreeTextCreate freeTextTool = mToolManager.CurrentTool as FreeTextCreate;
                freeTextTool.SetTargetPoint(mPopupTargetPoint);
            }
            else if (title.Equals("signature", StringComparison.OrdinalIgnoreCase))
            {
                mNextToolMode = ToolType.e_signature;
                mAnnot = null;
                mToolManager.CreateTool(ToolType.e_signature, this);
                Signature signatureTool = mToolManager.CurrentTool as Signature;
                signatureTool.SetTargetPoint(mPopupTargetPoint);
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }

            if (mCommandMenu != null)
            {
                mCommandMenu.Hide();
                mCommandMenu = null;
            }
            mIsShowingCommandMenu = false;
        }

        #endregion Handlers For Popup Menu
    }





}

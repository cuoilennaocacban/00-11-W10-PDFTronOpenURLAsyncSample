using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;
using UIPopup = Windows.UI.Xaml.Controls.Primitives.Popup;

using pdftron.PDF;
using pdftron.Common;
using pdftron.PDF.Annots;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using Windows.UI.Input;
using pdftron.PDF.Tools.Controls;




namespace pdftron.PDF.Tools
{
    /// <summary>
    /// 
    /// 
    /// Note: Currently, PointerEvents when the textbox for editing FreeText annotations is open do not fire
    /// Therefore, we will allow regular scroll and zoom, but we won't allow resize while in text edit mode.
    /// </summary>
    class AnnotEdit : Tool
    {
        internal delegate void AnnotPropertyChangedInHelperModeDelegate();
        internal event AnnotPropertyChangedInHelperModeDelegate AnnotPropertyChangedInHelperMode;

        protected Canvas mViewerCanvas;
        protected AnnotType mType;
        protected IMarkup mMarkup;
        protected ITextMarkup mTextMarkup;


        // For color picker
        protected FixedSizedBoxPopup mBoxPopup; 
        protected Color mSelectedColor; // The color we're currently working with
        protected bool mUseColor = false;
        protected const int e_stroke = 0;
        protected const int e_fill = 1;
        protected const int e_text_stroke = 2;
        protected const int e_text_fill = 3;
        protected const int e_highlight = 4;
        protected const int e_text_markup = 5;
        protected int mEffectiveBrush = -1;

        // text editing
        protected bool mIsEditingText = false;
        protected double mFontSize = 11;
        protected bool mKeyboardOpen = false;
        protected double mKeyboardTranslateOffset = 0;
        protected bool mAnnotIsSticky = false;
        protected bool mAnnotIsTextMarkup = false;
        protected UIPopup mTextPopup;
        protected RichEditBox mEditTextBox; 
        protected KeyboardHelper mKeyboardHelper; 

        // position of the selection tool
        protected double mLeft, mRight, mTop, mBottom, mHorizontalMiddle, mVerticalMiddle;
        protected double mOldLeft, mOldRight, mOldTop, mOldBottom;
        protected UIPoint[] mCtrlPointLocations; // Canvas coordinates

        // Drawing the selection tool
        protected Rectangle mAnnotBBoxRectangle;
        protected TranslateTransform[] mCtrlPointTransforms; // Positions Control Points
        protected Path[] mControlPointShapes;
        protected SolidColorBrush mSelectionBackgroundColor;
        protected double mCtrlPointRadius;
        protected SolidColorBrush mCtrlPointBorderBrush;
        protected double mCtrlPointBorderThickness;
        protected SolidColorBrush mCtrlPointFillBrush;

        // Manipulation
        protected int CONTROL_POINT_FOR_MOVING = 8;
        protected int mEffectiveCtrlPoint = 8; // 8 for translate, -1 for none.
        protected bool mIsManipulated = false;
        protected bool mStartMoving = false;
        protected Rect mPageCropOnClient;
        protected int mPointerID = -1;
        protected Point mPt1, mPt2;
        protected bool mAnnotIsScalable = true;
        protected bool mAnnotIsMovable = true;

        protected PDFRect mHelperModePositionRect = null;

        protected const int e_ll = 0;	//lower left control point
        protected const int e_lm = 1;	//lower middle
        protected const int e_lr = 2;	//lower right
        protected const int e_mr = 3;	//middle right
        protected const int e_ur = 4;	//upper left
        protected const int e_um = 5;	//upper middle
        protected const int e_ul = 6;	//upper left
        protected const int e_ml = 7;	//middle left
        protected const int START_MOVING_THRESHOLD = 10; // The distance we need to drag before the annot starts moving

        // Notes
        protected StackPanel mNoteDisplay = null;
        protected TextBox mNoteTextBox = null;

        protected Dictionary<string, string> mMenuTitles;
        protected PopupCommandMenu mSecondMenu; // Popup menu which contains the options for things like opacity
        protected bool mIsShowingSecondMenu = false;
        
        public AnnotEdit(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_annot_edit;
            mToolMode = ToolType.e_annot_edit;
            mSelectedColor = new Color();

            mPt1 = new Point();
            mPt2 = new Point();

            // appearance of the selection widget
            mSelectionBackgroundColor = new SolidColorBrush(Color.FromArgb(128, 128, 128, 125));
            mCtrlPointFillBrush = new SolidColorBrush(Color.FromArgb(255, 150, 180, 255));
            mCtrlPointBorderBrush = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));
            mCtrlPointBorderThickness = 2;
            mCtrlPointRadius = 7.5;

            // register for the keyboard callback
            mKeyboardHelper = new KeyboardHelper();
            mKeyboardHelper.SubscribeToKeyboard(true);
        }

        internal override void OnCreate()
        {
            PopulateMenu(); // Can't be done in constructor, since mAnnot hasn't been set.

            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            mViewerCanvas.Children.Add(this);

            // Figure out selection appearance according to type of annot
            mAnnotBBoxRectangle = new Rectangle();
            mAnnotBBoxRectangle.Stroke = mCtrlPointBorderBrush;
            mAnnotBBoxRectangle.StrokeDashArray = new DoubleCollection() { 2, 2 };
            mAnnotBBoxRectangle.StrokeThickness = 2;

            // The size of the gray rectangle is accomplished by scaling it
            mAnnotBBoxRectangle.Width = 1;
            mAnnotBBoxRectangle.Height = 1;   

            this.Children.Add(mAnnotBBoxRectangle);
            if (mAnnotIsScalable)
            {

                mCtrlPointTransforms = new TranslateTransform[8];
                mCtrlPointLocations = new UIPoint[8];
                mControlPointShapes = new Path[8];

                for (int i = 0; i < 8; i++)
                {
                    mCtrlPointTransforms[i] = new TranslateTransform();
                    mCtrlPointLocations[i] = new UIPoint();
                    mControlPointShapes[i] = new Path();

                    EllipseGeometry geom = new EllipseGeometry();
                    //geom.Center = mCtrlPointLocations[i];
                    geom.RadiusX = mCtrlPointRadius;
                    geom.RadiusY = mCtrlPointRadius;
                    mControlPointShapes[i].Data = geom;
                    mControlPointShapes[i].Stroke = mCtrlPointBorderBrush;
                    mControlPointShapes[i].StrokeThickness = mCtrlPointBorderThickness;
                    mControlPointShapes[i].Fill = mCtrlPointFillBrush;
                    mControlPointShapes[i].RenderTransform = mCtrlPointTransforms[i];
                    this.Children.Add(mControlPointShapes[i]);
                }
            }
            this.IsHitTestVisible = false;
        }

        internal override void OnClose()
        {
            if (mSecondMenu != null)
            {
                mSecondMenu.Hide();
                mSecondMenu = null;
            }
            if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                EnableScrolling();
            }
            if (mTextPopup != null)
            {
                mTextPopup.IsOpen = false;
                mTextPopup = null;
            }

            if (mViewerCanvas != null && mViewerCanvas.Children.Contains(this))
            {
                mViewerCanvas.Children.Remove(this);
            }
            EnableScrolling();
            base.OnClose();
            DelayedUnsubscribe();
        }

        // This delays the unsubscribing of the keyboard, so that the UI will have time to invoke the callbacks it needs.
        protected async void DelayedUnsubscribe()
        {

            await Task.Delay(50);
            if (mEditTextBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mEditTextBox);
                mKeyboardHelper.SetHidingHandler(null);
            }
            if (mNoteTextBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mNoteTextBox);
            }
            mKeyboardHelper.SubscribeToKeyboard(false);
        }

        internal void CreateAppearance(bool showMenu = true)
        {
            SetPosition();

            if (mAnnotIsSticky && showMenu)
            {
                HandleNote();
            }
            else if (mToolManager.IsPopupMenuEnabled && showMenu)
            {
                mIsShowingCommandMenu = true;
                PositionMenu(mCommandMenu);
                ShowMenu(mCommandMenu);
            }
        }

        internal override bool OnSize()
        {
            SetPosition();
            return false;
        }

        internal override bool OnScale()
        {
            SetPosition();

            PositionMenu(mCommandMenu);
            if (mIsShowingCommandMenu)
            {
                ShowMenu(mCommandMenu);
            }
            base.OnScale();
            return false;
        }

        internal override bool OnPageNumberChanged(int current_page, int num_pages)
        {
            mNextToolMode = ToolType.e_pan;
            return false;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool) // this should be the only entry point into the tool - Except for stylus select button
            {
                mJustSwitchedFromAnotherTool = false;
                if (mAnnot == null)
                {
                    return false;
                }
                CreateAppearance();
                EnableScrolling();
            }
            else if (mBoxPopup != null) // e.g. color picker is open
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                EnableScrolling();
            }
            else
            {
                // check bounds
                double x = e.GetPosition(mViewerCanvas).X;
                double y = e.GetPosition(mViewerCanvas).Y;

                // tap on annot
                if (x >= mLeft && x <= mRight && y >= mTop && y <= mBottom)
                {
                    if (mAnnotIsTextMarkup && mToolManager.IsPopupMenuEnabled)
                    {
                        mIsShowingCommandMenu = true;
                        PositionMenu(mCommandMenu);
                        ShowMenu(mCommandMenu);
                    }
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                    if (mIsEditingText)
                    {
                        SaveFreeText();
                    }
                }
            }
            return true;
        }

        internal override bool DoubleClickedHandler(object sender, PointerRoutedEventArgs e)
        {
            // might want to open notes here.
            return true;
        }

        internal override bool RightTappedHandler(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                return true;
            }
            // If we remove a menu, don't open app bar.
            if (mSecondMenu != null)
            {
                mSecondMenu.Hide();
                mSecondMenu = null;
                e.Handled = true;
            }
            if (mIsShowingCommandMenu) // we removed a menu, don't open app bar.
            {
                mCommandMenu.Hide();
                mIsShowingCommandMenu = false;
                e.Handled = true;
            }
            return false;
        }

        internal override bool HoldingHandler(object sender, HoldingRoutedEventArgs e)
        {
            if (mEffectiveCtrlPoint == -1) // would have been set to something else if we had hit a ctrl point or the square
            {
                mNextToolMode = ToolType.e_pan;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Updates the locations of control points and transforms
        /// </summary>
        protected virtual void CreateSelectionAppearance()
        {
            // update rectangle
            mAnnotBBoxRectangle.SetValue(Canvas.LeftProperty, mLeft - mPageCropOnClient.x1);
            mAnnotBBoxRectangle.SetValue(Canvas.TopProperty, mTop - mPageCropOnClient.y1);
            mAnnotBBoxRectangle.Width = mRight - mLeft;
            mAnnotBBoxRectangle.Height = mBottom - mTop;

            if (mAnnotIsScalable)
            {
                // update mCtrlPointLocations
                mCtrlPointLocations[e_ll].X = mLeft;
                mCtrlPointLocations[e_ll].Y = mBottom;
                mCtrlPointLocations[e_lm].X = mHorizontalMiddle;
                mCtrlPointLocations[e_lm].Y = mBottom;
                mCtrlPointLocations[e_lr].X = mRight;
                mCtrlPointLocations[e_lr].Y = mBottom;
                mCtrlPointLocations[e_mr].X = mRight;
                mCtrlPointLocations[e_mr].Y = mVerticalMiddle;
                mCtrlPointLocations[e_ur].X = mRight;
                mCtrlPointLocations[e_ur].Y = mTop;
                mCtrlPointLocations[e_um].X = mHorizontalMiddle;
                mCtrlPointLocations[e_um].Y = mTop;
                mCtrlPointLocations[e_ul].X = mLeft;
                mCtrlPointLocations[e_ul].Y = mTop;
                mCtrlPointLocations[e_ml].X = mLeft;
                mCtrlPointLocations[e_ml].Y = mVerticalMiddle;
           
                for (int i = 0; i < 8; i++)
                {
                    mCtrlPointTransforms[i].X = mCtrlPointLocations[i].X - mPageCropOnClient.x1;
                    mCtrlPointTransforms[i].Y = mCtrlPointLocations[i].Y - mPageCropOnClient.y1;
                }
            }
        }

        protected virtual void SetPosition()
        {
            // Place the tool's canvas on top of the page
            mPageCropOnClient = BuildPageBoundBoxOnClient(mAnnotPageNum);
            this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
            this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
            this.Width = mPageCropOnClient.x2 - mPageCropOnClient.x1;
            this.Height = mPageCropOnClient.y2 - mPageCropOnClient.y1;

            BuildAnnotBBox();
            // Convert to screen space
            PDFDouble x1 = new PDFDouble(mAnnotBBox.x1);
            PDFDouble y1 = new PDFDouble(mAnnotBBox.y1);
            PDFDouble x2 = new PDFDouble(mAnnotBBox.x2);
            PDFDouble y2 = new PDFDouble(mAnnotBBox.y2);

            // Get coordinates of two opposite points in screen space
            mPDFView.ConvPagePtToScreenPt(x1, y1, mAnnotPageNum);
            mPDFView.ConvPagePtToScreenPt(x2, y2, mAnnotPageNum);

            if (mToolMode != ToolType.e_annot_edit)
            {
                return;
            }

            mLeft = mOldLeft = Math.Min(x1.Value, x2.Value) + mPDFView.GetAnnotationCanvasHorizontalOffset();
            mRight = mOldRight = Math.Max(x1.Value, x2.Value) + mPDFView.GetAnnotationCanvasHorizontalOffset();
            mTop = mOldTop = Math.Min(y1.Value, y2.Value) + mPDFView.GetAnnotationCanvasVerticalOffset();
            mBottom = mOldBottom = Math.Max(y1.Value, y2.Value) + mPDFView.GetAnnotationCanvasVerticalOffset();
            mHorizontalMiddle = (mLeft + mRight) / 2.0;
            mVerticalMiddle = (mTop + mBottom) / 2.0;

            CreateSelectionAppearance();

            if (mIsShowingCommandMenu)
            {
                HideMenu(mCommandMenu);
                PositionMenu(mCommandMenu);
                ShowMenu(mCommandMenu);
            }
        }

        /// <summary>
        /// Adds in the menu options according to the type of annotation
        /// </summary>
        protected void PopulateMenu()
        {
            if (mAnnot != null)
            {
                mMenuTitles = null;
                try
                {
                    mMenuTitles = new Dictionary<string, string>();
                    //locks the document first as accessing annotation/doc information isn't thread safe.
                    mPDFView.DocLockRead();
                    mType = mAnnot.GetAnnotType();

                    mMenuTitles["done"] = ResourceHandler.GetString("EditOption_Done");
                    
                    mAnnotIsSticky = (mType == AnnotType.e_Text);
                    mAnnotIsTextMarkup = (mType == AnnotType.e_Highlight ||
                                            mType == AnnotType.e_Underline ||
                                            mType == AnnotType.e_StrikeOut ||
                                            mType == AnnotType.e_Squiggly);

                    if ((mAnnot.IsMarkup() && mType != AnnotType.e_FreeText)
                        || mType == AnnotType.e_Text)
                    {
                        bool addNote = true;
                        if (mAnnot.GetAnnotType() == AnnotType.e_Stamp)
                        {
                            pdftron.SDF.Obj obj = mAnnot.GetSDFObj();
                            obj = obj.FindObj(ToolManager.SignatureAnnotationIdentifyingString);
                            if (obj != null)
                            {
                                addNote = false;
                            }
                        }
                        if (addNote)
                        {
                            mMenuTitles["Note"] = ResourceHandler.GetString("EditOption_Note");
                        }
                    }

                    if (!mAnnotIsSticky && mAnnot.IsMarkup() && 
                        mType != AnnotType.e_Stamp && mType != AnnotType.e_Caret && mType != AnnotType.e_FileAttachment && mType != AnnotType.e_Sound)
                    {
                        mMenuTitles["Style"] = ResourceHandler.GetString("EditOptiob_Style");
                    }

                    if (mAnnotIsTextMarkup)
                    {
                        mMenuTitles["copy to clipboard"] = ResourceHandler.GetString("EditOption_CopyToClipboard");
                    }

                    if (mType == AnnotType.e_FreeText)
                    {
                        mMenuTitles["edit text"] = ResourceHandler.GetString("EditOption_EditText");
                    }

                    if (mAnnotIsSticky)
                    {
                        mAnnotIsScalable = false;
                    }
                    if (mAnnotIsTextMarkup)
                    {
                        mAnnotIsScalable = false;
                        mAnnotIsMovable = false;
                        mMenuTitles["Type"] = ResourceHandler.GetString("EditOption_TextMarkupType");
                    }

                    mMarkup = (IMarkup)mAnnot;
                    if (mAnnotIsTextMarkup)
                    {
                        mTextMarkup = (ITextMarkup)mAnnot;
                    }


                    mMenuTitles["delete"] = ResourceHandler.GetString("EditOption_Delete");
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlockRead();
                }


                if (mMenuTitles != null && !mAnnotIsSticky) // if sticky, we don't create menu until it's needed
                {
                    mCommandMenu = new PopupCommandMenu(mPDFView, mMenuTitles, OnCommandMenuClicked);
                    mCommandMenu.UseFadeAnimations(true);
                }
            }
        }


        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerID != -1)
            {
                return false; // ignore additional pointer presses
            }
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                return false; // If mouse, make sure it's the left button
            }
            if (mIsEditingText)
            {
                // do nothing, handled by tap
                return false;
            }

            // Hide menus
            mIsShowingCommandMenu = false;
            HideMenu(mCommandMenu);
            HideMenu(mSecondMenu);
            mSecondMenu = null;

            if (mBoxPopup != null) // e.g. color picker is open
            {
                return false;
            }
            if (mAnnotIsTextMarkup) // markup can't be moved
            {
                return false;
            }

            mPointerID = (int)e.Pointer.PointerId;
            mStartMoving = false; // reset whether we're moving or not.

            PointerPoint cn_downPoint = e.GetCurrentPoint(mViewerCanvas);

            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                if (cn_downPoint.Properties.IsEraser)
                {
                    EndCurrentTool(ToolType.e_pan);
                    return false;
                }
                if (mJustSwitchedFromAnotherTool)
                {
                    if (cn_downPoint.Properties.IsBarrelButtonPressed)
                    {
                        CreateAppearance(false);
                    }
                    else
                    {
                        EndCurrentTool(ToolType.e_pan);
                        return false;
                    }
                }
            }

            // see if we hit anything interesting in mViewerCanvas
            GetControlPoint(cn_downPoint.Position);

            if (mEffectiveCtrlPoint >= 0)
            {
                if (mJustSwitchedFromAnotherTool)
                {
                    mEffectiveCtrlPoint = CONTROL_POINT_FOR_MOVING; // always do a move the first time
                }
                DisableScrolling();
            }
            else if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                mNextToolMode = ToolType.e_pan;
            }
            mViewerCanvas.CapturePointer(e.Pointer);

            mJustSwitchedFromAnotherTool = false;
            return false;
        }

        protected virtual void GetControlPoint(UIPoint p)
        {
            double x = p.X;
            double y = p.Y;
            mEffectiveCtrlPoint = -1;

            // see if we're close to a ctrl point
            if (mAnnotIsScalable)
            {
                double thresh = Math.Pow(mCtrlPointRadius * 2.25, 2); // cheaper than sqrt
                double shortest_dist = thresh + 1;
                for (int i = 0; i < 8; ++i)
                {
                    double s = mCtrlPointLocations[i].X;
                    double t = mCtrlPointLocations[i].Y;

                    double dist = (x - s) * (x - s) + (y - t) * (y - t);
                    if (dist <= thresh && (dist < shortest_dist || shortest_dist < 0))
                    {
                        mEffectiveCtrlPoint = i;
                        shortest_dist = dist;
                    }
                }
            }

            // if we didn't get a ctrl point, check bounds
            if (mEffectiveCtrlPoint == -1)
            {
                if (x >= mLeft && x <= mRight && y >= mTop && y <= mBottom)
                {
                    mEffectiveCtrlPoint = 8;
                }
            }

            // means we're manipulating the widget
            if (mEffectiveCtrlPoint >= 0)
            {
                mPt1.x = x;
                mPt1.y = y;
                mIsManipulated = true;
                mOldLeft = mLeft;
                mOldRight = mRight;
                mOldTop = mTop;
                mOldBottom = mBottom;
            }
        }

        internal override bool PointerMovedHandler(object sender, PointerRoutedEventArgs e)
        {
            if (!mIsManipulated)
            {
                return false;
            }
            mPt2.x = e.GetCurrentPoint(mViewerCanvas).Position.X;
            mPt2.y = e.GetCurrentPoint(mViewerCanvas).Position.Y;
            if (!mStartMoving) // ensure we actually move a little before we start with updates and stuff
            {
                if ((Math.Abs(mPt2.x - mPt1.x) > START_MOVING_THRESHOLD)
                    || (Math.Abs(mPt2.y - mPt1.y) > START_MOVING_THRESHOLD))
                {
                    mStartMoving = true;
                }
                else
                {
                    return false;
                }
            }

            if (mToolMode != ToolType.e_annot_edit)
            {
                return false;
            }


            // check bounds
            switch (mEffectiveCtrlPoint)
            {
                case 8: // regular move
                    mLeft = mOldLeft + mPt2.x - mPt1.x;
                    mLeft = Math.Max(mLeft, mPageCropOnClient.x1);
                    mLeft = Math.Min(mLeft, mPageCropOnClient.x2 + mOldLeft - mOldRight);
                    mRight = mLeft + mOldRight - mOldLeft;
                    mTop = mOldTop + mPt2.y - mPt1.y;
                    mTop = Math.Max(mTop, mPageCropOnClient.y1);
                    mTop = Math.Min(mTop, mPageCropOnClient.y2 + mOldTop - mOldBottom);
                    mBottom = mTop + mOldBottom - mOldTop;
                    break;
                case e_ll:
                    mLeft = mOldLeft + mPt2.x - mPt1.x;
                    mLeft = Math.Max(mLeft, mPageCropOnClient.x1);
                    mLeft = Math.Min(mLeft, mRight);
                    mBottom = mOldBottom + mPt2.y - mPt1.y;
                    mBottom = Math.Max(mBottom, mTop);
                    mBottom = Math.Min(mBottom, mPageCropOnClient.y2);
                    break;
                case e_lm:
                    mBottom = mOldBottom + mPt2.y - mPt1.y;
                    mBottom = Math.Max(mBottom, mTop);
                    mBottom = Math.Min(mBottom, mPageCropOnClient.y2);
                    break;
                case e_lr:
                    mRight = mOldRight + mPt2.x - mPt1.x;
                    mRight = Math.Max(mRight, mOldLeft);
                    mRight = Math.Min(mRight, mPageCropOnClient.x2);
                    mBottom = mOldBottom + mPt2.y - mPt1.y;
                    mBottom = Math.Max(mBottom, mTop);
                    mBottom = Math.Min(mBottom, mPageCropOnClient.y2);
                    break;
                case e_mr:
                    mRight = mOldRight + mPt2.x - mPt1.x;
                    mRight = Math.Max(mRight, mOldLeft);
                    mRight = Math.Min(mRight, mPageCropOnClient.x2);
                    break;
                case e_ur:
                    mRight = mOldRight + mPt2.x - mPt1.x;
                    mRight = Math.Max(mRight, mOldLeft);
                    mRight = Math.Min(mRight, mPageCropOnClient.x2);
                    mTop = mOldTop + mPt2.y - mPt1.y;
                    mTop = Math.Max(mTop, mPageCropOnClient.y1);
                    mTop = Math.Min(mTop, mBottom);
                    break;
                case e_um:
                    mTop = mOldTop + mPt2.y - mPt1.y;
                    mTop = Math.Max(mTop, mPageCropOnClient.y1);
                    mTop = Math.Min(mTop, mBottom);
                    break;
                case e_ul:
                    mLeft = mOldLeft + mPt2.x - mPt1.x;
                    mLeft = Math.Max(mLeft, mPageCropOnClient.x1);
                    mLeft = Math.Min(mLeft, mRight);
                    mTop = mOldTop + mPt2.y - mPt1.y;
                    mTop = Math.Max(mTop, mPageCropOnClient.y1);
                    mTop = Math.Min(mTop, mBottom);
                    break;
                case e_ml:
                    mLeft = mOldLeft + mPt2.x - mPt1.x;
                    mLeft = Math.Max(mLeft, mPageCropOnClient.x1);
                    mLeft = Math.Min(mLeft, mRight);
                    break;
            }
            mHorizontalMiddle = (mLeft + mRight) / 2;
            mVerticalMiddle = (mTop + mBottom) / 2;
            CreateSelectionAppearance();

            return true;
        }


        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)

        {
            if (mPointerID < 0 || e.Pointer.PointerId != mPointerID)
            {
                return false;
            }
            mPointerID = -1;
            mEffectiveCtrlPoint = -1;
            mViewerCanvas.ReleasePointerCaptures();

            if (mToolMode != ToolType.e_annot_edit)
            {
                return false;
            }

            if (mIsManipulated)
            {
                if (mStartMoving)
                {
                    try
                    {
                        mPDFView.DocLock(true);

                        // points in Screen space
                        PDFDouble x1 = new PDFDouble(mLeft - mPDFView.GetAnnotationCanvasHorizontalOffset());
                        PDFDouble y1 = new PDFDouble(mTop - mPDFView.GetAnnotationCanvasVerticalOffset());
                        PDFDouble x2 = new PDFDouble(mRight - mPDFView.GetAnnotationCanvasHorizontalOffset());
                        PDFDouble y2 = new PDFDouble(mBottom - mPDFView.GetAnnotationCanvasVerticalOffset());

                        // Get coordinates of two opposite points in screen space
                        mPDFView.ConvScreenPtToPagePt(x1, y1, mAnnotPageNum);
                        mPDFView.ConvScreenPtToPagePt(x2, y2, mAnnotPageNum);

                        Rect newAnnotRect = new Rect(x1.Value, y1.Value, x2.Value, y2.Value);
                        newAnnotRect.Normalize();

                        mAnnot.Resize(newAnnotRect);

                        if (mType == AnnotType.e_Line || mType == AnnotType.e_Circle || mType == AnnotType.e_Square || mType == AnnotType.e_Polyline
                            || mType == AnnotType.e_Polygon || mType == AnnotType.e_Ink || mType == AnnotType.e_FreeText)
                        {
                            mAnnot.RefreshAppearance();
                        }



                        BuildAnnotBBox();
                        mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                        mToolManager.RaiseAnnotationEditedEvent(mAnnot);

                        // Update the viewer at the location of the old rectangle
                        Rect updateRect = new Rect(mOldLeft - mPDFView.GetAnnotationCanvasHorizontalOffset(), mOldTop - mPDFView.GetAnnotationCanvasVerticalOffset(),
                            mOldRight - mPDFView.GetAnnotationCanvasHorizontalOffset(), mOldBottom - mPDFView.GetAnnotationCanvasVerticalOffset());
                        mPDFView.Update(updateRect);

                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        mPDFView.DocUnlock();
                    }
                }

                // Our new values now become our old values for the next manipulation
                mOldLeft = mLeft;
                mOldRight = mRight;
                mOldTop = mTop;
                mOldBottom = mBottom;

                if (mToolManager.IsPopupMenuEnabled)
                {
                    if (mCommandMenu == null)
                    {
                        mCommandMenu = new PopupCommandMenu(mPDFView, mMenuTitles, OnCommandMenuClicked);
                        mCommandMenu.UseFadeAnimations(true);
                    }
                    mIsShowingCommandMenu = true;
                    PositionMenu(mCommandMenu);
                    ShowMenu(mCommandMenu);
                }
            }
            // reset values concerned with moving the widget
            mStartMoving = false;
            mIsManipulated = false;
            EnableScrolling();
            return false;
        }


        internal override bool PointerCaptureLostHandler(object sender, PointerRoutedEventArgs e)
        {
            if (mEffectiveCtrlPoint >= 0) // we were moving the widget, just abort
            {
                mNextToolMode = ToolType.e_pan;
            }
            //if (mPointerID >= 0) // we were moving the widget, just abort
            //{
            //    mNextToolMode = ToolType.e_pan;
            //}
            mPointerID = -1;
            return true;
        }

        internal override bool PointerCanceledHandler(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            mNextToolMode = ToolType.e_pan;
            return true;
        }



        internal override bool OnViewChanged(Object sender, ScrollViewerViewChangedEventArgs e)
        {
            // hide menus if we scroll or zoom
            HideMenu(mSecondMenu);
            mSecondMenu = null;
            if (mIsShowingCommandMenu)
            {
                HideMenu(mCommandMenu);
                mIsShowingCommandMenu = false;
            }
            base.OnViewChanged(sender, e);
            return false;
        }

        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)

        {
            if (mPointerID >= 0) // if we're currently moving the widget, don't allow key presses
            {
                return false;
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (mBoxPopup != null)
                {
                    mBoxPopup.Hide();
                    mBoxPopup = null;
                    mPDFView.Focus(Windows.UI.Xaml.FocusState.Pointer);
                }
                else if (mSecondMenu != null)
                {
                    HideMenu(mSecondMenu);
                    mSecondMenu = null;
                }
                else if (mIsShowingCommandMenu)
                {
                    HideMenu(mCommandMenu);
                    mIsShowingCommandMenu = false;
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                }
                return false;
            }
            if (mBoxPopup != null)
            {
                return false;
            }

            if (e.Key == Windows.System.VirtualKey.Delete) // delete annotation
            {
                HideMenu(mCommandMenu);
                mIsShowingCommandMenu = false;
                try
                {
                    mPDFView.DocLock(true);
                    mPage = mPDFView.GetDoc().GetPage(mAnnotPageNum);
                    mPage.AnnotRemove(mAnnot);
                    mToolManager.RaiseAnnotationRemovedEvent(mAnnot);
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                    mAnnot = null;
                }
                mNextToolMode = ToolType.e_pan;
                return true;
            }

            return base.KeyDownAction(sender, e);
        }

        internal override bool CloseOpenDialog()
        {
            bool closed = false;
            if (mSecondMenu != null)
            {
                mSecondMenu.Hide();
                mSecondMenu = null;
                closed = true;
            }
            else if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                EnableScrolling();
                closed = true;
            }
            else if (mTextPopup != null)
            {
                mTextPopup.IsOpen = false;
                mTextPopup = null;
                closed = true;
            }
            else if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
                mIsShowingCommandMenu = false;
                closed = true;
            }
            return closed;
        }

        #region Handlers For Popup Menu

         internal override void OnCommandMenuClicked(string title)
        {
            mIsShowingCommandMenu = false;
            HideMenu(mCommandMenu);

            if (title.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                CreateNewTool(ToolType.e_pan);
            }
            else if (title.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                mViewerCanvas.Children.Clear();
                try
                {
                    mPDFView.DocLock(true);
                    mPage = mPDFView.GetDoc().GetPage(mAnnotPageNum);
                    mPage.AnnotRemove(mAnnot);
                    mToolManager.RaiseAnnotationRemovedEvent(mAnnot);
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                    mAnnot = null;
                }

                CreateNewTool(ToolType.e_pan);
            }
            else if (title.Equals("note", StringComparison.OrdinalIgnoreCase))
            {
                if (mAnnotIsSticky)
                {
                    HandleNote();
                }
                else
                {
                    HandleNote();
                }
            }
            else if (title.Equals("thickness", StringComparison.OrdinalIgnoreCase))
            {
                HandleThickness();
            }
            else if (title.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                HandleType();
            }
            else if (title.Equals("stroke color", StringComparison.OrdinalIgnoreCase))
            {
                mEffectiveBrush = e_stroke;
                ShowColorPicker();
            }
            else if (title.Equals("fill color", StringComparison.OrdinalIgnoreCase))
            {
                if (mAnnot.GetAnnotType() == AnnotType.e_FreeText)
                {
                    mEffectiveBrush = e_text_fill;
                    ShowColorPicker();
                }
                else
                {
                    mEffectiveBrush = e_fill;
                    ShowColorPicker();
                }
            }
            else if (title.Equals("opacity", StringComparison.OrdinalIgnoreCase))
            {
                HandleOpacity();
            }
            else if (title.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                AnnotType aType = mAnnot.GetAnnotType();
                if (aType == AnnotType.e_Line || aType == AnnotType.e_Ink || aType == AnnotType.e_Polyline)
                {
                    mEffectiveBrush = e_stroke;
                    ShowColorPicker();
                }
                else if (aType == AnnotType.e_Highlight)
                {
                    mEffectiveBrush = e_highlight;
                    ShowColorPicker();
                }
                else
                {
                    mEffectiveBrush = e_text_markup;
                    ShowColorPicker();
                }
            }
            else if (title.Equals("edit text", StringComparison.OrdinalIgnoreCase))
            {
                mIsShowingCommandMenu = false;
                HideMenu(mCommandMenu); 
                //mCommandMenu = null; // we don't want this anymore
                HandleEditText();
            }
            else if (title.Equals("text Color", StringComparison.OrdinalIgnoreCase))
            {
                mEffectiveBrush = e_text_stroke;
                ShowColorPicker();
            }
            else if (title.Equals("font size", StringComparison.OrdinalIgnoreCase))
            {
                HandleFontSize();
            }
            else if (title.Equals("copy to clipboard", StringComparison.OrdinalIgnoreCase))
            {
                HandleCopy();
            }
            else if (title.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                HandleStyle();
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }
        }

        /// <summary>
        /// Prepares and shows the color picker
        /// </summary>
        protected void ShowColorPicker()
        {
            GetCurrentColor();
            bool canChooseEmpty = false;
            if (mEffectiveBrush == e_fill || mEffectiveBrush == e_text_fill)
            {
                canChooseEmpty = true;
            }

            Utilities.ColorPickerSimple cp = new Utilities.ColorPickerSimple();
            cp.AllowEmpty = canChooseEmpty;
            cp.VisibleColors = Utilities.ColorPickerSimple.ColorsToSelect.eight;
            if (mEffectiveBrush == e_highlight)
            {
                cp.VisibleColors = Utilities.ColorPickerSimple.ColorsToSelect.highlight;
            }
            cp.ColorSelected += cps_ColorSelected;

            mBoxPopup = new FixedSizedBoxPopup(mPDFView, cp, true, false);
            mBoxPopup.PreferAbove = true;
            mBoxPopup.Show(GetBoxPopupRect());

            // This callback allows the tool to catch the escape key from the color picker
            mBoxPopup.KeyDown += ColorPickerKeyDown;
        }

        void cps_ColorSelected(Color color)
        {
            if (color.A == 0)
            {
                ColorPickerHandler(false, true, color);
            }
            else
            {
                ColorPickerHandler(false, false, color);
            }
        }

        protected void ColorPickerKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ColorPickerHandler(true, false, Colors.Black);
            }
        }



        /// <summary>
        /// Delegate for the ColorPicker. Receives the color and saves the choice in the roaming settings
        /// </summary>
        /// <param name="canceled">If the user clicked on cancel</param>
        /// <param name="empty">If the selected Empty Color</param>
        /// <param name="color">The selected Color</param>
        protected void ColorPickerHandler(bool canceled, bool empty, Color color)
        {
            mBoxPopup.Hide();
            mBoxPopup = null;
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
            EnableScrolling();
            if (canceled)
            {
                return;
            }
            else
            {
                mUseColor = !empty;
                if (mUseColor)
                {
                    mSelectedColor = color;
                }

                SettingsColor sc = new SettingsColor() { R = color.R, G = color.G, B = color.B, Use = mUseColor };
                switch (mEffectiveBrush)
                {
                    case (e_stroke):
                        if (!mUseColor)
                        {
                            SettingsColor oldColor;
                            if (Settings.HasMarkupStrokeColor)
                            {
                                oldColor = Settings.MarkupStrokeColor;
                            }
                            else
                            {
                                oldColor = new SettingsColor() { R = mSelectedColor.R, G = mSelectedColor.G, B = mSelectedColor.B };
                            }
                            sc.R = oldColor.R;
                            sc.G = oldColor.G;
                            sc.B = oldColor.B;
                        }
                        Settings.MarkupStrokeColor = sc;
                        break;
                    case (e_fill):
                        Settings.MarkupFillColor = sc;
                        break;
                    case e_text_stroke:
                        Settings.TextStrokeColor = sc;
                        break;
                    case (e_text_fill):
                        Settings.TextFillColor = sc;
                        break;
                    case (e_highlight):
                        Settings.HighlightColor = sc;
                        break;
                    case (e_text_markup):
                        Settings.TextMarkupColor = sc;
                        break;
                }
            }
            SetCurrentAnnotColor();
        }


        /// <summary>
        /// Gets the selected color of the current annotation
        /// </summary>
        protected void GetCurrentColor()
        {
            ColorPt color = new ColorPt();
            int r = 0;
            int g = 0;
            int b = 0;
            mSelectedColor.A = 255;

            switch (mEffectiveBrush)
            {
                case e_stroke:
                case e_text_fill:
                case e_highlight:
                case e_text_markup:
                    color = mAnnot.GetColorAsRGB();
                    if (mAnnot.GetColorCompNum() == 0)
                    {
                        mSelectedColor.A = 0;
                    }
                    r = (int)(color.Get(0) * 255 + 0.5);
                    g = (int)(color.Get(1) * 255 + 0.5);
                    b = (int)(color.Get(2) * 255 + 0.5);
                    mSelectedColor.R = (byte)r;
                    mSelectedColor.G = (byte)g;
                    mSelectedColor.B = (byte)b;
                    break;
                case e_fill:
                    color = mMarkup.GetInteriorColor();
                    if ( mMarkup.GetInteriorColorCompNum() == 0)
                    {
                        mSelectedColor.A = 0;
                    }
                    r = (int)(color.Get(0) * 255 + 0.5);
                    g = (int)(color.Get(1) * 255 + 0.5);
                    b = (int)(color.Get(2) * 255 + 0.5);
                    mSelectedColor.R = (byte)r;
                    mSelectedColor.G = (byte)g;
                    mSelectedColor.B = (byte)b;
                    break;
                case e_text_stroke:
                    //pdftron.PDF.Annots.FreeText ft = new pdftron.PDF.Annots.FreeText(mAnnot.GetSDFObj());
                    pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                    color = ft.GetTextColor();
                    r = (int)(color.Get(0) * 255 + 0.5);
                    g = (int)(color.Get(1) * 255 + 0.5);
                    b = (int)(color.Get(2) * 255 + 0.5);
                    mSelectedColor.R = (byte)r;
                    mSelectedColor.G = (byte)g;
                    mSelectedColor.B = (byte)b;
                    break;
            }
        }

        /// <summary>
        /// Sets the selected color of the current annotation
        /// </summary>
        protected void SetCurrentAnnotColor()
        {
            ColorPt color = new ColorPt(mSelectedColor.R / 255.0, mSelectedColor.G / 255.0, mSelectedColor.B / 255.0);
            switch (mAnnot.GetAnnotType())
            {
                case AnnotType.e_Circle:
                case AnnotType.e_Square:
                case AnnotType.e_Line:
                case AnnotType.e_Ink:
                case AnnotType.e_Highlight:
                case AnnotType.e_Underline:
                case AnnotType.e_Squiggly:
                case AnnotType.e_StrikeOut:
                case AnnotType.e_Polygon:
                case AnnotType.e_Polyline:
                    //pdftron.PDF.Annots.Markup markup = new pdftron.PDF.Annots.Markup(mAnnot.GetSDFObj());
                    if (mEffectiveBrush == e_stroke || mEffectiveBrush == e_highlight || mEffectiveBrush == e_text_markup)
                    {
                        if (mUseColor)
                        {
                            mMarkup.SetColor(color, 3);
                        }
                        else
                        {
                            ColorPt emptyColor = new ColorPt(0, 0, 0, 0);
                            mMarkup.SetColor(emptyColor, 0); // using 0 color points makes it transparent
                        }
                    }
                    else if (mEffectiveBrush == e_fill)
                    {
                        if (mUseColor)
                        {
                            mMarkup.SetInteriorColor(color, 3);
                        }
                        else
                        {
                            ColorPt emptyColor = new ColorPt(0, 0, 0, 0);
                            mMarkup.SetInteriorColor(emptyColor, 0);
                        }
                    }
                    mMarkup.RefreshAppearance();
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                    break;
                case AnnotType.e_FreeText:
                    //pdftron.PDF.Annots.FreeText ft = new pdftron.PDF.Annots.FreeText(mAnnot.GetSDFObj());
                    pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                    if (mEffectiveBrush == e_text_stroke)
                    {
                        if (mUseColor)
                        {
                            ft.SetTextColor(color, 3);
                        }
                        else
                        {
                            ColorPt emptyColor = new ColorPt(0, 0, 0, 0);
                            ft.SetTextColor(emptyColor, 0);
                        }
                    }

                    if (mEffectiveBrush == e_text_fill)
                    {
                        if (mUseColor)
                        {
                            ft.SetColor(color, 3);
                        }
                        else
                        {
                            ColorPt emptyColor = new ColorPt(0, 0, 0, 0);
                            ft.SetColor(emptyColor, 0);
                        }
                    }
                    ft.RefreshAppearance();
                    mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                    break;
            }
        }

        #region Opacity
        protected void HandleOpacity()
        {
            Dictionary<string, string> opacityDict = new Dictionary<string, string>();
            opacityDict["25%"] = string.Format(ResourceHandler.GetString("EditOpacity_Format"), 25);
            opacityDict["50%"] = string.Format(ResourceHandler.GetString("EditOpacity_Format"), 50);
            opacityDict["75%"] = string.Format(ResourceHandler.GetString("EditOpacity_Format"), 75);
            opacityDict["100%"] = string.Format(ResourceHandler.GetString("EditOpacity_Format"), 100);
            mSecondMenu = new PopupCommandMenu(mPDFView, opacityDict, OnOpacitySelected);
            mSecondMenu.UseFadeAnimations(true);

            PositionMenu(mSecondMenu);
            ShowMenu(mSecondMenu);
            mIsShowingSecondMenu = true;
        }

        protected void OnOpacitySelected(string title)
        {
            mIsShowingSecondMenu = false;
            HideMenu(mSecondMenu);
            mSecondMenu = null;
            double opacity = -1;
            if (title.Equals("0%", StringComparison.OrdinalIgnoreCase))
            {
                opacity = 0;
            }
            else if (title.Equals("25%", StringComparison.OrdinalIgnoreCase))
            {
                opacity = 0.25;
            }
            else if (title.Equals("50%", StringComparison.OrdinalIgnoreCase))
            {
                opacity = 0.5;
            }
            else if (title.Equals("75%", StringComparison.OrdinalIgnoreCase))
            {
                opacity = 0.75;
            }
            else if (title.Equals("100%", StringComparison.OrdinalIgnoreCase))
            {
                opacity = 1.0;
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }

            if (opacity < 0 || opacity > 1)
            {
                return;
            }

            EnableScrolling();

            // Update settings
            AnnotType type = mAnnot.GetAnnotType();

            if (type == AnnotType.e_Underline || type == AnnotType.e_StrikeOut || type == AnnotType.e_Squiggly)
            {
                Settings.TextMarkupOpacity = opacity;
            }
            else if (type == AnnotType.e_Highlight)
            {
                Settings.HightlightOpacity = opacity;
                //roamingSettings.Values["Tools:HighlightOpacity"] = opacity;
            }
            else if (mAnnot.IsMarkup())
            {
                Settings.MarkupOpacity = opacity;
            }

            SetOpacity(opacity);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }

        protected void SetOpacity(double opacity)
        {
            mMarkup.SetOpacity(opacity);
            mMarkup.RefreshAppearance();
            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
        }

        #endregion Opacity

        #region Thickness
        protected void HandleThickness()
        {
            Dictionary<string, string> thicknessDict = new Dictionary<string, string>();
            thicknessDict["0.5 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 0.5);
            thicknessDict["1 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 1);
            thicknessDict["1.5 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 1.5);
            thicknessDict["3 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 3);
            thicknessDict["5 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 5);
            thicknessDict["7 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 7);
            thicknessDict["9 pt"] = string.Format(ResourceHandler.GetString("EditThickness_Format"), 9);

            mSecondMenu = new PopupCommandMenu(mPDFView, thicknessDict, OnThicknessSelected);
            mSecondMenu.UseFadeAnimations(true);

            PositionMenu(mSecondMenu);
            ShowMenu(mSecondMenu);
            mIsShowingSecondMenu = true;
        }

        protected void OnThicknessSelected(string title)
        {
            double thickness = -1;
            if (title.Equals("0.5 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 0.5;
            }
            else if (title.Equals("1 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 1.0;
            }
            else if (title.Equals("1.5 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 1.5;
            }
            else if (title.Equals("3 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 3.0;
            }
            else if (title.Equals("5 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 5.0;
            }
            else if (title.Equals("7 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 7.0;
            }
            else if (title.Equals("9 pt", StringComparison.OrdinalIgnoreCase))
            {
                thickness = 9.0;
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }
            mIsShowingCommandMenu = false;
            HideMenu(mSecondMenu);
            mSecondMenu = null;
            EnableScrolling();
            if (thickness < 0)
            {
                return;
            }

            // Update settings
            AnnotType type = mAnnot.GetAnnotType();
            if (type == AnnotType.e_Underline || type == AnnotType.e_StrikeOut || type == AnnotType.e_Squiggly)
            {
                Settings.TextMarkupThickness = thickness;
            }
            else
            {
                Settings.MarkupStrokeThickness = thickness;
            }
            SetThickness(thickness);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }


        protected void SetThickness(double thickness)
        {
            AnnotBorderStyle bs = mMarkup.GetBorderStyle();
            bs.width = thickness;
            mMarkup.SetBorderStyle(bs);
            mMarkup.RefreshAppearance();
            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
        }

        #endregion Thickness

        #region Text Markup Type

        protected void HandleType()
        {
            Dictionary<string, string> typeDict = new Dictionary<string, string>();
            AnnotType annotType = mAnnot.GetAnnotType();
            if (annotType != AnnotType.e_Highlight)
            {
                typeDict["Highlight"] = ResourceHandler.GetString("TextSelect_Option_Highlight");
            }
            if (annotType != AnnotType.e_Underline)
            {
                typeDict["Underline"] = ResourceHandler.GetString("TextSelect_Option_Underline");
            }
            if (annotType != AnnotType.e_StrikeOut)
            {
                typeDict["StrikeOut"] = ResourceHandler.GetString("TextSelect_Option_Strikeout");
            }
            if (annotType != AnnotType.e_Squiggly)
            {
                typeDict["Squiggly"] = ResourceHandler.GetString("TextSelect_Option_Squiggly");
            }

            mSecondMenu = new PopupCommandMenu(mPDFView, typeDict, OnTypeSelected);
            mSecondMenu.UseFadeAnimations(true);

            PositionMenu(mSecondMenu);
            ShowMenu(mSecondMenu);
            mIsShowingSecondMenu = true;
        }

        protected void OnTypeSelected(string title)
        {
            mIsShowingCommandMenu = false;
            HideMenu(mSecondMenu);
            mSecondMenu = null;
            EnableScrolling();

            SetSubType(title);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }


        protected void SetSubType(string subType)
        {
            try
            {
                mPDFView.DocLock(true);
                mAnnot.GetSDFObj().PutName("Subtype", subType);
            }
            catch (Exception) { }
            finally
            {
                mPDFView.DocUnlock();
            }
            mAnnot.RefreshAppearance();
            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
        }
        #endregion Text Markup Type

        #region Copy

        protected void HandleCopy()
        {
            pdftron.PDF.TextExtractor te = new pdftron.PDF.TextExtractor();
            mPage = mPDFView.GetDoc().GetPage(mAnnotPageNum);
            if (mPage != null)
            {
                te.Begin(mPage);
                String text = te.GetTextUnderAnnot(mAnnot);
                if (text == null)
                {
                    text = "";
                }

                UtilityFunctions.CopySelectedTextToClipBoard(text);
            }
        }
        #endregion Copy

        #region Notes


        protected void HandleNote()
        {
            if (mMarkup != null && mPDFView.IsEnabled)
            {
                Utilities.NoteDialogBase noteDialog = null;
                if (mToolManager.IsOnPhone)
                {
                    noteDialog = new Utilities.NoteDialogPhone(mMarkup, mPDFView, mToolManager, mAnnotPageNum);
                }
                else
                {
                    noteDialog = new Utilities.NoteDialogPhone(mMarkup, mPDFView, mToolManager, mAnnotPageNum);
                }
                noteDialog.NoteClosed += noteDialog_NoteClosed;
            }
            return;
        }

        void noteDialog_NoteClosed(bool wasDeleted)
        {
            if (wasDeleted)
            {
                EndCurrentTool(ToolType.e_pan);
            }
            else if (mToolManager.IsPopupMenuEnabled)
            {
                if (mCommandMenu == null)
                {
                    mCommandMenu = new PopupCommandMenu(mPDFView, mMenuTitles, OnCommandMenuClicked);
                    mCommandMenu.UseFadeAnimations(true);
                }
                mIsShowingCommandMenu = true;
                PositionMenu(mCommandMenu);
                ShowMenu(mCommandMenu);
            }
        }

        protected void NoteKeyDown(object sender, KeyRoutedEventArgs e)

        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
            }
        }


        /// <summary>
        /// Translates the typing area if necessary in order to keep the text area above virtual keyboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void NoteCustomKeyboardHandler(object sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            double RemainingHeight = e.OccludedRect.Y; // Y value of keyboard top (thus, also height of rest).
            double TotalHeight = mPDFView.ActualHeight;
            double KeyboardHeight = e.OccludedRect.Height; // height of keyboard

            e.EnsuredFocusedElementInView = true;
            mKeyboardOpen = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(mNoteDisplay);
            UIRect viewRect = GetElementRect(mPDFView);
            double bottomMargin = RemainingHeight - textRect.Bottom - 10;
            double topMargin = textRect.Top - viewRect.Top;

            // figure out translation
            if (bottomMargin < 0)
            {
                mKeyboardTranslateOffset = -bottomMargin;
                if (mKeyboardTranslateOffset > topMargin)
                {
                    mKeyboardTranslateOffset = topMargin;
                }
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mNoteDisplay, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
                sb.Begin();
            }
        }

        /// <summary>
        /// Translates the PDFViewCtrl back to offset 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void NoteInputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            mKeyboardOpen = false;
            if (mKeyboardTranslateOffset != 0)
            {
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mNoteDisplay, -mKeyboardTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
            }
        }

        #endregion Notes

        #region Text Edit

        protected void HandleEditText()
        {
            if (mToolManager.IsOnPhone)
            {
                HandleEditTextPhone();
            }
            else
            {
                HandleEditTextWindows();
            }
        }

        protected void HandleEditTextWindows()
        {
            mIsEditingText = true;
            this.Children.Clear();

            mEditTextBox = new RichEditBox();
            mEditTextBox.AcceptsReturn = true;
            mEditTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap;

            // Add two pixels per side, to make sure it cover the entire annotation
            //mEditTextBox.SetValue(Canvas.LeftProperty, mLeft - mPageCropOnClient.x1 - 1);
            //mEditTextBox.SetValue(Canvas.TopProperty, mTop - mPageCropOnClient.y1 - 1);
            mEditTextBox.Width = mRight - mLeft + 4;
            mEditTextBox.Height = mBottom - mTop + 4;
            mEditTextBox.KeyDown += FreeTextPopupKeyDown;

            //pdftron.PDF.Annots.FreeText ft = new pdftron.PDF.Annots.FreeText(mAnnot.GetSDFObj());
            pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
            // fetch text color
            int colorPts = ft.GetTextColorCompNum();
            ColorPt color = ft.GetTextColor();
            if (colorPts == 1) // greyscale
            {
                int r = (int)(color.Get(0) * 255 + 0.5);
                int g = (int)(color.Get(0) * 255 + 0.5);
                int b = (int)(color.Get(0) * 255 + 0.5);
                mSelectedColor.R = (byte)r;
                mSelectedColor.G = (byte)g;
                mSelectedColor.B = (byte)b;
            }
            else if (colorPts == 3)
            {
                int r = (int)(color.Get(0) * 255 + 0.5);
                int g = (int)(color.Get(1) * 255 + 0.5);
                int b = (int)(color.Get(2) * 255 + 0.5);
                mSelectedColor.R = (byte)r;
                mSelectedColor.G = (byte)g;
                mSelectedColor.B = (byte)b;
            }
            else // default to red
            {
                mSelectedColor = Colors.Red;
            }

            // Fetch background color
            Color BackgroundColor = Colors.White; // default
            colorPts = ft.GetColorCompNum();
            color = ft.GetColorAsRGB();
            if (colorPts == 3)
            {
                int r = (int)(color.Get(0) * 255 + 0.5);
                int g = (int)(color.Get(1) * 255 + 0.5);
                int b = (int)(color.Get(2) * 255 + 0.5);
                BackgroundColor.R = (byte)r;
                BackgroundColor.G = (byte)g;
                BackgroundColor.B = (byte)b;
            }            mFontSize = ft.GetFontSize();
            if (mFontSize <= 0)
            {
                mFontSize = 11;
            }

            mEditTextBox.FontSize = mFontSize * mPDFView.GetZoom();

            Windows.UI.Xaml.Style style = new Windows.UI.Xaml.Style { TargetType = typeof(RichEditBox) };
            style.Setters.Add(new Windows.UI.Xaml.Setter(TextBox.BackgroundProperty, new SolidColorBrush(BackgroundColor)));
            style.Setters.Add(new Windows.UI.Xaml.Setter(TextBox.ForegroundProperty, new SolidColorBrush(mSelectedColor)));

            mEditTextBox.Style = style;
            mEditTextBox.BorderThickness = new Thickness(0);

            mEditTextBox.Document.SetText(Windows.UI.Text.TextSetOptions.ApplyRtfDocumentDefaults, ft.GetContents());
            mEditTextBox.Loaded += (s, e) =>
                {
                    mEditTextBox.Focus(Windows.UI.Xaml.FocusState.Programmatic); // can't set focus until loaded
                    string outString = "";
                    mEditTextBox.Document.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out outString);
                    mEditTextBox.Document.Selection.StartPosition = outString.Length;
                };

            Border border = new Border();
            border.Width = mEditTextBox.Width;
            border.Height = mEditTextBox.Height;

            UIRect offsetRect = GetElementRect(mPDFView);

            mTextPopup = new Windows.UI.Xaml.Controls.Primitives.Popup();
            mTextPopup.Width = mEditTextBox.Width + (mFontSize * 0.2);
            mTextPopup.Height = mEditTextBox.Height + (mFontSize);
            mTextPopup.HorizontalOffset = mLeft - mPDFView.GetAnnotationCanvasHorizontalOffset() + offsetRect.X;
            mTextPopup.VerticalOffset = mTop - mPDFView.GetAnnotationCanvasVerticalOffset() + offsetRect.Y;

            border.Child = mEditTextBox;

            mTextPopup.Child = border;
            mTextPopup.IsOpen = true;

            DisableScrolling();

            mKeyboardHelper.AddShowingHandler(mEditTextBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(InputPaneHiding));
        }

        private void FreeTextPopupKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                CreateNewTool(ToolType.e_pan);
            }
        }

        pdftron.PDF.Tools.Utilities.NoteDialogPhone _NoteDialog = null;
        protected void HandleEditTextPhone()
        {
            _NoteDialog = pdftron.PDF.Tools.Utilities.NoteDialogPhone.CreateAsTextBoxHelper(mPDFView, mToolManager, mAnnotPageNum);
            _NoteDialog.NoteClosed += NoteDialog_NoteClosed;
            _NoteDialog.FocusWhenOpened = true;

            _NoteDialog.Loaded += (s, e) =>
            {
                try
                {
                    mPDFView.DocLockRead();
                    pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                    _NoteDialog.AnnotText = ft.GetContents();
                }
                catch (Exception) { }
                finally
                {
                    mPDFView.DocUnlockRead();
                }
            };
        }

        private void NoteDialog_NoteClosed(bool deleted)
        {
            if (!deleted)
            {
                SaveFreeText(_NoteDialog.AnnotText);
                _NoteDialog = null;
            }
            PositionMenu(mCommandMenu);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }


        protected void SaveFreeText(string text = null)
        {
            pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
            string outString = text;
            if (outString == null)
            {
                mEditTextBox.Document.GetText(Windows.UI.Text.TextGetOptions.UseCrlf, out outString);
            }
            try
            {
                mPDFView.DocLock(true);
                ft.SetContents(outString);
                ft.RefreshAppearance();
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
        }

        /// <summary>
        /// Translates the entire PDFViewCtrl if necessary in order to keep the text area above virtual keyboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void CustomKeyboardHandler(object sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            double RemainingHeight = e.OccludedRect.Y; // Y value of keyboard top (thus, also height of rest).
            double TotalHeight = mPDFView.ActualHeight;
            double KeyboardHeight = e.OccludedRect.Height; // height of keyboard

            e.EnsuredFocusedElementInView = true;
            mKeyboardOpen = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(mEditTextBox);
            UIRect viewRect = GetElementRect(mPDFView);
            double bottomMargin = RemainingHeight - textRect.Bottom - 10;
            double topMargin = textRect.Top - viewRect.Top;

            // figure out translation
            if (bottomMargin < 0)
            {
                mKeyboardTranslateOffset = -bottomMargin;
                if (mKeyboardTranslateOffset > topMargin)
                {
                    mKeyboardTranslateOffset = topMargin;
                }
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mPDFView, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
                Windows.UI.Xaml.Media.Animation.Storyboard sbText = KeyboardTranslate(mEditTextBox, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
                sb.Begin();
                sbText.Begin();
            }
        }

        /// <summary>
        /// Translates the PDFViewCtrl back to offset 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void InputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            mKeyboardOpen = false;
            if (mKeyboardTranslateOffset != 0)
            {
                Windows.UI.Xaml.Media.Animation.Storyboard sb = KeyboardTranslate(mPDFView, -mKeyboardTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
                Windows.UI.Xaml.Media.Animation.Storyboard sbText = KeyboardTranslate(mEditTextBox, -mKeyboardTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
                sbText.Begin();
            }
        }

        protected UIRect GetElementRect(Windows.UI.Xaml.FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }
        #endregion Text Edit

        #region Font Size

        protected void HandleFontSize()
        {
            Dictionary<string, string> fontsizeDict = new Dictionary<string, string>();
            fontsizeDict["8"] = string.Format(ResourceHandler.GetString("EditFontSize_Format"), 8);
            fontsizeDict["11"] = string.Format(ResourceHandler.GetString("EditFontSize_Format"), 11);
            fontsizeDict["16"] = string.Format(ResourceHandler.GetString("EditFontSize_Format"), 16);
            fontsizeDict["24"] = string.Format(ResourceHandler.GetString("EditFontSize_Format"), 24);
            fontsizeDict["36"] = string.Format(ResourceHandler.GetString("EditFontSize_Format"), 36);

            mSecondMenu = new PopupCommandMenu(mPDFView, fontsizeDict, OnFontSizeSelected);
            mSecondMenu.UseFadeAnimations(true);
            PositionMenu(mSecondMenu);
            mSecondMenu.Show();
        }

        protected void OnFontSizeSelected(string title)
        {
            mIsShowingCommandMenu = false;
            HideMenu(mSecondMenu);
            mSecondMenu = null;
            double fontSize = -1;
            if (title.Equals("8", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = 8;
            }
            else if (title.Equals("11", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = 11;
            }
            else if (title.Equals("16", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = 16;
            }
            else if (title.Equals("24", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = 24;
            }
            else if (title.Equals("36", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = 36;
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }

            if (fontSize < 0)
            {
                return;
            }

            // Update settings
            Settings.FontSize = fontSize;

            //SetFontSize(new pdftron.PDF.Annots.FreeText(mAnnot.GetSDFObj()), fontSize);
            SetFontSize((FreeText)mAnnot, fontSize);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }

        /// <summary>
        /// This function sets the font size of the free text annotation
        /// </summary>
        /// <param name="ft">The Free Text Annotation</param>
        /// <param name="fontSize">The size of the font</param>
        protected void SetFontSize(pdftron.PDF.Annots.FreeText ft, double fontSize)
        {
            try
            {
                mPDFView.DocLock(true);
                ft.SetFontSize(fontSize);
                ft.RefreshAppearance();
            }
            catch (Exception)
            {
            }
            finally
            {
                mPDFView.DocUnlock();
            }
            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
        }

        #endregion Font Size

        #region AnnotationProperties

        private void HandleStyle()
        {
            AnnotationPropertyDialog dialog = new AnnotationPropertyDialog();

            SetDialogProperties(dialog.ViewModel);

            dialog.ViewModel.AssociatedTool = UtilityFunctions.GetToolTypeFromAnnotType(mAnnot);

            UIRect positionRect = GetBoxPopupRect();
            UIRect pdfViewCtrlRect = UtilityFunctions.GetElementRect(mPDFView);
            UIRect bound = Window.Current.Bounds;

            positionRect.X += pdfViewCtrlRect.Left;
            positionRect.Y += pdfViewCtrlRect.Top;

            double left = positionRect.Left + pdfViewCtrlRect.Left;
            double right = positionRect.Right + pdfViewCtrlRect.Left;
            double top = positionRect.Top + pdfViewCtrlRect.Top;
            double bottom = positionRect.Bottom + pdfViewCtrlRect.Top;

            if (left < 0)
            {
                left = 0;
            }
            if (top < 0)
            {
                top = 0;
            }
            if (right > bound.Width)
            {
                right = bound.Width;
            }
            if (bottom > bound.Height)
            {
                bottom = bound.Height;
            }

            pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesDisplay display = pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesDisplay.CreateAnnotationPropertiesDisplay(
                new UIRect(left, top, right - left, bottom - top), dialog,
                Controls.ViewModels.AnnotationPropertiesDisplay.Placement.PreferBeside);
            display.AnnotationPropertyDialogClosed += Display_AnnotationPropertyDialogClosed;
        }

        private void Display_AnnotationPropertyDialogClosed(AnnotationPropertyDialog dialog)
        {
            SetAnnotPropertiesFromDialog(dialog.ViewModel);
        }

        private void SetDialogProperties(pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesViewModel viewModel)
        {
            try
            {
                mPDFView.DocLockRead();
                SettingsColor primary = GetPrimaryColor();
                viewModel.MainColor = primary;
                if (HasSecondaryColor())
                {
                    SettingsColor secondary = GetSecondaryColor();
                    viewModel.FillColor = secondary;
                }

                if (mType == AnnotType.e_Line || mType == AnnotType.e_Ink || mType == AnnotType.e_Square
                    || mType == AnnotType.e_Circle || mType == AnnotType.e_Polygon || mType == AnnotType.e_Polyline
                    || mType == AnnotType.e_Underline || mType == AnnotType.e_Squiggly || mType == AnnotType.e_StrikeOut)
                {
                    AnnotBorderStyle bs = mAnnot.GetBorderStyle();
                    viewModel.Thickness = bs.width;
                }

                if (mAnnotIsTextMarkup || mType == AnnotType.e_Line || mType == AnnotType.e_Ink
                    || mType == AnnotType.e_Square || mType == AnnotType.e_Circle || mType == AnnotType.e_Polygon
                    || mType == AnnotType.e_Polyline || mType == AnnotType.e_FreeText)
                {
                    viewModel.Opacity = mMarkup.GetOpacity();
                }

                if (mType == AnnotType.e_FreeText)
                {
                    pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                    viewModel.FontSize = ft.GetFontSize();
                }
            }
            catch (Exception) { }
            finally
            {
                mPDFView.DocUnlockRead();
            }
        }

        private void SetAnnotPropertiesFromDialog(pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesViewModel viewModel)
        {
            if (!viewModel.PropertyWasUpdated)
            {
                return;
            }
            try
            {
                mPDFView.DocLock(true);

                SetPrimaryColor(viewModel.MainColor);

                if (HasSecondaryColor())
                {
                    SetSecondaryColor(viewModel.FillColor);
                }

                if (mType == AnnotType.e_Line || mType == AnnotType.e_Ink || mType == AnnotType.e_Square
                    || mType == AnnotType.e_Circle || mType == AnnotType.e_Polygon || mType == AnnotType.e_Polyline
                    || mType == AnnotType.e_Underline || mType == AnnotType.e_Squiggly || mType == AnnotType.e_StrikeOut)
                {
                    AnnotBorderStyle bs = mMarkup.GetBorderStyle();
                    bs.width = viewModel.Thickness;
                    mMarkup.SetBorderStyle(bs);
                }

                if (mAnnotIsTextMarkup || mType == AnnotType.e_Line || mType == AnnotType.e_Ink
                    || mType == AnnotType.e_Square || mType == AnnotType.e_Circle || mType == AnnotType.e_Polygon
                    || mType == AnnotType.e_Polyline || mType == AnnotType.e_FreeText)
                {
                    mMarkup.SetOpacity(viewModel.Opacity);
                }

                if (mType == AnnotType.e_FreeText)
                {
                    pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                    ft.SetFontSize(viewModel.FontSize);
                }
            }
            catch (Exception) { }
            finally
            {
                mPDFView.DocUnlock();
            }
            mAnnot.RefreshAppearance();
            mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
            mToolManager.RaiseAnnotationEditedEvent(mAnnot);

            RaisePropertyChangedInHelperMode();
        }

        private SettingsColor GetPrimaryColor()
        {
            int r;
            int g;
            int b;
            if (mType == AnnotType.e_FreeText)
            {
                pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                ColorPt textColor = ft.GetTextColor();
                r = (int)(textColor.Get(0) * 255 + 0.5);
                g = (int)(textColor.Get(1) * 255 + 0.5);
                b = (int)(textColor.Get(2) * 255 + 0.5);
                return new SettingsColor((byte)r, (byte)g, (byte)b, true);
            }

            ColorPt color = mAnnot.GetColorAsRGB();
            r = (int)(color.Get(0) * 255 + 0.5);
            g = (int)(color.Get(1) * 255 + 0.5);
            b = (int)(color.Get(2) * 255 + 0.5);
            return new SettingsColor((byte)r, (byte)g, (byte)b, true);
        }

        private void SetPrimaryColor(SettingsColor color)
        {
            ColorPt mainColor = new ColorPt(color.R / 255.0, color.G / 255.0, color.B / 255.0);
            if (mType == AnnotType.e_FreeText)
            {
                pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                ft.SetTextColor(mainColor, 3);
            }
            else
            {
                mAnnot.SetColor(mainColor, 3);
            }
        }

        private bool HasSecondaryColor()
        {
            switch (mType)
            {
                case AnnotType.e_Square:
                case AnnotType.e_Circle:
                case AnnotType.e_Polygon:
                case AnnotType.e_FreeText:
                    return true;
            }
            return false;
        }

        private SettingsColor GetSecondaryColor()
        {
            int r;
            int g;
            int b;
            if (mType == AnnotType.e_FreeText)
            {
                pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                if (ft.GetColorCompNum() == 0)
                {
                    return new SettingsColor(0, 0, 0, false);
                }
                ColorPt color = ft.GetColorAsRGB();
                r = (int)(color.Get(0) * 255 + 0.5);
                g = (int)(color.Get(1) * 255 + 0.5);
                b = (int)(color.Get(2) * 255 + 0.5);
                return new SettingsColor((byte)r, (byte)g, (byte)b, true);
            }
            if (mMarkup.GetInteriorColorCompNum() == 0)
            {
                return new SettingsColor(0, 0, 0, false);
            }
            ColorPt fillColor = mMarkup.GetInteriorColor();
            r = (int)(fillColor.Get(0) * 255 + 0.5);
            g = (int)(fillColor.Get(1) * 255 + 0.5);
            b = (int)(fillColor.Get(2) * 255 + 0.5);
            return new SettingsColor((byte)r, (byte)g, (byte)b, true);
        }

        private void SetSecondaryColor(SettingsColor color)
        {
            ColorPt fillColor = new ColorPt(color.R / 255.0, color.G / 255.0, color.B / 255.0);
            ColorPt emptyColor = new ColorPt(0, 0, 0, 0);
            if (mType == AnnotType.e_FreeText)
            {
                pdftron.PDF.Annots.FreeText ft = (FreeText)mAnnot;
                if (color.Use)
                {
                    ft.SetColor(fillColor, 3);
                }
                else
                {
                    
                    mMarkup.SetColor(emptyColor, 0);
                }
            }
            else
            {
                if (color.Use)
                {
                    mMarkup.SetInteriorColor(fillColor, 3);
                }
                else
                {
                    mMarkup.SetInteriorColor(emptyColor, 0);
                }
            }
        }

        #endregion AnnotationProperties

        #endregion Handlers For Popup Menu

        #region Helper Functions

        internal void ShowMenu(PopupCommandMenu m)
        {
            if (m != null)
            {
                m.Show();
                DisableScrolling();
            }
        }


        internal void HideMenu(PopupCommandMenu m)
        {
            
            if (m != null)
            {
                m.Hide();
            }
        }

        internal virtual void PositionMenu(PopupCommandMenu m)
        {
            if (m != null)
            {
                if (mHelperModePositionRect != null)
                {
                    m.TargetSquare(mHelperModePositionRect.x1, mHelperModePositionRect.y1, mHelperModePositionRect.x2, mHelperModePositionRect.y2);
                }
                else
                {
                    m.TargetSquare(mLeft - mPDFView.GetAnnotationCanvasHorizontalOffset(), mTop - mPDFView.GetAnnotationCanvasVerticalOffset(), mRight - mPDFView.GetAnnotationCanvasHorizontalOffset(), mBottom - mPDFView.GetAnnotationCanvasVerticalOffset());
                }
            }
        }

        internal virtual UIRect GetBoxPopupRect()
        {
            if (mHelperModePositionRect != null)
            {
                return new UIRect(mHelperModePositionRect.x1, mHelperModePositionRect.y1, mHelperModePositionRect.Width(), mHelperModePositionRect.Height());
            }
            return new UIRect(mLeft - mPDFView.GetAnnotationCanvasHorizontalOffset(), mTop - mPDFView.GetAnnotationCanvasVerticalOffset(), mRight - mLeft, mBottom - mTop);
        }

        internal void SetAnnotEditAsHelperTool(IAnnot annot, int pageNumber)
        {
            mAnnot = annot;
            mAnnotPageNum = pageNumber;
            PopulateMenu();
            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            mIsInHelperMode = true;
        }


        internal void HelperModeShowMenu(PDFRect rect)
        {
            mHelperModePositionRect = rect;
            mCommandMenu.TargetSquare(rect.x1, rect.y1, rect.x2, rect.y2);
            ShowMenu(mCommandMenu);
            mIsShowingCommandMenu = true;
        }

        internal void HelperModeHideMenu()
        {
            if (mIsShowingCommandMenu)
            {
                mCommandMenu.Hide();
            }
            mIsShowingCommandMenu = false;
            if (mSecondMenu != null)
            {
                mSecondMenu.Hide();
            }
        }

        protected void RaisePropertyChangedInHelperMode()
        {
            if (mIsInHelperMode && AnnotPropertyChangedInHelperMode != null)
            {
                AnnotPropertyChangedInHelperMode();
            }
        }

        #endregion Helper Functions
    }
}

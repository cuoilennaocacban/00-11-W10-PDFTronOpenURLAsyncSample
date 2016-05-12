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

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;
using UIPopup = Windows.UI.Xaml.Controls.Primitives.Popup;

using pdftron.PDF;
using pdftron.SDF;
using PDFDouble = pdftron.Common.DoubleRef;
using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;

namespace pdftron.PDF.Tools
{
    class FormFill : Tool
    {
        protected FixedSizedBoxPopup mBoxPopup;

        protected pdftron.PDF.Field mField;
        protected Canvas mViewerCanvas;
        protected Rect mPageCropOnClient;

        protected UIPopup mTextPopup = null;
        protected Border mTextBorder = null;
        protected TextBox mTextBox = null;
        protected PasswordBox mPasswordBox = null;
        protected double mBorderWidth = 0;
        protected string mOldText = "";
        protected bool mIsMultiLine = false;

        protected Brush mBackgroundBrush;
        protected Brush mTextBrush;
        protected double mFontSize = 0;

        // choice
        protected bool mIsCombo = false;
        protected bool mIsMultiChoice = false;
        ListView mSelectList;
        protected bool mIsChoiceSaved = false;

        // keyboard
        protected KeyboardHelper mKeyboardHelper;
        protected bool mKeyboardOpen = false;
        protected double mKeyboardTranslateOffset = 0;

        // Text popup for phone
        protected bool _IsText = false;
        protected pdftron.PDF.Tools.Utilities.NoteDialogPhone _NoteDialog = null;

        public FormFill(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_form_fill;
            mToolMode = ToolType.e_form_fill;

            // register for the keyboard callback
            mKeyboardHelper = new KeyboardHelper();
            mKeyboardHelper.SubscribeToKeyboard(true);
        }

        internal override void OnCreate()
        {
            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            mViewerCanvas.Children.Add(this);

            // Make ourselves cover the page
            mPageCropOnClient = BuildPageBoundBoxOnClient(mAnnotPageNum);
            this.SetValue(Canvas.LeftProperty, mPageCropOnClient.x1);
            this.SetValue(Canvas.TopProperty, mPageCropOnClient.y1);
            this.Width = mPageCropOnClient.x2 - mPageCropOnClient.x1;
            this.Height = mPageCropOnClient.y2 - mPageCropOnClient.y1;

            //this.Background = new SolidColorBrush(Color.FromArgb(50, 150, 50, 150));
        }

        internal override void OnClose()
        {
            if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
            }
            if (mTextPopup != null)
            {
                mTextPopup.IsOpen = false;
                mTextPopup = null;
            }
            mToolManager.mCurrentBoxPopup = null;
            EnableScrolling();
            mViewerCanvas.Children.Remove(this);
            base.OnClose();
            DelayedUnsubscribe();
        }

        // This delays the unsubscribing of the keyboard, so that the UI will have time to invoke the callbacks it needs.
        private async void DelayedUnsubscribe()
        {
            await Task.Delay(50);
            if (mTextBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mTextBox);
                mKeyboardHelper.SetHidingHandler(null);
            }
            if (mPasswordBox != null)
            {
                mKeyboardHelper.RemoveShowingHandler(mPasswordBox);
                mKeyboardHelper.SetHidingHandler(null);
            }
            mKeyboardHelper.SubscribeToKeyboard(false);
        }

        internal override bool OnScale()
        {
            mNextToolMode = ToolType.e_pan;
            return false;
        }

        internal override bool OnSize()
        {
            mNextToolMode = ToolType.e_pan;
            return false;
        }

        internal override bool OnViewChanged(Object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            mNextToolMode = ToolType.e_pan;
            base.OnViewChanged(sender, e);
            return false;
        }

        internal override bool OnPageNumberChanged(int current_page, int num_pages)
        {
            if (mBoxPopup != null)
            {
                mNextToolMode = ToolType.e_pan;
            }
            if (mTextBox != null)
            {
                SaveText();
                mNextToolMode = ToolType.e_pan;
            }
            base.OnPageNumberChanged(current_page, num_pages);
            return false;
        }

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            base.PointerPressedHandler(sender, e);
            if (mTextBox != null || mPasswordBox != null)
            {
                DisableScrolling();
                mToolManager.SuppressTap = true;
                return true;
            }
            return false;
        }

        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            base.PointerReleasedHandler(sender, e);
            if (mToolManager.ContactPoints.Count == 0)
            {
                if (mTextBox != null || mPasswordBox != null)
                {
                    mNextToolMode = ToolType.e_pan;
                    if (mTextBox != null)
                    {
                        SaveText();
                    }
                    if (mPasswordBox != null)
                    {
                        SavePassword();
                    }
                    return true;
                }
            }
            return false;
        }

        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            UIPoint p = e.GetPosition(mPDFView);
            if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                SaveChoice();
                mNextToolMode = ToolType.e_pan;
            }
            else if (mJustSwitchedFromAnotherTool || IsPointInsideAnnot(p.X, p.Y))
            {
                handleForm(p.X, p.Y);
            }
            else
            {
                if (mTextBox != null)
                {
                    SaveText();
                }
                if (mPasswordBox != null)
                {
                    SavePassword();
                }
                if (mTextPopup != null)
                {
                    mTextPopup.IsOpen = false;
                }

                int x = (int)(p.X + 0.5);
                int y = (int)(p.Y + 0.5);

                mPDFView.DocLockRead();

                bool foundAnotherFormElement = false;

                if (mKeyboardTranslateOffset == 0)
                {
                    mAnnot = null;
                    SelectAnnot(x, y);
                    if (mAnnot != null)
                    {
                        try
                        {
                            if (mAnnot.GetAnnotType() == AnnotType.e_Widget)
                            {
                                foundAnotherFormElement = true;
                                // get rid of any popup window
                                if (mBoxPopup != null)
                                {
                                    mBoxPopup.Hide();
                                    mBoxPopup = null;
                                }
                                if (mTextPopup != null)
                                {
                                    mTextPopup.IsOpen = false;
                                    mTextPopup = null;
                                }

                                // get rid of keyboard handlers
                                if (mTextBox != null)
                                {
                                    mKeyboardHelper.RemoveShowingHandler(mTextBox);
                                    mKeyboardHelper.SetHidingHandler(null);
                                }
                                if (mPasswordBox != null)
                                {
                                    mKeyboardHelper.RemoveShowingHandler(mPasswordBox);
                                    mKeyboardHelper.SetHidingHandler(null);
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
                    }

                }
                mPDFView.DocUnlockRead();
                if (foundAnotherFormElement)
                {
                    handleForm(p.X, p.Y);
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                }
            }
            mJustSwitchedFromAnotherTool = false;
            return true;
        }

        // At this point we can expect to have tapped on a widget annot
        private async void handleForm(double x, double y) {
		
            if (mAnnot != null)
            {
                try
                {
                    mPDFView.DocLockRead();
                    pdftron.PDF.Annots.Widget w = (pdftron.PDF.Annots.Widget)mAnnot;
                    mField = w.GetField();

                    if (mField.IsValid() && !mField.GetFlag(FieldFlag.e_read_only))
                    {
                        FieldType fieldType = mField.GetType();
                        if (fieldType == FieldType.e_check)
                        {
                            mField.SetValue(!mField.GetValueAsBool());
                            Rect updateRect = mField.GetUpdateRect();
                            updateRect = ConvertFromPageRectToScreenRect(updateRect, mAnnotPageNum);
                            updateRect.Normalize();
                            mPDFView.Update(mField);
                            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                            mNextToolMode = ToolType.e_pan;
                        }

                        else if (fieldType == FieldType.e_radio)
                        {
                            mField.SetValue(true);
                            Rect updateRect = mField.GetUpdateRect();
                            updateRect = ConvertFromPageRectToScreenRect(updateRect, mAnnotPageNum);
                            updateRect.Normalize();
                            mPDFView.Update(mField);
                            mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                            mNextToolMode = ToolType.e_pan;
                        }

                        else if (fieldType == FieldType.e_button)
                        {
                            pdftron.PDF.Annots.Link link = new pdftron.PDF.Annots.Link(mAnnot.GetSDFObj());
                            // Can't use regular casting, since mAnnot is of type Widget.

                            pdftron.PDF.Action a = link.GetAction();
                            if (a != null && a.IsValid())
                            {
                                ActionType at = a.GetType();
                                if (at == ActionType.e_URI)
                                {
                                    pdftron.SDF.Obj o = a.GetSDFObj();
                                    o = o.FindObj("URI");
                                    if (o != null)
                                    {
                                        String uristring = o.GetAsPDFText();
                                        var uri = new Uri(uristring);

                                        var success = await Windows.System.Launcher.LaunchUriAsync(uri);
                                        if (!success)
                                        {
                                            // Take appropriate action if desirable
                                        }
                                    }
                                }
                                else if (at == ActionType.e_GoTo)
                                {
                                    mPDFView.ExecuteAction(a);
                                }
                            }
                            mNextToolMode = ToolType.e_pan;
                        }

                        else if (fieldType == FieldType.e_choice)
                        {
                            HandleChoice();
                        }

                        else if (fieldType == FieldType.e_text)
                        {
                            if (mField.GetFlag(FieldFlag.e_password))
                            {
                                HandlePassword();
                            }
                            else if (mField.GetFlag(FieldFlag.e_multiline))
                            {
                                mIsMultiLine = true;
                                HandleText();
                            }
                            else
                            {
                                mIsMultiLine = false;
                                HandleText();
                            }
                        }

                        else if (fieldType == FieldType.e_signature)
                        {
                            mNextToolMode = ToolType.e_signature;
                        }
                    }
                }
                catch (Exception)
                {
                }
                finally {
                    mPDFView.DocUnlockRead();
                }
            }
        }

        protected void HandleText()
        {
            if (mToolManager.IsOnPhone)
            {
                HandleTextPhone();
            }
            else
            {
                HandleTextWindows();
            }
        }

        protected void HandleTextWindows()
        {
            BuildAnnotBBox();

            mTextBox = new TextBox();
            mTextBox.Background = new SolidColorBrush(Colors.White);
            mTextBox.Foreground = new SolidColorBrush(Colors.Black);
            mTextBox.MinHeight = 1; // override the default
            mTextBox.MinWidth = 1;
            mTextBox.Padding = new Windows.UI.Xaml.Thickness(0);
            mTextBox.FontFamily = new Windows.UI.Xaml.Media.FontFamily("Arial");

            mTextPopup = new Windows.UI.Xaml.Controls.Primitives.Popup();

            mOldText = mField.GetValueAsString();

            if (mIsMultiLine)
            {
                mTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap;
                mTextBox.AcceptsReturn = true;
            }
            else
            {
                mTextBox.TextWrapping = Windows.UI.Xaml.TextWrapping.NoWrap;
                mTextBox.AcceptsReturn = false;
            }

            // Sets the fontsize
            SetFontSize();
            if (mFontSize > 0)
            {
                mTextBox.FontSize = mFontSize;
            }
            else
            {
                mTextBox.FontSize = 10;
            }

            // Get text from field, together with text properties
            ExtractTextProperties();

            // position the textbox
            PositionElement(mTextBox);

            // Get the colors for the textbox
            MapColorFont();
            mTextBox.Foreground = mTextBrush;
            mTextBox.Background = mBackgroundBrush;


            mTextBox.Loaded += (s, e) =>
            {
                mTextBox.Focus(Windows.UI.Xaml.FocusState.Pointer); // can't set focus until loaded
                mTextBox.SelectionStart = mTextBox.Text.Length; // set pointer at end of text
            };

            mTextBorder = new Border();
            mTextBorder.Background = mTextBox.Background;
            mTextBorder.Child = mTextBox;
            mTextPopup.Child = mTextBorder;
            mTextPopup.IsOpen = true;

            mKeyboardHelper.AddShowingHandler(mTextBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(InputPaneHiding));
        }

        protected void HandleTextPhone()
        {
            _IsText = true;
            _NoteDialog = pdftron.PDF.Tools.Utilities.NoteDialogPhone.CreateAsTextBoxHelper(mPDFView, mToolManager, mAnnotPageNum);
            _NoteDialog.NoteClosed += NoteDialog_NoteClosed;
            if (!mIsMultiLine)
            {
                _NoteDialog.TextInputMode = Utilities.NoteDialogPhone.InputMode.SingleLineText;
            }
            _NoteDialog.FocusWhenOpened = true;

            _NoteDialog.Loaded += (s, e) =>
            {
                try
                {
                    mPDFView.DocLockRead();
                    // set initial text
                    mOldText = mField.GetValueAsString();
                    _NoteDialog.AnnotText = mOldText;
                }
                catch (Exception) { }
                finally
                {
                    mPDFView.DocUnlockRead();
                }
            };
        }

        protected void HandlePassword()
        {
            if (mToolManager.IsOnPhone)
            {
                HandlePasswordPhone();
            }
            else
            {
                HandlePasswordWindows();
            }
        }

        protected void HandlePasswordWindows()
        {
            BuildAnnotBBox();

            mPasswordBox = new PasswordBox();
            mPasswordBox.Background = new SolidColorBrush(Colors.White);
            mPasswordBox.Foreground = new SolidColorBrush(Colors.Black);
            mPasswordBox.MinHeight = 1; // override the default
            mPasswordBox.MinWidth = 1;
            mPasswordBox.Padding = new Windows.UI.Xaml.Thickness(0);

            mTextPopup = new Windows.UI.Xaml.Controls.Primitives.Popup();

            mOldText = mField.GetValueAsString();

            // position the password box
            PositionElement(mPasswordBox);

            // Sets the fontsize
            SetFontSize();
            mPasswordBox.FontSize = mFontSize;

            // Get text from field, together with text properties
            ExtractTextProperties();

            // Get the colors for the textbox
            MapColorFont();
            mPasswordBox.Foreground = mTextBrush;
            mPasswordBox.Background = mBackgroundBrush;

            mPasswordBox.Loaded += (s, e) =>
            {
                mPasswordBox.Focus(Windows.UI.Xaml.FocusState.Pointer); // can't set focus until loaded
            };

            //this.Children.Add(mPasswordBox);
            mTextBorder = new Border();
            mTextBorder.Background = mPasswordBox.Background;
            mTextBorder.Child = mPasswordBox;
            mTextPopup.Child = mTextBorder;
            mTextPopup.IsOpen = true;

            mKeyboardHelper.AddShowingHandler(mPasswordBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(InputPaneHiding));
        }

        protected void HandlePasswordPhone()
        {
            _IsText = false;
            _NoteDialog = pdftron.PDF.Tools.Utilities.NoteDialogPhone.CreateAsTextBoxHelper(mPDFView, mToolManager, mAnnotPageNum);
            _NoteDialog.NoteClosed += NoteDialog_NoteClosed;
            _NoteDialog.TextInputMode = Utilities.NoteDialogPhone.InputMode.Password;
            _NoteDialog.FocusWhenOpened = true;

            _NoteDialog.Loaded += (s, e) =>
            {
                try
                {
                    mPDFView.DocLockRead();
                    // set initial text
                    mOldText = mField.GetValueAsString();
                    _NoteDialog.AnnotText = mOldText;
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
                SavePassword(_NoteDialog.AnnotText);
            }
            _NoteDialog = null;
        }

        protected void ExtractTextProperties()
        {
            try
            {
                // compute border width
                AnnotBorderStyle bs = mAnnot.GetBorderStyle();
                Obj aso = mAnnot.GetSDFObj();
                bool hasbs = aso.FindObj("BS") == null;
                bool hasborder = aso.FindObj("Border") == null;

                if (aso.FindObj("BS") == null && aso.FindObj("Border") == null)
                {
                    //bs.width = 0; //.SetWidth(0);
                } 
                if (bs.border_style == AnnotBorderStyleStyle.e_beveled
                        || bs.border_style == AnnotBorderStyleStyle.e_inset)
                {
                    bs.width *= 2;
                }
                mBorderWidth = bs.width * mPDFView.GetZoom();

                if (mTextBox != null)
                {
                    // compute alignment
                    FieldTextJustification just = mField.GetJustification();//  .GetJustification();
                    if (just == FieldTextJustification.e_left_justified)
                    {
                        mTextBox.TextAlignment = Windows.UI.Xaml.TextAlignment.Left;
                    }
                    else if (just == FieldTextJustification.e_centered)
                    {
                        mTextBox.TextAlignment = Windows.UI.Xaml.TextAlignment.Center;
                    }
                    else if (just == FieldTextJustification.e_right_justified)
                    {
                        mTextBox.TextAlignment = Windows.UI.Xaml.TextAlignment.Right;
                    }
                }

                // set initial text
                mOldText = mField.GetValueAsString();
                if (mTextBox != null)
                {
                    mTextBox.Text = mOldText;
                }
                if (mPasswordBox != null)
                {
                    mPasswordBox.Password = mOldText;
                }

                // comb and max length
                int maxLength = mField.GetMaxLen(); 
                if (maxLength >= 0)
                {
                    if (mTextBox != null)
                    {
                        mTextBox.MaxLength = maxLength;
                    }
                    if (mPasswordBox != null)
                    {
                        mPasswordBox.MaxLength = maxLength;
                    }
                }

            }
            catch (Exception)
            {
            }
        }

        protected void PositionElement(Windows.UI.Xaml.FrameworkElement target)
        {
            BuildAnnotBBox();
            Rect annotRect = ConvertFromPageRectToScreenRect(mAnnotBBox, mAnnotPageNum);
            UIRect pdfViewRect = GetElementRect(mPDFView);
            annotRect.Normalize();

            AnnotBorderStyle bs = mAnnot.GetBorderStyle();
            double width = bs.width;

            target.Width = annotRect.Width() - (2 * mBorderWidth);
            target.Height = annotRect.Height() - (2 * mBorderWidth);

            mTextPopup.Width = target.Width;
            mTextPopup.Height = target.Width;

            mTextPopup.HorizontalOffset = annotRect.x1 + pdfViewRect.Left + 2;// +mBorderWidth;
            mTextPopup.VerticalOffset = annotRect.y1 + pdfViewRect.Top + 2;// +mBorderWidth;
        }

        protected void SetFontSize()
        {
            mFontSize = 10 * mPDFView.GetZoom();

            try
            {
                double fontSize = 10 * (double)mPDFView.GetZoom();
                GState gs = mField.GetDefaultAppearance();
                if (gs != null)
                {
                    fontSize = (double)gs.GetFontSize();
                    if (fontSize <= 0)
                    {
                        //auto size
                        fontSize = (double)(mTextBox.Height / 2.5);
                    }
                    else
                    {
                        fontSize *= (double)mPDFView.GetZoom();
                    }
                }
                mFontSize = fontSize;
                //mTextBox.FontSize = fontSize;
            }
            catch (Exception)
            {
            }
            finally
            {
            }
        }

        protected void MapColorFont()
        {
            mTextBrush = new SolidColorBrush(Color.FromArgb(255, 50, 255, 150));
            mBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 180, 180, 120));

            try
            {
                GState gs = mField.GetDefaultAppearance();
                if (gs != null)
                {
                    //set text color
                    ColorPt color = new ColorPt();
                    ColorSpace cs = gs.GetFillColorSpace();
                    string s = cs.ToString();
                    ColorPt fc = gs.GetFillColor();
                    string s2 = fc.ToString();
                    cs.Convert2RGB(fc, color);
                    int r = (int)(color.Get(0) * 255 + 0.5);
                    int g = (int)(color.Get(1) * 255 + 0.5);
                    int b = (int)(color.Get(2) * 255 + 0.5);
                    Color textColor = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
                    mTextBrush = new SolidColorBrush(textColor);

                    //set background color
                    color = GetFieldBkColor();
                    if (color == null)
                    {
                        r = 255;
                        g = 255;
                        b = 255;
                        mBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, (byte)r, (byte)g, (byte)b));
                        if ((((double)textColor.R * 0.299) + ((double)textColor.G * 0.587) + ((double)textColor.B * 0.114)) > 186) // this is a bright color
                        {
                            mBackgroundBrush = new SolidColorBrush(Colors.DarkGray);
                        }
                    }
                    else
                    {
                        r = (int)(color.Get(0) * 255 + 0.5);
                        g = (int)(color.Get(1) * 255 + 0.5);
                        b = (int)(color.Get(2) * 255 + 0.5);
                        mBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, (byte)r, (byte)g, (byte)b));
                    }

                    //set the font of the EditBox to match the PDF form field's. in order to do this,
                    //you need to bundle with you App the fonts, such as "Times", "Arial", "Courier", "Helvetica", etc.
                    //the following is just a place holder.
                    Font font = gs.GetFont();
                    if (font != null && font.IsValid())
                    {
                        String name = font.GetName();
                        if (name == null || name.Length == 0)
                        {
                            name = "Times New Roman";
                        }
                        if (name.Contains("Times"))
                        {
                            //NOTE: you need to bundle the font file in you App and use it here.
                            //TypeFace tf == Typeface.create(...);
                            //mInlineEditBox.setTypeface(tf);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        protected ColorPt GetFieldBkColor()
        {
            try
            {
                Obj o = mAnnot.GetSDFObj().FindObj("MK");
                if (o != null)
                {
                    Obj bgc = o.FindObj("BG");
                    if (bgc != null && bgc.IsArray())
                    {
                        int sz = (int)bgc.Size();
                        switch (sz)
                        {
                            case 1:
                                Obj n = bgc.GetAt(0);
                                if (n.IsNumber())
                                {
                                    return new ColorPt(n.GetNumber(), n.GetNumber(), n.GetNumber());
                                }
                                break;
                            case 3:
                                Obj r = bgc.GetAt(0), g = bgc.GetAt(1), b = bgc.GetAt(2);
                                if (r.IsNumber() && g.IsNumber() && b.IsNumber())
                                {
                                    return new ColorPt(r.GetNumber(), g.GetNumber(), b.GetNumber());
                                }
                                break;
                            case 4:
                                Obj c = bgc.GetAt(0), m = bgc.GetAt(1), y = bgc.GetAt(2), k = bgc.GetAt(3);
                                if (c.IsNumber() && m.IsNumber() && y.IsNumber() && k.IsNumber())
                                {
                                    ColorPt cp = new ColorPt(c.GetNumber(), m.GetNumber(), y.GetNumber(), k.GetNumber());
                                    ColorPt cpout = new ColorPt();
                                    ColorSpace cs = ColorSpace.CreateDeviceCMYK();
                                    cs.Convert2RGB(cp, cpout);
                                    return cpout;
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private void SaveText(string newText = null)
        {

            string str = newText;
            if (str == null)
            {
                str = mTextBox.Text;
            }
            if (!str.Equals(mOldText, StringComparison.Ordinal))
            {
                try
                {
                    mPDFView.DocLock(true);



                    Obj fieldObj = mField.GetSDFObj();
                    Obj DAObj = fieldObj.FindObj("DA");
                    string appearanceString = null;
                    if (DAObj != null && DAObj.IsString())
                    {
                        appearanceString = DAObj.GetAsPDFText();
                        // parse and replace color
                        
                    }
                    else
                    {
                        appearanceString = "/Helv 12 Tf 0 0 0 rg";
                    }
                    fieldObj.PutString("DA", appearanceString);

                    mField.SetValue(str.Replace("\r\n", "\n"));

                    mField.RefreshAppearance();


                    mPDFView.Update(mField);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
            }
        }

        protected void SavePassword(string newText = null)
        {
            string str = newText;
            if (str == null)
            {
                str = mPasswordBox.Password;
            }
            if (!str.Equals(mOldText, StringComparison.Ordinal))
            {
                try
                {
                    mPDFView.DocLock(true);
                    mField.SetValue(str);
                    mField.RefreshAppearance();
                    mPDFView.Update(mField);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
            }
        }


        protected void HandleChoice()
        {
            // Get annotation bounding box
            BuildAnnotBBox();
            Rect annotRect = ConvertFromPageRectToScreenRect(mAnnotBBox, mAnnotPageNum);
            annotRect.Normalize();
            double sx = mPDFView.GetAnnotationCanvasHorizontalOffset();
            double sy = mPDFView.GetAnnotationCanvasVerticalOffset();
            annotRect.Set(annotRect.x1 + sx, annotRect.y1 + sy, annotRect.x2 + sx, annotRect.y2 + sy);

            // Set flags
            mIsMultiChoice = mField.GetFlag(FieldFlag.e_multiselect);
            mIsCombo = mField.GetFlag(FieldFlag.e_combo);

            // create the selection widget
            Border bg = new Border();
            bg.Width = 306;
            bg.Height = 406;
            bg.BorderThickness = new Windows.UI.Xaml.Thickness(3);
            bg.Background = this.Resources.ThemeDictionaries["ContentDialogBackgroundThemeBrush"] as Brush;
            bg.BorderBrush = new SolidColorBrush(Colors.Black);

            mSelectList = new ListView();
            mSelectList.Width = 292;
            mSelectList.Height = 392;
            bg.Child = mSelectList;

            List<string> optionList = GetOptionList();

            // Insert the options
            foreach (string str in optionList)
            {
                Border b = new Border();
                b.Width = mSelectList.Width;
                b.Height = 48;
                TextBlock tb = new TextBlock();
                tb.Foreground = this.Resources.ThemeDictionaries["ContentDialogContentForegroundBrush"] as Brush;
                tb.FontSize = 26;
                tb.Text = str;
                tb.Margin = new Thickness(5, 0, 5, 0);
                b.Child = tb;
                mSelectList.Items.Add(b);
            }

            // Set the ListView's selection mode
            if (mIsMultiChoice)
            {
                mSelectList.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                mSelectList.IsItemClickEnabled = true;
                mSelectList.ItemClick += (s, e) =>
                {
                    mSelectList.SelectedItem = e.ClickedItem;
                    SaveChoice();
                    mBoxPopup.Hide();
                    mBoxPopup = null;
                    mToolManager.CreateTool(ToolType.e_pan, this);
                };
            }

            // Get selected item(s)
            if (!mIsMultiChoice)
            {
                string selectedStr = mField.GetValueAsString();
                foreach (object o in mSelectList.Items)
                {
                    Border b =  o as Border;
                    TextBlock tb = b.Child as TextBlock;
                    string s = tb.Text;
                    if (s.Equals(selectedStr, StringComparison.Ordinal))
                    {
                        mSelectList.SelectedItem = o;
                    }
                }
            }
            else
            {
                List<int> selList = GetSelectedPositions();
                foreach (int i in selList)
                {
                    mSelectList.SelectedItems.Add(mSelectList.Items[i]);
                }
            }

            // Display options
            mBoxPopup = new FixedSizedBoxPopup(mPDFView, bg, true, false);

            mBoxPopup.Show(new UIRect(annotRect.x1 - mPDFView.GetAnnotationCanvasHorizontalOffset(), annotRect.y1 - mPDFView.GetAnnotationCanvasVerticalOffset(), annotRect.x2 - annotRect.x1, annotRect.y2 - annotRect.y1), !mToolManager.IsOnPhone);
            mToolManager.mCurrentBoxPopup = mBoxPopup;
            mBoxPopup.BoxPopupClosed += MBoxPopup_BoxPopupClosed;

            DisableScrolling();
        }

        private void MBoxPopup_BoxPopupClosed(object sender, object e)
        {
            mToolManager.mCurrentBoxPopup = null;
            if (mIsMultiChoice)
            {
                SaveChoice();
            }
            EndCurrentTool(ToolType.e_pan);
        }

        //populate list from the choice annotation
        protected List<string> GetOptionList()
        {
            try
            {
                List<string> retList = new List<string>();
                Obj obj = mAnnot.GetSDFObj().FindObj("Opt");
                if (obj != null && obj.IsArray())
                {
                    int sz = (int)obj.Size();
                    for (int i = 0; i < sz; ++i)
                    {
                        Obj o = obj.GetAt(i);
                        if (o != null)
                        {
                            if (o.IsString())
                            {
                                retList.Add(o.GetAsPDFText());
                            }
                            else if (o.IsArray() && o.Size() == 2)
                            {
                                Obj s = o.GetAt(1);
                                if (s != null && s.IsString())
                                {
                                    retList.Add(s.GetAsPDFText());
                                }
                            }
                        }
                    }
                    return retList;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        //find the selected items from a multiple choice list
        protected List<int> GetSelectedPositions()
        {
            try
            {
                List<int> retList = new List<int>();
                Obj val = mField.GetValue();
                if (val != null)
                {
                    if (val.IsString())
                    {
                        Obj o = mAnnot.GetSDFObj().FindObj("Opt");
                        if (o != null)
                        {
                            int id = GetOptIdx(val, o);
                            if (id >= 0)
                            {
                                retList.Add(id);
                            }
                        }
                    }
                    else if (val.IsArray())
                    {
                        int sz = (int)val.Size();
                        for (int i = 0; i < sz; ++i)
                        {
                            Obj entry = val.GetAt(i);
                            if (entry.IsString())
                            {
                                Obj o = mAnnot.GetSDFObj().FindObj("Opt");
                                if (o != null)
                                {
                                    int id = GetOptIdx(entry, o);
                                    if (id >= 0)
                                    {
                                        retList.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }
                return retList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Get index of options in multiple choice list
        protected int GetOptIdx(Obj str_val, Obj opt)
        {
            try
            {
                int sz = (int)opt.Size();
                string str_val_string = str_val.GetAsPDFText();
                for (int i = 0; i < sz; ++i)
                {
                    Obj v = opt.GetAt(i);
                    if (v.IsString() && str_val.Size() == v.Size())
                    {
                        string v_string = v.GetAsPDFText();
                        if (str_val_string.Equals(v_string))
                        {
                            return i;
                        }
                    }
                    else if (v.IsArray() && v.Size() >= 2 && v.GetAt(1).IsString() && str_val.Size() == v.GetAt(1).Size())
                    {
                        v = v.GetAt(1);
                        String v_string = v.GetAsPDFText();
                        if (str_val_string.Equals(v_string, StringComparison.Ordinal))
                        {
                            return i;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        
        // Save the selection
        public void SaveChoice()
        {
            if (mIsChoiceSaved)
            {
                return;
            }
            mIsChoiceSaved = true;
            if (mIsCombo)
            {
                try
                {
                    mPDFView.DocLock(true);
                    object o = mSelectList.SelectedItem;
                    Border b = o as Border;
                    TextBlock tb = b.Child as TextBlock;
                    string str = tb.Text;
                    if (mField.GetValueAsString() != str)
                    {
                        mField.SetValue(str);
                        mField.RefreshAppearance();
                        mPDFView.Update(mField);
                        mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
            }
            else // single and mutliple choice list
            {
                try
                {
                    mPDFView.DocLock(true);
                    PDFDoc doc = mPDFView.GetDoc();
                    Obj arr = doc.CreateIndirectArray();

                    // We need to add the items in the order that they appear, so we can't just take the SelectedItems directly
                    // Otherwise they will not appear
                    foreach (object o in mSelectList.Items)
                    {
                        if (mSelectList.SelectedItems.Contains(o))
                        {
                            Border b = o as Border;
                            TextBlock tb = b.Child as TextBlock;
                            arr.PushBackText(tb.Text);
                        }
                    }
                    mField.SetValue(arr);
                    mField.EraseAppearance();
                    mField.RefreshAppearance();
                    mPDFView.Update(mField);
                    mToolManager.RaiseAnnotationEditedEvent(mAnnot);
                }
                catch (Exception)
                {
                }
                finally
                {
                    mPDFView.DocUnlock();
                }
            }
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

            Windows.UI.Xaml.FrameworkElement element = sender as Windows.UI.Xaml.FrameworkElement;

            e.EnsuredFocusedElementInView = true;
            mKeyboardOpen = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(element);
            UIRect viewRect = GetElementRect(mPDFView);
            double bottomMargin = RemainingHeight - textRect.Bottom - 20;
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
                Windows.UI.Xaml.Media.Animation.Storyboard sbText = KeyboardTranslate(mTextBorder, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
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
                Windows.UI.Xaml.Media.Animation.Storyboard sbText = KeyboardTranslate(mTextBorder, -mKeyboardTranslateOffset, 0, TimeSpan.FromMilliseconds(367));
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

        internal override bool KeyDownAction(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (mBoxPopup != null || mTextBox != null)
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    mNextToolMode = ToolType.e_pan;
                }
                return true;
            }
            return base.KeyDownAction(sender, e);
        }

    }
}

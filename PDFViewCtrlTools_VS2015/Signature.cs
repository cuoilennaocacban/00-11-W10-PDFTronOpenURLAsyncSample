using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;


using pdftron.PDF;
using pdftron.SDF;

using PDFPoint = pdftron.PDF.Point;
using UIPoint = Windows.Foundation.Point;
using PDFRect = pdftron.PDF.Rect;
using UIRect = Windows.Foundation.Rect;



namespace pdftron.PDF.Tools
{
    class Signature : Tool
    {
        public enum SignatureFieldState
        {
            DigitallySigned,
            ImageAppearanceOnly,
            PathAppearanceOnly,
            Empty,
        }

        // Floating signature
        protected SignatureDialog mSignatureDialog;
        protected Utilities.StampManager mStampManager;

        protected UIPoint mTargetPoint;

        // Form filling signature
        protected Annots.Widget mWidget;
        protected SignatureFieldState mSignatureFieldState;
        protected Dictionary<string, string> mMenuTitles;
        protected FixedSizedBoxPopup mBoxPopup;
        protected int mPageNumber = -1;

        protected bool mShouldAddNote = false;

        public Signature(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_signature;
            mToolMode = ToolType.e_signature;
            mStampManager = new Utilities.StampManager();
        }

        public void SetTargetPoint(UIPoint point)
        {
            mTargetPoint = point;
            CreateMenuIfNeeded();
        }

        internal bool GoBack()
        {
            if (mSignatureDialog != null)
            {
                mSignatureDialog.Close();
                mPDFView.IsEnabled = true;
                mToolManager.CreateTool(ToolType.e_pan, this, false);
                return true;
            }
            return false;
        }

        internal override void OnCreate()
        {
            if (mAnnot != null)
            {

            }
        }

        internal override void OnClose()
        {
            if (mSignatureDialog != null)
            {
                mSignatureDialog.Close();
            }
            if (mBoxPopup != null)
            {
                mBoxPopup.Hide();
            }
            base.OnClose();
            EnableScrolling();
            mPDFView.RequestRendering();
        }
        
        protected async void CreateMenuIfNeeded()
        {

            bool hasSignature = await mStampManager.HasDefaultSignature();
            if (hasSignature) // has a signature
            {
                Dictionary<string, string> createSignatureOptionsDict = new Dictionary<string, string>();
                createSignatureOptionsDict["my signature"] = ResourceHandler.GetString("CreateOption_MySignature");
                createSignatureOptionsDict["new signature"] = ResourceHandler.GetString("CreateOption_NewSignature");
                mCommandMenu = new PopupCommandMenu(mPDFView, createSignatureOptionsDict, FloatingOnCommandMenuClicked);
                mCommandMenu.UseFadeAnimations(true);
                mCommandMenu.TargetPoint(mTargetPoint);
                mIsShowingCommandMenu = true;
                mCommandMenu.Show();
            }
            else // does not have a signature, go straight to menu
            {
                ShowSignatureDialog();
            }
        }



        internal async void FloatingOnCommandMenuClicked(string title)
        {
            mCommandMenu.Hide();
            mIsShowingCommandMenu = false;
            if (title.Equals("my signature", StringComparison.OrdinalIgnoreCase))
            {
                Page page = await mStampManager.GetDefaultSignature();
                AddStamp(page);
            }
            else if (title.Equals("new signature", StringComparison.OrdinalIgnoreCase))
            {
                ShowSignatureDialog();
            }
            else
            {
                throw new Exception("The option doesn't exist");
            }
        }

        protected async void ShowSignatureDialog()
        {
            mPDFView.IsEnabled = false;
            mSignatureDialog = new SignatureDialog();
            mSignatureDialog.SignatureDone += mSignatureDialog_SignatureDone;
            if (!await mStampManager.HasDefaultSignature())
            {
                mSignatureDialog.ShouldOverwriteOldSignature = true;
            }
            mPDFView.CancelRendering();
        }

        protected async void mSignatureDialog_SignatureDone(bool ShouldSign)
        {
            mPDFView.IsEnabled = true;
            try
            {
                if (ShouldSign)
                {
                    bool hasDefault = await mStampManager.HasDefaultSignature();
                    bool makeDefault = false;
                    bool clearSignature = false;
                    bool createSignature = true;
                    bool shouldClose = true;

                    // check if overwriting
                    if (mSignatureDialog.ShouldOverwriteOldSignature && hasDefault)
                    {
                        if (mSignatureDialog.Strokes.Count > 0)
                        {
                            Windows.UI.Popups.MessageDialog msg = new Windows.UI.Popups.MessageDialog(ResourceHandler.GetString("SignatureDialog_Overwrite_Info"), ResourceHandler.GetString("SignatureDialog_Overwrite_Title"));
                            msg.Commands.Add(new Windows.UI.Popups.UICommand(ResourceHandler.GetString("SignatureDialog_Overwrite_Yes"), (command) =>
                            {
                                makeDefault = true;
                            }));
                            msg.Commands.Add(new Windows.UI.Popups.UICommand(ResourceHandler.GetString("SignatureDialog_Overwrite_No"), (command) =>
                            {
                                shouldClose = false;
                                createSignature = false;
                            }));
                            await msg.ShowAsync();
                        }
                        else
                        {
                            createSignature = false;
                            Windows.UI.Popups.MessageDialog msg = new Windows.UI.Popups.MessageDialog(ResourceHandler.GetString("SignatureDialog_Delete_Info"), ResourceHandler.GetString("SignatureDialog_Delete_Title"));
                            msg.Commands.Add(new Windows.UI.Popups.UICommand(ResourceHandler.GetString("SignatureDialog_Delete_Yes"), (command) =>
                            {
                                clearSignature = true;
                            }));
                            msg.Commands.Add(new Windows.UI.Popups.UICommand(ResourceHandler.GetString("SignatureDialog_Delete_No"), (command) =>
                            {
                                shouldClose = false;
                            }));
                            await msg.ShowAsync();
                        }
                    }
                    else if (mSignatureDialog.ShouldOverwriteOldSignature)
                    {
                        makeDefault = true;
                        if (mSignatureDialog.Strokes.Count == 0)
                        {
                            createSignature = false;
                        }
                    }
                    else if (mSignatureDialog.Strokes.Count == 0)
                    {
                        createSignature = false;
                        shouldClose = true;
                    }

                    if (clearSignature)
                    {
                        try
                        {
                            await mStampManager.DeleteDefaultSignatureFile();
                        }
                        catch (Exception)
                        {
                            // Failed to delete default signature
                        }
                    }

                    if (createSignature)
                    {
                        try
                        {
                            pdftron.PDF.Page page = await mStampManager.CreateSignatureAsync(mSignatureDialog.Strokes, mSignatureDialog.StrokeColor, mSignatureDialog.LeftMost, mSignatureDialog.RightMost, mSignatureDialog.TopMost, mSignatureDialog.BottomMost, makeDefault, mSignatureDialog.MadeWithStylus);
                            AddStamp(page);
                        }
                        catch (Exception)
                        {
                            // failed to create signature
                        }
                    }
                    else if (shouldClose)
                    {
                        mToolManager.CreateTool(ToolType.e_pan, this);
                    }
                }
                else
                {
                    mToolManager.CreateTool(ToolType.e_pan, this);
                }
            }
            catch (Exception)
            {
                mToolManager.CreateTool(ToolType.e_pan, this);
            }
        }

        protected void AddStamp(Page stampPage)
        {
            int pageNumber = mPDFView.GetPageNumberFromScreenPoint(mTargetPoint.X, mTargetPoint.Y);
            if (pageNumber <= 0)
            {
                return;
            }

            try
            {
                mPDFView.DocLock(true);
                PDFDoc doc = mPDFView.GetDoc();
                Page page = doc.GetPage(pageNumber);

                PageRotate viewRotation = mPDFView.GetRotation();
                PageRotate pageRotation = page.GetRotation();

                // If the page itself is rotated, we want to "rotate" width and height as well
                PDFRect pageCropBox = page.GetCropBox();
                double pageWidth = pageCropBox.Width();
                if (pageRotation == PageRotate.e_90 || pageRotation == PageRotate.e_270)
                {
                    pageWidth = pageCropBox.Height();
                }
                double pageHeight = pageCropBox.Height();
                if (pageRotation == PageRotate.e_90 || pageRotation == PageRotate.e_270)
                {
                    pageHeight = pageCropBox.Width();
                }

                PDFRect stampRect = stampPage.GetCropBox();
                double maxWidth = 200;
                double maxHeight = 200;

                if (pageWidth < maxWidth)
                {
                    maxWidth = pageWidth;
                }
                if (pageHeight < maxHeight)
                {
                    maxHeight = pageHeight;
                }

                double stampWidth = stampRect.Width();
                double stampHeight = stampRect.Height();

                // if the viewer rotates pages, we want to treat it as if it's the stamp that's rotated
                if (viewRotation == PageRotate.e_90 || viewRotation == PageRotate.e_270)
                {
                    double temp = stampWidth;
                    stampWidth = stampHeight;
                    stampHeight = temp;
                }

                double scaleFactor = Math.Min(maxWidth / stampWidth, maxHeight / stampHeight);
                stampWidth *= scaleFactor;
                stampHeight *= scaleFactor;

                Stamper stamper = new Stamper(StamperSizeType.e_absolute_size, stampWidth, stampHeight);
                stamper.SetAlignment(StamperHorizontalAlignment.e_horizontal_left, StamperVerticalAlignment.e_vertical_bottom);
                stamper.SetAsAnnotation(true);

                pdftron.Common.DoubleRef x = new Common.DoubleRef(mTargetPoint.X);
                pdftron.Common.DoubleRef y = new Common.DoubleRef(mTargetPoint.Y);
                mPDFView.ConvScreenPtToPagePt(x, y, pageNumber);

                pdftron.Common.Matrix2D mtx = page.GetDefaultMatrix(); // This matrix takes into account page rotation and crop box
                mtx.Mult(x, y);

                double xPos = x.Value - (stampWidth / 2);
                double yPos = y.Value - (stampHeight / 2);

                if (xPos > pageWidth - stampWidth)
                {
                    xPos = pageWidth - stampWidth;
                }
                if (xPos < 0)
                {
                    xPos = 0;
                }
                if (yPos > pageHeight - stampHeight)
                {
                    yPos = pageHeight - stampHeight;
                }
                if (yPos < 0)
                {
                    yPos = 0;
                }
                    
                stamper.SetPosition(xPos, yPos);

                int stampRotation = (4 - (int)viewRotation) % 4; // 0 = 0, 90 = 1; 180 = 2, and 270 = 3
                stamper.SetRotation(stampRotation * 90.0);

                stamper.StampPage(doc, stampPage, new PageSet(pageNumber));
                    
                int numAnnots = page.GetNumAnnots();
                    
                IAnnot annot = page.GetAnnot(numAnnots - 1);
                pdftron.SDF.Obj obj = annot.GetSDFObj();
                obj.PutString(ToolManager.SignatureAnnotationIdentifyingString, "");
                AnnotType theType = annot.GetAnnotType();
                Annots.RubberStamp stamp = (Annots.RubberStamp)annot;

                mPDFView.UpdateWithAnnot(annot, pageNumber);
                mToolManager.RaiseAnnotationAddedEvent(mAnnot);
                    
                // Set up to transfer to AnnotEditTool
                mAnnot = annot;
                BuildAnnotBBox();
                mAnnotPageNum = pageNumber;

                mNextToolMode = ToolType.e_annot_edit;
            }
            catch (Exception)
            {
                mNextToolMode = ToolType.e_pan;
            }
            finally
            {
                mPDFView.DocUnlock();
            }

            mToolManager.CreateTool(mNextToolMode, this);
            if (mNextToolMode == ToolType.e_annot_edit)
            {
                AnnotEdit annotEdit = mToolManager.CurrentTool as AnnotEdit;
                annotEdit.CreateAppearance();
            }

        }

        #region Event Handlers

        internal override bool PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            mShouldAddNote = false;
            AddPointer(e.Pointer);
            if (mToolManager.ContactPoints.Count > 1)
            {
                mToolManager.EnableScrollByManipulation = true;
                return false;
            }


            UIPoint sc_DownPoint = e.GetCurrentPoint(mPDFView).Position;
            int downPageNumber = mPDFView.GetPageNumberFromScreenPoint(sc_DownPoint.X, sc_DownPoint.Y);
            if (downPageNumber < 1)
            {
                if (mIsShowingCommandMenu)
                {
                    mIsShowingCommandMenu = false;
                    mCommandMenu.Hide();
                }
                mToolManager.EnableOneFingerScroll = true;
                mToolManager.EnableScrollByManipulation = true;
                return false;
            }

            if (mIsShowingCommandMenu) 
            {
                EndCurrentTool(ToolType.e_pan);
            }
            else // this means we haven't gotten a point yet, we should do so now
            {
                DisableScrolling();
            }
            mShouldAddNote = true;
            
            return false;
        }


        internal override bool PointerReleasedHandler(object sender, PointerRoutedEventArgs e)
        {
            RemovePointer(e.Pointer);
            if (mToolManager.ContactPoints.Count == 0 && mShouldAddNote)
            {
                UIPoint upPoint = e.GetCurrentPoint(mPDFView).Position;
                int pageNumber = mPDFView.GetPageNumberFromScreenPoint(upPoint.X, upPoint.Y);
                if (pageNumber > 0)
                {
                    SetTargetPoint(upPoint);
                }
                else
                {
                    EndCurrentTool(ToolType.e_pan);
                }
            }
            return false;
        }

        
        #endregion Event Handlers

        //////////////////////////////////////////////////////////////////////////
        // Form signature
        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mAnnot != null)
            {
                if (mJustSwitchedFromAnotherTool)
                {
                    mJustSwitchedFromAnotherTool = false;

                    // We know we're looking at a form widget with a digital signature
                    mPDFView.DocLockRead();
                    mWidget = (pdftron.PDF.Annots.Widget)mAnnot;
                    mPDFView.DocUnlockRead();

                    UIPoint screenPoint = e.GetPosition(mPDFView);
                    mPageNumber = mPDFView.GetPageNumberFromScreenPoint(screenPoint.X, screenPoint.Y);

                    GetCommandMenuOptions();
                    if (mSignatureFieldState == SignatureFieldState.Empty)
                    {
                        ShowWidgetSignatureDialog();
                    }
                    else if (mSignatureFieldState == SignatureFieldState.DigitallySigned)
                    {
                        ShowSignatureInformation();
                    }
                    else if (mToolManager.IsPopupMenuEnabled)
                    {
                        CreateCommandMenu();
                    }
                }
                else
                {
                    mNextToolMode = ToolType.e_pan;
                }
            }

            return true;
        }

        protected async void ShowWidgetSignatureDialog()
        {
            //mToolManager.RaiseFullScreenToolShowingChangedEvent(true);
            mPDFView.IsEnabled = false;
            mSignatureDialog = new SignatureDialog();
            mSignatureDialog.SignatureDone += mSignatureDialog_WidgetSignatureDone;
            mSignatureDialog.ShowOptionToOverwriteSignature = false;
            if (await mStampManager.HasDefaultSignature())
            {
                mSignatureDialog.ShowOptionToUseDefaultSignature = true;
                mSignatureDialog.UseDefaultSelected += mSignatureDialog_UseDefaultSelected;
            }
            mPDFView.CancelRendering();
        }

        protected async void mSignatureDialog_WidgetSignatureDone(bool ShouldSign)
        {
            //mToolManager.RaiseFullScreenToolShowingChangedEvent(false);
            mPDFView.IsEnabled = true;
            if (ShouldSign)
            {
                try
                {
                    pdftron.PDF.Page page = await Task.Run(async () =>
                    {
                        return await mStampManager.CreateSignatureAsync(mSignatureDialog.Strokes, mSignatureDialog.StrokeColor, mSignatureDialog.LeftMost,
                            mSignatureDialog.RightMost, mSignatureDialog.TopMost, mSignatureDialog.BottomMost, false, mSignatureDialog.MadeWithStylus);
                    });
                    await CreateTemporaryImageAsync(page);
                    mPDFView.UpdateWithAnnot(mWidget, mPageNumber);
                    mToolManager.RaiseAnnotationEditedEvent(mWidget);
                    mToolManager.CreateTool(ToolType.e_pan, null);
                }
                catch (Exception)
                {
                    // do nothing
                }

            }
            else
            {
                mToolManager.CreateTool(ToolType.e_pan, null);
            }
        }


        protected async void mSignatureDialog_UseDefaultSelected()
        {
            //mToolManager.RaiseFullScreenToolShowingChangedEvent(false);
            mPDFView.IsEnabled = true;
            try
            {
                pdftron.PDF.Page page = await mStampManager.GetDefaultSignature();
                await CreateTemporaryImageAsync(page);
                mPDFView.UpdateWithAnnot(mAnnot, mAnnotPageNum);
                mToolManager.RaiseAnnotationAddedEvent(mAnnot);
            }
            catch (Exception)
            {

            }
            finally
            {
                mToolManager.CreateTool(ToolType.e_pan, this);
            }
        }

        protected async Task CreateTemporaryImageAsync(Page page)
        {
            PDFDraw drawer = new PDFDraw();

            // First, we need to save the document to the apps sandbox.
            string tempName = "SignatureTempFile.png";
            string fullFileName = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, tempName);

            PDFRect cropBox = page.GetCropBox();
            int width = (int)cropBox.Width();
            int height = (int)cropBox.Height();
            drawer.SetImageSize(width, height, true, false);
            drawer.SetPageTransparent(true);

            drawer.Export(page, fullFileName, "png");
            SetImageAsAppearance(fullFileName, width, height);

            Windows.Storage.IStorageItem imageFile = null;
            try
            {
                imageFile = await Windows.Storage.ApplicationData.Current.TemporaryFolder.GetItemAsync(tempName);
            }
            catch (System.Exception)
            {
            }
            if (imageFile != null)
            {
                try
                {
                    await imageFile.DeleteAsync();
                }
                catch (Exception)
                {

                }
            }
        }


        protected void SetImageAsAppearance(string fileName, int width, int hegiht)
        {
            try
            {
                mPDFView.DocLock(true);

                PDFDoc doc = mPDFView.GetDoc();

                // Add the signature appearance
                ElementWriter apWriter = new ElementWriter();
                ElementBuilder apBuilder = new ElementBuilder();
                apWriter.Begin(doc.GetSDFDoc());

                Image sigImg = Image.Create(doc.GetSDFDoc(), fileName);


                // Scaling the image to fit in the widget, centered and with preserved aspect ratio, is quite complicated.
                // Creating an image with a matrix with scale factors of pixel withd and pixel high in the horizotal and vertical
                // directions, respectively, will create an image that fills the widget.
                PDFRect widgetRect = mWidget.GetRect();

                // We need the width to height ratio of both the widget and the image
                double formRatio = widgetRect.Width() / widgetRect.Height();
                double imageRatio = width / ((double)hegiht);

                double widthAdjust = 1.0;
                double heightAdjust = 1.0;

                // If the form has a higher width to height ratio than the image, that means the image can scale further width-wise
                // We therefore have to limit the scaling in that direction by the ratio of the ratios...
                if (imageRatio < formRatio)
                {
                    widthAdjust = imageRatio / formRatio;
                }
                else if (imageRatio > formRatio)
                {
                    // If the form has a higher height to width ratio than the image, that means the image can scale further height-wise
                    // So in this case we limit the scaling of the height in the same way.
                    heightAdjust = formRatio / imageRatio;
                }

                // Now, we want to calculate the horizontal or vertical translation (we should only need one of them).
                // The image will be scaled by the smallest of the the rations between the widgets and images width or height
                double horzTranslate = 0.0;
                double vertTranslate = 0.0;
                double widthRatio = widgetRect.Width() / width; // the scale needed to fit width
                double heightRatio = widgetRect.Height() / hegiht; // the scale needed to fit height

                double scale2 = Math.Min(widthRatio, heightRatio); // we pick the smallest of them as our scale factor

                // We calculate the scaling in page space, which is half of the width or height difference between the widget and scaled image.
                horzTranslate = (widgetRect.Width() - (width * scale2)) / 2;
                vertTranslate = (widgetRect.Height() - (hegiht * scale2)) / 2;

                // The widget will scale width and height internally, so we need to add a transformation matrix to the image to make it show up in the right position.
                // If you use the identity matrix, the image won't show up at all. We also need to adjust the scaling of the image with the ratio from before.
                // Finally, the translation needs to happen in the space of the widget, as opposed to page space. Therefore, we need to remove the scaling factor, but keep
                // the width or height adjustment.

                // Conceptually, assume you have a square image, and a widget that is 3 times wider than it is hight.
                // The image is then scaled by its width and height, and then again by the widget that will scale it to it's width and height. So, the image will now be 3
                // times as wide as it is high. Therefore, what width adjust does it will change the initial scaling of the width to be 1 3rd of the image's width, 
                // so that with the scaling from the widget, the total width scaling is the same as the height scaling. Similarly, the translation needs to operate in the
                // widget's scaled space.
                pdftron.Common.Matrix2D mtx = new pdftron.Common.Matrix2D(width * widthAdjust, 0, 0, hegiht * heightAdjust, horzTranslate * widthAdjust / scale2, vertTranslate * heightAdjust / scale2);

                Element apElement = apBuilder.CreateImage(sigImg, mtx);
                apWriter.WritePlacedElement(apElement);

                Obj apObj = apWriter.End();
                apObj.PutRect("BBox", 0, 0, width, hegiht);
                apObj.PutName("Subtype", "Form");
                apObj.PutName("Type", "XObject");

                ElementWriter apWriter2 = new ElementWriter();
                apWriter2.Begin(doc.GetSDFDoc());
                apElement = apBuilder.CreateForm(apObj);

                apWriter2.WritePlacedElement(apElement);
                apObj = apWriter2.End();

                apObj.PutRect("BBox", 0, 0, width, hegiht);
                apObj.PutName("Subtype", "Form");
                apObj.PutName("Type", "XObject");

                mWidget.SetAppearance(apObj);
                mWidget.RefreshAppearance();


            }
            catch (Exception)
            {

            }
            finally
            {
                mPDFView.DocUnlock();
            }
        }


        //////////////////////////////////////////////////////////////////////////
        // Command menu
        protected void GetCommandMenuOptions()
        {
            GetSignatureState();
            // in case an old one is somehow up
            if (mIsShowingCommandMenu)
            {
                mIsShowingCommandMenu = false;
                mCommandMenu.Hide();
            }

            mMenuTitles = new Dictionary<string, string>();
            if (mSignatureFieldState == SignatureFieldState.ImageAppearanceOnly || mSignatureFieldState == SignatureFieldState.PathAppearanceOnly)
            {
                mMenuTitles["delete"] = ResourceHandler.GetString("ExistingSignatureOptions_Delete");
            }
        }

        protected void CreateCommandMenu()
        {
            mCommandMenu = new PopupCommandMenu(mPDFView, mMenuTitles, WidgetOnCommandMenuClicked);
            mCommandMenu.UseFadeAnimations(true);
            PositionMenu();
            ShowMenu();
        }

        internal void WidgetOnCommandMenuClicked(string title)
        {
            if (title.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                HideMenu();
                try
                {
                    mPDFView.DocLock(true);
                    mWidget.GetSDFObj().Erase("AP");
                    mWidget.RefreshAppearance();
                }
                catch (System.Exception) { }
                finally
                {
                    mPDFView.DocUnlock();
                }

                mPDFView.UpdateWithAnnot(mWidget, mAnnotPageNum);
                mToolManager.RaiseAnnotationRemovedEvent(mWidget);
                mToolManager.CreateTool(ToolType.e_pan, this);
            }
        }


        protected void GetSignatureState()
        {
            mSignatureFieldState = SignatureFieldState.Empty;
            try
            {
                mPDFView.DocLockRead();

                if (mWidget.GetField().GetValue() != null)
                {
                    mSignatureFieldState = SignatureFieldState.DigitallySigned;
                }
                else
                {

                    ElementReader reader = new ElementReader();
                    Obj app = mWidget.GetAppearance();
                    Element element = GetFirstElementOfType(reader, app, ElementType.e_form);
                    if (element != null)
                    {
                        // XObject
                        Obj o = element.GetXObject();
                        ElementReader objReader = new ElementReader();
                        objReader.Begin(o);
                        for (Element el = objReader.Next(); el != null; el = objReader.Next())
                        {
                            if (el.GetType() == ElementType.e_path)
                            {
                                mSignatureFieldState = SignatureFieldState.PathAppearanceOnly;
                                break;
                            }
                            if (el.GetType() == ElementType.e_image)
                            {
                                mSignatureFieldState = SignatureFieldState.ImageAppearanceOnly;
                                break;
                            }
                        }
                        objReader.End();
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

        //////////////////////////////////////////////////////////////////////////
        // Information for already signed document
        protected void ShowSignatureInformation()
        {
            if (mWidget != null)
            {
                try
                {
                    Obj sigDict = mWidget.GetField().GetValue();
                    if (sigDict != null)
                    {
                        String location = "";
                        Obj val = sigDict.FindObj("Location");
                        if (val != null)
                        {
                            location = val.GetAsPDFText();
                        }
                        String reason = "";
                        val = sigDict.FindObj("Reason");
                        if (val != null)
                        {
                            reason = val.GetAsPDFText();
                        }
                        String name = "";
                        val = sigDict.FindObj("Name");
                        if (val != null)
                        {
                            name = val.GetAsPDFText();
                        }

                        PDFRect rect = mWidget.GetRect();
                        rect = ConvertFromPageRectToScreenRect(rect, mAnnotPageNum);
                        rect.Normalize();

                        mBoxPopup = new FixedSizedBoxPopup(mPDFView, CreateInformationView(location, reason, name));
                        mBoxPopup.Show(new UIRect(rect.x1, rect.y1, rect.Width(), rect.Height()));
                    }
                }
                catch (System.Exception) { }
            }
        }

        protected Windows.UI.Xaml.FrameworkElement CreateInformationView(string location, string reason, string name)
        {
            Grid mainGrid = new Grid();
            mainGrid.Width = 300;
            mainGrid.Height = 350;
            mainGrid.Background = new SolidColorBrush(Colors.Black);

            RowDefinition row0 = new RowDefinition();
            row0.Height = new Windows.UI.Xaml.GridLength(1, GridUnitType.Auto);
            RowDefinition row1 = new RowDefinition();
            row1.Height = new Windows.UI.Xaml.GridLength(1, GridUnitType.Auto);
            RowDefinition row2 = new RowDefinition();
            row2.Height = new Windows.UI.Xaml.GridLength(1, GridUnitType.Star);
            RowDefinition row3 = new RowDefinition();
            row3.Height = new Windows.UI.Xaml.GridLength(1, GridUnitType.Auto);

            mainGrid.RowDefinitions.Add(row0);
            mainGrid.RowDefinitions.Add(row1);
            mainGrid.RowDefinitions.Add(row2);
            mainGrid.RowDefinitions.Add(row3);

            TextBlock headerText = new TextBlock();
            headerText.FontSize = 34;
            headerText.Text = "Signature Info";
            headerText.Margin = new Thickness(15);
            mainGrid.Children.Add(headerText);

            TextBlock infoText = new TextBlock();
            infoText.SetValue(Grid.RowProperty, 1);
            infoText.Margin = new Windows.UI.Xaml.Thickness(15, 10, 10, 10);
            infoText.FontSize = 20;
            infoText.TextWrapping = TextWrapping.Wrap;
            infoText.Text = "This field is already signed. Below is some information about the signature entry:";
            mainGrid.Children.Add(infoText);

            Grid infoGrid = new Grid();
            infoGrid.SetValue(Grid.RowProperty, 2);
            infoGrid.Margin = new Windows.UI.Xaml.Thickness(12, 7, 7, 7);
            mainGrid.Children.Add(infoGrid);

            row0 = new RowDefinition();
            row0.Height = new Windows.UI.Xaml.GridLength(0, GridUnitType.Auto);
            row1 = new RowDefinition();
            row1.Height = new Windows.UI.Xaml.GridLength(0, GridUnitType.Auto);
            row2 = new RowDefinition();
            row2.Height = new Windows.UI.Xaml.GridLength(0, GridUnitType.Auto);

            infoGrid.RowDefinitions.Add(row0);
            infoGrid.RowDefinitions.Add(row1);
            infoGrid.RowDefinitions.Add(row2);

            ColumnDefinition col0 = new ColumnDefinition();
            col0.Width = new Windows.UI.Xaml.GridLength(1, GridUnitType.Auto);
            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = new Windows.UI.Xaml.GridLength(1, GridUnitType.Auto);

            infoGrid.ColumnDefinitions.Add(col0);
            infoGrid.ColumnDefinitions.Add(col1);

            TextBlock label = new TextBlock();
            label.Margin = new Windows.UI.Xaml.Thickness(3);
            label.FontSize = 16;
            label.Text = "Location:";
            infoGrid.Children.Add(label);

            label = new TextBlock();
            label.Margin = new Windows.UI.Xaml.Thickness(3);
            label.SetValue(Grid.RowProperty, 1);
            label.FontSize = 16;
            label.Text = "Reason:";
            infoGrid.Children.Add(label);

            label = new TextBlock();
            label.Margin = new Windows.UI.Xaml.Thickness(3);
            label.SetValue(Grid.RowProperty, 2);
            label.FontSize = 16;
            label.Text = "Name:";
            infoGrid.Children.Add(label);

            TextBlock infoValue = new TextBlock();
            infoValue.Margin = new Windows.UI.Xaml.Thickness(3);
            infoValue.SetValue(Grid.ColumnProperty, 1);
            infoValue.FontSize = 16;
            infoValue.Text = location;
            infoGrid.Children.Add(infoValue);

            infoValue = new TextBlock();
            infoValue.Margin = new Windows.UI.Xaml.Thickness(3);
            infoValue.SetValue(Grid.RowProperty, 1);
            infoValue.SetValue(Grid.ColumnProperty, 1);
            infoValue.FontSize = 16;
            infoValue.Text = reason;
            infoGrid.Children.Add(infoValue);

            infoValue = new TextBlock();
            infoValue.Margin = new Windows.UI.Xaml.Thickness(3);
            infoValue.SetValue(Grid.RowProperty, 2);
            infoValue.SetValue(Grid.ColumnProperty, 1);
            infoValue.FontSize = 16;
            infoValue.Text = name;
            infoGrid.Children.Add(infoValue);

            Button okayButton = new Button();
            okayButton.SetValue(Grid.RowProperty, 3);
            okayButton.HorizontalAlignment = HorizontalAlignment.Right;
            okayButton.Margin = new Windows.UI.Xaml.Thickness(5);
            okayButton.FontSize = 16;
            okayButton.Content = "OK";
            mainGrid.Children.Add(okayButton);

            okayButton.Click += (s, e) =>
            {
                mBoxPopup.Hide();
                mBoxPopup = null;
                mToolManager.CreateTool(ToolType.e_pan, this);
            };

            return mainGrid;
        }

        //////////////////////////////////////////////////////////////////////////
        // Utility Functions
        protected void ShowMenu()
        {
            if (mCommandMenu != null && !mIsShowingCommandMenu)
            {
                mIsShowingCommandMenu = true;
                mCommandMenu.Show();
                DisableScrolling();
            }
        }

        protected void HideMenu()
        {
            if (mCommandMenu != null && mIsShowingCommandMenu)
            {
                mIsShowingCommandMenu = false;
                mCommandMenu.Hide();
            }
        }
        protected virtual void PositionMenu()
        {
            if (mCommandMenu != null)
            {
                PDFRect rect = mWidget.GetRect();
                rect = ConvertFromPageRectToScreenRect(rect, mAnnotPageNum);
                mCommandMenu.TargetSquare(rect.x1, rect.y1, rect.x2, rect.y2);
            }
        }

        protected Element GetFirstElementOfType(ElementReader reader, Obj obj, ElementType elementType)
        {
            try
            {
                mPDFView.DocLockRead();
                if (obj != null)
                {
                    reader.Begin(obj);
                    for (Element element = reader.Next(); element != null; element = reader.Next())
                    {
                        if (element.GetType() == elementType)
                        {
                            reader.End();
                            return element;
                        }
                    }
                }
            }
            catch (System.Exception)
            { }
            finally
            {
                mPDFView.DocUnlockRead();
            }

            return null;
        }

    }
}

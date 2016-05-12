using pdftron.PDF.Annots;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using UIPopup = Windows.UI.Xaml.Controls.Primitives.Popup;
using UIRect = Windows.Foundation.Rect;
using PDFRect = pdftron.PDF.Rect;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Utilities
{
    public sealed partial class NoteDialogWindows : NoteDialogBase
    {
        FixedSizedBoxPopup _Popup;
        SizeChangedEventHandler _SizeHandler;

        private bool _DeleteOnClose = false;
        private bool _AnnotWasDeleted = false;

        // keyboard
        private double mKeyboardTranslateOffset = 0;
        private KeyboardHelper mKeyboardHelper;

        public NoteDialogWindows()
        {
            this.InitializeComponent();

        }
        public NoteDialogWindows(IMarkup annotation, PDFViewCtrl ctrl, ToolManager toolManager, 
            int annotPageNumber, bool deleteOnClose = false)
            : base(annotation, ctrl, toolManager, annotPageNumber)
        {
            this.InitializeComponent();
            this.Loaded += NoteDialog_Loaded;
            _SizeHandler = new SizeChangedEventHandler(_PDFViewCtrl_SizeChanged);
            _PDFViewCtrl.SizeChanged += _SizeHandler;
            MainTextBox.Text = GetTextFromAnnot();
            MainTextBox.SelectionStart = MainTextBox.Text.Length;
            CreatePopup();
            _ToolManager.mCurrentNoteDialog = this;

            _DeleteOnClose = deleteOnClose;

            if (annotation.GetAnnotType() == AnnotType.e_Text)
            {
                CancelButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                DeleteButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }

            MainTextBox.KeyDown += MainTextBox_KeyDown;

            mKeyboardHelper = new KeyboardHelper();
            mKeyboardHelper.SubscribeToKeyboard(true);

            mKeyboardHelper.AddShowingHandler(MainTextBox, new InputPaneShowingHandler(CustomKeyboardHandler));
            mKeyboardHelper.SetHidingHandler(new InputPaneHidingHandler(NoteInputPaneHiding));
        }

        void MainTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (_DeleteOnClose)
                {
                    DeleteAnnot();
                    _AnnotWasDeleted = true;
                }
                Close();
            }
        }

        public override void Close()
        {
            if (_SizeHandler != null)
            {
                _PDFViewCtrl.SizeChanged -= _SizeHandler;
            }
            _Popup.Hide();
            _ToolManager.mCurrentNoteDialog = null;

            mKeyboardHelper.RemoveShowingHandler(MainTextBox);
            mKeyboardHelper.SetHidingHandler(null);
            mKeyboardHelper.SubscribeToKeyboard(false);

            RaiseNoteClosedEvent(_AnnotWasDeleted);
            base.Close();
        }

        private void CreatePopup()
        {

            _Popup = new FixedSizedBoxPopup(_PDFViewCtrl, this);
            _Popup.Show(GetPopupPosition());
        }

        private UIRect GetPopupPosition()
        {
            PDFRect rect = _Annotation.GetRect();
            int pageNumber = _PDFViewCtrl.GetCurrentPage();

            Page page = _Annotation.GetPage();
            if (page != null)
            {
                pageNumber = page.GetIndex();
            }
            rect = _ToolManager.CurrentTool.ConvertFromPageRectToScreenRect(rect, pageNumber);
            rect.Normalize();
            return new UIRect(rect.x1, rect.y1, rect.Width(), rect.Height());
        }

        void NoteDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (FocusWhenOpened)
            {
                MainTextBox.Focus(FocusState.Programmatic);
            }
        }

        void _PDFViewCtrl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _Popup.Position(GetPopupPosition());
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SetAnnotText(MainTextBox.Text);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteAnnot();
            _AnnotWasDeleted = true;
            Close();
        }

        /// <summary>
        /// Translates the typing area if necessary in order to keep the text area above virtual keyboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CustomKeyboardHandler(object sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            double RemainingHeight = e.OccludedRect.Y; // Y value of keyboard top (thus, also height of rest).
            double TotalHeight = _PDFViewCtrl.ActualHeight;
            double KeyboardHeight = e.OccludedRect.Height; // height of keyboard

            e.EnsuredFocusedElementInView = true;

            // figure out how much of the text box will be covered.
            UIRect textRect = GetElementRect(MainTextBox);
            UIRect viewRect = GetElementRect(_PDFViewCtrl);
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
                Windows.UI.Xaml.Media.Animation.Storyboard sb = Tool.KeyboardTranslate(this, 0, -mKeyboardTranslateOffset, TimeSpan.FromMilliseconds(733));
                sb.Begin();
            }
        }

        /// <summary>
        /// Translates the PDFViewCtrl back to offset 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NoteInputPaneHiding(Windows.UI.ViewManagement.InputPane sender, Windows.UI.ViewManagement.InputPaneVisibilityEventArgs e)
        {
            if (mKeyboardTranslateOffset != 0)
            {
                Windows.UI.Xaml.Media.Animation.Storyboard sb = Tool.KeyboardTranslate(this, -mKeyboardTranslateOffset,
                    0, TimeSpan.FromMilliseconds(367));
                sb.Begin();
            }
        }

        private UIRect GetElementRect(Windows.UI.Xaml.FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }
    }
}

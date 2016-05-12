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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Utilities
{
    public sealed partial class NoteDialogPhone : NoteDialogBase
    {
        private bool _DeleteOnClose = false;
        private bool _AnnotWasDeleted = false;

        public enum InputMode
        {
            SingleLineText,
            MultiLineText,
            Password,
        }

        private InputMode _TextInputMode = InputMode.MultiLineText;

        public InputMode TextInputMode
        {
            get { return _TextInputMode; }
            set
            {
                _TextInputMode = value;
                switch (value)
                {
                    case InputMode.MultiLineText:
                        MainTextBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        SingleLineTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        PassWordBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        break;
                    case InputMode.SingleLineText:
                        MainTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        SingleLineTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        PassWordBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        break;
                    case InputMode.Password:
                        MainTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        SingleLineTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        PassWordBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        break;
                }
            }
        }



        // These variables lets us use the NoteDialog for FreeText annotations
        private bool _IsTextBoxHelper = false;
        /// <summary>
        /// The text in the user has filled in.
        /// </summary>
        public string AnnotText 
        { 
            get 
            { 
                switch (TextInputMode)
                {
                    case InputMode.MultiLineText:
                        return MainTextBox.Text;
                    case InputMode.SingleLineText:
                        return SingleLineTextBlock.Text;
                    case InputMode.Password:
                        return PassWordBox.Password;
                }
                return string.Empty;
            } 
            set
            {
                if (value != null)
                {
                    MainTextBox.Text = value;
                    SingleLineTextBlock.Text = value;
                    PassWordBox.Password = value;

                    switch (TextInputMode)
                    {
                        case InputMode.MultiLineText:
                            MainTextBox.Focus(FocusState.Programmatic);
                            MainTextBox.SelectionStart = value.Length;
                            break;
                        case InputMode.SingleLineText:
                            SingleLineTextBlock.Focus(FocusState.Programmatic);
                            SingleLineTextBlock.SelectionStart = value.Length;
                            break;
                        case InputMode.Password:
                            PassWordBox.Focus(FocusState.Programmatic);
                            break;
                    }
                }
            }
        }

        UIPopup _Popup;
        SizeChangedEventHandler _SizeHandler;

        public NoteDialogPhone()
        {
            this.InitializeComponent();

            TextInputMode = _TextInputMode;
        }


        public NoteDialogPhone(IMarkup annotation, PDFViewCtrl ctrl, ToolManager toolManager, int annotPageNumber, bool deleteOnClose = false)
            : base(annotation, ctrl, toolManager, annotPageNumber)
        {
            this.InitializeComponent();

            TextInputMode = _TextInputMode;
            _DeleteOnClose = deleteOnClose;

            CreatePopup();
            _PDFViewCtrl.IsEnabled = false;
            _SizeHandler = new SizeChangedEventHandler(_PDFViewCtrl_SizeChanged);
            _PDFViewCtrl.SizeChanged += _SizeHandler;
            this.Loaded += NoteDialog_Loaded;
            _ToolManager.mCurrentNoteDialog = this;

            
            if (annotation != null && annotation.GetAnnotType() == AnnotType.e_Text)
            {
                CancelButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                DeleteButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                CancelButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                DeleteButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        public static NoteDialogPhone CreateAsTextBoxHelper(PDFViewCtrl ctrl, ToolManager toolManager, int annotPageNumber)
        {
            NoteDialogPhone noteDialog = new NoteDialogPhone(null, ctrl, toolManager, annotPageNumber);
            noteDialog._IsTextBoxHelper = true;
            return noteDialog;
        }

        public override void CancelAndClose()
        {
            if (DeleteButton.Visibility == Windows.UI.Xaml.Visibility.Visible && _DeleteOnClose)
            {
                DeleteAnnot();
            }
            _AnnotWasDeleted = true;
            base.CancelAndClose();
        }

        public override void Close()
        {
            if (_SizeHandler != null)
            {
                _PDFViewCtrl.SizeChanged -= _SizeHandler;
            }
            _PDFViewCtrl.IsEnabled = true;
            _ToolManager.mCurrentNoteDialog = null;
            _Popup.IsOpen = false;

            RaiseNoteClosedEvent(_AnnotWasDeleted);
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
            PositionPopup();
        }

        private void CreatePopup()
        {
            _Popup = new UIPopup();
            _Popup.Child = this;
            _Popup.IsOpen = true;
            PositionPopup();

            MainTextBox.Text = GetTextFromAnnot();
            MainTextBox.SelectionStart = MainTextBox.Text.Length;
        }

        void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            PositionPopup();
        }

        private void PositionPopup()
        {
            UIRect rect = UtilityFunctions.GetElementRect(_PDFViewCtrl);

            _Popup.HorizontalOffset = rect.X;
            //_Popup.VerticalOffset = rect.Y;

            MainGrid.Width = rect.Width;
            if (rect.Width > rect.Height) // landscape mode
            {
                VisualStateManager.GoToState(this, "WideLayout", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "TallLAyout", true);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_IsTextBoxHelper)
            {
                SetAnnotText(MainTextBox.Text);
            }
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _AnnotWasDeleted = _IsTextBoxHelper; // delete annot if we're a textbox helper
            Close();
        }


        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteAnnot();
            _AnnotWasDeleted = true;
            Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

// This partial definition can be shared between Phone and Windows, allowing them to each specify their own styles and extra code behind

namespace pdftron.PDF.Tools.Controls
{
    partial class UserBookmarkControl
    {
        ViewModels.UserBookmarksViewModel _ViewModel = null;

        public UserBookmarkControl()
        {
            this.InitializeComponent();
        }

        public void SaveBookmarks()
        {
            _ViewModel.SaveBookmarks();
        }

        public UserBookmarkControl(PDFViewCtrl control, string docTag)
        {
            this.InitializeComponent();
            SetPDFViewCtrl(control, docTag);
        }

        public void SetPDFViewCtrl(PDFViewCtrl control, string docTag)
        {
            //_ViewModel = new ViewModels.UserBookmarksViewModel(control, docTag); -> See comment on UserBookmarksViewModel.UserBookmarksViewModel()
            _ViewModel = new ViewModels.UserBookmarksViewModel();
            _ViewModel.Init(control, docTag);
            this.DataContext = _ViewModel;
            _ViewModel.FocusOnSelectedItemRequested += ViewModelFocusOnSelectedItemRequested;
            BookmarksListView.ItemClick += BookmarksListView_ItemClick;
        }

        void BookmarksListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClicked != null)
            {
                ItemClicked(this, e);
            }
        }

        private KeyEventHandler _KeyDownHandler = null;
        private RoutedEventHandler _FocusLostHandler = null;
        private string _OriginalText = "";

        public event EventHandler<ItemClickEventArgs> ItemClicked;

        private void ViewModelFocusOnSelectedItemRequested(ViewModels.UserBookmarksViewModel.UserBookmarkItem item)
        {
            ShowTextBox(item);
        }

        private void ShowTextBox(ViewModels.UserBookmarksViewModel.UserBookmarkItem item)
        {
            FrameworkElement containerElement = BookmarksListView.ContainerFromItem(item) as FrameworkElement;
            if (containerElement != null)
            {
                BookmarksListView.ScrollIntoView(item);
                TextBox box = GetChildTextbox(containerElement);
                if (box != null)
                {
                    SetFocus(box);
                }
            }
        }

        private void SetFocus(TextBox box)
        {
            box.LostFocus += TextBox_LostFocus;
            box.KeyDown += TextBox_KeyDown;
            box.GotFocus += box_GotFocus;
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
            _OriginalText = box.Text;
        }

        void box_GotFocus(object sender, RoutedEventArgs e)
        {
            _ViewModel.TextBoxGotFocus();
        }

        void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ViewModel.FinishEditingText();
                e.Handled = true;
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                _ViewModel.CancelEditingText();
                TextBox box = sender as TextBox;
                box.Text = _OriginalText;
                e.Handled = true;
            }
        }

        void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _ViewModel.TextBoxLostFocus();
            _ViewModel.FinishEditingText();
            TextBox box = sender as TextBox;
            CloseTextBox(box);
        }

        private void CloseTextBox(TextBox box)
        {
            if (_KeyDownHandler != null)
            {
                box.KeyDown -= _KeyDownHandler;
            }
            if (_FocusLostHandler != null)
            {
                box.LostFocus -= _FocusLostHandler;
            }
        }

        private TextBox GetChildTextbox(FrameworkElement containerElement)
        {
            DependencyObject box = FindChildControl<TextBox>(containerElement);
            return box as TextBox;
        }

        private DependencyObject FindChildControl<T>(DependencyObject control)
        {
            int childNumber = VisualTreeHelper.GetChildrenCount(control);
            for (int i = 0; i < childNumber; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(control, i);
                FrameworkElement fe = child as FrameworkElement;
                // Not a framework element or is null
                if (fe == null) return null;

                if (child is T)
                {
                    // Found the control so return
                    return child;
                }
                else
                {
                    // Not found it - search children
                    DependencyObject nextLevel = FindChildControl<T>(child);
                    if (nextLevel != null)
                        return nextLevel;
                }
            }
            return null;
        }

        /// <summary>
        /// Will go back if possible.
        /// </summary>
        /// <returns>Return true if it went back, false otherwise</returns>
        public bool GoBack()
        {
            if (_ViewModel != null)
            {
                return _ViewModel.GoBack();
            }
            return false;
        }
    }
}

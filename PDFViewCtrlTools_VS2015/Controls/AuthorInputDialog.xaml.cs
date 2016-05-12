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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Utilities
{
    public sealed partial class AuthorInputDialog : UserControl, IAuthorDialog
    {
        public AuthorInputDialog()
        {
            this.InitializeComponent();
            this.Loaded += AuthorInputDialog_Loaded;
            
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                MainGrid.Margin = new Thickness(0);
                Window.Current.SizeChanged += Current_SizeChanged;
                HandleSize(Window.Current.Bounds.Width, Window.Current.Bounds.Height);
            }

        }

        void AuthorInputDialog_Loaded(object sender, RoutedEventArgs e)
        {
            AuthorNameTextBox.Focus(FocusState.Programmatic);
        }

        public void SetAuthorViewModel(pdftron.PDF.Tools.Controls.ViewModels.AuthorDialogViewModel ViewModel)
        {
            this.DataContext = ViewModel;
        }

        public void Show()
        {

        }

        public void Hide()
        {

        }

        void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            HandleSize(e.Size.Width, e.Size.Height);
        }


        private void HandleSize(double width, double height)
        {
            if (width < 500)
            {
                VisualStateManager.GoToState(this, "NarrowLayout", true);
            }
            else if (height < 500)
            {
                VisualStateManager.GoToState(this, "FlatLayout", true);
            }
        }
    }
}

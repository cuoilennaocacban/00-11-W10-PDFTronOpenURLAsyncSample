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

namespace pdftron.PDF.Tools.Controls
{
    public sealed partial class AnnotationPropertyDialog : UserControl
    {
        private pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesViewModel _ViewModel;
        public pdftron.PDF.Tools.Controls.ViewModels.AnnotationPropertiesViewModel ViewModel { get { return _ViewModel; } }

        public AnnotationPropertyDialog()
        {
            this.InitializeComponent();
            _ViewModel = new ViewModels.AnnotationPropertiesViewModel();
            this.DataContext = _ViewModel;
        }
    }
}

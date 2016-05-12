using pdftron.PDF.Tools.Controls.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public sealed partial class ClickablePageNumberIndicator : UserControl
    {
        public enum ShowingBehaviours
        {
            /// <summary>
            /// Always show the page number indicator
            /// </summary>
            AlwaysShow,
            /// <summary>
            /// Show the page number indicator whenever the page changes, for a while
            /// </summary>
            ShowTimed,
        }

        static DependencyProperty ShowingBehaviourProperty =
            DependencyProperty.Register("ShowingBehaviour", typeof(ShowingBehaviours),
            typeof(ClickablePageNumberIndicator), new PropertyMetadata(ShowingBehaviours.ShowTimed));

        public ShowingBehaviours ShowingBehaviour
        {
            get { return (ShowingBehaviours)(GetValue(ShowingBehaviourProperty)); }
            set 
            { 
                SetValue(ShowingBehaviourProperty, value);
                if (ViewModel != null)
                {
                    if (value == ShowingBehaviours.AlwaysShow)
                    {
                        ViewModel.AutomaticallyHideTimerAfterDelay = false;
                    }
                    else
                    {
                        ViewModel.AutomaticallyHideTimerAfterDelay = true;
                    }
                }
            }
        }

        private bool _SmallView = false;
        public bool SmallView
        {
            get { return _SmallView; }
            set
            {
                if (value != _SmallView)
                {
                    _SmallView = value;
                    double fontSize = (double)this.Resources["DefaultFontSize"];
                    Thickness textBoxPadding = (Thickness)this.Resources["DefaultTextBoxPadding"];
                    if (_SmallView)
                    {
                        fontSize = (double)this.Resources["SmallViewFontSize"];
                        textBoxPadding = (Thickness)this.Resources["SmallViewTextBoxPadding"];
                    }
                    SetFontSizes(fontSize);
                    PageNumberTextBox.Padding = textBoxPadding;
                }
            }
        }

        private void SetFontSizes(double fontSize)
        {
            CurrentPageTextBlock.FontSize = fontSize;
            PageNumberTextBox.FontSize = fontSize;
            SeparatorTextBox.FontSize = fontSize;
            TotalPagesTextBox.FontSize = fontSize;
        }


        private PDFViewCtrl _PDFViewCtrl;
        public PDFViewCtrl PDFViewCtrl
        {
            get { return _PDFViewCtrl; }
            set
            {
                if (_PDFViewCtrl != null && ViewModel != null)
                {
                    ViewModel.PDFViewCtrl = null;
                }
                _PDFViewCtrl = value;
                if (_PDFViewCtrl != null)
                {
                    ViewModel = new ClickablePageNumberIndicatorViewModel(_PDFViewCtrl);
                    if (ShowingBehaviour == ShowingBehaviours.AlwaysShow)
                    {
                        ViewModel.AutomaticallyHideTimerAfterDelay = false;
                    }
                    else
                    {
                        ViewModel.AutomaticallyHideTimerAfterDelay = true;
                    }
                    ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                }
            }
        }

        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsEditTextVisible", StringComparison.OrdinalIgnoreCase))
            {
                if (ViewModel.IsEditTextVisible)
                {
                    await Task.Delay(50);
                    PageNumberTextBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    FocusStealer.IsTabStop = true;
                    FocusStealer.Focus(FocusState.Programmatic);
                    FocusStealer.IsTabStop = false;
                }
            }
        }

        private ClickablePageNumberIndicatorViewModel _ViewModel;
        private ClickablePageNumberIndicatorViewModel ViewModel
        {
            get { return _ViewModel; }
            set
            {
                _ViewModel = value;
                this.DataContext = _ViewModel;
            }
        }

        public ClickablePageNumberIndicator()
        {
            this.InitializeComponent();
            this.DataContext = null;

            SetUpInputScope();
        }

        public ClickablePageNumberIndicator(PDFViewCtrl ctrl)
        {
            this.InitializeComponent();
            PDFViewCtrl = ctrl;

            SetUpInputScope();
        }

        private void SetUpInputScope()
        {
            bool isHardwareButtonsAPIPresent =
                Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");

            if (isHardwareButtonsAPIPresent)
            {
                InputScope scope = new InputScope();
                InputScopeName name = new InputScopeName();

                name.NameValue = InputScopeNameValue.CurrencyAmountAndSymbol;
                scope.Names.Add(name);

                PageNumberTextBox.InputScope = scope;

                SmallView = true;
            }
        }

        public void Show()
        {
            if (ViewModel != null)
            {
                ViewModel.IsIndicatorVisible = true;
            }
        }

        public void Hide()
        {
            if (ViewModel != null)
            {
                ViewModel.IsIndicatorVisible = false;
            }
        }

        /// <summary>
        /// Will go back if possible.
        /// </summary>
        /// <returns>Return true if it went back, false otherwise</returns>
        public bool GoBack()
        {
            if (ViewModel != null)
            {
                return ViewModel.GoBack();
            }
            return false;
        }
    }
}

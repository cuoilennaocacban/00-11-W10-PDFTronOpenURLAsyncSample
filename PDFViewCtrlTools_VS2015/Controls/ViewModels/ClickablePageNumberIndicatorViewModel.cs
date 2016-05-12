using pdftron.PDF.Tools.Controls.ViewModels.Common;
using System;
using Windows.UI.Xaml;

namespace pdftron.PDF.Tools.Controls.ViewModels
{
    public class ClickablePageNumberIndicatorViewModel : ViewModelBase
    {
        private PDFViewCtrl _PDFViewCtrl;
        public PDFViewCtrl PDFViewCtrl
        {
            get { return _PDFViewCtrl; }
            set
            {
                if (_PDFViewCtrl != null)
                {
                    _PDFViewCtrl.OnSetDoc -= PDFViewCtrl_OnSetDoc;
                    _PDFViewCtrl.OnPageNumberChanged -= PDFViewCtrl_OnPageNumberChanged;
                }
                _PDFViewCtrl = value;
                if (_PDFViewCtrl != null)
                {
                    _PDFViewCtrl.OnSetDoc += PDFViewCtrl_OnSetDoc;
                    _PDFViewCtrl.OnPageNumberChanged += PDFViewCtrl_OnPageNumberChanged;
                    IsEditTextVisible = false;
                }
                SetUpPageNumbers();
            }
        }

        public ClickablePageNumberIndicatorViewModel()
        {
            Init();
        }

        public ClickablePageNumberIndicatorViewModel(PDFViewCtrl ctrl)
        {
            Init();
            PDFViewCtrl = ctrl;
        }

        private void Init()
        {
            InitCommands();
            _FadeAwayTimer.Interval = TimeBeforeFading;
            _FadeAwayTimer.Tick += FadeAwayTimer_Tick;
        }

        #region Commands

        private void InitCommands()
        {
            IndicatorTappedCommand = new RelayCommand(IndicatorTappedCommandImpl);
            PageNumberTextLostFocusCommand = new RelayCommand(PageNumberTextLostFocusCommandImpl);
            PageNumberTextKeyUpCommand = new RelayCommand(PageNumberTextKeyUpCommandImpl);
            PageNumberTextKeyDownCommand = new RelayCommand(PageNumberTextKeyDownCommandImpl);
            PageNumberTextChangedCommand = new RelayCommand(PageNumberTextChangedCommandImpl);
            DoneButtonClickedCommand = new RelayCommand(DoneButtonClickedCommandImpl);
        }

        public RelayCommand IndicatorTappedCommand { get; private set; }
        public RelayCommand PageNumberTextLostFocusCommand { get; private set; }
        public RelayCommand PageNumberTextKeyUpCommand { get; private set; }
        public RelayCommand PageNumberTextKeyDownCommand { get; private set; }
        public RelayCommand PageNumberTextChangedCommand { get; private set; }
        public RelayCommand DoneButtonClickedCommand { get; private set; }

        private void IndicatorTappedCommandImpl(object param)
        {
            if (!IsEditTextVisible)
            {
                EditText = string.Empty;
                IsEditTextVisible = true;
            }
            else
            {
                IsEditTextVisible = false;
            }
        }

        private void PageNumberTextLostFocusCommandImpl(object param)
        {
            IsEditTextVisible = false;
        }

        private void PageNumberTextKeyUpCommandImpl(object param)
        {
            Windows.UI.Xaml.Input.KeyRoutedEventArgs args = param as Windows.UI.Xaml.Input.KeyRoutedEventArgs;
            switch (args.Key)
            {
                case Windows.System.VirtualKey.Enter:
                    SetPageFromText();
                    IsEditTextVisible = false;
                    break;
                case Windows.System.VirtualKey.Escape:
                    IsEditTextVisible = false;
                    break;
            }
        }

        private void PageNumberTextKeyDownCommandImpl(object param)
        {
            Windows.UI.Xaml.Input.KeyRoutedEventArgs args = param as Windows.UI.Xaml.Input.KeyRoutedEventArgs;
            if (!string.IsNullOrEmpty(EditText) && EditText.Length >= TotalPageNumberDigits)
            {
                args.Handled = true;
                return;
            }
            switch (args.Key)
            {
                case Windows.System.VirtualKey.Number0:
                case Windows.System.VirtualKey.Number1:
                case Windows.System.VirtualKey.Number2:
                case Windows.System.VirtualKey.Number3:
                case Windows.System.VirtualKey.Number4:
                case Windows.System.VirtualKey.Number5:
                case Windows.System.VirtualKey.Number6:
                case Windows.System.VirtualKey.Number7:
                case Windows.System.VirtualKey.Number8:
                case Windows.System.VirtualKey.Number9:
                case Windows.System.VirtualKey.NumberPad0:
                case Windows.System.VirtualKey.NumberPad1:
                case Windows.System.VirtualKey.NumberPad2:
                case Windows.System.VirtualKey.NumberPad3:
                case Windows.System.VirtualKey.NumberPad4:
                case Windows.System.VirtualKey.NumberPad5:
                case Windows.System.VirtualKey.NumberPad6:
                case Windows.System.VirtualKey.NumberPad7:
                case Windows.System.VirtualKey.NumberPad8:
                case Windows.System.VirtualKey.NumberPad9:
                    return;
            }
            args.Handled = true;
        }   

        private void PageNumberTextChangedCommandImpl(object param)
        {
            string text = param as string;
            if (text != null)
            {
                EditText = text;
            }
        }

        private void DoneButtonClickedCommandImpl(object obj)
        {
            SetPageFromText();
            IsEditTextVisible = false;
        }

        #endregion Commands


        #region Properties

        private string _CurrentPageNumberText = string.Empty;
        public string CurrentPageNumberText
        {
            get { return _CurrentPageNumberText; }
            set
            {
                if (value != null && !value.Equals(_CurrentPageNumberText))
                {
                    _CurrentPageNumberText = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _TotalPagesText = string.Empty;
        public string TotalPagesText
        {
            get { return _TotalPagesText; }
            set
            {
                if (value != null && !value.Equals(_TotalPagesText, StringComparison.OrdinalIgnoreCase))
                {
                    _TotalPagesText = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _EditText = string.Empty;
        public string EditText
        {
            get { return _EditText; }
            set
            {
                if (value != null && !value.Equals(_EditText))
                {
                    _EditText = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int _TotalPageNumberDigits = 1;
        public int TotalPageNumberDigits
        {
            get { return _TotalPageNumberDigits; }
            set
            {
                if (value != _TotalPageNumberDigits)
                {
                    _TotalPageNumberDigits = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _IsEditTextVisible = false;
        public bool IsEditTextVisible
        {
            get { return _IsEditTextVisible; }
            set
            {
                if (value != _IsEditTextVisible)
                {
                    _IsEditTextVisible = value;
                    RaisePropertyChanged();
                    _FadeAwayTimer.Stop();
                    if (!_IsEditTextVisible && AutomaticallyHideTimerAfterDelay)
                    {
                        _FadeAwayTimer.Start();
                    }
                }
            }
        }

        private bool _IsIndicatorVisible = false;
        public bool IsIndicatorVisible
        {
            get { return _IsIndicatorVisible; }
            set
            {
                if (value != _IsIndicatorVisible)
                {
                    _IsIndicatorVisible = value;
                    RaisePropertyChanged();
                    if (AutomaticallyHideTimerAfterDelay)
                    {
                        _FadeAwayTimer.Stop();
                        _FadeAwayTimer.Start();
                    }
                }
            }
        }

        #endregion Properties


        #region Impl

        private void SetPageFromText()
        {
            if (PDFViewCtrl == null)
            {
                return;
            }
            Int32 pageNumber = 0;
            if (Int32.TryParse(EditText, out pageNumber))
            {
                if (pageNumber < 1 || pageNumber > PDFViewCtrl.GetPageCount())
                {
                    return;
                }
                PDFViewCtrl.SetCurrentPage(pageNumber);
            }
            IsIndicatorVisible = true;
            if (AutomaticallyHideTimerAfterDelay)
            {
                _FadeAwayTimer.Start();
            }
        }

        private void PDFViewCtrl_OnPageNumberChanged(int current_page, int num_pages)
        {
            CurrentPageNumberText = "" + current_page;
            TotalPagesText = "" + num_pages;
            IsIndicatorVisible = true;
            if (AutomaticallyHideTimerAfterDelay)
            {
                _FadeAwayTimer.Stop();
                _FadeAwayTimer.Start();
            }
        }

        private void PDFViewCtrl_OnSetDoc()
        {
            SetUpPageNumbers();
        }

        private void SetUpPageNumbers()
        {
            if (PDFViewCtrl != null)
            {
                CurrentPageNumberText = "" + PDFViewCtrl.GetCurrentPage();
                TotalPagesText = "" + PDFViewCtrl.GetPageCount();
                int totalPages = PDFViewCtrl.GetPageCount();
                int digits = 0;
                while (totalPages > 0)
                {
                    digits++;
                    totalPages /= 10;
                }
                TotalPageNumberDigits = Math.Max(digits, 1);
            }
            else
            {
                CurrentPageNumberText = "1";
                TotalPagesText = "1";
                TotalPageNumberDigits = 1;
            }
            
            if (AutomaticallyHideTimerAfterDelay)
            {
                _FadeAwayTimer.Start();
            }
        }

        #endregion Impl


        #region Timer

        private bool _AutomaticallyHideTimerAfterDelay = false;
        public bool AutomaticallyHideTimerAfterDelay
        {
            get { return _AutomaticallyHideTimerAfterDelay; }
            set
            {
                if (value != _AutomaticallyHideTimerAfterDelay)
                {
                    _AutomaticallyHideTimerAfterDelay = value;
                    IsIndicatorVisible = true;
                    if (_AutomaticallyHideTimerAfterDelay)
                    {
                        _FadeAwayTimer.Start();
                    }
                    else
                    {
                        _FadeAwayTimer.Stop();
                    }
                }
            }
        }


        private TimeSpan _TimeBeforeFading = TimeSpan.FromSeconds(3);
        /// <summary>
        /// The amount of time to wait before fading the page number indicator away
        /// </summary>
        public TimeSpan TimeBeforeFading
        {
            get { return _TimeBeforeFading; }
            set 
            { 
                _TimeBeforeFading = value;
                _FadeAwayTimer.Interval = value;
            }
        }

        private DispatcherTimer _FadeAwayTimer = new DispatcherTimer();

        private void FadeAwayTimer_Tick(object sender, object e)
        {
            _FadeAwayTimer.Stop();
            if (AutomaticallyHideTimerAfterDelay)
            {
                IsIndicatorVisible = false;
            }
        }

        #endregion Timer

        #region Back Button

        public bool GoBack()
        {
            if (IsEditTextVisible)
            {
                IsEditTextVisible = false;
                return true;
            }
            return false;
        }

        #endregion Back Button

    }
}

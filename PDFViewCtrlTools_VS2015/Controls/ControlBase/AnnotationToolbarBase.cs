using pdftron.PDF.Tools.Controls.ControlBase;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace pdftron.PDF.Tools.Controls
{
    public enum AnnotationToolbarOrientation
    {
        Horizontal,
        Vertical,
    }

    public class AnnotationToolbarBase : UserControl, ICloseableControl
    {
        public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.RegisterAttached("Orientation", typeof(AnnotationToolbarOrientation),
        typeof(AnnotationToolbarBase), new PropertyMetadata(null, OnOrientationPropertyChanged));

        public static void SetOrientation(DependencyObject d, AnnotationToolbarOrientation value)
        {
            d.SetValue(OrientationProperty, value);
        }

        public static AnnotationToolbarOrientation GetOrientation(DependencyObject d)
        {
            return (AnnotationToolbarOrientation)d.GetValue(OrientationProperty);
        }

        private static void OnOrientationPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            AnnotationToolbarOrientation oldValue = (AnnotationToolbarOrientation)e.OldValue;
            AnnotationToolbarOrientation newValue = (AnnotationToolbarOrientation)e.NewValue;
            if (newValue != oldValue)
            {
                
            }
        }

        ViewModels.AnnotationToolbarViewModel _ViewModel;
        public AnnotationToolbarBase() 
        {
            _ViewModel =  new ViewModels.AnnotationToolbarViewModel(this);
            this.DataContext =  _ViewModel;
            this.SizeChanged += AnnotationToolbar_SizeChanged;
        }

        /// <summary>
        /// Gets or sets whether or not the buttons stay down once pressed.
        /// When set to true, the toolbar will not switch back to the pan tool after each 
        /// annotation is drawn.
        /// </summary>
        public ToolManager ToolManager
        {
            get { return _ViewModel.ToolManager; }
            set { _ViewModel.ToolManager = value; }
        }

        /// <summary>
        /// Gets or sets whether or not the buttons stay down once pressed.
        /// When set to true, the toolbar will not switch back to the pan tool after each 
        /// annotation is drawn.
        /// </summary>
        public bool ButtonsStayDown
        {
            get { return _ViewModel.ButtonsStayDown; }
            set { _ViewModel.ButtonsStayDown = value; }
        }

        public event pdftron.PDF.Tools.Controls.ControlBase.ControlClosedDelegate ControlClosed = delegate { };

        public void CloseControl()
        {
            ControlClosed();
        }

        #region resizing

        protected double MINIMUM_BUTTON_WIDTH = 80;
        protected double MINIMUM_BUTTON_WIDTH_SMALL_SCREEN = 80;
        protected Windows.UI.Xaml.Controls.Primitives.ButtonBase[] ToolbarButtonRanking;
        private bool _IsHeightSet = false;

        protected virtual void AnnotationToolbar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ToolbarButtonRanking == null)
            {
                return;
            }
            if (!_IsHeightSet && this.ActualHeight > 0)
            {
                //SlideOutAnimation.To = -this.ActualHeight;
                //(InkToolbar.RenderTransform as TranslateTransform).Y = -this.ActualHeight;
                //_IsHeightSet = true;
            }

            double windWidth = Window.Current.Bounds.Width;

            int numButtons = ToolbarButtonRanking.Length;
            double minButtonWidth = MINIMUM_BUTTON_WIDTH;
            if (ToolManager != null && ToolManager.IsOnPhone)
            {
                minButtonWidth = MINIMUM_BUTTON_WIDTH_SMALL_SCREEN;
            }

            int maxButtons = Math.Min((int)(e.NewSize.Width / minButtonWidth), numButtons);

            double buttonwidth = e.NewSize.Width / maxButtons;
            double remainder = 0;

            for (int i = 0; i < maxButtons; i++)
            {
                double trunkWidth = (int)buttonwidth;
                remainder += buttonwidth - trunkWidth;
                if (remainder >= 1)
                {
                    ++trunkWidth;
                    --remainder;
                }
                ToolbarButtonRanking[i].Visibility = Windows.UI.Xaml.Visibility.Visible;
                ToolbarButtonRanking[i].Width = (int)trunkWidth;

            }
            for (int i = maxButtons; i < numButtons; i++)
            {
                ToolbarButtonRanking[i].Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        #endregion resizing
    }
}

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
    public sealed partial class PageNumberIndicator : UserControl
    {
        private enum AnimationState
        {
            IsOpen, IsOpening, IsClosed, IsClosing
        }
        private AnimationState _AnimationState = AnimationState.IsClosed;


        public string Text
        {
            get { return PageText.Text; }
            set 
            {
                PageText.Text = value;
                Show();
            }
        }

        private TimeSpan _TimeBeforeFading = TimeSpan.FromSeconds(3);
        /// <summary>
        /// The amount of time to wait before fading the page number indicator away
        /// </summary>
        public TimeSpan TimeBeforeFading
        {
            get { return _TimeBeforeFading; }
            set { _TimeBeforeFading = value; }
        }

        private DispatcherTimer _FadeAwayTimer = new DispatcherTimer();
        
        private bool _FadeAwayOverTime = true;
        /// <summary>
        /// Sets whether the page number indicator should fade away after TimeBeforeFading amount of time has passed
        /// </summary>
        public bool FadeAwayOverTime { set { _FadeAwayOverTime = value; } }


        public PageNumberIndicator()
        {
            this.InitializeComponent();
            PageText.Text = "";
            _FadeAwayTimer.Tick += FadeAwayTimer_Tick;
        }

        void FadeAwayTimer_Tick(object sender, object e)
        {
            _FadeAwayTimer.Stop();
            if (_FadeAwayOverTime)
            {
                Hide();
            }
        }

        /// <summary>
        ///  Show the page number indicator
        /// </summary>
        public void Show()
        {
            _FadeAwayTimer.Stop();
            _FadeAwayTimer.Interval = TimeBeforeFading;
            if (_FadeAwayOverTime)
            {
                _FadeAwayTimer.Start();
            }
            if (_AnimationState == AnimationState.IsClosing || _AnimationState == AnimationState.IsClosed)
            {
                this.Visibility = Visibility.Visible;
                FadeIn.Begin();
                _AnimationState = AnimationState.IsOpening;
            }
        }

        /// <summary>
        ///  Hide the page number indicator
        /// </summary>
        public void Hide()
        {
            if (_AnimationState == AnimationState.IsOpening || _AnimationState == AnimationState.IsOpen)
            {
                FadeOut.Begin();
                _AnimationState = AnimationState.IsClosing;
            }
        }

        /// <summary>
        ///  Use the smaller page number indicator
        /// </summary>
        public void Shrink()
        {
            MainBorder.Style = this.Resources["SmallBorderStyle"] as Style;
            PageText.Style = this.Resources["SmallTextBlockStyle"] as Style;
        }

        /// <summary>
        ///  Use the larger page number indicator
        /// </summary>
        public void Grow()
        {
            MainBorder.Style = this.Resources["LargeBorderStyle"] as Style;
            PageText.Style = this.Resources["LargeTextBlockStyle"] as Style;
        }


        private void FadeInCompleted(object sender, object e)
        {
            if (_AnimationState == AnimationState.IsOpening)
            {
                _AnimationState = AnimationState.IsOpen;
            }
        }

        private void FadeOutCompleted(object sender, object e)
        {
            if (_AnimationState == AnimationState.IsClosing)
            {
                _AnimationState = AnimationState.IsClosed;
                this.Visibility = Visibility.Collapsed;
            }
        }

    }
}

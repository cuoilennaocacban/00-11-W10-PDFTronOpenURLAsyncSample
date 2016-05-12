using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;

namespace pdftron.PDF.Tools.Controls
{
    class EntranceAnimationContentControl : Windows.UI.Xaml.Controls.ContentControl
    {
        #region dependency property

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
          "IsOpen",
          typeof(bool),
          typeof(EntranceAnimationContentControl),
          new PropertyMetadata(false, new PropertyChangedCallback(OnIsOpenChanged))
        );

        public bool IsOpen
        {
            get
            {
                return (bool)GetValue(IsOpenProperty);
            }
            set
            {
                SetValue(IsOpenProperty, value);
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            EntranceAnimationContentControl ctrl = d as EntranceAnimationContentControl; //null checks omitted
            bool s = (bool)e.NewValue; //null checks omitted
            if (s) // opening
            {
                ctrl.Open();
            }
            else // closing
            {
                ctrl.Close();
            }
        }



        public static readonly DependencyProperty EntranceAnimationProperty = DependencyProperty.Register(
          "EntranceAnimation",
          typeof(Storyboard),
          typeof(EntranceAnimationContentControl),
          new PropertyMetadata(false, new PropertyChangedCallback(OnEntranceAnimationChanged))
        );

        public Storyboard EntranceAnimation
        {
            get
            {
                try
                {
                    object storyBoardObject = GetValue(EntranceAnimationProperty);

                    if (storyBoardObject != null && storyBoardObject is Storyboard)
                    {
                        return (Storyboard)storyBoardObject;
                    }
                    return null;
                }
                catch (InvalidCastException)
                { 
                    System.Diagnostics.Debug.WriteLine("Entrance animation failed for: " + this.Name);                
                }
                return null;
            }
            set
            {
                SetValue(EntranceAnimationProperty, value);
            }
        }

        private static void OnEntranceAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            EntranceAnimationContentControl ctrl = d as EntranceAnimationContentControl; //null checks omitted
            Storyboard storyBoard = e.NewValue as Storyboard;
            if (storyBoard != null)
            {
                ctrl.EntranceHandler = new EventHandler<Object>(ctrl.EntranceAnimation_Completed);
                storyBoard.Completed += ctrl.EntranceHandler;
            }
        }

        private EventHandler<object> EntranceHandler;


        public static readonly DependencyProperty ExitAnimationProperty = DependencyProperty.Register(
          "ExitAnimation",
          typeof(Storyboard),
          typeof(EntranceAnimationContentControl),
          new PropertyMetadata(false)
        );

        public Storyboard ExitAnimation
        {
            get
            {
                try
                {
                    return (Storyboard)GetValue(ExitAnimationProperty);
                }
                catch (InvalidCastException)
                { }
                return null;
            }
            set
            {
                SetValue(ExitAnimationProperty, value);
            }
        }

        #endregion dependency property

        private EventHandler<object> _ExitCompletedHandler;
        private bool _IsOpen = false;

        protected void Open()
        {
            _IsOpen = true;
            this.IsEnabled = true;
            this.Visibility = Windows.UI.Xaml.Visibility.Visible;
            if (EntranceAnimation != null)
            {
                EntranceAnimation.Begin();
            }
        }

        protected void Close()
        {
            _IsOpen = false;
            if (ExitAnimation != null)
            {
                if (_ExitCompletedHandler == null)
                {
                    _ExitCompletedHandler = new EventHandler<object>(ExitAnimation_Completed);
                    ExitAnimation.Completed += _ExitCompletedHandler;
                }
                ExitAnimation.Begin();
            }
            else
            {
                this.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void EntranceAnimation_Completed(object sender, object e)
        {
        }

        void ExitAnimation_Completed(object sender, object e)
        {
            if (!_IsOpen)
            {
                this.IsEnabled = false;
                this.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }
    }
}

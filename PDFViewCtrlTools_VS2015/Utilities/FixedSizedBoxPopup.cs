using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media;
using Windows.UI;

using pdftron.PDF;

using UIRect = Windows.Foundation.Rect;
using UIPoint = Windows.Foundation.Point;

namespace pdftron.PDF.Tools
{
    /// <summary>
    /// This class will position a rectangle of determined size on the screen relative to an element it is attached to.
    /// The class allows the user to specify if they prefer the object to be positioned above or to the side
    /// </summary>
    class FixedSizedBoxPopup : ContentControl
    {
        private PDFViewCtrl mPDFView;
        private Grid _BackgroundGrid;
        private Canvas _SpanningCanvas; // This canvas occopies the whole screen.
        private FrameworkElement _Content;
        private UIRect _WindowBounds; // relative to monitor
        private UIRect _TargetBounds; // relative to monitor
        private Popup _BoxPopup;
        private bool _LeftSide = false;

        private bool _AnimationEnabled = true;
        private bool _IsShowing = false; // needed to make sure _FadeOut callback doesn't kill menu
        private Storyboard _FadeIn = null;
        private Storyboard _FadeOut = null;

        private bool _IsArrowCreated = false;

        private bool _IsModal = true;
        private bool _PreferAbove = false;
        public bool PreferAbove
        {
            get { return _PreferAbove; }
            set { _PreferAbove = value; }
        }
        private bool _TopSide = false;

        private const double MIN_MARGIN = 25;
        private const double VERTICAL_MARGIN = 10;

        public event EventHandler<object> BoxPopupClosed;

        /// <summary>
        /// Create a popup that will position itself relative to an element.
        /// </summary>
        /// <param name="ctrl">The PDFViewCtrl in which to position itself</param>
        /// <param name="content">The FrameWorkElement to put inside. Content must have a specific width and height</param>
        /// <param name="target">The rectange to position itself relative to (in PDFview space)</param>
        public FixedSizedBoxPopup(PDFViewCtrl ctrl, FrameworkElement content, bool useAnimations = true, bool isModal = true)
        {
            mPDFView = ctrl;
            _Content = content;
            _IsModal = isModal;
            PreparePopup();
            if (useAnimations)
            {
                SetUpAnimations();
            }
            else
            {
                useAnimations = false;
            }

        }

        private void PreparePopup()
        {
            _BoxPopup = new Popup();

            this.IsTabStop = true;
            this.IsHitTestVisible = true;
            this.PointerReleased += FixedSizedBoxPopup_PointerReleased;
            this.Loaded += FixedSizedBoxPopup_Loaded;

            _SpanningCanvas = new Canvas();
            _SpanningCanvas.Width = Window.Current.Bounds.Width;
            _SpanningCanvas.Height = Window.Current.Bounds.Height;
            if (_IsModal)
            {
                _SpanningCanvas.Background = new Windows.UI.Xaml.Media.SolidColorBrush(Colors.Transparent);
            }
            _BackgroundGrid = new Grid();
            _BackgroundGrid.Width = _Content.Width;
            _BackgroundGrid.Height = _Content.Height;

            _BackgroundGrid.Children.Add(_Content);
            _SpanningCanvas.Children.Add(_BackgroundGrid);
            this.Content = _SpanningCanvas;
            _BoxPopup.Child = this;
        }

        void FixedSizedBoxPopup_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus(FocusState.Programmatic);
        }

        void FixedSizedBoxPopup_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.Focus(FocusState.Pointer);
            e.Handled = true;
        }

        private void SetUpAnimations()
        {
            _FadeIn = Fade(_SpanningCanvas, 0, 1, TimeSpan.FromMilliseconds(150), new QuadraticEase { EasingMode = EasingMode.EaseIn });
            _FadeOut = Fade(_SpanningCanvas, 1, 0, TimeSpan.FromMilliseconds(150), new QuadraticEase { EasingMode = EasingMode.EaseOut });
            _FadeOut.Completed += (s, e) =>
            {
                if (!_IsShowing)
                {
                    _BoxPopup.IsOpen = false;
                }
            };
            _SpanningCanvas.Opacity = 0;
        }

        public void Position(UIRect rect)
        {
            _WindowBounds = GetWindowBounds();
            UIRect r = GetElementRect(mPDFView);
            _TargetBounds = new UIRect(new UIPoint(r.Left + rect.Left, r.Top + rect.Top), new Windows.Foundation.Size(rect.Width, rect.Height));
            if (_PreferAbove)
            {
                PositionVertical();
            }
            else
            {
                PositionHorizontal();
            }
        }

        private void PositionHorizontal()
        {
            double lspace = _TargetBounds.Left - _WindowBounds.Left;
            double rspace = _WindowBounds.Right - _TargetBounds.Right;

            double left;
            // For left or right, we have no preference, we simply take the bigger one
            if (lspace >= rspace)
            {
                left = ((_TargetBounds.Left + _TargetBounds.Right) / 2) - MIN_MARGIN - _BackgroundGrid.Width;
                left = Math.Max(left, _WindowBounds.Left);
                _LeftSide = true;
            }
            else
            {
                left = ((_TargetBounds.Left + _TargetBounds.Right) / 2) + MIN_MARGIN;
                left = Math.Min(left, _WindowBounds.Right - _BackgroundGrid.Width);
                _LeftSide = false;
            }

            double top = ((_TargetBounds.Top + _TargetBounds.Bottom) / 2) - (_BackgroundGrid.Height / 2);
            // check bounds
            if (top > _WindowBounds.Bottom - _BackgroundGrid.Height - MIN_MARGIN)
            {
                top = _WindowBounds.Bottom - _BackgroundGrid.Height - MIN_MARGIN;
            }
            if (top < _WindowBounds.Top + MIN_MARGIN)
            {
                top = _WindowBounds.Top + MIN_MARGIN;
            }
            if (_TargetBounds.Height > _WindowBounds.Height + (2 * MIN_MARGIN))
            {
                top = _WindowBounds.Top + (Math.Max(0, (_WindowBounds.Height - _TargetBounds.Height) / 2));
            }

            _BackgroundGrid.SetValue(Canvas.LeftProperty, left);
            _BackgroundGrid.SetValue(Canvas.TopProperty, top);
        }

        private void PositionVertical()
        {
            double tSpace = _TargetBounds.Top - _WindowBounds.Top;
            double bSpace = _WindowBounds.Bottom - _TargetBounds.Bottom;

            double top = 0;

            // Here, we prefer the top, so we take it if there's enough room, or as long as there's more room than below.
            _TopSide = false;
            if (tSpace > _BackgroundGrid.Height + MIN_MARGIN + VERTICAL_MARGIN || tSpace >= bSpace)
            {
                _TopSide = true;
                top = _WindowBounds.Top + tSpace - _BackgroundGrid.Height - VERTICAL_MARGIN;
            }
            else
            {
                top = _TargetBounds.Bottom + VERTICAL_MARGIN;
            }
            
            // check bounds
            if (top > _WindowBounds.Bottom - _BackgroundGrid.Height - MIN_MARGIN)
            {
                top = _WindowBounds.Bottom - _BackgroundGrid.Height - MIN_MARGIN;
            }
            if (top < _WindowBounds.Top + MIN_MARGIN)
            {
                top = _WindowBounds.Top + MIN_MARGIN;
            }

            double left = ((_TargetBounds.Left + _TargetBounds.Right) / 2) - (_BackgroundGrid.Width / 2);
            // check bounds
            if (left > _WindowBounds.Right - _BackgroundGrid.Width - MIN_MARGIN)
            {
                left = _WindowBounds.Right - _BackgroundGrid.Width - MIN_MARGIN;
            }
            if (left < _WindowBounds.Left + MIN_MARGIN)
            {
                left = _WindowBounds.Left + MIN_MARGIN;
            }

            _BackgroundGrid.SetValue(Canvas.LeftProperty, left);
            _BackgroundGrid.SetValue(Canvas.TopProperty, top);
        }

        private void CreateArrow()
        {
            if (!_IsArrowCreated)
            {
                _IsArrowCreated = true;
                if (_PreferAbove)
                {
                    CreateTopBottomArrow();
                }
                else
                {
                    CreateLeftRightArrow();
                }
            }

        }

        private void CreateLeftRightArrow()
        {
            double offset = 20;
            double margin = 10;
            Path path = new Path();
            path.StrokeLineJoin = PenLineJoin.Miter;
            path.Fill = new SolidColorBrush(Colors.Black);

            PathFigure arrow = new PathFigure();
            arrow.IsClosed = true;
            arrow.StartPoint = new UIPoint(0, 0);
            if (_LeftSide)
            {
                arrow.Segments.Add(new LineSegment() { Point = new UIPoint(offset, offset) });
            }
            else
            {
                arrow.Segments.Add(new LineSegment() { Point = new UIPoint(-offset, offset) });
            }

            arrow.Segments.Add(new LineSegment() { Point = new UIPoint(0, 2 * offset) });


            PathGeometry geom = new PathGeometry();
            PathFigureCollection figures = new PathFigureCollection();
            figures.Add(arrow);
            geom.Figures = figures;
            path.Data = geom;
            //_SpanningCanvas.Children.Add(path);
            _BackgroundGrid.Children.Add(path);

            // Find Vertical positioning
            double top = ((_TargetBounds.Top + _TargetBounds.Bottom) / 2) - ((double)_BackgroundGrid.GetValue(Canvas.TopProperty)) - offset;

            // cap it so that there's at least a 10 pixel margin
            if (top < margin)
            {
                top = margin;
            }
            else if (top > (_BackgroundGrid.Height - (2 * offset) - margin))
            {
                top = (_BackgroundGrid.Height - (2 * offset) - margin);
            }

            double gridTop = (double)_BackgroundGrid.GetValue(Canvas.TopProperty);
            path.SetValue(Canvas.TopProperty, gridTop + top);

            double gridLeft = (double)_BackgroundGrid.GetValue(Canvas.LeftProperty);
            if (_LeftSide)
            {
                path.SetValue(Canvas.LeftProperty, gridLeft + _BackgroundGrid.Width);
            }
            else
            {
                path.SetValue(Canvas.LeftProperty, gridLeft);
            }

            TranslateTransform transform = new TranslateTransform();
            if (_LeftSide)
            {
                transform.X = _BackgroundGrid.Width;
            }
            transform.Y = top;
            path.RenderTransform = transform;
        }

        private void CreateTopBottomArrow()
        {
            double offset = 20;
            double margin = 10;
            Path path = new Path();
            path.StrokeLineJoin = PenLineJoin.Miter;
            path.Fill = new SolidColorBrush(Colors.Black);

            PathFigure arrow = new PathFigure();
            arrow.IsClosed = true;
            arrow.StartPoint = new UIPoint(0, 0);
            if (_TopSide)
            {
                arrow.Segments.Add(new LineSegment() { Point = new UIPoint(offset, offset) });
            }
            else
            {
                arrow.Segments.Add(new LineSegment() { Point = new UIPoint(offset, -offset) });
            }

            arrow.Segments.Add(new LineSegment() { Point = new UIPoint(2 * offset, 0) });


            PathGeometry geom = new PathGeometry();
            PathFigureCollection figures = new PathFigureCollection();
            figures.Add(arrow);
            geom.Figures = figures;
            path.Data = geom;
            _BackgroundGrid.Children.Add(path);

            double left = ((_TargetBounds.Left + _TargetBounds.Right) / 2) - (double)_BackgroundGrid.GetValue(Canvas.LeftProperty) - offset;
            if (left < margin)
            {
                left = margin;
            }
            if (left > _BackgroundGrid.Width - (2 * offset) - margin)
            {
                left = _BackgroundGrid.Width - (2 * offset) - margin;
            }

            TranslateTransform transform = new TranslateTransform();
            if (_TopSide)
            {
                transform.Y = _BackgroundGrid.Height;
            }
            transform.X = left;
            path.RenderTransform = transform;
        }

        private UIRect GetWindowBounds()
        {
            UIRect pdfrect = GetElementRect(mPDFView);
            UIRect windrect = Window.Current.Bounds;

            // windrect might not start at 0 (if you have multiple monitors)
            // So shift it to 0, since all calculations are relative to it.
            windrect.X = 0;
            windrect.Y = 0;

            double left = Math.Max(windrect.Left, pdfrect.Left);
            double top = Math.Max(windrect.Top, pdfrect.Top);
            double right = Math.Min(windrect.Right, pdfrect.Right);
            double bottom = Math.Min(windrect.Bottom, pdfrect.Bottom);
            return new UIRect(left, top, right - left, bottom - top);
        }

        private UIRect GetElementRect(FrameworkElement element)
        {
            Windows.UI.Xaml.Media.GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

        /// <summary>
        /// Creates a basic storyboard. Each object can only have one opacity animation of with the same target opacity
        /// (due to naming conflicts)
        /// </summary>
        private Storyboard Fade(FrameworkElement _target, Double? _from, Double _to, TimeSpan _duration, EasingFunctionBase _easing)
        {
            Storyboard sb = new Storyboard();                                                               // Create a storyboard
            String ID = _target.Name.ToString();
            _target.Resources.Add(ID + "OpacityAnimation" + _to, sb);

            _target.RenderTransform = new Windows.UI.Xaml.Media.CompositeTransform();

            DoubleAnimation opacity = new DoubleAnimation
            {
                From = _from,
                To = _to,
                Duration = new Duration(_duration),
                RepeatBehavior = new RepeatBehavior { Count = 1 },
                EasingFunction = _easing
            };

            Storyboard.SetTarget(opacity, _target);
            Storyboard.SetTargetName(opacity, _target.Name);
            Storyboard.SetTargetProperty(opacity, "(UIElement.Opacity)");
            sb.Children.Add(opacity);

            return sb;
        }


        public void Show(UIRect targetRect, bool showArrow = true)
        {
            Position(targetRect);
            if (showArrow)
            {
                CreateArrow();
            }
            _BoxPopup.IsOpen = true;
            _IsShowing = true;
            if (_AnimationEnabled)
            {
                _FadeIn.Begin();
            }
            this.IsEnabled = true;
            // This makes the PDFViewCtrl unable to respond to user interaction. This is necessary to make this popup behave like a modal popup.
            if (_IsModal)
            {
                mPDFView.IsEnabled = false;
            }
        }

        public void Hide()
        {
            _IsShowing = false;
            if (_AnimationEnabled)
            {
                _FadeOut.Begin();
            }
            else
            {
                _BoxPopup.IsOpen = false;
            }
            this.IsEnabled = false;
            // This makes the PDFViewCtrl able to respond to user interaction again. This is necessary to make this popup behave like a modal popup.
            if (_IsModal)
            {
                mPDFView.IsEnabled = true;
            }
            if (BoxPopupClosed != null)
            {
                BoxPopupClosed(this, null);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using UIRect = Windows.Foundation.Rect;
using UIPoint = Windows.Foundation.Point;

namespace pdftron.PDF.Tools
{

    public delegate void CommandMenuClickedHandler(string title);

    public sealed class PopupCommandMenu
    {
        private UIRect _CurrentPosition;
        // Default ideal values
        private double IDEAL_SPACE = 20;
        private double TOP_LIMIT = 5;
        private double SIDE_MARGIN = 50;
        private double SIDE_MARGIN_THRESHOLD = 10;
        private double MINIMUM_BUTTON_WIDTH = 40;

        private double BUTTON_SIDE_BORDER = 2;
        private double BUTTON_TOPBOTTOM_BORDER = 2;
        private double BORDER_THICKNESS = 0;

        // Dimensions
        private double _RowHeight;
        private double _TotalWidth;
        private double _ColumnWidth;

        private int _TotalRows = 0;
        private double _BottomRowWidth;
        private StackPanel _BottomStack;

        private bool _Shown = false;
        private bool _Loaded = false;

        // animation
        private bool _AnimationEnabled = false;
        private bool _IsShowing = false; // needed to make sure _FadeOut callback doesn't kill menu
        private Storyboard _FadeIn = null;
        private Storyboard _FadeOut = null;

        private pdftron.PDF.PDFViewCtrl mPDFView;
        private IList<string> _Items;
        private Dictionary<string, string> _ItemValues = null;
        private CommandMenuClickedHandler _ClickHandle;        

        // button containers
        private Popup _CommandPopup;
        private Grid _BackgroundGrid;
        private Border _BackgroundBorder;
        private StackPanel _MainStack;
        private List<Button> _Buttons;
        private List<double> _Widths;
        private SolidColorBrush _BackgroundBrush;

        // arrow placement
        private double _Left;
        private double _Right;
        private Path _Arrow;
        private bool _Above = true;
        private double _Offset = 20;
        private double _Width;
        private double _Height;

        private bool _HasAppliedWidthCalculation = false;

        Utilities.PopupCommandMenubuttonHost _ButtonHost = new Utilities.PopupCommandMenubuttonHost();

        /// <summary>
        /// Creates a new command menu that will position itself relative to a point or element
        /// </summary>
        /// <param name="ctrl">The PDFViewCtrl in which to position itself</param>
        /// <param name="its">A list of options</param>
        /// <param name="dlg">A handler for when an option is selected</param>
        public PopupCommandMenu(pdftron.PDF.PDFViewCtrl ctrl, IList<string> its, CommandMenuClickedHandler dlg)
        {
            _Items = its;
            Init(ctrl, dlg);
        }

        public PopupCommandMenu(pdftron.PDF.PDFViewCtrl ctrl, Dictionary<string, string> its, CommandMenuClickedHandler dlg)
        {
            _ItemValues = its;
            Init(ctrl, dlg);
        }

        private void Init(pdftron.PDF.PDFViewCtrl ctrl, CommandMenuClickedHandler dlg)
        {
            _CommandPopup = new Popup();
            _CommandPopup.Loaded += _CommandPopup_Loaded;
            _BackgroundGrid = new Grid();
            _BackgroundBorder = new Border();
            _BackgroundBorder.BorderThickness = new Windows.UI.Xaml.Thickness(BORDER_THICKNESS);
            _BackgroundBorder.BorderBrush = new SolidColorBrush(Colors.White);
            _BackgroundBorder.Child = _BackgroundGrid;

            _MainStack = new StackPanel();
            _Buttons = new List<Button>();
            _Widths = new List<double>();
            mPDFView = ctrl;
            _ClickHandle = dlg;

            if (!double.IsNaN(mPDFView.ActualWidth) && !double.IsNaN(mPDFView.ActualHeight)
                && (mPDFView.ActualWidth < 500 || mPDFView.ActualHeight < 500))
            {
                _ButtonHost.UseSmallButtons = true;
            }

            SetUpButtons();
            SetUpArrow();
        }

        // This calculates the size of the buttons
        private void _CommandPopup_Loaded(object sender, RoutedEventArgs e)
        {
            _RowHeight = _BackgroundBorder.ActualHeight - (2 * BORDER_THICKNESS);
            _TotalWidth = 2 * BORDER_THICKNESS;
            foreach (Button b in _Buttons)
            {
                _Widths.Add(b.ActualWidth);
                _TotalWidth += b.ActualWidth + (2 * BUTTON_SIDE_BORDER);
            }
            _ColumnWidth = _TotalWidth;
            _Loaded = true;
            PositionPopup(1);
        }

        /// <summary>
        /// Will fit the popup width-wise. If the menu is to wide, it will add a row until it fits
        /// </summary>
        /// <param name="rows">The number of rows to start with, typically 1</param>
        private void PositionPopup(int rows)
        {
            _TotalRows = rows;
            if (rows > 10)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            // width and height of menu
            _Width = _ColumnWidth;
            _Height = (_RowHeight * rows) + (2 * BORDER_THICKNESS);

            // width and height of visible area of PDFViewCtrl
            UIRect _Window = GetWindowBounds();

            // get leftmost and rightmost visible part of target element
            _Left = Math.Max(_CurrentPosition.Left, _Window.Left);
            _Right = Math.Min(_CurrentPosition.Right, _Window.Right);

            // horizonal positioning
            if (_Width < _Window.Width - (2 * SIDE_MARGIN)) // make sure we fit
            {
                // Center on top of element
                _CommandPopup.HorizontalOffset = ((_Right + _Left) / 2) - (_Width / 2);
                // check left side
                if (_CommandPopup.HorizontalOffset < SIDE_MARGIN)
                {
                    _CommandPopup.HorizontalOffset = SIDE_MARGIN;
                }
                // Check right side
                if (_CommandPopup.HorizontalOffset + _Width > _Window.Right - SIDE_MARGIN)
                {
                    _CommandPopup.HorizontalOffset = _Window.Right - _Width - SIDE_MARGIN;
                }
            }
            else if (_Width < _Window.Width - (2 * SIDE_MARGIN_THRESHOLD)) // Tight, always center
            {
                _CommandPopup.HorizontalOffset = (_Window.Width - _Width) / 2;
            }
            else // make another row
            {
                LayoutButtons(rows + 1); // get the new layout
                PositionPopup(rows + 1); // position with an extra row
                return;
            }

            // At this point, our menu fits on the screen
            _CommandPopup.UpdateLayout();

            // Now, fix the vertical position
            double TopHeight = _CurrentPosition.Top - _Window.Top;
            double BottomHeight = _Window.Bottom - _CurrentPosition.Bottom;
            if (TopHeight >= _Height + IDEAL_SPACE + TOP_LIMIT) // best option, above the target, with room to spare
            {
                _CommandPopup.VerticalOffset = _Window.Top + TopHeight - (_Height + IDEAL_SPACE);
                _Above = true;
            }
            else if (BottomHeight >= _Height + IDEAL_SPACE + TOP_LIMIT) // place it below the target
            {
                _CommandPopup.VerticalOffset = _Window.Bottom - BottomHeight + IDEAL_SPACE;
                _Above = false;
            }
            else if (TopHeight >= _Height + TOP_LIMIT) // Tight, but fits above target
            {
                _CommandPopup.VerticalOffset = _Window.Top + ((TopHeight - _Height + TOP_LIMIT) / 2);
                _Above = true;
            }
            else if (BottomHeight >= _Height + TOP_LIMIT) // Tight, but fits below target
            {
                _CommandPopup.VerticalOffset = _Window.Bottom - BottomHeight + ((BottomHeight - _Height + TOP_LIMIT) / 2);
                _Above = false;
            }
            else // Rectangle covers all vertical space, place menu in middle
            {
                _CommandPopup.VerticalOffset = _Window.Top + (_Window.Height / 2) - _Height;
                _Above = true;
            }
            PositionArrow();
        }


        private void LayoutButtons(int rows)
        {
            // remove buttons from UI elements
            foreach (UIElement item in _MainStack.Children)
            {
                StackPanel temp = item as StackPanel;
                temp.Children.Clear();
            }

            double RowThreshold = _TotalWidth / rows; // Lets us know when to start a new row
            _BottomRowWidth = 2 * BORDER_THICKNESS;
            _ColumnWidth = 0;

            _MainStack = new StackPanel();
            _BottomStack = new StackPanel();
            _BottomStack.Orientation = Orientation.Horizontal;
            _BottomStack.HorizontalAlignment = HorizontalAlignment.Center;

            int i = 0;
            foreach (Button b in _Buttons)
            {
                StackPanel parent = b.Parent as StackPanel;

                // remove the button from it's current parent
                if (b.Parent != null)
                {
                    parent.Children.Remove(b);
                }

                _BottomStack.Children.Add(b);

                // update width
                _BottomRowWidth += _Widths[i] + (2 * BUTTON_SIDE_BORDER);
                i++;
                if (_BottomRowWidth > _ColumnWidth)
                {
                    _ColumnWidth = _BottomRowWidth; // save widest column
                }
                if (_BottomRowWidth > RowThreshold) // Start a new row if necessary
                {
                    _MainStack.Children.Add(_BottomStack);
                    _BottomStack.Tag = "" + _BottomRowWidth;
                    _BottomStack = new StackPanel();
                    _BottomStack.Orientation = Orientation.Horizontal;
                    _BottomStack.HorizontalAlignment = HorizontalAlignment.Center;
                    _BottomRowWidth = 2 * BORDER_THICKNESS;
                }
            }
            if (_BottomRowWidth <= RowThreshold && !_MainStack.Children.Contains(_BottomStack)) // add the current row if necessary
            {
                _MainStack.Children.Add(_BottomStack);
                _BottomStack.Tag = "" + _BottomRowWidth;
            }

            _BackgroundGrid.Children.Clear();
            _BackgroundGrid.Children.Add(_Arrow);
            _BackgroundGrid.Children.Add(_MainStack);

            _ButtonHost.HostGrid.Children.Clear();
            _ButtonHost.HostGrid.Children.Add(_BackgroundBorder);
            _CommandPopup.Child = _ButtonHost;
        }

        private void PositionArrow()
        {
            double margin = 10;
            double width = _ColumnWidth;
            double leftOffset = ((_Left + _Right) / 2) - _CommandPopup.HorizontalOffset - _Offset;

            // check bounds
            if (leftOffset < margin)
            {
                leftOffset = margin;
            }
            else if (leftOffset > (width - (2 * _Offset) - margin))
            {
                leftOffset = width - (2 * _Offset) - margin;
            }

            if (_TotalRows > 1 && !_HasAppliedWidthCalculation)
            {
                foreach (object stackPanelObj in _MainStack.Children)
                {
                    StackPanel stack = stackPanelObj as StackPanel;
                    string widthString = stack.Tag as string;
                    double stackWidth = 0;
                    bool parsed = double.TryParse(widthString, out stackWidth);
                    if (parsed)
                    {
                        double widthDifference = (_ColumnWidth - stackWidth) / 2;
                        if (widthDifference > 0)
                        {
                            double addedWidth = 2 * widthDifference / stack.Children.Count;
                            foreach (object obj in stack.Children)
                            {
                                Button b = obj as Button;
                                b.Width = b.ActualWidth + addedWidth;
                            }
                        }
                    }
                }
                _HasAppliedWidthCalculation = true;
            }
            CompositeTransform trans = new CompositeTransform();
            trans.TranslateX = leftOffset;
            if (_Above)
            {
                trans.TranslateY = _Height;
            }
            else
            {
                trans.ScaleY = -1;
            }
            _Arrow.RenderTransform = trans;
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

            _target.RenderTransform = new CompositeTransform();

            DoubleAnimation opacity = new DoubleAnimation
            {
                From = _from,
                To = _to,
                Duration = new Duration(_duration),
                RepeatBehavior = new RepeatBehavior(1),
                EasingFunction = _easing
            };

            Storyboard.SetTarget(opacity, _target);
            Storyboard.SetTargetName(opacity, _target.Name);
            Storyboard.SetTargetProperty(opacity, "(UIElement.Opacity)");
                
            sb.Children.Add(opacity);

            return sb;
        }


        private void SetUpButtons()
        {
            IEnumerable<string> buttonValues = null;
            if (_Items != null)
            {
                buttonValues = _Items;
            }
            else if (_ItemValues != null)
            {
                buttonValues = _ItemValues.Keys;
            }
            else
            {
                return;
            }

            foreach (string t in buttonValues)
            {
                Button b = new Button();
                if (_ItemValues != null)
                {
                    b.Content = _ItemValues[t];
                }
                else
                {
                    b.Content = t;
                }
                b.Tag = t;
                //b.BorderThickness = new Thickness(2);
                b.Click += HandleClick;
                b.Margin = new Windows.UI.Xaml.Thickness(BUTTON_SIDE_BORDER, BUTTON_TOPBOTTOM_BORDER, BUTTON_SIDE_BORDER, BUTTON_TOPBOTTOM_BORDER);
                b.MinWidth = MINIMUM_BUTTON_WIDTH;
                _Buttons.Add(b);
            }

        }

        private void SetUpArrow()
        {
            double offset = 20;
            _Arrow = new Path();
            _Arrow.StrokeLineJoin = PenLineJoin.Miter;
            
            _Arrow.Fill = _ButtonHost.Resources["BackgroundBrush"] as SolidColorBrush;
            _Arrow.IsHitTestVisible = false;

            PathFigure arrow = new PathFigure();
            arrow.IsClosed = true;
            arrow.StartPoint = new UIPoint(0, 0);
            arrow.Segments.Add(new LineSegment() { Point = new UIPoint(offset, offset) });
            arrow.Segments.Add(new LineSegment() { Point = new UIPoint(2 * offset, 0) });

            PathGeometry geom = new PathGeometry();
            PathFigureCollection figures = new PathFigureCollection();
            figures.Add(arrow);
            geom.Figures = figures;
            _Arrow.Data = geom;
            _BackgroundGrid.Children.Add(_Arrow);
        }


        private void HandleClick(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            string s = b.Tag as String;
            _ClickHandle(s);
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


        #region Public Functions
        /// <summary>
        /// Enables or disables fade in and out animations
        /// </summary>
        /// <param name="yes">true to enable, false to disable (default)</param>
        public void UseFadeAnimations(bool yes)
        {
            if (yes)
            {
                if (_FadeIn == null && _FadeOut == null)
                {
                    _FadeIn = Fade(_BackgroundBorder, 0, 1, TimeSpan.FromMilliseconds(150), new QuadraticEase { EasingMode = EasingMode.EaseIn });
                    _FadeOut = Fade(_BackgroundBorder, 1, 0, TimeSpan.FromMilliseconds(150), new QuadraticEase { EasingMode = EasingMode.EaseOut });
                    _FadeOut.Completed += (s, e) =>
                    {
                        if (!_IsShowing)
                        {
                            _CommandPopup.IsOpen = false;
                        }
                    };
                }
                if (!_CommandPopup.IsOpen)
                {
                    _BackgroundBorder.Opacity = 0;
                }
                _AnimationEnabled = true;
            }
            else
            {
                if (!_CommandPopup.IsOpen)
                {
                    _BackgroundBorder.Opacity = 1;
                }
                _AnimationEnabled = false;
            }
        }

        /// <summary>
        /// Shows the menu
        /// </summary>
        public void Show()
        {
            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                System.Diagnostics.Debug.WriteLine("Dark");
            }
            else if (Application.Current.RequestedTheme == ApplicationTheme.Light)
            {
                System.Diagnostics.Debug.WriteLine("Light");
            }


            _BackgroundBorder.IsHitTestVisible = true;
            if (_Shown && _Loaded) // not the first time
            {
                LayoutButtons(1);
                PositionPopup(1);
            }
            else if (!_Shown)// the first time we show it
            {
                StackPanel rowstack = new StackPanel();
                rowstack.Orientation = Orientation.Horizontal;

                foreach (Button b in _Buttons)
                {
                    rowstack.Children.Add(b);
                }

                _MainStack.Children.Add(rowstack);
                _BackgroundGrid.Children.Add(_MainStack);
                _BackgroundBorder.Child = _BackgroundGrid;
                _ButtonHost.HostGrid.Children.Clear();
                _ButtonHost.HostGrid.Children.Add(_BackgroundBorder);
                _CommandPopup.Child = _ButtonHost;

                _Shown = true;
            }
                

            _CommandPopup.IsOpen = true;
            _IsShowing = true;
            if (_AnimationEnabled)
            {
                _FadeIn.Begin();
            }
        }

        /// <summary>
        /// Hides the menu
        /// </summary>
        public void Hide()
        {
            _BackgroundBorder.IsHitTestVisible = false;
            _IsShowing = false;
            if (_AnimationEnabled)
            {
                _FadeOut.Begin();
            }
            else
            {
                _CommandPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// Provide a rectangle of the Framework Element that the menu should be attached to.
        /// Note: It is up to the user to update the PopupCommandMenu if that item moves
        /// Note: Also, it is up to the user to provide a new reference if the view somehow changes (e.g. tablet is rotated)
        /// </summary>
        /// <param name="element">The Framework element</param>
        public void TargetUIRectangle(FrameworkElement element)
        {
            _CurrentPosition = GetElementRect(element);
        }

        /// <summary>
        /// Gives a target rectangle as 4 points in PDFViewCtrl space
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        public void TargetSquare(double x1, double y1, double x2, double y2)
        {
            UIRect r = GetElementRect(mPDFView);

            double minx = Math.Min(x1, x2) + 5; // Add 5 to compensate for calculation error
            double miny = Math.Min(y1, y2);
            double w = Math.Abs(x2 - x1);
            double h = Math.Abs(y2 - y1);
            _CurrentPosition = new UIRect(new UIPoint(r.Left + minx, r.Top + miny), new Size(w, h));
                
        }


        /// <summary>
        /// Provide a point in screen space that the menu should be attached to.
        /// </summary>
        /// <param name="p">Note: Not a pdftron point</param>
        public void TargetPoint(UIPoint p)
        {
            _CurrentPosition = new UIRect(p.X, p.Y, 1, 1);
        }


        // gets the position of the element in PDFview screen coordinates.
        private UIRect GetElementRect(FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }



        #endregion Public Functions
    }
}

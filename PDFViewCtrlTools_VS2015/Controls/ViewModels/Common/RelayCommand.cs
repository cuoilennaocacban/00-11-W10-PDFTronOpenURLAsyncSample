using System;
using System.Collections.Generic;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace pdftron.PDF.Tools.Controls.ViewModels.Common
{
    public class RelayCommand : ICommand
    {
        // Event that fires when the enabled/disabled state of the cmd changes
        public event EventHandler CanExecuteChanged;

        // Delegate for method to call when the cmd needs to be executed        
        private readonly Action<object> _targetExecuteMethod;

        // Delegate for method that determines if cmd is enabled/disabled        
        private readonly Predicate<object> _targetCanExecuteMethod;

        public bool CanExecute(object parameter)
        {
            return _targetCanExecuteMethod == null || _targetCanExecuteMethod(parameter);
        }

        public void Execute(object parameter)
        {
            // Call the delegate if it's not null
            if (_targetExecuteMethod != null) _targetExecuteMethod(parameter);
        }

        public RelayCommand(Action<object> executeMethod, Predicate<object> canExecuteMethod = null)
        {
            _targetExecuteMethod = executeMethod;
            _targetCanExecuteMethod = canExecuteMethod;
        }

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null) CanExecuteChanged(this, EventArgs.Empty);
        }
    }

    public static class SelectionChangedCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(SelectionChangedCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ListViewBase control = d as ListViewBase;
            if (control != null)
                control.SelectionChanged += control_SelectionChanged;
        }

        static void control_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewBase control = sender as ListViewBase;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(sender))
            {
                command.Execute(sender);
            }
        }
    }

    public static class ItemClickCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(ItemClickCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ListViewBase control = d as ListViewBase;
            if (control != null)
                control.ItemClick += control_ItemClick;
        }

        static void control_ItemClick(object sender, ItemClickEventArgs e)
        {
            ListViewBase control = sender as ListViewBase;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }


    public static class LoadedCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(LoadedCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
                control.Loaded += control_Loaded;
        }

        static void control_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(sender))
            {
                command.Execute(sender);
            }
        }
    }

    public static class SizeChangedCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(SizeChangedCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
                control.SizeChanged += control_SizeChanged;
        }

        static void control_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }

    public static class PointerContactCommand
    {
        private static Dictionary<FrameworkElement, Dictionary<uint, Windows.UI.Xaml.Input.Pointer>> _ContactPoints = new Dictionary<FrameworkElement, Dictionary<uint, Windows.UI.Xaml.Input.Pointer>>();

        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(PointerContactCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
            {
                control.PointerPressed += control_PointerPressed;
                control.PointerMoved += control_PointerMoved;
                control.PointerReleased += control_PointerReleased;
                control.PointerCanceled += control_PointerCanceled;
                control.PointerCaptureLost += control_PointerCaptureLost;
            }
                
        }

        static void control_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            if (!_ContactPoints.ContainsKey(control))
            {
                _ContactPoints[control] = new Dictionary<uint, Windows.UI.Xaml.Input.Pointer>();
            }
            _ContactPoints[control][e.Pointer.PointerId] = e.Pointer;
            control.CapturePointer(e.Pointer);
            ExecuteCommand(sender, e);
        }

        static void control_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            if (_ContactPoints.ContainsKey(control))
            {
                if (_ContactPoints[control].ContainsKey(e.Pointer.PointerId))
                {
                    ExecuteCommand(sender, e);
                }
            }
        }

        static void control_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RemovePointer(sender, e);
        }

        static void control_PointerCanceled(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RemovePointer(sender, e);
        }

        static void control_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RemovePointer(sender, e);
        }

        private static void RemovePointer(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            if (_ContactPoints.ContainsKey(control))
            {
                if (_ContactPoints[control].ContainsKey(ptr.PointerId))
                {
                    _ContactPoints[control].Remove(ptr.PointerId);
                    if (_ContactPoints[control].Count == 0)
                    {
                        _ContactPoints.Remove(control);
                    }
                    control.ReleasePointerCapture(e.Pointer);
                }
            }
        }

        private static void ExecuteCommand(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(new Tuple<FrameworkElement, Windows.UI.Xaml.Input.PointerRoutedEventArgs>(control, e));
            }
        }
    }

    public static class TextChangedCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(TextChangedCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as TextBox;
            if (control != null)
                control.TextChanged += control_TextChanged;
        }

        private static void control_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox control = sender as TextBox;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(control.Text))
            {
                command.Execute(control.Text);
            }
        }
    }

    public static class KeyUpCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(KeyUpCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
                control.KeyUp += control_KeyUp;
        }

        static void control_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }

    public static class KeyDownCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(KeyDownCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
                control.KeyDown += control_KeyDown;
        }

        static void control_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }

    public static class TappedCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(TappedCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
            {
                if (e.NewValue != null)
                {
                    control.Tapped += control_Tapped;
                }
                else
                {
                    control.Tapped -= control_Tapped;
                }
            }
        }

        static void control_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }

    public static class LostfocusCommand
    {
        public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand),
        typeof(LostfocusCommand), new PropertyMetadata(null, OnCommandPropertyChanged));

        public static void SetCommand(DependencyObject d, ICommand value)
        {
            d.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(DependencyObject d)
        {
            return (ICommand)d.GetValue(CommandProperty);
        }

        private static void OnCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var control = d as FrameworkElement;
            if (control != null)
            {
                if (e.NewValue != null)
                {
                    control.LostFocus += control_LostFocus;
                }
                else
                {
                    control.LostFocus -= control_LostFocus;
                }
            }
        }

        static void control_LostFocus(object sender, RoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            var command = GetCommand(control);

            if (command != null && command.CanExecute(e))
            {
                command.Execute(e);
            }
        }
    }
}

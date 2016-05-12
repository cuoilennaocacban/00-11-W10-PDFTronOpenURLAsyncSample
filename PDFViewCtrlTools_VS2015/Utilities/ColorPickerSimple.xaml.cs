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
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools.Utilities
{
    public sealed partial class ColorPickerSimple : UserControl
    {
        public enum ColorsToSelect
        {
            three, eight, highlight,
        }

        private ColorsToSelect _VisibleColors = ColorsToSelect.eight;
        public ColorsToSelect VisibleColors
        {
            set
            {
                _VisibleColors = value;
                if (_VisibleColors == ColorsToSelect.eight)
                {
                    EightColorSelector.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    ThreeColorSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    HighlightSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
                else if (_VisibleColors == ColorsToSelect.three)
                {
                    EightColorSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    ThreeColorSelector.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    HighlightSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
                else if (_VisibleColors == ColorsToSelect.highlight)
                {
                    EightColorSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    ThreeColorSelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    HighlightSelector.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                Size size = CalculateSize();
                this.Width = size.Width;
                this.Height = size.Height;
            }
        }

        public bool AllowEmpty
        {
            set
            {
                if (value)
                {
                    LastColorButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    EmptyButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    LastColorButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    EmptyButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }

        public delegate void ColorSelectedHandler(Windows.UI.Color color);
        public event ColorSelectedHandler ColorSelected;

        public ColorPickerSimple()
        {
            this.InitializeComponent();
            CalculateSize();
            CreateCheckerPattern();
        }

        public Size CalculateSize()
        {
            if (ColorsToSelect.eight == _VisibleColors || ColorsToSelect.highlight == _VisibleColors)
            {
                return new Size(4 * ((double)this.Resources["ButtonSize"]), 2 * ((double)this.Resources["ButtonSize"]));
            }
            else
            {
                return new Size(3 * ((double)this.Resources["ButtonSize"]), ((double)this.Resources["ButtonSize"]));
            }
        }

        private void CreateCheckerPattern()
        {
            int rowsAndCols = 6;
            SolidColorBrush darkCheckBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100));
            SolidColorBrush lightCheckBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150));
           
            for (int i = 0; i < rowsAndCols; i++)
            {
                CheckeredGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                CheckeredGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (int i = 0; i < rowsAndCols; i++)
            {
                for (int j = 0; j < rowsAndCols; j++)
                {
                    Rectangle rect = new Rectangle();
                    rect.SetValue(Grid.RowProperty, i);
                    rect.SetValue(Grid.ColumnProperty, j);
                    if ((i + j) % 2 == 0)
                    {
                        rect.Fill = darkCheckBrush;
                    }
                    else
                    {
                        rect.Fill = lightCheckBrush;
                    }
                    CheckeredGrid.Children.Add(rect);
                }
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                Rectangle rectangle = button.Content as Rectangle;
                if (rectangle != null)
                {
                    SolidColorBrush brush = rectangle.Fill as SolidColorBrush;
                    if (brush != null)
                    {
                        Windows.UI.Color color = brush.Color;
                        if (ColorSelected != null)
                        {
                            ColorSelected(color);
                        }
                    }
                }
                else
                {
                    if (ColorSelected != null)
                    {
                        ColorSelected(Windows.UI.Colors.Transparent);
                    }
                }
            }

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace pdftron.PDF.Tools.Controls.Resources
{
    class CheckerGrid : Windows.UI.Xaml.Controls.Grid
    {
        private static byte LightShade = 200;
        private static byte DarkShade = 100;
        private static SolidColorBrush DarkCheckBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, DarkShade, DarkShade, DarkShade));
        private static SolidColorBrush LightCheckBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, LightShade, LightShade, LightShade));

        private int _Rows = 4;
        private int _Columns = 4;

        public static readonly DependencyProperty NumberOfRowsProperty =
        DependencyProperty.RegisterAttached("NumberOfRows", typeof(int),
        typeof(CheckerGrid), new PropertyMetadata(null));

        public int NumberOfRows
        {
            get { return _Rows; }
            set 
            { 
                _Rows = value;
                BuildGrid();
            }
        }

        public static readonly DependencyProperty NumberOfColumnsProperty =
        DependencyProperty.RegisterAttached("NumberOfColumns", typeof(int),
        typeof(CheckerGrid), new PropertyMetadata(null));

        public int NumberOfColumns
        {
            get { return _Columns; }
            set 
            {
                _Columns = value;
                BuildGrid();
            }
        }

        public CheckerGrid()
        {
            BuildGrid();
        }

        private void BuildGrid()
        {
            this.Children.Clear();
            this.RowDefinitions.Clear();
            this.ColumnDefinitions.Clear();
            
            for (int i = 0; i < _Rows; ++i)
            {
                RowDefinition rd = new RowDefinition();
                rd.Height = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star);
                this.RowDefinitions.Add(rd);
            }
            for (int j = 0; j < _Columns; ++j)
            {
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new Windows.UI.Xaml.GridLength(1, Windows.UI.Xaml.GridUnitType.Star);
                this.ColumnDefinitions.Add(cd);
            }
            for (int i = 0; i < _Rows; ++i)
            {
                for (int j = 0; j < _Columns; ++j)
                {
                    Rectangle rect = new Rectangle();
                    if ((i + j) % 2 == 0)
                    {
                        rect.Fill = DarkCheckBrush;
                    }
                    else
                    {
                        rect.Fill = LightCheckBrush;
                    }
                    rect.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch;
                    rect.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Stretch;
                    this.Children.Add(rect);
                    rect.SetValue(Grid.RowProperty, i);
                    rect.SetValue(Grid.ColumnProperty, j);
                }
            }
        }
    }
}

using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;


namespace pdftron.PDF.Tools.Controls.ViewModels.Common
{
    public class BooleanToBackgroundColorConverter : IValueConverter
    {
        public static SolidColorBrush SelectedBrush = new SolidColorBrush(Color.FromArgb(255, 100, 110, 255));
        public static SolidColorBrush UnSelectedBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = System.Convert.ToBoolean(value);
            if (isSelected)
            {
                return SelectedBrush;
            }
            else
            {
                return UnSelectedBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            SolidColorBrush brush = value as SolidColorBrush;
            if (brush != null)
            {
                if (brush.Color == SelectedBrush.Color)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !System.Convert.ToBoolean(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return !System.Convert.ToBoolean(value);
        }
    }

    public class ToolTypeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value.ToString().Equals(parameter as string));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if ((bool)value)
            {
                foreach (ToolType toolType in Enum.GetValues(typeof(ToolType)))
                {
                    if (string.Equals(parameter as string, toolType.ToString()))
                    {
                        return toolType;
                    }
                }
            }
            return ToolType.e_pan;
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool visible = System.Convert.ToBoolean(value);
            if (visible)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            Visibility vis = (Visibility)value;
            return (vis == Visibility.Visible);
        }
    }

    public class BooleanToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool visible = System.Convert.ToBoolean(value);
            if (visible)
            {
                return 1.0;
            }
            else
            {
                return 0.0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double opacity = (double)value;
            if (opacity > 0)
            {
                return true;
            }
            return false;
        }
    }

    public class NumberToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double number = System.Convert.ToDouble(value);
            if (number > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool visible = System.Convert.ToBoolean(value);
            if (visible)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            Visibility vis = (Visibility)value;
            return (vis != Visibility.Visible);
        }
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("This makes little sense anyway");
        }
    }

    public class NotNullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value != null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("This makes little sense anyway");
        }
    }

    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string val = value.ToString();
            string param = parameter as string;
            if (!string.IsNullOrEmpty(val) && !string.IsNullOrEmpty(param) && val.Equals(param, StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class RatioToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            return val * 100;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            return val / 100.0;
        }
    }


    /// <summary>
    /// This lets you go from an enum value to one of two styles, based on whether the converter parameter matches the enum's tostring.
    /// You need to provide it with the styles. This can be done in XAML, using the attached dependency properties.
    /// </summary>
    public class EnumToStyleConverter : IValueConverter
    {
        public static readonly DependencyProperty MatchedStyleProperty =
        DependencyProperty.RegisterAttached("MatchedStyle", typeof(Style),
        typeof(EnumToStyleConverter), new PropertyMetadata(null));

        private Style _MatchedStyle;
        public Style MatchedStyle
        {
            get { return _MatchedStyle; }
            set { _MatchedStyle = value; }
        }

        public static readonly DependencyProperty NotMatchedStyleProperty =
        DependencyProperty.RegisterAttached("NotMatchedStyle", typeof(Style),
        typeof(EnumToStyleConverter), new PropertyMetadata(null));

        private Style _NotMatchedStyle;
        public Style NotMatchedStyle
        {
            get { return _NotMatchedStyle; }
            set { _NotMatchedStyle = value; }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string val = value.ToString();
            string param = parameter as string;
            if (!string.IsNullOrEmpty(val) && !string.IsNullOrEmpty(param) && val.Equals(param, StringComparison.OrdinalIgnoreCase))
            {
                return MatchedStyle;
            }
            return NotMatchedStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            return new Thickness(val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    ///////////////////////////////////////////////////////////////
    // Output Converters

    public class RatioToPercentageLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            int percentage = (int)(val * 100);
            return "" + percentage + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToPointLabelConverter : IValueConverter
    {
        public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.RegisterAttached("DecimalPlaces", typeof(int),
        typeof(DoubleToPointLabelConverter), new PropertyMetadata(null));

        private int _DecimalPlaces = 1;
        public int DecimalPlaces
        {
            get { return _DecimalPlaces; }
            set { _DecimalPlaces = value; }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            string unitString = ResourceHandler.GetString("Thickness_Units");
            if (val < 1)
            {
                return val.ToString("f" + (_DecimalPlaces + 1)) + unitString;
            }
            return val.ToString("f" + _DecimalPlaces) + unitString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ThicknessToHalfPointConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            if (val < 1)
            {
                return val;
            }
            return (val + 1) / 2.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double val = (double)value;
            if (val < 1)
            {
                return val;
            }
            return (val * 2) - 1;
        }
    }


}

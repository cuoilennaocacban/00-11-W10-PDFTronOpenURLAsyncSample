using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

using pdftron.PDF;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools
{
     
    public delegate void OnCompletedDelegate(bool canceled, bool empty, Color color);

    public sealed partial class ColorPicker : UserControl
    {
        private double DEFAULTWIDTH = 300;
        private double DEFAULTHEIGHT = 500;
        private OnCompletedDelegate OnCompleted = null;

        private bool _pointer_down = false;
        private int _pointer_ID = -1;

        private Ellipse _HS_selector;
        private TranslateTransform tr;

        

        // Chain of Updates
        // Hue-Saturation changed -> update value slider's HSV (V always 1) -> Update value slider's RGB -> set Value Slider Color to slider's RGB
        // Also From: update value sliders HSV (V always 1) -> Update Current HSV (HS from Hue-Saturation Rectangle, V from slider)
        // -> Update Current RGB -> Update color in top right rectangle to Current RGB
        //
        // Value Slider Changed -> Update Current HSV (HS from Hue-Saturation Rectangle, V from slider) 
        // -> Update Current RGB -> Update color in top right rectangle to Current RGB
        private Color _value_color;
        private Color _ValueColor
        {
            get
            {
                return _value_color;
            }
            set
            {
                if (_value_color == null || _value_color.A != value.A || _value_color.R != value.R || _value_color.G != value.G || _value_color.B != value.B)
                {
                    _value_color = value;
                    SliderRGB.Color = _value_color;
                }
            }
        }

        private Color _final_color;
        private Color _FinalColor
        {
            get
            {
                return _final_color;
            }
            set
            {
                if (_final_color == null || _final_color.A != value.A || _final_color.R != value.R || _final_color.G != value.G || _final_color.B != value.B)
                {
                    _final_color = value;
                    ChosenColorRect.Fill = new SolidColorBrush(_final_color);
                }
            }
        }

        private struct RGB
        {
            public int R; // 0 - 255
            public int G;
            public int B; 

            public RGB(int r, int g, int b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        private struct HSV
        {
            public double H; // 0 - 1
            public double S; // 0 - 1
            public double V; // 0 - 1

            public HSV(double h, double s, double v)
            {
                H = h;
                S = s;
                V = v;
            }
        }

        private RGB _current_rgb;
        private RGB _current_RGB
        {
            get
            {
                return _current_rgb;
            }
            set
            {
                if (_current_rgb.R != value.R || _current_rgb.G != value.G || _current_rgb.B != value.B)
                {
                    _current_rgb = value;
                    _FinalColor = RGBtoColor(_current_rgb);
                }
            }
        }


        private HSV _current_hsv;
        private HSV _current_HSV
        {
            get
            {
                return _current_hsv;
            }
            set
            {
                if (_current_hsv.H != value.H || _current_hsv.S != value.S || _current_hsv.V != value.V)
                {
                    _current_hsv = value;
                    _current_RGB = HSVtoRGB(_current_hsv);
                }
            }
        }
        private RGB _slider_rgb;
        private RGB _slider_RGB
        {
            get
            {
                return _slider_rgb;
            }
            set
            {
                if (_slider_rgb.R != value.R || _slider_rgb.G != value.G || _slider_rgb.B != value.B)
                {
                    _slider_rgb = value;
                    _ValueColor = RGBtoColor(_slider_rgb);
                }
            }
        }

        private HSV _slider_hsv;
        private HSV _slider_HSV
        {
            get
            {
                return _slider_hsv;
            }
            set
            {
                if (_slider_hsv.H != value.H || _slider_hsv.S != value.S || _slider_hsv.V != value.V)
                {
                    _slider_hsv = value;
                    _slider_RGB = HSVtoRGB(_slider_hsv);
                    _current_HSV = new HSV(_slider_hsv.H, _slider_hsv.S, (1 - ValueSlider.Value));
                }
            }
        }

        public ColorPicker()
        {
            this.InitializeComponent();
            _current_rgb = new RGB(255, 0, 0); // don't want to propagate changes
            this.Width = DEFAULTWIDTH;
            this.Height = DEFAULTHEIGHT;
            Setup();
        }

        public ColorPicker(double w, double h)
        {
            this.InitializeComponent();
            _current_rgb = new RGB(255, 0, 0); // don't want to propagate changes
            this.Width = w;
            this.Height = h;
            Setup();
        }


        public ColorPicker(Color color)
        {
            this.InitializeComponent();
            _current_rgb = new RGB(color.R, color.G, color.B); // don't want to propagate changes
            this.Width = DEFAULTWIDTH;
            this.Height = DEFAULTHEIGHT;
            Setup();
        }

        //public ColorPicker(Color color, double w, double h)
        //{
        //    try
        //    {
        //        this.InitializeComponent();
        //    }
        //    catch (Exception e)
        //    {
        //        string s = e.ToString();
        //    }
        //    ChooseEmptyButton.IsEnabled = false;
        //    _current_rgb = new RGB(color.R, color.G, color.B); // don't want to propagate changes
        //    this.Width = w;
        //    this.Height = h;
        //    Setup();
        //}

        public ColorPicker(Color color, double w, double h, bool canChooseEmpty)
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception e)
            {
                string s = e.ToString();
            }
            if (!canChooseEmpty)
            {
                ChooseEmptyButton.IsEnabled = false;
            }
            _current_rgb = new RGB(color.R, color.G, color.B); // don't want to propagate changes
            this.Width = w;
            this.Height = h;
            Setup();
        }

        public ColorPicker(Color color, bool canChooseEmpty)
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception e)
            {
                string s = e.ToString();
            }
            if (!canChooseEmpty)
            {
                ChooseEmptyButton.IsEnabled = false;
            }
            _current_rgb = new RGB(color.R, color.G, color.B); // don't want to propagate changes
            this.Width = DEFAULTWIDTH;
            this.Height = DEFAULTHEIGHT;
            Setup();
        }

        public void SetCompltedDelegate(OnCompletedDelegate del)
        {
            OnCompleted = del;
        }


        private void Setup()
        {
            _current_HSV = RGBtoHSV(_current_RGB); // don't want to propagate changes
            PositionValueSlider();
            _slider_HSV = new HSV(_current_hsv.H, _current_hsv.S, 1); // Want to propagate
            _FinalColor = RGBtoColor(_current_rgb);
            CreateSlectionCircle();
            ValueSlider.ValueChanged += ValueSlider_ValueChanged;
            ValueSlider.IsThumbToolTipEnabled = false;
            this.Loaded += ColorPicker_Loaded; 
        }

        private void ColorPicker_Loaded(object sender, RoutedEventArgs e)
        {
            PositionSelectionCircle();    
        }

        private void CreateSlectionCircle()
        {
            _HS_selector = new Ellipse();
            _HS_selector.StrokeThickness = 2;
            _HS_selector.Stroke = new SolidColorBrush(Colors.White);
            _HS_selector.Width = 13;
            _HS_selector.Height = 13;
            tr = new TranslateTransform();
            _HS_selector.RenderTransform = tr;
            HSCanvas.Children.Add(_HS_selector);
            PositionSelectionCircle();
        }

        private void PositionValueSlider()
        {
            ValueSlider.Value = 1 - _current_hsv.V;
        }

        // Set the initial position of the circle
        private void PositionSelectionCircle()
        {
            tr.X = (HSCanvas.ActualWidth * _current_HSV.H) - (_HS_selector.ActualWidth / 2);
            tr.Y = (HSCanvas.ActualHeight * (1 - _current_HSV.S)) - (_HS_selector.ActualHeight / 2);
        }

        private void HSCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_pointer_ID < 0) // We only ever want to follow one pointer
            {
                _pointer_ID = (int)e.Pointer.PointerId;
                _pointer_down = true;
                HSCanvas.CapturePointer(e.Pointer);
                PointerPoint pp = e.GetCurrentPoint(HSCanvas);
                HSPointer_Changed(pp.Position.X, pp.Position.Y);
            }
        }

        private void HSCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_pointer_down)
            {
                if (e.Pointer.PointerId == _pointer_ID)
                {
                    PointerPoint pp = e.GetCurrentPoint(HSCanvas);
                    HSPointer_Changed(pp.Position.X, pp.Position.Y);
                }
            }

        }

        private void HSCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == _pointer_ID)
            {
                _pointer_down = false;
                _pointer_ID = -1;
                HSCanvas.ReleasePointerCapture(e.Pointer);
            }
        }

        private void HSCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId == _pointer_ID)
            {
                _pointer_ID = -1;
                HSCanvas.ReleasePointerCapture(e.Pointer);
            }
        }

        private void HSCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _pointer_ID = -1;
        }

        // Call whenever the position of the point inside the HSCanvas moves
        private void HSPointer_Changed(double x, double y)
        {
            double x_pr, y_pr;
            x_pr = x / HSCanvas.ActualWidth;
            if (x_pr < 0)
            {
                x_pr = 0;
            }
            if (x_pr > 1)
            {
                x_pr = 1;
            }
            y_pr = y / HSCanvas.ActualHeight;
            if (y_pr < 0)
            {
                y_pr = 0;
            }
            if (y_pr > 1)
            {
                y_pr = 1;
            }

            tr.X = (HSCanvas.ActualWidth * x_pr) - (_HS_selector.ActualWidth / 2);
            tr.Y = (HSCanvas.ActualHeight * y_pr) - (_HS_selector.ActualHeight / 2);
            _slider_HSV = new HSV(x_pr, (1 - y_pr), 1);
        }

        private void ValueSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _current_HSV = new HSV(_slider_HSV.H, _slider_HSV.S, (1 - ValueSlider.Value));
        }

        // button clicks
        private void ChooseColor_Clicked(object sender, RoutedEventArgs e)
        {
            OnCompleted(false, false, _final_color);
        }

        private void ChooseEmpty_Clicked(object sender, RoutedEventArgs e)
        {
            OnCompleted(false, true, _final_color);

        }

        private void Cancel_Clicked(object sender, RoutedEventArgs e)
        {
            OnCompleted(true, false, _final_color);
        }


        private Color RGBtoColor(RGB rgb)
        {
            return Color.FromArgb(255, (byte)rgb.R, (byte)rgb.G, (byte)rgb.B);
        }

        private RGB HSVtoRGB(HSV hsv) 
			{
			// HSV contains values scaled as in the color wheel:
			// that is, all from 0 to 255. 

			// for ( this code to work, HSV.Hue needs
			// to be scaled from 0 to 360 (it//s the angle of the selected
			// point within the circle). HSV.Saturation and HSV.value must be 
			// scaled to be between 0 and 1.

			double h;
			double s;
			double v;

			double r = 0;
			double g = 0;
			double b = 0;

			// Scale Hue to be between 0 and 360. Saturation
			// and value scale to be between 0 and 1.
			h = ((double) hsv.H * 360) % 360;
			s = (double) hsv.S;
			v = (double) hsv.V;

			if ( s == 0 ) 
			{
				// If s is 0, all colors are the same.
				// This is some flavor of gray.
				r = v;
				g = v;
				b = v;
			} 
			else 
			{
				double p;
				double q;
				double t;

				double fractionalSector;
				int sectorNumber;
				double sectorPos;

				// The color wheel consists of 6 sectors.
				// Figure out which sector you//re in.
				sectorPos = h / 60;
				sectorNumber = (int)(Math.Floor(sectorPos));

				// get the fractional part of the sector.
				// That is, how many degrees into the sector
				// are you?
				fractionalSector = sectorPos - sectorNumber;

				// Calculate values for the three axes
				// of the color. 
				p = v * (1 - s);
				q = v * (1 - (s * fractionalSector));
				t = v * (1 - (s * (1 - fractionalSector)));

				// Assign the fractional colors to r, g, and b
				// based on the sector the angle is in.
				switch (sectorNumber) 
				{
					case 0:
						r = v;
						g = t;
						b = p;
						break;

					case 1:
						r = q;
						g = v;
						b = p;
						break;

					case 2:
						r = p;
						g = v;
						b = t;
						break;

					case 3:
						r = p;
						g = q;
						b = v;
						break;

					case 4:
						r = t;
						g = p;
						b = v;
						break;

					case 5:
						r = v;
						g = p;
						b = q;
						break;
				}
			}
			// return an RGB structure, with values scaled
			// to be between 0 and 255.
			return new RGB((int)(r * 255), (int)(g * 255), (int)(b * 255));
		}

		private HSV RGBtoHSV(RGB rgb) 
		{
			// In this function, R, G, and B values must be scaled 
			// to be between 0 and 1.
			// HSV.Hue will be a value between 0 and 360, and 
			// HSV.Saturation and value are between 0 and 1.
			// The code must scale these to be between 0 and 255 for
			// the purposes of this application.

			double min;
			double max;
			double delta;

			double r = (double) rgb.R / 255;
			double g = (double) rgb.G / 255;
			double b = (double) rgb.B / 255;

			double h;
			double s;
			double v;

			min = Math.Min(Math.Min(r, g), b);
			max = Math.Max(Math.Max(r, g), b);
			v = max;
			delta = max - min;
			if ( max == 0 || delta == 0 ) 
			{
				// R, G, and B must be 0, or all the same.
				// In this case, S is 0, and H is undefined.
				// Using H = 0 is as good as any...
				s = 0;
				h = 0;
			} 
			else 
			{
				s = delta / max;
				if ( r == max ) 
				{
					// Between Yellow and Magenta
					h = (g - b) / delta;
				} 
				else if ( g == max ) 
				{
					// Between Cyan and Yellow
					h = 2 + (b - r) / delta;
				} 
				else 
				{
					// Between Magenta and Cyan
					h = 4 + (r - g) / delta;
				}

			}
			// Scale h to be between 0 and 360. 
			// This may require adding 360, if the value
			// is negative.
			h *= 60;
			if ( h < 0 ) 
			{
				h += 360;
			}

			// Scale to the requirements of this 
			// application. All values are between 0 and 1.
            return new HSV((h / 360), s, v);
		}



	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace pdftron.PDF.Tools.Controls.Resources
{
    public class TabButton : Button
    {
        public TabButton()
            : base()
        {
        }

        public static readonly DependencyProperty PointerOverBackgroundBrushProperty =
        DependencyProperty.RegisterAttached("PointerOverBackgroundBrush", typeof(Brush),
        typeof(TabButton), new PropertyMetadata(null));

        private Brush _PointerOverBackgroundBrush;
        public Brush PointerOverBackgroundBrush
        {
            get { return _PointerOverBackgroundBrush; }
            set { _PointerOverBackgroundBrush = value; }
        }

        public static readonly DependencyProperty PointerOverForegroundBrushProperty =
        DependencyProperty.RegisterAttached("PointerOverForegroundBrush", typeof(Brush),
        typeof(TabButton), new PropertyMetadata(null));

        private Brush _PointerOverForegroundBrush;
        public Brush PointerOverForegroundBrush
        {
            get { return _PointerOverForegroundBrush; }
            set { _PointerOverForegroundBrush = value; }
        }

        public static readonly DependencyProperty PointerPressedBackgroundBrushProperty =
        DependencyProperty.RegisterAttached("PointerPressedBackgroundBrush", typeof(Brush),
        typeof(TabButton), new PropertyMetadata(null));

        private Brush _PointerPressedBackgroundBrush;
        public Brush PointerPressedBackgroundBrush
        {
            get { return _PointerPressedBackgroundBrush; }
            set { _PointerPressedBackgroundBrush = value; }
        }

        public static readonly DependencyProperty PointerPressedForegroundBrushProperty =
        DependencyProperty.RegisterAttached("PointerPressedForegroundBrush", typeof(Brush),
        typeof(TabButton), new PropertyMetadata(null));

        private Brush _PointerPressedForegroundBrush;
        public Brush PointerPressedForegroundBrush
        {
            get { return _PointerPressedForegroundBrush; }
            set { _PointerPressedForegroundBrush = value; }
        }

        public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius),
        typeof(TabButton), new PropertyMetadata(null));

        private CornerRadius _CornerRadius;
        public CornerRadius CornerRadius
        {
            get { return _CornerRadius; }
            set { _CornerRadius = value; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UIPoint = Windows.Foundation.Point;
using PDFPoint = pdftron.PDF.Point;
using UIRect = Windows.Foundation.Rect;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace pdftron.PDF.Tools
{
    class UtilityFunctions
    {

        public static double PointToLineDistance(PDFPoint basePoint, PDFPoint tipPoint, PDFPoint target)
        {
            double dx;
            double dy;
            double dist;

            double lineXDist = tipPoint.x - basePoint.x;
            double lineYDist = tipPoint.y - basePoint.y;

            double squaredDist = (lineXDist * lineXDist) + (lineYDist * lineYDist);

            if (double.IsNaN(squaredDist) || Math.Abs(squaredDist) < 0.001)
            {
                dx = target.x - basePoint.x;
                dy = target.y - basePoint.y;
                dist = (dx * dx) + (dy * dy);
                return dist;
            }

            double distRatio = ((target.x - basePoint.x) * lineXDist + (target.y - basePoint.y) * lineYDist) / squaredDist;

            if (distRatio < 0)
            {
                distRatio = 0; // This way, we will compare against mBasePoint
            }
            if (distRatio > 1)
            {
                distRatio = 0; // This way, we will compare against mTipPoint
            }

            dx = basePoint.x - target.x + distRatio * lineXDist;
            dy = basePoint.y - target.y + distRatio * lineYDist;
            dist = (dx * dx) + (dy * dy);

            return dist;
        }


        public static UIPoint GetPointCloserToLine(UIPoint basePoint, UIPoint tipPoint, UIPoint target)
        {
            UIPoint returnPoint = new UIPoint(target.X, target.Y);

            double lineXDist = tipPoint.X - basePoint.X;
            double lineYDist = tipPoint.Y - basePoint.Y;

            double squaredDist = (lineXDist * lineXDist) + (lineYDist * lineYDist);

            double distRatio = ((target.X - basePoint.X) * lineXDist + (target.Y - basePoint.Y) * lineYDist) / squaredDist;

            if (double.IsNaN(squaredDist) || Math.Abs(squaredDist) < 0.001)
            {
                returnPoint.X = (target.X + basePoint.X) / 2;
                returnPoint.Y = (target.Y + basePoint.Y) / 2;
                return returnPoint;
            }

            if (distRatio < 0)
            {
                distRatio = 0; // This way, we will compare against mBasePoint
            }
            if (distRatio > 1)
            {
                distRatio = 0; // This way, we will compare against mTipPoint
            }

            double dx = basePoint.X - target.X + distRatio * lineXDist;
            double dy = basePoint.Y - target.Y + distRatio * lineYDist;

            returnPoint = new UIPoint(target.X + (dx / 2), target.Y + (dy / 2));

            return returnPoint;
        }

        public static UIRect GetElementRect(Windows.UI.Xaml.FrameworkElement element, Windows.UI.Xaml.FrameworkElement target = null)
        {
            Windows.UI.Xaml.Media.GeneralTransform elementtransform = element.TransformToVisual(target);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

        public static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        return (T)child;
                    }

                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null) return childItem;
                }
            }
            return null;
        }

        public static void CopySelectedTextToClipBoard(string text)
        {
            Windows.ApplicationModel.DataTransfer.DataPackage dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            dataPackage.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }

        public static void SetCursor(Windows.UI.Core.CoreCursorType cursorType)
        {
            Windows.UI.Xaml.Window.Current.CoreWindow.PointerCursor =
                new Windows.UI.Core.CoreCursor(cursorType, 1);
        }

        public static SettingsColor ConvertToSettingsColor(Color color)
        {
            return new SettingsColor(color.R, color.G, color.B, color.A != 0);
        }

        public static Color ConvertToColor(SettingsColor color)
        {
            byte alpha = 0;
            if (color.Use)
            {
                alpha = 255;
            }
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static ToolType GetToolTypeFromAnnotType(IAnnot annot)
        {
            switch (annot.GetAnnotType())
            {
                case AnnotType.e_Line:
                    Annots.Line line = new Annots.Line(annot.GetSDFObj());
                    if (IsArrow(line))
                    {
                        return ToolType.e_arrow_create;
                    }
                    return ToolType.e_line_create;
                case AnnotType.e_Square:
                    return ToolType.e_rect_create;
                case AnnotType.e_Circle:
                    return ToolType.e_oval_create;
                case AnnotType.e_Ink:
                    return ToolType.e_ink_create;
                case AnnotType.e_Polyline:
                    return ToolType.e_polyline_placeholder;
                case AnnotType.e_Polygon:
                    return ToolType.e_polygon_placeholder;
                case AnnotType.e_Text:
                    return ToolType.e_sticky_note_create;
                case AnnotType.e_FreeText:
                    return ToolType.e_text_annot_create;
                case AnnotType.e_Highlight:
                    return ToolType.e_text_highlight;
                case AnnotType.e_Underline:
                    return ToolType.e_text_underline;
                case AnnotType.e_StrikeOut:
                    return ToolType.e_text_strikeout;
                case AnnotType.e_Squiggly:
                    return ToolType.e_text_squiggly;
            }
            return ToolType.e_none;
        }

        public static bool IsArrow(pdftron.PDF.Annots.Line line)
        {
            Annots.LineEndingStyle endingStyle = line.GetStartStyle();
            if (endingStyle == Annots.LineEndingStyle.e_ClosedArrow || endingStyle == Annots.LineEndingStyle.e_OpenArrow
                || endingStyle == Annots.LineEndingStyle.e_RClosedArrow || endingStyle == Annots.LineEndingStyle.e_ROpenArrow)
            {
                return true;
            }
            endingStyle = line.GetEndStyle();
            if (endingStyle == Annots.LineEndingStyle.e_ClosedArrow || endingStyle == Annots.LineEndingStyle.e_OpenArrow
                || endingStyle == Annots.LineEndingStyle.e_RClosedArrow || endingStyle == Annots.LineEndingStyle.e_ROpenArrow)
            {
                return true;
            }
            return false;
        }

        public static bool AreFilesEqual(Windows.Storage.StorageFile file1, Windows.Storage.StorageFile file2)
        {
            if (file1 == null && file2 == null)
            {
                return true;
            }
            else if (file1 != null && file2 != null)
            {
                return file1.IsEqual(file2);
            }
            return false;
        }
    }
}

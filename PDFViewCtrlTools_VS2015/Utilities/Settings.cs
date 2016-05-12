using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;


namespace pdftron.PDF.Tools
{
    public struct SettingsColor
    {
        public byte R;
        public byte G;
        public byte B;
        public bool Use; // For when you select an empty color.

        public SettingsColor(byte r, byte g, byte b, bool use)
        {
            R = r;
            G = g;
            B = b;
            Use = use;
        }
    }

    public static class Settings
    {
        internal static Windows.Storage.ApplicationDataContainer roamingSettings =
                Windows.Storage.ApplicationData.Current.RoamingSettings;
        internal static Windows.Storage.ApplicationDataContainer localSettings =
                Windows.Storage.ApplicationData.Current.LocalSettings;
        internal static Windows.Storage.ApplicationDataContainer GetStorageContainer(bool roaming)
        {
            if (roaming)
            {
                return roamingSettings;
            }
            else
            {
                return localSettings;
            }
        }

        

        internal static bool HasFontSize { get { return HasValue("FontSize"); } }
        internal static double FontSize
        {
            get { return (double)GetValue("FontSize"); }
            set { SetValue("FontSize", value); }
        }

        internal static bool HasTextStrokeColor { get { return HasColor("TextStrokeColor"); } }
        internal static SettingsColor TextStrokeColor
        {
            get { return GetColor("TextStrokeColor"); }
            set { SetColor("TextStrokeColor", value); }
        }

        internal static bool HasTextFillColor { get { return HasColor("TextFillColor"); } }
        internal static SettingsColor TextFillColor
        {
            get { return GetColor("TextFillColor"); }
            set { SetColor("TextFillColor", value); }
        }

        internal static bool HasMarkupOpacity { get { return HasValue("MarkupOpacity"); } }
        internal static double MarkupOpacity
        {
            get { return (double)GetValue("MarkupOpacity"); }
            set { SetValue("MarkupOpacity", value); }
        }

        internal static bool HasMarkupStrokeThickness { get { return HasValue("MarkupStrokeThickness"); } }
        internal static double MarkupStrokeThickness
        {
            get { return (double)GetValue("MarkupStrokeThickness"); }
            set { SetValue("MarkupStrokeThickness", value); }
        }

        internal static bool HasMarkupFillColor { get { return HasColor("MarkupFillColor"); } }
        internal static SettingsColor MarkupFillColor
        {
            get { return GetColor("MarkupFillColor"); }
            set { SetColor("MarkupFillColor", value); }
        }

        internal static bool HasMarkupStrokeColor { get { return HasColor("MarkupStrokeColor"); } }
        internal static SettingsColor MarkupStrokeColor
        {
            get { return GetColor("MarkupStrokeColor"); }
            set { SetColor("MarkupStrokeColor", value); }
        }

        // Text Markups
        internal static bool HasTextMarkupOpacity { get { return HasValue("TextMarkupOpacity"); } }
        internal static double TextMarkupOpacity
        {
            get { return (double)GetValue("TextMarkupOpacity"); }
            set { SetValue("TextMarkupOpacity", value); }
        }

        internal static bool HasTextMarkupThickness { get { return HasValue("TextMarkupThickness"); } }
        internal static double TextMarkupThickness
        {
            get { return (double)GetValue("TextMarkupThickness"); }
            set { SetValue("TextMarkupThickness", value); }
        }

        internal static bool HasTextMarkupColor { get { return HasColor("TextMarkupColor"); } }
        internal static SettingsColor TextMarkupColor
        {
            get { return GetColor("TextMarkupColor"); }
            set { SetColor("TextMarkupColor", value); }
        }

        // Highlights
        internal static bool HasHightlightOpacity { get { return HasValue("HightlightOpacity"); } }
        internal static double HightlightOpacity
        {
            get { return (double)GetValue("HightlightOpacity"); }
            set { SetValue("HightlightOpacity", value); }
        }

        internal static bool HasHighlightColor { get { return HasColor("HighlightColor"); } }
        internal static SettingsColor HighlightColor
        {
            get { return GetColor("HighlightColor"); }
            set { SetColor("HighlightColor", value); }
        }



        internal static void SetColor(string key, SettingsColor color)
        {
            roamingSettings.Values["PDFViewCtrlTools" + key + "U"] = color.Use;
            roamingSettings.Values["PDFViewCtrlTools" + key + "R"] = color.R;
            roamingSettings.Values["PDFViewCtrlTools" + key + "G"] = color.G;
            roamingSettings.Values["PDFViewCtrlTools" + key + "B"] = color.B;
        }

        internal static bool HasColor(string key)
        {
            // Enough to check the 'U' flag
            return roamingSettings.Values.ContainsKey("PDFViewCtrlTools" + key + "U");

        }

        internal static SettingsColor GetColor(string key)
        {
            SettingsColor sc = new SettingsColor();
            sc.Use = (bool)roamingSettings.Values["PDFViewCtrlTools" + key + "U"];
            sc.R = (byte)roamingSettings.Values["PDFViewCtrlTools" + key + "R"];
            sc.G = (byte)roamingSettings.Values["PDFViewCtrlTools" + key + "G"];
            sc.B = (byte)roamingSettings.Values["PDFViewCtrlTools" + key + "B"];
            return sc;
        }


        internal static void SetValue(string key, object val)
        {
            roamingSettings.Values["PDFViewCtrlTools" + key] = val;
        }

        internal static bool HasValue(string key, bool isRoaming = true)
        {
            
            return GetStorageContainer(isRoaming).Values.ContainsKey("PDFViewCtrlTools" + key);
        }


        internal static object GetValue(string key)
        {
            object val = roamingSettings.Values["PDFViewCtrlTools" + key];
            if (val is Windows.Storage.ApplicationDataCompositeValue)
            {
                Windows.Storage.ApplicationDataCompositeValue colors = (Windows.Storage.ApplicationDataCompositeValue)val;
                SettingsColor color;
                color.Use = (bool)colors["Use"];
                color.R = (byte)colors["R"];
                color.G = (byte)colors["G"];
                color.B = (byte)colors["B"];
                return color;
            }         
            return roamingSettings.Values["PDFViewCtrlTools" + key];
        }

        private static string _AnnotationAuthorHasBeenAskedSettingsString = "PDFViewCtrlToolsAnnotationAuthorHasBeenAsked";
        internal static bool AnnotationAuthorHasBeenAsked
        {
            get
            {
                if (localSettings.Values.ContainsKey(_AnnotationAuthorHasBeenAskedSettingsString))
                {
                    return (bool)localSettings.Values[_AnnotationAuthorHasBeenAskedSettingsString];
                }
                return false;
            }
            set
            {
                localSettings.Values[_AnnotationAuthorHasBeenAskedSettingsString] = value;
            }
        }

        private static string _AnnotationAuthorNameSettingsString = "PDFViewCtrlToolsAnnotationAuthorName";
        public static string AnnotationAutor
        {
            get
            {
                if (roamingSettings.Values.ContainsKey(_AnnotationAuthorNameSettingsString))
                {
                    return (string)roamingSettings.Values[_AnnotationAuthorNameSettingsString];
                }
                return string.Empty;
            }
            set
            {
                roamingSettings.Values[_AnnotationAuthorNameSettingsString] = value;
                if (!string.IsNullOrEmpty(value))
                {
                    // We don't need to ask this if the user has set their author name already.
                    // Checking for blank space makes sure it's not an accidental setting from a binding.
                    AnnotationAuthorHasBeenAsked = true; 
                }
            }
        }

        #region Settings with Custom Strings

        private static IList<Color> DEFAULT_PRESET_COLOR_OPTIONS = new List<Color>() { Colors.Red, Color.FromArgb(255, 255, 128, 0), Colors.Yellow, Colors.Lime, 
            Colors.Green, Color.FromArgb(255, 0, 255, 255), Colors.Blue, Colors.Magenta , Colors.Black, Colors.White };
        private static IList<Color> CUSTOM_COLOR_OPTIONS;
        private static string CUSTOM_COLOR_OPTIONS_STRING =
            //"246,206,206:245,236,206:245,246,206:227,246,206:206,246,206:206,246,245:206,216,246:227,206,246:246,206,236:255,255,255:250,88,88:247,211,88:244,250,88:172,250,88:88,250,88:88,250,244:88,130,250:172,88,250:250,88,208:189,189,189:254,46,46:250,204,46:247,254,46:154,254,46:46,254,46:46,254,247:46,100,254:154,46,254:254,46,200:164,164,164:255,0,0:255,191,0:255,255,0:128,255,0:0,255,0:0,255,255:0,64,255:128,0,255:255,0,191:132,132,132:223,1,1:219,169,1:215,223,1:116,223,0:1,223,1:1,223,215:1,58,223:116,1,223:223,1,165:110,110,110:180,4,4:177,137,4:174,180,4:95,180,4:4,180,4:4,180,174:4,49,180:95,4,180:180,4,134:88,88,88:138,8,8:136,106,8:134,138,8:75,138,8:8,138,8:8,138,133:8,41,138:75,8,138:138,8,104:66,66,66:97,11,11:95,76,11:94,97,11:56,97,11:11,97,11:11,97,94:11,33,97:56,11,97:97,11,75:46,46,46:59,11,11:58,47,11:57,59,11:36,59,11:11,59,11:11,59,57:11,23,59:36,11,59:59,11,46:28,28,28:42,10,10:41,34,10:41,42,10:27,42,10:10,42,10:10,42,41:10,18,42:27,10,42:42,10,34:0,0,0";
            //"248,224,224:248,236,224:247,248,224:224,248,224:224,248,247:224,224,248:236,224,248:248,224,247:255,255,255:245,169,169:245,208,169:242,245,169:169,245,169:169,245,242:169,169,245:208,169,245:245,169,242:230,230,230:250,88,88:250,172,88:244,250,88:88,250,88:88,250,244:72,72,250:172,88,250:250,88,244:189,189,189:255,0,0:255,128,0:255,255,0:0,255,0:0,255,255:0,0,255:128,0,255:255,0,255:128,128,128:180,4,4:180,95,4:174,180,4:4,180,4:4,180,174:4,4,180:95,4,180:180,4,174:88,88,88:128,0,0:128,64,0:128,128,0:0,128,0:0,128,128:0,0,128:64,0,128:128,0,128:68,68,68:97,11,11:97,56,11:94,97,11:11,97,11:11,97,94:11,11,97:56,11,97:97,11,94:46,46,46:42,10,10:42,27,10:41,42,10:10,42,10:10,42,41:10,10,42:27,10,42:42,10,41:0,0,0";

            "248,224,224:248,236,224:247,248,224:224,248,224:224,248,247:224,224,248:236,224,248:248,224,247:255,255,255:245,169,169:245,208,169:242,245,169:169,245,169:169,245,242:169,169,245:208,169,245:245,169,242:230,230,230:250,88,88:250,172,88:244,250,88:88,250,88:88,250,244:72,72,250:172,88,250:250,88,244:189,189,189:255,0,0:255,128,0:255,255,0:0,255,0:0,255,255:0,0,255:128,0,255:255,0,255:128,128,128:180,4,4:180,95,4:174,180,4:4,180,4:4,180,174:4,4,180:95,4,180:180,4,174:88,88,88:128,0,0:128,64,0:128,128,0:0,128,0:0,128,128:0,0,128:64,0,128:128,0,128:68,68,68:97,11,11:97,56,11:94,97,11:11,97,11:11,97,94:11,11,97:56,11,97:97,11,94:46,46,46:42,10,10:42,27,10:41,42,10:10,42,10:10,42,41:10,10,42:27,10,42:42,10,41:0,0,0";

        private const string SHAPE_STRING = "Markup_Property";
        private const string INK_STRING = "Ink_Property";
        private const string TEXT_MARKUP_STRING = "Text_Markup_Property";
        private const string HIGHLIGHT_STRING = "highlight_Property";
        private const string TEXT_ANNOT_STRING = "Text_Annot_Property";

        internal static SettingsColor DEFAULT_SHAPE_COLOR = new SettingsColor(255, 0, 0, true);
        internal static SettingsColor DEFAULT_SHAPE_FILL = new SettingsColor(255, 0, 0, false);
        internal static SettingsColor DEFAULT_INK_COLOR = new SettingsColor(255, 0, 0, true);
        internal static SettingsColor DEFAULT_TEXT_COLOR = new SettingsColor(255, 0, 0, true);
        internal static SettingsColor DEFAULT_TEXT_BACKGROUND = new SettingsColor(255, 255, 255, false);
        internal static SettingsColor DEFAULT_TEXTMARKUP_COLOR = new SettingsColor(255, 0, 0, true);
        internal static SettingsColor DEFAULT_TEXTHIGHLIGHT_COLOR = new SettingsColor(255, 255, 0, true);
        internal static SettingsColor[] DEFAULT_INK_COLORS = new SettingsColor[] 
        {new SettingsColor(255, 0, 0, true), new SettingsColor(0, 0, 255, true), new SettingsColor(0, 0, 0, true)};


        internal static double DEFAULT_THICKNESS = 1;
        internal static double DEFAULT_OPACITY = 1;
        internal static double DEFAULT_FONT_SIZE = 12;

        public enum PropertyType
        {
            thickness,
            opacity,
            fontsize,
            color,
            view,
            label,
        }

        internal static T GetCustomValue<T>(string key, T _default, bool isRoaming = true)
        {
            if (HasValue(key, isRoaming))
            {
                object val = GetStorageContainer(isRoaming).Values["PDFViewCtrlTools" + key];
                try
                {
                    T retval = (T)val;
                    if (retval != null)
                    {
                        return retval;
                    }
                }
                catch (Exception e)
                {
                    string errorString = string.Format("An error receiving setting for key {0}", key);
                    System.Diagnostics.Debug.WriteLine(errorString);
                    pdftron.PDF.Tools.Utilities.AnalyticsHandlerBase.CURRENT.SendException(e, "Tools", errorString);
                }
            }
            return _default;
        }

        internal static void SetCustomValue<T>(string key, T value, bool isRoaming = true)
        {
            GetStorageContainer(isRoaming).Values["PDFViewCtrlTools" + key] = value;
        }

        internal static void EraseValue(string key, bool isRoaming = true)
        {
            if (HasValue(key, isRoaming))
            {
                GetStorageContainer(isRoaming).Values.Remove(key);
            }
        }

        internal static IList<Color> GetPresetColors(ToolType toolType, bool hasFill)
        {
            string key = GetPresetColorKeyFromToolType(toolType, hasFill);
            if (HasValue(key))
            {
                object colors = GetValue(key);
                string colorString = colors as string;
                if (colorString != null)
                {
                    return ParseColorString(colorString);
                }
            }

            return DEFAULT_PRESET_COLOR_OPTIONS;
        }

        internal static IList<Color> GetCustomColors()
        {
            if (CUSTOM_COLOR_OPTIONS == null)
            {
                CUSTOM_COLOR_OPTIONS = ParseColorString(CUSTOM_COLOR_OPTIONS_STRING);
            }
            return CUSTOM_COLOR_OPTIONS;
        }

        internal static void SetPresetColors(ToolType toolType, bool hasFill, IList<Color> colors)
        {
            string key = GetPresetColorKeyFromToolType(toolType, hasFill);
            string value = CreateColorString(colors);
            SetValue(key, value);
        }

        internal static IList<Color> ParseColorString(string colorString)
        {
            try
            {
                List<Color> colors = new List<Color>();
                char[] separateColors = { ':' };
                char[] separateBytes = { ',' };
                string[] colorStrings = colorString.Split(separateColors);
                foreach (string color in colorStrings)
                {
                    string[] byteStrings = color.Split(separateBytes);
                    if (byteStrings.Length == 3)
                    {
                        colors.Add(Color.FromArgb(255, Byte.Parse(byteStrings[0]), Byte.Parse(byteStrings[1]), Byte.Parse(byteStrings[2])));
                    }
                }
                return colors;
            }
            catch (Exception e)
            {
                pdftron.PDF.Tools.Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return DEFAULT_PRESET_COLOR_OPTIONS;
        }

        internal static string CreateColorString(IList<Color> colors)
        {
            string colorString = "";
            foreach (Color color in colors)
            {
                string col = string.Format("{0},{1},{2}", color.R, color.G, color.B);
                if (!string.IsNullOrEmpty(colorString))
                {
                    colorString += ":";
                }
                colorString += col;
            }
            return colorString;
        }

        #region Specific Properties

        internal static bool HasSettableProperties(ToolType toolType)
        {
            return toolType == ToolType.e_ink_create || toolType == ToolType.e_rect_create || toolType == ToolType.e_oval_create
                || toolType == ToolType.e_line_create || toolType == ToolType.e_arrow_create || toolType == ToolType.e_text_annot_create
                || toolType == ToolType.e_text_highlight || toolType == ToolType.e_text_underline || toolType == ToolType.e_text_strikeout
                || toolType == ToolType.e_text_squiggly || toolType == ToolType.e_polygon_placeholder || toolType == ToolType.e_polyline_placeholder;
        }

        internal static double GetThickness(ToolType toolType, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.thickness, false, additionalNumber);
                if (HasValue(key))
                {
                    object val = GetValue(key);
                    return (double)val;
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return DEFAULT_THICKNESS;
        }

        internal static void SetThickness(ToolType toolType, double thickness, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.thickness, false, additionalNumber);
                SetValue(key, thickness);
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
        }

        internal static double GetOpacity(ToolType toolType, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.opacity, false, additionalNumber);
                if (HasValue(key))
                {
                    object val = GetValue(key);
                    return (double)val;
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return DEFAULT_OPACITY;
        }

        internal static void SetOpacity(ToolType toolType, double opacity, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.opacity, false, additionalNumber);
                SetValue(key, opacity);
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
        }

        internal static double GetFontSize(ToolType toolType, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.fontsize, false, additionalNumber);
                if (HasValue(key))
                {
                    object val = GetValue(key);
                    return (double)val;
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return DEFAULT_FONT_SIZE;
        }

        internal static void SetFontSize(ToolType toolType, double fontSize, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.fontsize, false, additionalNumber);
                SetValue(key, fontSize);
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
        }

        internal static SettingsColor GetShapeColor(ToolType toolType, bool isFill, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.color, isFill, additionalNumber);
                if (HasColor(key))
                {
                    object colorObj = GetColor(key);
                    if (colorObj is SettingsColor)
                    {
                        SettingsColor color = (SettingsColor)colorObj;
                        return color;
                    }
                }
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return GetDefaultColor(toolType, isFill, additionalNumber);
        }

        internal static void SetShapeColor(ToolType toolType, bool isFill, SettingsColor color, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.color, isFill, additionalNumber);
                SetColor(key, color);
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
        }

        internal static SettingsColor GetDefaultColor(ToolType toolType, bool isFill, int additionalNumber = -1)
        {
            switch (toolType)
            {
                case ToolType.e_text_annot_create:
                    if (isFill)
                    {
                        return DEFAULT_TEXT_BACKGROUND;
                    }
                    else
                    {
                        return DEFAULT_TEXT_COLOR;
                    }
                case ToolType.e_text_squiggly:
                case ToolType.e_text_strikeout:
                case ToolType.e_text_underline:
                    return DEFAULT_TEXTMARKUP_COLOR;
                case ToolType.e_text_highlight:
                    return DEFAULT_TEXTHIGHLIGHT_COLOR;
                default:
                    if ((toolType == ToolType.e_rect_create || toolType == ToolType.e_oval_create) && isFill)
                    {
                        return DEFAULT_SHAPE_FILL;
                    }
                    break;
            }
            if (toolType == ToolType.e_ink_create && additionalNumber >= 0 && additionalNumber < DEFAULT_INK_COLORS.Length)
            {
                return DEFAULT_INK_COLORS[additionalNumber];
            }

            return DEFAULT_SHAPE_COLOR;
        }

        public static int GetView(ToolType toolType, int defaultView, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.view, false, additionalNumber);
                int val = GetCustomValue<int>(key, defaultView);
                return val;
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
            return defaultView;
        }


        public static void SetView(ToolType toolType, int viewNumber, int additionalNumber = -1)
        {
            try
            {
                string key = GetKeyFromToolType(toolType, PropertyType.view, false, additionalNumber);
                SetCustomValue<int>(key, viewNumber);
            }
            catch (Exception e)
            {
                Utilities.AnalyticsHandlerBase.CURRENT.SendException(e);
            }
        }

        internal static int GetPropertyLabel(ToolType toolType)
        {
            string key = GetKeyFromToolType(toolType, PropertyType.label);
            int label = GetCustomValue<int>(key, 1);
            return label;
        }

        internal static void SetPropertylLabel(ToolType toolType, int label)
        {
            string key = GetKeyFromToolType(toolType, PropertyType.label);
            SetCustomValue<int>(key, label);
        }

        #endregion Specific Properties


        #region Utility Functions

        private static string GetKeyFromToolType(ToolType toolType, PropertyType property, bool hasFill = false, int additionalNumber = -1)
        {
            string retString = toolType.ToString();
            retString += property.ToString();
            if (hasFill)
            {
                retString += "fill";
            }
            if (additionalNumber >= 0)
            {
                retString += additionalNumber;
            }
            return retString;
        }

        private static string GetPresetColorKeyFromToolType(ToolType toolType, bool hasFill)
        {
            return GetKeyFromToolType(toolType, PropertyType.color, hasFill) + "PresetColors";
        }

        #endregion Utility Functions


        #endregion Settings with Custom Strings

    }
}

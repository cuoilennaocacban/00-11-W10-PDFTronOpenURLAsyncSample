using pdftron.PDF.Tools.Controls.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

using UIRect = Windows.Foundation.Rect;

namespace pdftron.PDF.Tools.Controls.ViewModels
{
    #region Preset Colors

    public class AnnotationPropertiesPresetsColorGridViewModel : ViewModelBase
    {
        public static string SegoeRemoveString = new string((char)0xE108, 1);
        public static string SegoeAddString = new string((char)0xE109, 1);
        public static string SegoeAcceptString = new string((char)0xE10B, 1);

        private static int MAX_PRESETS_ROWS = 5;

        #region Preset Color Item

        public class PresetItem : ViewModelBase
        {
            public bool IsColorOption { get; private set; }
            public bool IsEmptyOption { get; private set; }
            public bool IsAddRemoveButton { get { return IsAddButton || IsRemoveButton; } }
            public SolidColorBrush ColorBrush { get; private set; }
            public Grid CheckPatternGrid { get; private set; }

            public PresetItem MySelf { get; private set; }

            private string _AddOrRemoveContent;
            public string AddOrRemoveContent 
            {
                get { return _AddOrRemoveContent; }
                set
                {
                    if (value != null || _AddOrRemoveContent != null)
                    {
                        if (!string.Equals(_AddOrRemoveContent, value))
                        {
                            _AddOrRemoveContent = value;
                            RaisePropertyChanged();
                        }
                    }
                }
            }

            public bool IsAddButton { get; private set; }
            public bool IsRemoveButton { get; private set; }

            private bool _ShowDelete = false;
            public bool ShowDelete
            {
                get { return _ShowDelete; }
                set
                {
                    if (_ShowDelete != value)
                    {
                        _ShowDelete = value;
                        RaisePropertyChanged();
                        RaisePropertyChanged("ShowSelected");
                    }
                }
            }

            private bool _IsSelected = false;
            public bool IsSelected
            {
                get { return _IsSelected; }
                set
                {
                    if (_IsSelected != value)
                    {
                        _IsSelected = value;
                        RaisePropertyChanged();
                        RaisePropertyChanged("ShowSelected");
                    }
                }
            }

            public bool ShowSelected
            {
                get
                {
                    return IsSelected && !ShowDelete;
                }
            }

            public PresetItem(Color color)
            {
                Init(color, false, false, false);
            }

            public PresetItem(Color color, bool isEmpty, bool isAdd, bool isRemove)
            {
                Init(color, isEmpty, isAdd, isRemove);
            }

            private void Init(Color color, bool isEmpty, bool isAdd, bool isRemove)
            {
                MySelf = this;
                IsColorOption = false;
                IsEmptyOption = false;
                IsAddButton = false;
                IsRemoveButton = false;
                ColorBrush = new SolidColorBrush(Colors.Transparent);
                CheckPatternGrid = new Grid();
                AddOrRemoveContent = "";

                if (isAdd)
                {
                    IsAddButton = true;
                    AddOrRemoveContent = AnnotationPropertiesPresetsColorGridViewModel.SegoeAddString;
                }
                else if (isRemove)
                {
                    IsRemoveButton = true;
                    AddOrRemoveContent = AnnotationPropertiesPresetsColorGridViewModel.SegoeRemoveString;
                }
                else if (isEmpty)
                {
                    IsEmptyOption = true;
                    //CheckPatternGrid.Children.Add(CreateEmptyGrid());
                }
                else
                {
                    IsColorOption = true;
                    ColorBrush = new SolidColorBrush(color);
                }
            }
        }

        #endregion Preset Color Item

        public delegate void ColorSelectedDelegate(Color color, bool isEmpty);
        public event ColorSelectedDelegate ColorSelectedEvent = delegate { };
        public delegate void RequestNewColorDelegate(IList<Color> existingColors);
        public event RequestNewColorDelegate RequestNewColor = delegate { };

        public AnnotationPropertiesPresetsColorGridViewModel(ToolType toolType, bool canBeEmpty, int maxColumns) : base()
        {
            _ColorItems = new ObservableCollection<PresetItem>();
            Init(toolType, canBeEmpty, maxColumns);
        }

        private ObservableCollection<PresetItem> _ColorItems = null;
        public ObservableCollection<PresetItem> PresetsColors 
        { 
            get { return _ColorItems; }
            private set
            {
                _ColorItems = value;
                RaisePropertyChanged();
            }
        }

        private int _PresetsMaxColumns = 5;
        public int PresetsMaxColumns
        {
            get { return _PresetsMaxColumns; }
            set
            {
                if (value != _PresetsMaxColumns)
                {
                    _PresetsMaxColumns = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _CanBeEmpty = false;
        private ToolType _ToolType = ToolType.e_none;

        private void Init(ToolType toolType, bool canBeEmpty, int maxColumns)
        {
            PresetsMaxColumns = maxColumns;
            _CanBeEmpty = canBeEmpty;
            _ToolType = toolType;
            IList<PresetItem> items = GetItems();
            foreach (PresetItem item in items)
            {
                PresetsColors.Add(item);
            }
            if (canBeEmpty)
            {
                PresetsColors.Add(new PresetItem(Colors.Transparent, true, false, false));
            }
            ResolveAddRemoveButtons(_PresetsMaxColumns * MAX_PRESETS_ROWS);
            //PresetsColors.Add(new PresetItem(Colors.Transparent, false, true, false));
            //PresetsColors.Add(new PresetItem(Colors.Transparent, false, false, true));

            ItemClickCommand = new RelayCommand(ItemClickCommandImpl);
        }

        private IList<PresetItem> GetItems()
        {
            IList<PresetItem> items = new List<PresetItem>();

            IList<Color> colors = Settings.GetPresetColors(_ToolType, !_CanBeEmpty);
            foreach (Color color in colors)
            {
                PresetItem item = new PresetItem(color);
                items.Add(item);
            }
            return items;
        }

        /// <summary>
        /// Use Transparent to select empty color
        /// </summary>
        /// <param name="color"></param>
        public bool Select(Color color)
        {
            bool selected = false;
            foreach (PresetItem item in PresetsColors)
            {
                if ((color.A == 0 && item.IsEmptyOption)
                    || (item.IsColorOption && item.ColorBrush.Color == color))
                {
                    item.IsSelected = true;
                    selected = true;
                }
                else
                {
                    item.IsSelected = false;
                }
            }
            return selected;
        }

        public void AddNewColor(Color color)
        {
            if (!Select(color))
            {
                // check if we have room for add button.
                ResolveAddRemoveButtons(PresetsMaxColumns * MAX_PRESETS_ROWS - 1);
                int index = PresetsColors.Count - 1;
                while (index >= 0 && !PresetsColors[index].IsColorOption)
                {
                    --index;
                }
                PresetItem item = new PresetItem(color, false, false, false);
                PresetsColors.Insert(index + 1, item);
                item.IsSelected = true;
                IList<Color> colors = GetColorsFromItems();
                Settings.SetPresetColors(_ToolType, !_CanBeEmpty, colors);
            }
            ColorSelectedEvent(color, false);
        }



        #region Commands

        public RelayCommand ItemClickCommand { get; private set; }

        private void ItemClickCommandImpl(object sender)
        {
            PresetItem item = sender as PresetItem;
            if (item != null)
            {
                if (_CurrentMode == ButtonMode.normal)
                {
                    HandleClickInNormalMode(item);
                }
                else if (_CurrentMode == ButtonMode.remove)
                {
                    HandleClickInRemoveMode(item);
                }
            }
        }

        #endregion Commands

        #region Behaviour

        private enum ButtonMode
        {
            normal,
            remove
        }

        private ButtonMode _CurrentMode = ButtonMode.normal;
        private bool _WasItemDeleted = false;


        private void HandleClickInNormalMode(PresetItem item)
        {
            if (item.IsColorOption)
            {
                ColorSelectedEvent(item.ColorBrush.Color, false);
                Select(item.ColorBrush.Color);
            }
            if (item.IsEmptyOption)
            {
                ColorSelectedEvent(Colors.Transparent, true);
                Select(Colors.Transparent);
            }
            if (item.IsRemoveButton)
            {
                _CurrentMode = ButtonMode.remove;
                _WasItemDeleted = false;
                item.AddOrRemoveContent = AnnotationPropertiesPresetsColorGridViewModel.SegoeAcceptString;
                foreach (PresetItem preset in PresetsColors)
                {
                    if (preset.IsColorOption)
                    {
                        preset.ShowDelete = true;
                    }
                }
            }
            if (item.IsAddButton)
            {
                IList<Color> colors = new List<Color>();
                foreach (PresetItem itm in _ColorItems)
                {
                    if (itm.IsColorOption)
                    {
                        colors.Add(itm.ColorBrush.Color);
                    }
                }
                RequestNewColor(colors);
            }
        }

        private void HandleClickInRemoveMode(PresetItem item)
        {
            if (item.IsColorOption)
            {
                if (PresetsColors.Contains(item))
                {
                    _WasItemDeleted = true;
                    PresetsColors.Remove(item);
                }
            }
            else if (item.IsRemoveButton)
            {
                _CurrentMode = ButtonMode.normal;
                item.AddOrRemoveContent = AnnotationPropertiesPresetsColorGridViewModel.SegoeRemoveString;
                foreach (PresetItem preset in PresetsColors)
                {
                    if (preset.IsColorOption)
                    {
                        preset.ShowDelete = false;
                    }
                }
                if (_WasItemDeleted)
                {
                    IList<Color> colors = GetColorsFromItems();
                    Settings.SetPresetColors(_ToolType, !_CanBeEmpty, colors);
                    ResolveAddRemoveButtons(PresetsMaxColumns * MAX_PRESETS_ROWS);
                }
            }
        }

        private IList<Color> GetColorsFromItems()
        {
            IList<Color> colors = new List<Color>();
            foreach (PresetItem item in PresetsColors)
            {
                if (item.IsColorOption)
                {
                    colors.Add(item.ColorBrush.Color);
                }
            }
            return colors;
        }

        private void ResolveAddRemoveButtons(int maxButtons)
        {
            bool hasEmpty = false;
            PresetItem addButton = null;
            PresetItem removeButton = null;
            foreach (PresetItem item in PresetsColors)
            {
                if (item.IsEmptyOption)
                {
                    hasEmpty = true;
                }
                if (item.IsAddButton)
                {
                    addButton = item;
                }
                if (item.IsRemoveButton)
                {
                    removeButton = item;
                }
            }
            int numColors = PresetsColors.Count;
            if (addButton != null)
            {
                --numColors;
            }
            if (removeButton != null)
            {
                --numColors;
            }

            // for the purpose of the add button, we consider the empty button a color
            if (addButton == null && numColors < maxButtons - 1)
            {
                if (removeButton != null)
                {
                    PresetsColors.Insert(PresetsColors.Count - 1, new PresetItem(Colors.Transparent, false, true, false));
                }
                else
                {
                    PresetsColors.Add(new PresetItem(Colors.Transparent, false, true, false));
                }
            }
            if (addButton != null && numColors >= maxButtons - 1)
            {
                PresetsColors.Remove(addButton);
            }

            // for the purpose of the remove button, empty is not considered a color
            if (hasEmpty)
            {
                --numColors;
            }
            if (removeButton == null && numColors > 0 )
            {
                PresetsColors.Add(new PresetItem(Colors.Transparent, false, false, true));
            }

            if (removeButton != null && numColors == 0)
            {
                PresetsColors.Remove(removeButton);
            }
        }

        #endregion Behaviour


    }


    #endregion Preset Colors

    #region Preset Buttons

    public class PresetButtonViewModel : ViewModelBase
    {
        #region Preset Button Item

        public class PresetButtonItem : ViewModelBase
        {
            public enum SelectionState
            {
                selected,
                notselected,
            }

            private SelectionState _SelectedStatus = SelectionState.notselected;
            public SelectionState SelectedStatus
            {
                get { return _SelectedStatus; }
                set
                {
                    if (value != _SelectedStatus)
                    {
                        _SelectedStatus = value;
                        RaisePropertyChanged();
                    }
                }
            }


            private bool _IsOption = true;
            public bool IsOption { get { return _IsOption; } }
            public bool IsSeparator { get { return !_IsOption; } }
            public string Option { get; set; }

            private double _ItemWidth = 0;
            public double ItemWidth
            {
                get { return _ItemWidth; }
                set
                {
                    if (_ItemWidth != value && value > 0)
                    {
                        _ItemWidth = value;
                        RaisePropertyChanged();
                    }
                }
            }

            public PresetButtonItem(string content)
            {
                if (!string.IsNullOrEmpty(content))
                {
                    _IsOption = true;
                    Option = content;
                }
                else
                {
                    _IsOption = false;
                }
            }
        }

        #endregion Preset Button Item

        private const double SeparatorWidth = 1;

        public delegate void OptionSelectedDelegate(string option);
        public event OptionSelectedDelegate OptionSelected = delegate { };

        public ObservableCollection<PresetButtonItem> Items { get; private set; }

        public RelayCommand ItemClickCommand { get; private set; }
        public RelayCommand SizeChangedCommand { get; private set; }

        public ColumnDefinitionCollection ColumnDefs { get; private set; }

        private PresetButtonItem _SelectedButton = null;

        public PresetButtonViewModel(IList<string> options)
        {
            this.ItemClickCommand = new RelayCommand(ItemClickCommandImpl);
            this.SizeChangedCommand = new RelayCommand(SizeChangedCommandImpl);

            Items = new ObservableCollection<PresetButtonItem>();

            foreach (string option in options)
            {
                if (Items.Count > 0)
                {
                    Items.Add(new PresetButtonItem(null));
                }
                Items.Add(new PresetButtonItem(option));
            }
        }

        public void Select(string val)
        {
            if (_SelectedButton != null && !val.Equals(_SelectedButton.Option))
            {
                _SelectedButton.SelectedStatus = PresetButtonItem.SelectionState.notselected;
                _SelectedButton = null;
            }

            foreach (PresetButtonItem item in Items)
            {
                if (item.IsOption)
                {
                    string content = item.Option;
                    if (val.Equals(content))
                    {
                        _SelectedButton = item;
                        _SelectedButton.SelectedStatus = PresetButtonItem.SelectionState.selected;
                    }
                }
            }
        }


        private void ItemClickCommandImpl(object content)
        {
            string val = content as string;
            if (!string.IsNullOrWhiteSpace(val))
            {
                OptionSelected(val);
            }

        }

        private void SizeChangedCommandImpl(object sizeArgs)
        {
            SizeChangedEventArgs args = sizeArgs as SizeChangedEventArgs;

            int numItems = (Items.Count + 1) / 2;
            double buttonWidth = (args.NewSize.Width - numItems) / numItems;
            foreach (PresetButtonItem item in Items)
            {
                if (item.IsOption)
                {
                    item.ItemWidth = buttonWidth;
                }
                else
                {
                    item.ItemWidth = SeparatorWidth;
                }
            }
        }

    }

    #endregion Preset Buttons

    #region Custom Colors

    public class AnnotationPropertiesCustomColorGridViewModel : ViewModelBase
    {
        #region Custom Color Item

        public class CustomItem : ViewModelBase
        {
            public SolidColorBrush ColorBrush { get; private set; }
            public bool IsColorOption { get; private set; }
            public bool IsEmptyOption { get; private set; }

            private bool _IsSelectable = true;
            public bool IsSelectable
            {
                get { return _IsSelectable; }
                set
                {
                    if (value != _IsSelectable)
                    {
                        _IsSelectable = value;
                        RaisePropertyChanged();
                        RaisePropertyChanged("Opacity");
                    }
                }
            }
            public double Opacity
            {
                get 
                {
                    if (_IsSelectable)
                    {
                        return 1;
                    }
                    return 0.5;
                }
                
            }

            private bool _IsSelected = false;
            public bool IsSelected
            {
                get { return _IsSelected; }
                set
                {
                    if (_IsSelected != value)
                    {
                        _IsSelected = value;
                        RaisePropertyChanged();
                    }
                }
            }

            public CustomItem(Color color, bool isEmpty)
            {
                IsEmptyOption = isEmpty;
                IsColorOption = !isEmpty;
                if (IsColorOption)
                {
                    ColorBrush = new SolidColorBrush(color);
                }
                else
                {
                    ColorBrush = new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        #endregion Custom Color Item

        public delegate void ColorSelectedDelegate(Color color, bool isEmpty);
        public event ColorSelectedDelegate ColorSelectedEvent = delegate { };

        public AnnotationPropertiesCustomColorGridViewModel(bool canBeEmpty, int maxColumns, IList<Color> colorList)
        {
            Init(canBeEmpty, maxColumns, colorList);
        }

        private void Init(bool canBeEmpty, int maxColumns, IList<Color> colorList)
        {
            PointerContactCommand = new RelayCommand(PointerContactCommandImpl);

            CustomMaxColumns = maxColumns;

            CustomColors = new ObservableCollection<CustomItem>();
            foreach (Color color in colorList)
            {
                CustomItem item = new CustomItem(color, false);
                _CustomColors.Add(item);
            }
            if (canBeEmpty)
            {
                _CustomColors.Add(new CustomItem(Colors.Transparent, true));
            }
        }

        public int CustomMaxColumns { get; private set; }

        private ObservableCollection<CustomItem> _CustomColors;
        public ObservableCollection<CustomItem> CustomColors
        {
            get { return _CustomColors; }
            set
            {
                _CustomColors = value;
                RaisePropertyChanged();
            }
        }

        public void Select(Color color)
        {
            if (color.A == 0)
            {
                SelectEmpty();
                return;
            }
            foreach (CustomItem item in CustomColors)
            {
                item.IsSelected = (!item.IsEmptyOption && item.ColorBrush.Color == color);
            }
        }

        public void SelectEmpty()
        {
            foreach (CustomItem item in CustomColors)
            {
                item.IsSelected = item.IsEmptyOption;
            }
        }

        public void DisableColors(IList<Color> colors)
        {
            foreach (CustomItem item in _CustomColors)
            {
                if (item.IsColorOption && colors.Contains(item.ColorBrush.Color))
                {
                    item.IsSelectable = false;
                }
                else
                {
                    item.IsSelectable = true;
                }
            }
        }

        #region Commands


        public RelayCommand PointerContactCommand { get; private set; }

        private void PointerContactCommandImpl(object senderAndArgs)
        {
            Tuple<FrameworkElement, Windows.UI.Xaml.Input.PointerRoutedEventArgs> args =
                senderAndArgs as Tuple<FrameworkElement, Windows.UI.Xaml.Input.PointerRoutedEventArgs>;
            if (args != null)
            {
                FrameworkElement control = args.Item1;
                Windows.Foundation.Point point = args.Item2.GetCurrentPoint(control).Position;

                if (!double.IsNaN(control.ActualWidth) && control.ActualWidth > 0 && !double.IsNaN(control.ActualHeight) && control.ActualHeight > 0)
                {
                    double xPercentage = point.X / control.ActualWidth;
                    double yPercentage = point.Y / control.ActualHeight;
                    if (xPercentage >= 0 && xPercentage <= 1 && yPercentage >= 0 && yPercentage <= 1)
                    {
                        int numcols = Math.Min(_CustomColors.Count, CustomMaxColumns);
                        int numrows = (CustomMaxColumns - 1 + _CustomColors.Count) / CustomMaxColumns;
                        int col = (int)(xPercentage * numcols);
                        if (col >= numcols) // in case the percentage was right on 1
                        {
                            col = numcols - 1;
                        }
                        int row = (int)(yPercentage * numrows);
                        if (row >= numrows)
                        {
                            row = numrows - 1;
                        }
                        int index = row * CustomMaxColumns + col;
                        if (index < _CustomColors.Count)
                        {
                            ColorSelectedEvent(_CustomColors[index].ColorBrush.Color, _CustomColors[index].IsEmptyOption);
                        }
                    }
                }
            }
        }

        #endregion Commands

    }

    #endregion Custom Colors

    public class AnnotationPropertiesViewModel : ViewModelBase
    {
        private static int MAX_PRESETS_COLUMNS = 6;
        private static int MAX_CUSTOM_COLUMNS = 9;
        private static double MIN_THICKNESS = 0.5;
        private static double MAX_THICKNESS = 12;
        private static double MIN_FONT_SIZE = 4;
        private static double MAX_FONT_SIZE = 72;
        private static double MIN_OPACITY = 0.05;
        private static double MAX_OPACITY = 1;
        public double MinThickness { get { return MIN_THICKNESS; } }
        public double MaxThickness { get { return MAX_THICKNESS; } }
        public double MinFontSize { get { return MIN_FONT_SIZE; } }
        public double MaxFontSize { get { return MAX_FONT_SIZE; } }
        public double MinOpacity { get { return MIN_OPACITY; } }
        public double MaxOpacity { get { return MAX_OPACITY; } }


        public static string StrokeBrushIcon = new string((char)0xE235, 1);
        public static string TextBrushIcon = new string((char)0x0050, 1);
        public static string FillBrushIcon = new string((char)0x005D, 1);

        private static IList<string> _ThicknessOptions;
        private static IList<string> _OpacityOptions;
        private static IList<string> _FontSizeOptions;
        private static IList<string> ThicknessOptions
        {
            get
            {
                if (_ThicknessOptions == null)
                {
                    _ThicknessOptions = new List<string>();
                    _ThicknessOptions.Add("1");
                    _ThicknessOptions.Add("3");
                    _ThicknessOptions.Add("7");
                    _ThicknessOptions.Add("12");
                }
                return _ThicknessOptions;
            }
        }
        private static IList<string> OpacityOptions
        {
            get
            {
                if (_OpacityOptions == null)
                {
                    _OpacityOptions = new List<string>();
                    _OpacityOptions.Add("25");
                    _OpacityOptions.Add("50");
                    _OpacityOptions.Add("75");
                    _OpacityOptions.Add("100");
                }
                return _OpacityOptions;
            }
        }
        private static IList<string> FontSizeOptions
        {
            get
            {
                if (_FontSizeOptions == null)
                {
                    _FontSizeOptions = new List<string>();
                    _FontSizeOptions.Add("8");
                    _FontSizeOptions.Add("12");
                    _FontSizeOptions.Add("24");
                    _FontSizeOptions.Add("36");
                }
                return _FontSizeOptions;
            }
        }

        private string _ItemTag = "";
        public string ItemTag
        {
            get { return _ItemTag; }
            set 
            { 
                _ItemTag = value;
            }
        }

        private ToolType _AssociatedTool = ToolType.e_none;
        /// <summary>
        /// Please set the AnnotationProperties before you set this.
        /// </summary>
        public ToolType AssociatedTool
        {
            get { return _AssociatedTool; }
            set
            {
                _AssociatedTool = value;
                Init();
            }
        }

        private string _SettingString = "";

        public AnnotationPropertiesViewModel()
        {
            CreateCommands();
        }

        // things that need to be initialized before control is created.
        private void CreateCommands()
        {
            PresetOrCustomCommand = new RelayCommand(PresetOrCustomCommandImpl);
            PresetColorTargetCommand = new RelayCommand(PresetColorTargetCommandImpl);
            CustomColorTargetCommand = new RelayCommand(CustomColorTargetCommandImpl);
            NewColorAcceptCommand = new RelayCommand(NewColorAccpetCommandImpl);
            NewColorCancelCommand = new RelayCommand(NewColorCancelCommandImpl);
        }

        // things that need to be initialized once we have a tool type
        private void Init()
        {
            _SettingString = AssociatedTool.ToString() + ItemTag;
            if (Sequencenumber >= 0)
            {
                ActiveSubView = (SubviewStates)Settings.GetView(AssociatedTool, (int)(SubviewStates.presets), Sequencenumber);
            }
            else
            {
                ActiveSubView = (SubviewStates)Settings.GetView(AssociatedTool, (int)(SubviewStates.presets));
            }
            ResolveOptions(AssociatedTool);
            ResolvePreview(AssociatedTool);
        }

        private void ResolveOptions(ToolType toolType)
        {
            switch (toolType)
            {
                case ToolType.e_rect_create:
                case ToolType.e_oval_create:
                case ToolType.e_polygon_placeholder:
                    SecondaryColorType = ColorTypes.fill;
                    break;
                case ToolType.e_text_highlight:
                    HasThickness = false;
                    break;
                case ToolType.e_text_annot_create:
                    HasThickness = false;
                    PrimaryColorType = ColorTypes.text;
                    SecondaryColorType = ColorTypes.fill;
                    HasFontSize = true;
                    break;
            }

            PrimaryColorOption = new AnnotationPropertiesPresetsColorGridViewModel(toolType, false, MAX_PRESETS_COLUMNS);
            PrimaryColorOption.ColorSelectedEvent += PrimaryColorOption_ColorSelectedEvent;
            PrimaryColorOption.RequestNewColor += ColorOption_RequestNewColor;
            RaisePropertyChanged("PrimaryColorOption");
            PrimaryCustomColorOption = new AnnotationPropertiesCustomColorGridViewModel(false, MAX_CUSTOM_COLUMNS, Settings.GetCustomColors());
            PrimaryCustomColorOption.ColorSelectedEvent += PrimaryColorOption_ColorSelectedEvent;
            RaisePropertyChanged("PrimaryCustomColorOption");
            SelectMainColor();
            if (SecondaryColorType != ColorTypes.none)
            {
                SecondaryColorOption = new AnnotationPropertiesPresetsColorGridViewModel(toolType, true, MAX_PRESETS_COLUMNS);
                SecondaryColorOption.ColorSelectedEvent += SecondaryColorOption_ColorSelectedEvent;
                SecondaryColorOption.RequestNewColor += ColorOption_RequestNewColor;
                RaisePropertyChanged("SecondaryColorOption");
                SecondaryCustomColorOption = new AnnotationPropertiesCustomColorGridViewModel(true, MAX_CUSTOM_COLUMNS, Settings.GetCustomColors());
                SecondaryCustomColorOption.ColorSelectedEvent += SecondaryColorOption_ColorSelectedEvent;
                RaisePropertyChanged("SecondaryCustomColorOption");
                SelectFillColor();
            }

            if (HasThickness)
            {
                ThicknessButtonViewModel = new PresetButtonViewModel(ThicknessOptions);
                SelectThickness();
                ThicknessButtonViewModel.OptionSelected += ThicknessButtonViewModel_OptionSelected;
                RaisePropertyChanged("ThicknessButtonViewModel");
            }
            if (HasOpacity)
            {
                OpacityButtonViewModel = new PresetButtonViewModel(OpacityOptions);
                SelectOpacity();
                OpacityButtonViewModel.OptionSelected += OpacityButtonViewModel_OptionSelected;
                RaisePropertyChanged("OpacityButtonViewModel");
            }
            if (HasFontSize)
            {
                FontSizeButtonViewModel = new PresetButtonViewModel(FontSizeOptions);
                SelectFontSize();
                FontSizeButtonViewModel.OptionSelected += FontSizeButtonViewModel_OptionSelected;
                RaisePropertyChanged("FontSizeButtonViewModel");
            }
        }

        private void ResolvePreview(ToolType toolType)
        {
            switch (toolType)
            {
                case ToolType.e_line_create:
                case ToolType.e_arrow_create:
                case ToolType.e_polyline_placeholder:
                    PreviewType = PreviewTypes.DiagonalLine;
                    break;
                case ToolType.e_ink_create:
                    PreviewType = PreviewTypes.CurvedLine;
                    break;
                case ToolType.e_rect_create:
                case ToolType.e_polygon_placeholder:
                    PreviewType = PreviewTypes.Rectangle;
                    break;
                case ToolType.e_oval_create:
                    PreviewType = PreviewTypes.Ellipse;
                    break;
                case ToolType.e_text_underline:
                case ToolType.e_text_squiggly:
                case ToolType.e_text_strikeout:
                    PreviewType = PreviewTypes.HorizontalLine;
                    break;
                case ToolType.e_text_highlight:
                    PreviewType = PreviewTypes.HighlightedText;
                    break;
                case ToolType.e_text_annot_create:
                    PreviewType = PreviewTypes.Text;
                    break;
                default:
                    PreviewType = PreviewTypes.DiagonalLine;
                    break;
            }
        }

        void ThicknessButtonViewModel_OptionSelected(string option)
        {
            double result = 0;
            bool parsed = double.TryParse(option, out result);
            if (parsed)
            {
                ThicknessInternal = result;
            }
        }

        void OpacityButtonViewModel_OptionSelected(string option)
        {
            if (option.Length < 1)
            {
                return;
            }
            double result = 0;
            bool parsed = double.TryParse(option, out result);
            if (parsed)
            {
                OpacityInternal = result / 100.0;
            }
        }

        void FontSizeButtonViewModel_OptionSelected(string option)
        {
            double result = 0;
            bool parsed = double.TryParse(option, out result);
            if (parsed)
            {
                FontSizeInternal = result;
            }
        }

        private void SelectMainColor()
        {
            SettingsColor settingsColor;
            if (_MainColorSet)
            {
                settingsColor = MainColor;
            }
            else
            {
                MainColor = settingsColor = Settings.GetShapeColor(AssociatedTool, false);
            }
            PrimaryColorOption.Select(UtilityFunctions.ConvertToColor(settingsColor));
            PrimaryCustomColorOption.Select(UtilityFunctions.ConvertToColor(settingsColor));
        }

        private void SelectFillColor()
        {
            SettingsColor settingsColor;
            if (_FillColorSet)
            {
                settingsColor = FillColor;
            }
            else
            {
                FillColor = settingsColor = Settings.GetShapeColor(AssociatedTool, true);
            }
            SecondaryColorOption.Select(UtilityFunctions.ConvertToColor(settingsColor));
            SecondaryCustomColorOption.Select(UtilityFunctions.ConvertToColor(settingsColor));
        }

        private void SelectThickness()
        {
            if (Thickness < 0) // means it's not been set, so grab default
            {
                Thickness = Settings.GetThickness(AssociatedTool, Sequencenumber);
            }
            if (ThicknessButtonViewModel != null)
            {
                ThicknessButtonViewModel.Select("" + Thickness);
            }
        }

        private void SelectOpacity()
        {
            if (Opacity < 0) // means it's not been set, so grab default
            {
                Opacity = Settings.GetOpacity(AssociatedTool, Sequencenumber);
            }
            if (OpacityButtonViewModel != null)
            {
                OpacityButtonViewModel.Select("" + (int)((Opacity * 100) + 0.5));
            }
        }

        private void SelectFontSize()
        {
            if (FontSize < 0) // means it's not been set, so grab default
            {
                FontSize = Settings.GetFontSize(AssociatedTool, Sequencenumber);
            }
            if (FontSizeButtonViewModel != null)
            {
                FontSizeButtonViewModel.Select("" + FontSize);
            }
        }

        #region Annotation Properties Views

        public AnnotationPropertiesPresetsColorGridViewModel PrimaryColorOption { get; private set; }
        public AnnotationPropertiesPresetsColorGridViewModel SecondaryColorOption { get; private set; }
        public AnnotationPropertiesCustomColorGridViewModel PrimaryCustomColorOption { get; private set; }
        public AnnotationPropertiesCustomColorGridViewModel SecondaryCustomColorOption { get; private set; }
        public AnnotationPropertiesCustomColorGridViewModel _AddNewColorOptions = null;
        public AnnotationPropertiesCustomColorGridViewModel AddNewColorOptions
        {
            get { return _AddNewColorOptions; }
            set
            {
                if (value != _AddNewColorOptions)
                {
                    _AddNewColorOptions = value;
                    RaisePropertyChanged();
                }
            }
        }

        void PrimaryColorOption_ColorSelectedEvent(Color color, bool isEmpty)
        {
            MainColorInternal = new SettingsColor(color.R, color.G, color.B, !isEmpty);
            SelectMainColor();
        }

        void SecondaryColorOption_ColorSelectedEvent(Color color, bool isEmpty)
        {
            if (isEmpty)
            {
                FillColorInternal = new SettingsColor(0, 0, 0, false);
            }
            else
            {
                FillColorInternal = new SettingsColor(color.R, color.G, color.B, true);
            }
            SelectFillColor();
        }

        public PresetButtonViewModel ThicknessButtonViewModel { get; private set; }
        public PresetButtonViewModel OpacityButtonViewModel { get; private set; }
        public PresetButtonViewModel FontSizeButtonViewModel { get; set; }

        #endregion Annotation Properties Views

        #region Add a new Color

        private bool _NewColorSelected = false;
        public bool NewColorSelected
        {
            get { return _NewColorSelected; }
            set
            {
                if (value != _NewColorSelected)
                {
                    _NewColorSelected = value;
                    RaisePropertyChanged();
                }
            }
        }

        private Color _NewColor = Colors.Transparent;

        void ColorOption_RequestNewColor(IList<Color> existingColors)
        {
            AddNewColorOptions = new AnnotationPropertiesCustomColorGridViewModel(false, MAX_CUSTOM_COLUMNS, Settings.GetCustomColors());
            AddNewColorOptions.ColorSelectedEvent += AddNewColorOptions_ColorSelectedEvent;
            NewColorSelected = false;
            _NewColor = Colors.Transparent;
            //AddNewColorOptions.DisableColors(existingColors);
            ActiveSubView = SubviewStates.addnew;
        }

        private void AddNewColorOptions_ColorSelectedEvent(Color color, bool isEmpty)
        {
            AddNewColorOptions.Select(color);
            NewColorSelected = true;
            _NewColor = color;
        }

        public RelayCommand NewColorAcceptCommand { get; private set; }
        public RelayCommand NewColorCancelCommand { get; private set; }

        private void NewColorAccpetCommandImpl(object sender)
        {
            ActiveSubView = SubviewStates.presets;
            if (PresetColorTarget == ColorTarget.primary)
            {
                PrimaryColorOption.AddNewColor(_NewColor);
            }
            else
            {
                SecondaryColorOption.AddNewColor(_NewColor);
            }
        }

        private void NewColorCancelCommandImpl(object sender)
        {
            ActiveSubView = SubviewStates.presets;
        }

        #endregion Add a new Color

        #region AnnotationProperties

        private bool _PropertyWasUpdated = false;
        public bool PropertyWasUpdated { 
            get { return _PropertyWasUpdated; }
            private set { _PropertyWasUpdated = value; }
        }

        private bool _MainColorSet = false;
        private SettingsColor _MainColor;
        public SettingsColor MainColor
        {
            get { return _MainColor; }
            set 
            { 
                _MainColorSet = true; 
                _MainColor = value;
                RaisePropertyChanged("PrimaryBrush");
            }
        }

        private SettingsColor MainColorInternal
        {
            set
            {
                MainColor = value;
                PropertyWasUpdated = true;
                if (_SequenceNumber >= 0)
                {
                    Settings.SetShapeColor(AssociatedTool, false, MainColor, _SequenceNumber);
                }
                else
                {
                    Settings.SetShapeColor(AssociatedTool, false, MainColor);
                }
            }
        }

        private bool _FillColorSet = false;
        private SettingsColor _FillColor;
        /// <summary>
        /// Doubles as background color for FreeTextAnnotations
        /// </summary>
        public SettingsColor FillColor
        {
            get { return _FillColor; }
            set 
            {
                _FillColorSet = true;
                _FillColor = value;
                RaisePropertyChanged("SecondaryBrush");
            }
        }

        private SettingsColor FillColorInternal
        {
            set
            {
                FillColor = value;
                PropertyWasUpdated = true;
                if (_SequenceNumber >= 0)
                {
                    Settings.SetShapeColor(AssociatedTool, true, FillColor, _SequenceNumber);
                }
                else
                {
                    Settings.SetShapeColor(AssociatedTool, true, FillColor);
                }
            }
        }

        private double _Opacity = -1;
        public double Opacity
        {
            get { return _Opacity; }
            set 
            {
                if (value != _Opacity)
                {
                    _Opacity = value;
                    RaisePropertyChanged();
                    SelectOpacity();
                }
            }
        }

        public double OpacityInternal
        {
            get { return _Opacity; }
            set
            {
                Opacity = value;
                PropertyWasUpdated = true;
                if (_SequenceNumber >= 0)
                {
                    Settings.SetOpacity(AssociatedTool, Opacity, _SequenceNumber);
                }
                else
                {
                    Settings.SetOpacity(AssociatedTool, Opacity);
                }
                RaisePropertyChanged();
            }
        }

        private double _Thickness = -1;
        public double Thickness
        {
            get { return _Thickness; }
            set 
            {
                if (value != _Thickness)
                {
                    _Thickness = value;
                    RaisePropertyChanged();
                    SelectThickness();
                }
            }
        }

        public double ThicknessInternal
        {
            get { return _Thickness; }
            set
            {
                Thickness = value;
                PropertyWasUpdated = true;
                if (_SequenceNumber >= 0)
                {
                    Settings.SetThickness(AssociatedTool, Thickness, _SequenceNumber);
                }
                else
                {
                    Settings.SetThickness(AssociatedTool, Thickness);
                }
                RaisePropertyChanged();
            }
        }

        private double _FontSize = -1;
        public double FontSize
        {
            get { return _FontSize; }
            set 
            {
                if (value != _FontSize)
                {
                    _FontSize = value;
                    RaisePropertyChanged();
                    SelectFontSize();
                }
            }
        }


        public double FontSizeInternal
        {
            get { return _FontSize; }
            set
            {
                FontSize = value;
                PropertyWasUpdated = true;
                if (_SequenceNumber >= 0)
                {
                    Settings.SetFontSize(AssociatedTool, FontSize, _SequenceNumber);
                }
                else
                {
                    Settings.SetFontSize(AssociatedTool, FontSize);
                }
                RaisePropertyChanged();
            }
        }

        private int _SequenceNumber = -1;
        /// <summary>
        /// Sets a unique identifier for the purpose of the settings, making it possible to store multiple
        /// settings for the same tooltype. Anything above 0 will be identified by this value.
        /// </summary>
        public int Sequencenumber
        {
            get { return _SequenceNumber; }
            set { _SequenceNumber = value; }
        }

        #endregion Annotation Properties

        #region Commands

        public RelayCommand PresetOrCustomCommand { get; private set; }
        public RelayCommand PresetColorTargetCommand { get; private set; }
        public RelayCommand CustomColorTargetCommand { get; private set; }


        private void PresetOrCustomCommandImpl(object desiredView)
        {
            string desired = desiredView as string;
            if (!string.IsNullOrWhiteSpace(desired))
            {
                if (desired.Equals("presets", StringComparison.OrdinalIgnoreCase))
                {
                    ActiveSubView = SubviewStates.presets;
                }
                else if (desired.Equals("custom", StringComparison.OrdinalIgnoreCase))
                {
                    ActiveSubView = SubviewStates.custom;
                }

                if (Sequencenumber >= 0)
                {
                    Settings.SetView(AssociatedTool, (int)ActiveSubView, Sequencenumber);
                }
                else
                {
                    Settings.SetView(AssociatedTool, (int)ActiveSubView);
                }
            }
        }

        private void PresetColorTargetCommandImpl(object target)
        {
            string targetString = target as string;
            if (!string.IsNullOrWhiteSpace(targetString))
            {
                if (targetString.Equals("primary", StringComparison.OrdinalIgnoreCase))
                {
                    PresetColorTarget = ColorTarget.primary;
                }
                else if (targetString.Equals("secondary", StringComparison.OrdinalIgnoreCase))
                {
                    PresetColorTarget = ColorTarget.secondary;
                }
            }
        }

        private void CustomColorTargetCommandImpl(object target)
        {
            string targetString = target as string;
            if (!string.IsNullOrWhiteSpace(targetString))
            {
                if (targetString.Equals("primary", StringComparison.OrdinalIgnoreCase))
                {
                    CustomColorTarget = ColorTarget.primary;
                }
                else if (targetString.Equals("secondary", StringComparison.OrdinalIgnoreCase))
                {
                    CustomColorTarget = ColorTarget.secondary;
                }
            }
        }

        #endregion Commands

        #region Visual Properties

        public enum SubviewStates
        {
            presets,
            custom,
            addnew,
        }

        public enum ColorTypes
        {
            stroke,
            fill,
            text,
            none,
        }

        public enum ColorTarget
        {
            primary,
            secondary,
        }

        public enum PreviewTypes
        {
            HorizontalLine,
            DiagonalLine,
            CurvedLine,
            Rectangle,
            Ellipse,
            Text,
            HighlightedText,
        }

        private SubviewStates _ActiveSubView = SubviewStates.presets;
        public SubviewStates ActiveSubView
        {
            get { return _ActiveSubView; }
            set
            {
                if (_ActiveSubView != value)
                {
                    _ActiveSubView = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("AreSubViewSelectionButtonsVisible");
                }
            }
        }

        public bool AreSubViewSelectionButtonsVisible
        {
            get { return _ActiveSubView != SubviewStates.addnew; }
        }

        public bool HasSecondaryColor
        {
            get { return _SecondaryColorType != ColorTypes.none; }
        }

        private ColorTypes _PrimaryColorType = ColorTypes.stroke;
        public ColorTypes PrimaryColorType
        {
            get { return _PrimaryColorType; }
            set
            {
                if (_PrimaryColorType != value)
                {
                    _PrimaryColorType = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string PrimaryColorTypeIcon
        {
            get
            {
                if (_PrimaryColorType == ColorTypes.text)
                {
                    return TextBrushIcon;
                }
                return StrokeBrushIcon;
            }
        }

        private ColorTypes _SecondaryColorType = ColorTypes.none;
        public ColorTypes SecondaryColorType
        {
            get { return _SecondaryColorType; }
            set
            {
                if (_SecondaryColorType != value)
                {
                    _SecondaryColorType = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("HasSecondaryColor");
                }
            }
        }

        private ColorTarget _PresetColorTarget = ColorTarget.primary;
        public ColorTarget PresetColorTarget
        {
            get { return _PresetColorTarget; }
            set
            {
                if (_PresetColorTarget != value)
                {
                    _PresetColorTarget = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ColorTarget _CustomColorTarget = ColorTarget.primary;
        public ColorTarget CustomColorTarget
        {
            get { return _CustomColorTarget; }
            set
            {
                if (_CustomColorTarget != value)
                {
                    _CustomColorTarget = value;
                    RaisePropertyChanged();
                }
            }
        }

        private PreviewTypes _PreviewType = PreviewTypes.HorizontalLine;
        public PreviewTypes PreviewType
        {
            get { return _PreviewType; }
            set
            {
                if (value != _PreviewType)
                {
                    _PreviewType = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _HasThickness = true;
        public bool HasThickness
        {
            get { return _HasThickness; }
            set
            {
                if (_HasThickness != value)
                {
                    _HasThickness = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _HasOpacity = true;
        public bool HasOpacity
        {
            get { return _HasOpacity; }
            set
            {
                if (_HasOpacity != value)
                {
                    _HasOpacity = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _HasFontSize = false;
        public bool HasFontSize
        {
            get { return _HasFontSize; }
            set
            {
                if (_HasFontSize != value)
                {
                    _HasFontSize = value;
                    RaisePropertyChanged();
                }
            }
        }

        public SolidColorBrush PrimaryBrush
        {
            get
            {
                return new SolidColorBrush(Color.FromArgb(255, MainColor.R, MainColor.G, MainColor.B));
            }
        }

        public SolidColorBrush SecondaryBrush
        {
            get
            {
                byte alpha = 0;
                if (FillColor.Use)
                {
                    alpha = 255;
                }
                return new SolidColorBrush(Color.FromArgb(alpha, FillColor.R, FillColor.G, FillColor.B));
            }
        }

        #endregion Visual Properties
    }

    public class AnnotationPropertiesDisplay
    {
        public delegate void AnnotationPropertiesDisplayClosedHandler(AnnotationPropertyDialog dialog);
        public event AnnotationPropertiesDisplayClosedHandler AnnotationPropertyDialogClosed = delegate { };

        private Windows.UI.Xaml.Controls.Primitives.Popup _Popup;
        private UIRect _Target;
        private UIRect _TargetNormalizedToWindow;
        private ToolType _ToolType;
        private AnnotationPropertyDialog _Dialog;
        private Placement _Placement = Placement.Below;
        private CommitedPositions _CommitedPosition = CommitedPositions.None;
        private SizeChangedEventHandler _SizeChangedHandler;
        private EventHandler<object> _PopupClosedHandler;

        public enum Placement
        {
            Below,
            AlignTop,
            PreferBeside,
            PreferAboveOrBelow,
        }

        private enum CommitedPositions
        {
            None,
            Below,
            Above,
            Side,
        }

        private AnnotationPropertiesDisplay()
        {

        }

        public static AnnotationPropertiesDisplay CreateAnnotationPropertiesDisplay(UIRect position, AnnotationPropertyDialog dialog,
            Placement placement = Placement.Below)
        {
            AnnotationPropertiesDisplay display = new AnnotationPropertiesDisplay();
            display._Target = position;
            display._TargetNormalizedToWindow = position;
            //display._TargetNormalizedToWindow = Window.Current.Bounds.Left; // need to push it by the window bounds
            display._Dialog = dialog;
            display._Placement = placement;
            display.PlacePopup();

            dialog.HorizontalAlignment = HorizontalAlignment.Left;
            dialog.VerticalAlignment = VerticalAlignment.Top;

            return display;
        }

        private void PlacePopup()
        {
            _Popup = new Windows.UI.Xaml.Controls.Primitives.Popup();
            _Popup.Width = Window.Current.Bounds.Width;
            _Popup.Height = Window.Current.Bounds.Height;

            Grid g = new Grid();
            g.Width = _Popup.Width;
            g.Height = _Popup.Height;

            _Popup.Child = g;
            g.Children.Add(_Dialog);

            _Popup.IsLightDismissEnabled = true;
            _Popup.IsOpen = true;

            _PopupClosedHandler = new EventHandler<object>(Popup_Closed);
            _Popup.Closed += _PopupClosedHandler;

            _SizeChangedHandler = new SizeChangedEventHandler(PropertiesDialog_SizeChanged);
            _Dialog.SizeChanged += _SizeChangedHandler;

            _Dialog.MaxWidth = _Popup.Width;
            _Dialog.MaxHeight = _Popup.Height;
        }

        void Popup_Closed(object sender, object e)
        {
            _Dialog.SizeChanged -= _SizeChangedHandler;
            _Popup.Closed -= _PopupClosedHandler;
            AnnotationPropertyDialogClosed(_Dialog);
            _Popup = null;
            _Dialog = null;
        }

        void PropertiesDialog_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UIRect bounds = GetNormalizedWindowsBounds();

            if (_CommitedPosition == CommitedPositions.None)
            {
                if (_Placement == Placement.Below)
                {
                    PositionDialogBelow(e.NewSize);
                }
                else if (_Placement == Placement.AlignTop)
                {
                    PositionDialogAlignedTop(e.NewSize);
                }
                else if (_Placement == Placement.PreferBeside)
                {
                    PositionDialogPreferSide(e.NewSize, true);
                }
                else if (_Placement == Placement.PreferAboveOrBelow)
                {
                    PositionDialogPreferAboveOrBelow(e.NewSize, true);
                }
            }
            else if (_CommitedPosition == CommitedPositions.Side)
            {
                if (_Dialog.VerticalAlignment == VerticalAlignment.Top && _Dialog.Margin.Top + e.NewSize.Height + 1 > bounds.Height)
                {
                    // this means our position is determined by the bottom of the target, we want to instead use the bottom of the screen as the dialog is bigger than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, 0, 0, 0);
                }
                else if (_Dialog.VerticalAlignment == VerticalAlignment.Bottom && e.NewSize.Height + 1 < bounds.Bottom - _Target.Top)
                {
                    // this means our position is determined by the bottom of the screen, we want to instead use the bottom of the target as the dialog is smaller than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Top;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, _Target.Top, 0, 0);
                }
            }
            else if (_CommitedPosition == CommitedPositions.Below)
            {
                if (_Dialog.VerticalAlignment == VerticalAlignment.Top && _Dialog.Margin.Top + e.NewSize.Height + 1 > bounds.Height)
                {
                    // this means our position is determined by the bottom of the target, we want to instead use the bottom of the screen as the dialog is bigger than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, 0, 0, 0);
                }
                else if (_Dialog.VerticalAlignment == VerticalAlignment.Bottom && e.NewSize.Height + 1 < bounds.Bottom - _Target.Bottom)
                {
                    // this means our position is determined by the bottom of the screen, we want to instead use the bottom of the target as the dialog is smaller than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Top;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, _Target.Bottom, 0, 0);
                }
            }
            else // above
            {
                if (_Dialog.VerticalAlignment == VerticalAlignment.Bottom && _Dialog.Margin.Bottom + e.NewSize.Height + 1 > bounds.Height)
                {
                    // this means our position is determined by the top of the target, we want to instead use the top of the screen as the dialog is bigger than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Top;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, 0, 0, 0);
                }
                else if (_Dialog.VerticalAlignment == VerticalAlignment.Top && e.NewSize.Height + 1 < _Target.Top - bounds.Top)
                {
                    // this means our position is determined by the top of the screen, we want to instead use the top of the target as the dialog is smaller than that gap.
                    _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                    _Dialog.Margin = new Thickness(_Dialog.Margin.Left, 0, 0, bounds.Bottom - _Target.Top);
                }
            }
        }

        /// <summary>
        /// Will place the dialog below the target, or as far down as possible if it does not fit otherwise.
        /// </summary>
        /// <param name="dialogSize"></param>
        private void PositionDialogBelow(Windows.Foundation.Size dialogSize)
        {
            UIRect bounds = GetNormalizedWindowsBounds();

            // position vertically
            double verticalOffset = 0;
            if (dialogSize.Height + 1 >= bounds.Bottom - _Target.Bottom)
            {
                _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
            }
            else
            {
                _Dialog.VerticalAlignment = VerticalAlignment.Top;
                verticalOffset = _Target.Bottom;
            }

            // position horizontally
            double center = (_Target.Left + _Target.Right) / 2;
            double halfWidth = dialogSize.Width / 2;
            double horizontalOffset = 0;
            if (center + halfWidth > bounds.Right)
            {
                _Dialog.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                _Dialog.HorizontalAlignment = HorizontalAlignment.Left;
                horizontalOffset = center - halfWidth;
                if (horizontalOffset < 0)
                {
                    horizontalOffset = 0;
                }
            }
            _Dialog.Margin = new Thickness(horizontalOffset, verticalOffset, 0, 0);
        }

        /// <summary>
        /// Will place the dialog so that it aligns with the top of the target. It will place it
        /// to the side of the target, with preference to the right side, but will use the side with the most space.
        /// </summary>
        /// <param name="dialogSize"></param>
        private void PositionDialogAlignedTop(Windows.Foundation.Size dialogSize)
        {
            UIRect bounds = GetNormalizedWindowsBounds();
            // position vertically
            double verticalOffset = _Target.Top;
            _Dialog.VerticalAlignment = VerticalAlignment.Top;
            
            // position horizontally
            double horizontalOffset = 0;
            _Dialog.HorizontalAlignment = HorizontalAlignment.Left;
            if (bounds.Right - _Target.Right >= dialogSize.Width)
            {
                horizontalOffset = _Target.Right;
            }
            else if (_Target.Left - bounds.Left >= dialogSize.Width)
            {
                horizontalOffset = _Target.Left - dialogSize.Width;
            }
            else
            {
                if (bounds.Right - _Target.Right >= _Target.Left - bounds.Left)
                {
                    _Dialog.HorizontalAlignment = HorizontalAlignment.Right;
                }
                else
                {
                    _Dialog.HorizontalAlignment = HorizontalAlignment.Left;
                }
            }

            _Dialog.Margin = new Thickness(horizontalOffset, verticalOffset, 0, 0);
        }

        /// <summary>
        /// Will place the dialog to the side of the target if it fits completely, otherwise, it will put it above or below.
        /// If it doesn't fit anywhere, it will place it to the side with the most room.
        /// </summary>
        /// <returns>True if the dialog was placed</returns>
        /// <param name="dialogSize"></param>
        /// <param name="force">Forces the dialog to be placed. It will first try above or below though</param>
        private bool PositionDialogPreferSide(Windows.Foundation.Size dialogSize, bool force = false)
        {
            UIRect bounds = GetNormalizedWindowsBounds();

            bool placed = false;
            bool fitsHorizontally = false;
            // position vertically
            double verticalOffset = _Target.Top;
            _Dialog.VerticalAlignment = VerticalAlignment.Top;

            // position horizontally
            double horizontalOffset = 0;
            _Dialog.HorizontalAlignment = HorizontalAlignment.Left;
            if (bounds.Right - _Target.Right >= dialogSize.Width)
            {
                horizontalOffset = _Target.Right;
                fitsHorizontally = true;
                _CommitedPosition = CommitedPositions.Side;
            }
            else if (_Target.Left - bounds.Left >= dialogSize.Width)
            {
                horizontalOffset = _Target.Left - dialogSize.Width;
                fitsHorizontally = true;
                _CommitedPosition = CommitedPositions.Side;
            }
            else if (force)
            {
                placed = PositionDialogPreferAboveOrBelow(dialogSize);

                if (!placed)
                {
                    _CommitedPosition = CommitedPositions.Side;
                    if (bounds.Right - _Target.Right >= _Target.Left - bounds.Left)
                    {
                        _Dialog.HorizontalAlignment = HorizontalAlignment.Right;
                    }
                    else
                    {
                        _Dialog.HorizontalAlignment = HorizontalAlignment.Left;
                    }
                }
                fitsHorizontally = true;
            }

            if (fitsHorizontally && !placed)
            {
                if (dialogSize.Height > bounds.Bottom - _Target.Top)
                {
                    verticalOffset = 0;
                    _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                    //verticalOffset = bounds.Bottom - dialogSize.Height;
                    //if (verticalOffset < bounds.Top)
                    //{
                    //    verticalOffset = bounds.Top;
                    //}
                }
                placed = true;
                _Dialog.Margin = new Thickness(horizontalOffset, verticalOffset, 0, 0);
            }

            return placed;
        }

        /// <summary>
        /// Tries to position the dialog below the target, but if it doesn't fit will go above.
        /// If it can't fit the rectangle either above or below, it will return false.
        /// 
        /// If force is set to true, then it will see if it can place it to the side, and if that doesn't work, it will place it above or below, wherever there is more room.
        /// </summary>
        /// <param name="dialogSize"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        private bool PositionDialogPreferAboveOrBelow(Windows.Foundation.Size dialogSize, bool force = false)
        {
            UIRect bounds = GetNormalizedWindowsBounds();
            bool placed = false;
            bool fitsVertically = false;

            // position horizontally
            double horizontalOffset = _Target.Left;
            _Dialog.HorizontalAlignment = HorizontalAlignment.Left;

            // position vertically
            double verticalOffset = 0;
            _Dialog.VerticalAlignment = VerticalAlignment.Top;

            if (bounds.Bottom - _Target.Bottom >= dialogSize.Height)
            {
                verticalOffset = _Target.Bottom;
                fitsVertically = true;
                _CommitedPosition = CommitedPositions.Below;
            }
            else if (_Target.Top - bounds.Top >= dialogSize.Height)
            {
                _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                verticalOffset = bounds.Bottom - _Target.Top;
                fitsVertically = true;
                _CommitedPosition = CommitedPositions.Above;
            }
            else if (force)
            {
                placed = PositionDialogPreferSide(dialogSize);

                if (!placed)
                {
                    if (bounds.Bottom - _Target.Bottom >= _Target.Top - bounds.Top)
                    {
                        _CommitedPosition = CommitedPositions.Below;
                        _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
                    }
                    else
                    {
                        _CommitedPosition = CommitedPositions.Above;
                        _Dialog.VerticalAlignment = VerticalAlignment.Top;
                    }
                }
                fitsVertically = true;
            }

            if (fitsVertically && !placed)
            {
                if (dialogSize.Width > bounds.Right - _Target.Left)
                {
                    horizontalOffset = bounds.Right - dialogSize.Width;
                    if (horizontalOffset < bounds.Left)
                    {
                        horizontalOffset = bounds.Left;
                    }
                }
                
                placed = true;
                if (_Dialog.VerticalAlignment == VerticalAlignment.Bottom)
                {
                    _Dialog.Margin = new Thickness(horizontalOffset, 0, 0, verticalOffset);
                }
                else
                {
                    _Dialog.Margin = new Thickness(horizontalOffset, verticalOffset, 0, 0);
                }
                
            }

            return placed;
        }

        private UIRect GetNormalizedWindowsBounds()
        {
            UIRect bounds = Window.Current.Bounds;
            bounds.X = 0;
            bounds.Y = 0;
            return bounds;
        }

    }
}

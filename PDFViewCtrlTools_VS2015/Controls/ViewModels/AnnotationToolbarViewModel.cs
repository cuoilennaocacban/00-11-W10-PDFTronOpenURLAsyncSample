using System;
using System.Threading.Tasks;
using pdftron.PDF.Tools.Controls.ViewModels.Common;
using pdftron.PDF.Tools.Controls.ControlBase;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using UIRect = Windows.Foundation.Rect;
using Windows.UI.Xaml.Media;

namespace pdftron.PDF.Tools.Controls.ViewModels 
{
    public enum SelectedTool
    {
        Pan,
        Line,
        Arrow,
        Square,
        Oval,
        Ink,
        Eraser,
        TextBox,
        Sticky,
        Signature,
        Highlight,
        Underline,
        Strikeout,
        Squiggly,
        None,
    }
    

    class AnnotationToolbarViewModel : ViewModelBase
    {
        ICloseableControl _View;

#region Commands
        public RelayCommand ToolButtonClickedCommand {get; private set; }
        public RelayCommand InkToolbarCommand { get; private set; }
        public RelayCommand InkPresetButtonCommand { get; private set; }

#endregion Commands


        private ToolManager _ToolManager;
        /// <summary>
        /// Gets or sets the ToolManager
        /// </summary>
        internal ToolManager ToolManager
        {
            get { return _ToolManager; }
            set
            {
                if (_ToolManager != null)
                {
                    _ToolManager.NewToolCreated -= _ToolManager_NewToolCreated;
                }
                _ToolManager = value;
                if (_ToolManager != null)
                {
                    _ToolManager.NewToolCreated += _ToolManager_NewToolCreated;
                }
            }
        }

        private bool _ButtonsStayDown;
        /// <summary>
        /// Gets or sets whether or not the buttons stay down once pressed.
        /// When set to true, the toolbar will not switch back to the pan tool after each 
        /// annotation is drawn.
        /// </summary>
        internal bool ButtonsStayDown
        {
            get { return _ButtonsStayDown; }
            set
            {
                _ButtonsStayDown = value;
                if (_ToolManager != null)
                {
                    _ToolManager.CurrentTool.ReturnToPanModeWhenFinished = !value;
                }
            }
        }

        #region XAML properties

        public bool _ShowInkToolbar = false;
        public bool ShowInkToolbar
        {
            get { return _ShowInkToolbar; }
            set
            {
                if (value != _ShowInkToolbar)
                {
                    _ShowInkToolbar = value;
                    RaisePropertyChanged();

                    if (value)
                    {
                        SetInkPropertiesFromLabel();
                    }
                }
            }
        }

        public bool _ShowMainToolbar = false;
        public bool ShowMainToolbar
        {
            get { return _ShowMainToolbar; }
            set
            {
                if (value != _ShowMainToolbar)
                {
                    _ShowMainToolbar = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _CanRedoInk = false;
        public bool CanRedoInk
        {
            get { return _CanRedoInk; }
            set
            {
                if (value != _CanRedoInk)
                {
                    _CanRedoInk = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _CanSaveInk = false;
        public bool CanSaveInk
        {
            get { return _CanSaveInk; }
            set
            {
                if (value != _CanSaveInk)
                {
                    _CanSaveInk = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _GotToolFromToolManager = false;
        private ToolType _CurrentTool = ToolType.e_pan;
        public ToolType CurrentTool
        {
            get { return _CurrentTool; }
            set
            {
                if (_CurrentTool != value)
                {
                    _CurrentTool = value;
                    if (!_GotToolFromToolManager)
                    {
                        SetCurrentTool();
                    }
                    RaisePropertyChanged();
                    RaisePropertyChanged("PanSelected");
                }
            }
        }

        public bool PanSelected { get { return _CurrentTool == ToolType.e_pan; } }

        #endregion XAML properties

        private FreehandCreate _CurrentInkTool;

        /// <summary>
        /// Creates a new AnnotationToolbar that is not associated with a ToolManager.
        /// Use the ToolManager property to make the AnnotationToolbar Work with a ToolManager.
        /// </summary>
        public AnnotationToolbarViewModel(ICloseableControl view)
        {
            _View = view;
            Init();
        }

        /// <summary>
        /// Creates a new AnnotationToolbar that works with the ToolManager.
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="toolManager"></param>
        public AnnotationToolbarViewModel(ICloseableControl view, ToolManager toolManager)
        {
            _View = view;
            Init();
            this.ToolManager = toolManager;
        }

        private void Init()
        {
            // set up relay commands
            ToolButtonClickedCommand = new RelayCommand(ToolButtonClickedCommandImpl);
            InkToolbarCommand = new RelayCommand(InkToolbarCommandImpl);
            InkPresetButtonCommand = new RelayCommand(InkPresetButtonCommandImpl);
            ReadyInkPresets();
        }

        public void ToolButtonClickedCommandImpl(object option)
        {
            Windows.UI.Xaml.Controls.Button button = option as Windows.UI.Xaml.Controls.Button;
            if (button != null)
            {
                string toolName = button.Tag as string;
                ToolType toolType = GetToolTypeFromString(toolName);
                if (CurrentTool == toolType)
                {
                    if (Settings.HasSettableProperties(CurrentTool) && CurrentTool != ToolType.e_ink_create)
                    {
                        ShowPopup(button);
                    }
                }
                else
                {
                    CurrentTool = toolType;
                    SetCurrentTool();
                }
            }
        }

        void _ToolManager_NewToolCreated(ToolType toolType)
        {
            if (toolType == _CurrentTool)
            {
                return;
            }
            _GotToolFromToolManager = true;
            CurrentTool = toolType;
            ShowInkToolbar = false;
            _GotToolFromToolManager = false;
        }

        ToolType FilterOutInvalidTools(ToolType toolType)
        {
            if (toolType == ToolType.e_annot_edit || toolType == ToolType.e_line_edit
                || toolType == ToolType.e_link_action || toolType == ToolType.e_none || toolType == ToolType.e_rich_media
                || toolType == ToolType.e_text_select)
            {
                return ToolType.e_pan;
            }
            return toolType;
        }

        #region InkToolbar

        private void CurrentInkTool_StrokeAdded()
        {
            CanSaveInk = true;
            CanRedoInk = false;
        }

        private void InkToolbarCommandImpl(Object option)
        {
            string opt = option as string;
            if (opt.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                _CurrentInkTool.CommitAnnotation();
                _ToolManager.CreateTool(ToolType.e_pan, null);
                ShowInkToolbar = false;

                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Annotation Toolbar", "Freehand Save button selected");
            }
            else if (opt.Equals("U", StringComparison.OrdinalIgnoreCase))
            {
                _CurrentInkTool.UndoStroke();
                if (!_CurrentInkTool.CanUndoStroke())
                {
                    CanSaveInk = false;
                }
                CanRedoInk = true;

                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Annotation Toolbar", "Freehand Undo button selected");
            }
            else if (opt.Equals("R", StringComparison.OrdinalIgnoreCase))
            {
                _CurrentInkTool.RedoStroke();
                if (!_CurrentInkTool.CanRedoStroke())
                {
                    CanRedoInk = false;
                }
                CanSaveInk = true;

                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Annotation Toolbar", "Freehand Redo button selected");
            }
            else
            {
                ShowInkToolbar = false;
                ToolManager.CreateTool(ToolType.e_pan, null);
                Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Annotation Toolbar", "Freehand Cancel button selected");
            }
        }

        private void ReadyInkPresets()
        {
            int label = Settings.GetPropertyLabel(ToolType.e_ink_create);
            CurrentPresetLabel = label;
            for (int i = 0; i < 3; i++)
            {
                SettingsColor color = Settings.GetShapeColor(ToolType.e_ink_create, false, i);
                _PresetBrushes[i] = new SolidColorBrush(UtilityFunctions.ConvertToColor(color));
            }
        }

        private int _CurrentPresetLabel = -1;
        public int CurrentPresetLabel
        {
            get { return _CurrentPresetLabel; }
            set
            {
                if (value != _CurrentPresetLabel)
                {
                    _CurrentPresetLabel = value;
                    SetInkPropertiesFromLabel();
                    RaisePropertyChanged();
                }
            }
        }

        private void SetInkPropertiesFromLabel()
        {
            SettingsColor color = Settings.GetShapeColor(ToolType.e_ink_create, false, _CurrentPresetLabel);
            Settings.SetShapeColor(ToolType.e_ink_create, false, color);
            switch (CurrentPresetLabel)
            {
                case 0:
                    PresetBrush0 = new SolidColorBrush(UtilityFunctions.ConvertToColor(color));
                    break;
                case 1:
                    PresetBrush1 = new SolidColorBrush(UtilityFunctions.ConvertToColor(color));
                    break;
                case 2:
                    PresetBrush2 = new SolidColorBrush(UtilityFunctions.ConvertToColor(color));
                    break;
            }
            double thickness = Settings.GetThickness(ToolType.e_ink_create, _CurrentPresetLabel);
            Settings.SetThickness(ToolType.e_ink_create, thickness);
            double opacity = Settings.GetOpacity(ToolType.e_ink_create, _CurrentPresetLabel);
            Settings.SetOpacity(ToolType.e_ink_create, opacity);

            if (_CurrentInkTool != null)
            {
                _CurrentInkTool.SetInkProperties(thickness, opacity, UtilityFunctions.ConvertToColor(color));
            }
        }

        private SolidColorBrush[] _PresetBrushes = new SolidColorBrush[3];
        public SolidColorBrush PresetBrush0
        {
            get { 
                return _PresetBrushes[0]; 
            }
            private set
            {
                _PresetBrushes[0] = value;
                RaisePropertyChanged();
            }
        }
        public SolidColorBrush PresetBrush1
        {
            get { return _PresetBrushes[1]; }
            private set
            {
                _PresetBrushes[1] = value;
                RaisePropertyChanged();
            }
        }
        public SolidColorBrush PresetBrush2
        {
            get { return _PresetBrushes[2]; }
            private set
            {
                _PresetBrushes[2] = value;
                RaisePropertyChanged();
            }
        }

        private void InkPresetButtonCommandImpl(object obj)
        {
            Button button = obj as Button;
            if (button != null)
            {
                string tag = button.Tag as string;
                if (!string.IsNullOrEmpty(tag))
                {
                    int result = -1;
                    bool success = int.TryParse(tag, out result);
                    if (success)
                    {
                        if (result == CurrentPresetLabel)
                        {
                            ShowPopup(button);
                        }
                        else
                        {
                            CurrentPresetLabel = result;
                        }
                    }
                }
            }
        }


        #endregion InkToolbar

        #region Utilities

        private ToolType GetToolTypeFromString(string option)
        {
            if (option.Equals("Pan", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_pan;
            }
            if (option.Equals("Line", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_line_create;
            }
            if (option.Equals("Arrow", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_arrow_create;
            }
            if (option.Equals("Square", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_rect_create;
            }
            if (option.Equals("Oval", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_oval_create;
            }
            if (option.Equals("Ink", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_ink_create;
            }
            if (option.Equals("Eraser", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_ink_eraser;
            }
            if (option.Equals("Signature", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_signature;
            }
            if (option.Equals("TextBox", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_text_annot_create;
            }
            if (option.Equals("Sticky", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_sticky_note_create;
            }
            if (option.Equals("Highlight", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_text_highlight;
            }
            if (option.Equals("Underline", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_text_underline;
            }
            if (option.Equals("Strikeout", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_text_strikeout;
            }
            if (option.Equals("Squiggly", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_text_squiggly;
            }
            if (option.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return ToolType.e_none;
            }
            return ToolType.e_none;
        }

        private static string GetPropertyName<T>(System.Linq.Expressions.Expression<Func<T>> propertyExpression)
        {
            return (propertyExpression.Body as System.Linq.Expressions.MemberExpression).Member.Name;
        }

        private void SetCurrentTool()
        {
            if (CurrentTool != ToolType.e_ink_create)
            {
                ShowInkToolbar = false;
            }
            bool shouldButtonStayDown = this.ButtonsStayDown;
            _ToolManager.CreateTool(CurrentTool, null, ButtonsStayDown);
            switch (CurrentTool)
            {
                case ToolType.e_pan:
                    shouldButtonStayDown = false;
                    break;
                case ToolType.e_ink_create:
                    shouldButtonStayDown = false;
                    _CurrentInkTool = _ToolManager.CurrentTool as FreehandCreate;
                    _CurrentInkTool.MultiStrokeMode = true;
                    _CurrentInkTool.StrokeAdded += CurrentInkTool_StrokeAdded;
                    CanSaveInk = false;
                    CanRedoInk = false;
                    ShowInkToolbar = true;
                    break;
                case ToolType.e_signature:
                    shouldButtonStayDown = false;
                    break;
                case ToolType.e_none:
                    _View.CloseControl();
                    _ToolManager.CreateTool(ToolType.e_pan, null);
                    break;
            }
        }

        #endregion Utilities


        #region Annotation Properties


        private Popup _Popup;
        private FrameworkElement _Target;
        private AnnotationPropertyDialog _Dialog;
        private void ShowPopup(FrameworkElement target)
        {
            _Target = target;

            _Dialog = new AnnotationPropertyDialog();

            if (CurrentTool == ToolType.e_ink_create)
            {
                _Dialog.ViewModel.Sequencenumber = _CurrentPresetLabel;
            }

            _Dialog.ViewModel.AssociatedTool = CurrentTool;

            AnnotationPropertiesDisplay display = AnnotationPropertiesDisplay.CreateAnnotationPropertiesDisplay(UtilityFunctions.GetElementRect(_Target), _Dialog);
            display.AnnotationPropertyDialogClosed += display_AnnotationPropertyDialogClosed;
        }

        void display_AnnotationPropertyDialogClosed(AnnotationPropertyDialog dialog)
        {
            if (CurrentTool == ToolType.e_ink_create)
            {
                SetInkPropertiesFromLabel();
            }
        }

        void _Popup_Closed(object sender, object e)
        {
            _Popup = null;
            _Dialog = null;

            if (CurrentTool == ToolType.e_ink_create)
            {
                SetInkPropertiesFromLabel();
            }
        }

        void PropertiesDialog_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionDialog(e.NewSize);
        }
        
        private void PositionDialog(Windows.Foundation.Size dialogSize)
        {
            UIRect rect = UtilityFunctions.GetElementRect(_Target);
            UIRect bounds = Window.Current.Bounds;
            double verticalOffset = 0;

            // position vertically
            if (dialogSize.Height + 1 >= bounds.Bottom - rect.Bottom)
            {
                _Dialog.VerticalAlignment = VerticalAlignment.Bottom;
            }
            else
            {
                _Dialog.VerticalAlignment = VerticalAlignment.Top;
                verticalOffset = rect.Bottom;
            }

            // position horizontally
            double center = (rect.Left + rect.Right) / 2;
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


        #endregion Annotation Properties

        #region Public Interface

        public bool GoBack()
        {
            if (_Popup != null)
            {
                _Popup.IsOpen = false;
                return true;
            }
            if (ShowInkToolbar)
            {
                ShowInkToolbar = false;
                ToolManager.CreateDefaultTool();
                return true;
            }

            
            return false;
        }


        #endregion Public Interface
    }
}

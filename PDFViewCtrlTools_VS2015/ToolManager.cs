using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using pdftron.PDF;
using pdftron.PDF.Tools.Utilities;

namespace pdftron.PDF.Tools
{
    /// <summary>
    /// The various types of tools available for use.
    /// </summary>
    public enum ToolType
    {
        e_none = 0, e_pan = 1, e_annot_edit = 2, e_line_create = 3, e_arrow_create = 4,
        e_rect_create = 5, e_oval_create = 6, e_ink_create = 7, e_text_annot_create = 8, e_link_action = 9,
        e_text_select = 10, e_form_fill = 11, e_sticky_note_create = 12,
        e_text_highlight = 13, e_text_underline = 14, e_text_strikeout = 15, e_text_squiggly = 16, 
        e_line_edit = 17, e_rich_media = 18,
        e_signature = 20, e_ink_eraser = 21, e_annot_edit_text_markup = 22,
        e_polygon_placeholder = 23, e_polyline_placeholder = 24,
    };

    public delegate void OnFileChanged(Windows.Storage.StorageFile file);
    public delegate void NewToolCreatedDelegate(ToolType toolType);

    /// <summary>
    /// The ToolManager class will attach itself to a PDFViewCtrl and provides a lot of interactive features, 
    /// such as text selection, annotation creation and editing, and form filling.
    /// 
    /// It is important to Dispose the ToolManager when it is no longer needed. This is because it subscribes
    /// to certain events in the PDFViewCtrl that will prevent both the PDFViewCtrl and the ToolManager from
    /// being reclaimed by garbage collection.
    /// </summary>
    public sealed class ToolManager : IDisposable
    {
        private bool _IsDisposed = false;
        private PointerEventHandler _PointerReleasedHandler;
        private KeyEventHandler _KeyDownHandler;

        internal static string SignatureAnnotationIdentifyingString = "pdftronSignatureStamp";
        internal static string AnnotationAuthorHasBeenAskedSettingsString = "PDFViewCtrlToolsAnnotationAuthorHasBeenAsked";
        public static string DebugAnnotationAuthorHasBeenAskedSettingsString { get { return AnnotationAuthorHasBeenAskedSettingsString; } }

        private static string _AnnotationAuthorNameSettingsString = "PDFViewCtrlToolsAnnotationAuthorName";
        /// <summary>
        /// The name under which the tools store the author name in the ApplicationData.Current.RoamingSettings container.
        /// </summary>
        public static string AnnotationAuthorNameSettingsString { get {return _AnnotationAuthorNameSettingsString; } }

        private Tool _CurrentTool;
        /// <summary>
        /// Gets the currently active tool.
        /// </summary>
        public Tool CurrentTool
        {
            get
            {
                return _CurrentTool;
            }
        }


        private bool _EnablePopupMenuOnLongPress = false;
        /// <summary>
        /// Gets or sets whether or not the tools will show a popup menu
        /// when the user long presses with their finger. This will only happen while using the pan tool.
        /// </summary>
        public bool EnablePopupMenuOnLongPress
        {
            get
            { 
                return _EnablePopupMenuOnLongPress;
            }
            set
            {
                _EnablePopupMenuOnLongPress = value;
            }
        }


        private bool _EnablePopupMenu = true;
        /// <summary>
        /// Gets or sets whether or not the tools will use the popup menu.
        /// If set to false, it is up to users to modify the tools so that they can 
        /// be used with, for example, the app bar. Defaults to true.
        /// </summary>
        public bool IsPopupMenuEnabled
        {
            get
            {
                return _EnablePopupMenu;
            }
            set
            {
                _EnablePopupMenu = value;
            }
        }

        private bool _UseSmallPageNumberIndicator = true;
        /// <summary>
        /// This tells the Tools to use a smaller page number indicator. 
        /// If true, the page number indicator will be smaller and will only contain the current page number,
        /// but not the total number of pages.
        /// </summary>
        public bool UseSmallPageNumberIndicator
        {
            get
            {
                return _UseSmallPageNumberIndicator;
            }
            set
            {
                if (value)
                {
                    mPageNumberIndicator.Shrink();
                }
                else
                {
                    mPageNumberIndicator.Grow();
                }
                _UseSmallPageNumberIndicator = value;
            }
        }

        internal PageNumberIndicator mPageNumberIndicator;
        /// <summary>
        /// This is a small UI widget that displays a the current page number.
        /// </summary>
        public PageNumberIndicator PageNumberIndicator
        {
            get
            {
                return mPageNumberIndicator;
            }
        }


        private bool _TextMarkupAdobeHack = true;
        /// <summary>
        /// Gets or sets whether the TextMarkup annotations are compatible with Adobe
        /// (Adobe's quads don't follow the specification, but they don't handle quads that do).
        /// </summary>
        public bool TextMarkupAdobeHack {
            get { return _TextMarkupAdobeHack; }
            set { _TextMarkupAdobeHack = value; }
        }


        /// <summary>
        /// Determines the text selection behaviour of the pan tool
        /// </summary>
        public enum TextSelectionBehaviour
        {
            /// <summary>
            /// Clicking and dragging always pans the control
            /// </summary>
            AlwaysPan,
            /// <summary>
            /// Clicking and dragging always selects text
            /// </summary>
            AlwaysSelect,
            /// <summary>
            /// Will see if there is any text at the click point, if there is, it will select, otherwise it will pan
            /// </summary>
            Mixed,
        }

        private TextSelectionBehaviour _PanToolTextSelectionMode = TextSelectionBehaviour.Mixed;
        /// <summary>
        /// Set whether click and drag actions should select text or pan the view.
        /// </summary>
        public TextSelectionBehaviour PanToolTextSelectionMode
        {
            get { return _PanToolTextSelectionMode; }
            set { _PanToolTextSelectionMode = value; }
        }

        /// <summary>
        /// True if there currently is text selected as a result of the Tools.
        /// i.e. will return 
        /// </summary>
        public bool HasSelectedText
        {
            get
            {
                return (CurrentTool.ToolMode == ToolType.e_text_select && mPDFView.HasSelection());
            }
        }


        public string GetSelectedText()
        {
            string text = "";

            // Extract selected text
            int selectionStartPage = mPDFView.GetSelectionBeginPage();
            int selectionEndPage = mPDFView.GetSelectionEndPage();
            for (int pgnm = selectionStartPage; pgnm <= selectionEndPage; pgnm++)
            {
                if (mPDFView.HasSelectionOnPage(pgnm))
                {
                    text += mPDFView.GetSelection(pgnm).GetAsUnicode();
                }
            }

            return text;
        }


        /// <summary>
        /// This event is raised when the tools perform a save-as option. This happens when the users signs a 
        /// form. This event will provide the file the user chose to save the doc in, and should in the future
        /// be used by the app when the user wants to save the document.
        /// </summary>
        public event OnFileChanged FileHasChanged;

        internal void RaiseFileChangedEvent(Windows.Storage.StorageFile file)
        {
            if (FileHasChanged != null)
            {
                FileHasChanged(file);
            }
        }

        /// <summary>
        /// This even is raised every time a new tool is created. 
        /// The type of the new tools is passed as a parameter.
        /// </summary>
        public event NewToolCreatedDelegate NewToolCreated;

        /// <summary>
        /// Create an instance of IAuthorDialog and set it here to overwrite the default the tools are using.
        /// </summary>
        public Utilities.IAuthorDialog AuthorDialog { get; set; }

        /// <summary>
        /// Gets or sets whether an author should be added. Will open the autor dialog if none is entered yet.
        /// </summary>
        public bool AddAuthorToAnnotations { get; set; }

        internal List<DelayRemoveTimer> mDelayRemoveTimers;
        internal NoteDialogBase mCurrentNoteDialog;
        internal FixedSizedBoxPopup mCurrentBoxPopup;
        internal Controls.ViewModels.AuthorDialogViewModel mOpenAuthorDialogViewModel;

        private bool _IsEnabled = true;
        // Gets or sets whether or not the ToolManager will react to events from the PDFViewCtrl.
        public bool IsEnabled
        {
            get { return _IsEnabled; }
            set { _IsEnabled = value; }
        }


        // text selection scroll speeds when dragging to edge.
        // How far should you have to move the mouse before we can start scrolling
        public double MOVE_THRESHOLD_TO_START_SCROLL = 25;
        // the speed with which we scroll
        public double TEXT_SELECT_SCROLL_SPEED_X = 20;
        public double TEXT_SELECT_SCROLL_SPEED_Y = 50;
        // speed increases linearly if within this margin
        public double TEXT_SELECT_SCROLL_SOFT_MARGIN_X = 35;
        public double TEXT_SELECT_SCROLL_SOFT_MARGIN_Y = 35;
        // once we're outside this margin, we go full speed
        public double TEXT_SELECT_SCROLL_HARD_MARGIN_X = 10;
        public double TEXT_SELECT_SCROLL_HARD_MARGIN_Y = 10;
        // once margin is passed, increase speed by factor
        public double TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_X = 4;
        public double TEXT_SELECT_SCROLL_SPEED_MULTIPLIER_IF_POST_MARGIN_Y = 4;

        // Stylus inking
        // If user uses stylus in pan mode, we switch to ink mode. This is the time without additional strokes we will wait before saving this as an annotation.
        // set to 0 or <0 to disable this timer completely. Ink will then save as soon as the stroke is finished.
        public double INK_TIME_BEFORE_INK_SAVES_ANNOTATION_IN_MILLISECONDS = 2000;
        // When in above ink mode, we will also save and start a new annotation if the user draws far enough from the current ink.
        // set to 0 or <0 in order to disable this. If you disable it, every stroke will count as the same annotation.
        public double INK_MARGIN_FOR_STARTING_NEW_ANNOTATION = 100;



        // Manipulation Events variables. They need to be here since the tool can change mid manipulation
        private Dictionary<uint, Pointer> mContactPoints = new Dictionary<uint, Pointer>();

        internal Dictionary<uint, Pointer> ContactPoints { get { return mContactPoints; } }
        internal bool EnableScrollByManipulation { get; set; }
        internal bool EnableOneFingerScroll { get; set; }
        internal bool ManipulationTouch { get; set; }
        internal bool ManipulationInertiaStarted { get; set; }
        internal int TouchPoints { get; set; }

        public delegate void AnnotationModificationHandler(IAnnot annotation);

        /// <summary>
        /// This event is fired when an annotation has been added to a document
        /// </summary>
        public event AnnotationModificationHandler AnnotationAdded = delegate { };

        /// <summary>
        /// This event is raised whenever an annotation on the current document has been edited
        /// </summary>
        public event AnnotationModificationHandler AnnotationEdited = delegate { };

        /// <summary>
        /// This event is raised whenever an annotation has been deleted from the document
        /// </summary>
        public event AnnotationModificationHandler AnnotationRemoved = delegate { };
        

        private PDFViewCtrl mPDFView;

        public bool IsOnPhone { get; private set; }

        public ToolManager(PDFViewCtrl ctrl)
        {
            IsOnPhone = 
               Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");

            mPDFView = ctrl;
            mPageNumberIndicator = new PageNumberIndicator();
            mDelayRemoveTimers = new List<DelayRemoveTimer>();

            EnableScrollByManipulation = false;
            EnableOneFingerScroll = false;
            ManipulationTouch = false;
            ManipulationInertiaStarted = false;
            TouchPoints = 0;

            // tie into PDFViewCtrl events
            ctrl.OnScale += OnScaleHandler;
            ctrl.OnPageNumberChanged += PageNumberChangedHandler;
            ctrl.OnSetDoc += SetDocHandler;
            ctrl.OnSize += OnSizeHandler;
            ctrl.OnViewChanged += ViewChangedHandler;
            ctrl.OnRenderFinished += OnFinishedRenderingHandler;

            ctrl.PointerPressed += PointerPressedHandler;

            // Note: For pointer released, we have to use the AddHandler Syntax in order to catch events whose handled flag is set to true.
            // This is because, internally, PDFViewCtrl will set the focus to itself on a pointer released event. This means that the Handled
            // flag has to be set to true in order to prevent other elements in the visual tree from taking the focus once this event bubbles 
            // to them. This means that you can not expect a PointerReleasedEvent to always be preceded by a pointer pressed event.
            _PointerReleasedHandler = new PointerEventHandler(PointerReleasedHandler);
            ctrl.AddHandler(Windows.UI.Xaml.UIElement.PointerReleasedEvent, _PointerReleasedHandler, true);

            ctrl.PointerMoved += PointerMovedHandler;
            ctrl.PointerCanceled += PointerCanceledHandler;
            ctrl.PointerWheelChanged += PointerWheelChangedHandler;
            ctrl.PointerCaptureLost += PointerCaptureLostHandler;
            ctrl.PointerEntered += PointerEnteredHandler;
            ctrl.PointerExited += PointerExitedHandler;

            ctrl.Tapped += TappedHandler;
            ctrl.Holding += HoldingHandler;
            ctrl.RightTapped += RightTappedHandler;
            ctrl.DoubleTapped += DoubleTappedHandler;

            ctrl.ManipulationStarted += ManipulationStartedHandler;
            ctrl.ManipulationCompleted += ManipulationCompletedHandler;
            ctrl.ManipulationDelta += ManipulationDeltaHandler;
            ctrl.ManipulationInertiaStarting += ManipulationInertiaStartingHandler;
            ctrl.ManipulationStarting += ManipulationStartingHandler;

            // This handler needs to fire at all times in order to be able to, for example, change pages when the user presses an arrow key 
            // while the page is small enough so that it can't scroll.
            _KeyDownHandler = new KeyEventHandler(KeyDownHandler);
            ctrl.AddHandler(Windows.UI.Xaml.UIElement.KeyDownEvent, _KeyDownHandler, true);
            ctrl.KeyUp += KeyUpHandler;

            AddAuthorToAnnotations = true;
            CreateDefaultTool();
        }

        ~ToolManager()
        {
            Dispose();
        }

        /// <summary>
        /// Disposes the ToolManager. Always perform this when the ToolManager is not needed anymore.
        /// The ToolManager is subscribed to events on the PDFViewCtrl that prevent it from being reclaimed.
        /// Therefore it is very important to dispose the ToolManager, so that it can unsubscribe to these events.
        /// </summary>
        public void Dispose()
        {
            if (_IsDisposed)
            {
                return;
            }
            _IsDisposed = true;

            if (mPDFView == null)
            {
                return;
            }

            Unsubscribe();
        }

        /// <summary>
        /// If this is not done when the ToolManager is no longer needed, the PDFViewCtrl can not be reclaimed
        /// by the garbage collector.
        /// </summary>
        private void Unsubscribe()
        {
            if (_CurrentTool != null)
            {
                _CurrentTool.OnClose();
            }

            mPDFView.OnScale -= OnScaleHandler;
            mPDFView.OnPageNumberChanged -= PageNumberChangedHandler;
            mPDFView.OnSetDoc -= SetDocHandler;
            mPDFView.OnSize -= OnSizeHandler;
            mPDFView.OnViewChanged -= ViewChangedHandler;
            mPDFView.OnRenderFinished -= OnFinishedRenderingHandler;

            mPDFView.PointerPressed -= PointerPressedHandler;
            mPDFView.RemoveHandler(Windows.UI.Xaml.UIElement.PointerReleasedEvent, _PointerReleasedHandler);

            mPDFView.PointerMoved -= PointerMovedHandler;
            mPDFView.PointerCanceled -= PointerCanceledHandler;
            mPDFView.PointerWheelChanged -= PointerWheelChangedHandler;
            mPDFView.PointerCaptureLost -= PointerCaptureLostHandler;
            mPDFView.PointerEntered -= PointerEnteredHandler;
            mPDFView.PointerExited -= PointerExitedHandler;

            mPDFView.Tapped -= TappedHandler;
            mPDFView.Holding -= HoldingHandler;
            mPDFView.RightTapped -= RightTappedHandler;
            mPDFView.DoubleTapped -= DoubleTappedHandler;

            mPDFView.ManipulationStarted -= ManipulationStartedHandler;
            mPDFView.ManipulationCompleted -= ManipulationCompletedHandler;
            mPDFView.ManipulationDelta -= ManipulationDeltaHandler;
            mPDFView.ManipulationInertiaStarting -= ManipulationInertiaStartingHandler;
            mPDFView.ManipulationStarting -= ManipulationStartingHandler;

            // This handler needs to fire at all times in order to be able to, for example, change pages when the user presses an arrow key 
            // while the page is small enough so that it can't scroll.
            mPDFView.RemoveHandler(Windows.UI.Xaml.UIElement.KeyDownEvent, _KeyDownHandler);
            mPDFView.KeyUp -= KeyUpHandler;

            foreach (DelayRemoveTimer timer in mDelayRemoveTimers)
            {
                timer.Destroy();
            }
        }


        /// <summary>
        /// Creates the default tool (the Pan tool).
        /// </summary>
        /// <param name="ctrl"></param>
        public void CreateDefaultTool()
        {
            if (_CurrentTool != null)
            {
                _CurrentTool.OnClose();
            }
            CreateTool(ToolType.e_pan, null);
        }

        [Windows.Foundation.Metadata.DefaultOverload]
        public Tool CreateTool(ToolType mode, Tool current_tool)
        {
            return CreateTool(mode, current_tool, false);
        }


        public Tool CreateTool(ToolType mode, Tool current_tool, bool toolIsPersistent)
        {
            Tool t = null;
            if (!_ToolChangedInternally)
            {
                ToolModeAfterEditing = ToolType.e_none;
            }

            bool returningToOldTool = false;
            if (ToolModeAfterEditing != ToolType.e_none && mode != ToolType.e_annot_edit && mode != ToolType.e_annot_edit_text_markup && mode != ToolType.e_line_edit)
            {
                mode = ToolModeAfterEditing;
                returningToOldTool = true;
            }
            switch (mode)
            {
                case ToolType.e_pan:
                    t = new Pan(mPDFView, this);
                    break;
                case ToolType.e_annot_edit:
                    t = new AnnotEdit(mPDFView, this);
                    break;
                case ToolType.e_line_create:
                    t = new LineCreate(mPDFView, this);
                    break;
                case ToolType.e_arrow_create:
                    t = new ArrowCreate(mPDFView, this);
                    break;
                case ToolType.e_rect_create:
                    t = new RectCreate(mPDFView, this);
                    break;
                case ToolType.e_oval_create:
                    t = new OvalCreate(mPDFView, this);
                    break;
                case ToolType.e_ink_create:
                    t = new FreehandCreate(mPDFView, this);
                    break;
                case ToolType.e_text_annot_create:
                    t = new FreeTextCreate(mPDFView, this);
                    break;
                case ToolType.e_link_action:
                    t = new LinkAction(mPDFView, this);
                    break;
                case ToolType.e_text_select:
                    t = new TextSelect(mPDFView, this);
                    break;
                case ToolType.e_form_fill:
                    t = new FormFill(mPDFView, this);
                    break;
                case ToolType.e_sticky_note_create:
                    t = new StickyNoteCreate(mPDFView, this);
                    break;
                case ToolType.e_text_highlight:
                    t = new TextHightlightCreate(mPDFView, this);
                    break;
                case ToolType.e_text_underline:
                    t = new TextUnderlineCreate(mPDFView, this);
                    break;
                case ToolType.e_text_strikeout:
                    t = new TextStrikeoutCreate(mPDFView, this);
                    break;
                case ToolType.e_text_squiggly:
                    t = new TextSquigglyCreate(mPDFView, this);
                    break;
                case ToolType.e_line_edit:
                    t = new LineEdit(mPDFView, this);
                    break;
                case ToolType.e_rich_media:
                    t = new RichMedia(mPDFView, this);
                    break;
                case ToolType.e_signature:
                    t = new Signature(mPDFView, this);
                    break;
                case ToolType.e_ink_eraser:
                    t = new Eraser(mPDFView, this);
                    break;
                case ToolType.e_annot_edit_text_markup:
                    t = new TextMarkupEdit(mPDFView, this);
                    break;
                default:
                    t = new Pan(mPDFView, this);
                    break;
            }
            if (_CurrentTool != null && _CurrentTool != current_tool)
            {
                _CurrentTool.OnClose();
            }

            if (current_tool != null)
            {
                // Transfer some useful info
                t.Transfer((Tool)current_tool);
                current_tool.OnClose();		//close the old tool; old tool can use this to clean up things.
            }

            //call a tool's onCreate() function in which the tool can initialize things that require the transferred properties. 
            t.OnCreate();
            t.ReturnToPanModeWhenFinished = !toolIsPersistent && ToolModeAfterEditing == ToolType.e_none;
            _CurrentTool = t;

            if (returningToOldTool)
            {
                ToolModeAfterEditing = ToolType.e_none;
            }
            
            if (NewToolCreated != null)
            {
                NewToolCreated(mode);
            }

            return t;
        }

        /// <summary>
        /// Gets the list of options that are normally displayed when the user taps on blank space
        /// in the PDFViewCtrl (i.e. no annotation or text).
        /// </summary>
        /// <returns></returns>
        public IList<string> GetAnnotationCreationOptions()
        {
            return _CurrentTool.ToolCreationOptions();
        }

        /// <summary>
        /// Creates a new tool based on one of the strings from GetAnnotationCreationOptions()
        /// </summary>
        /// <param name="title"></param>
        public void CreateTool(string title)
        {
            CreateTool(title, false);
        }

        public void CreateTool(string title, bool toolIsPersistent)
        {
            _CurrentTool.HandleToolCreationOption(title, toolIsPersistent);
        }

        /// <summary>
        /// Closes any open dialog the Tools have open.
        /// User this to handle things like back buttons.
        /// </summary>
        /// <param name="all">Closes all open dialogs if true, otherwise only 1 (in case there are nested ones).</param>
        /// <returns></returns>
        public bool CloseOpenDialog(bool all = false)
        {
            bool closed = false;
            if (mCurrentNoteDialog != null)
            {
                mCurrentNoteDialog.CancelAndClose();
                closed = true;
            }

            if (CurrentTool is Signature && (CurrentTool as Signature).GoBack())
            {
                closed = true;
            }

            if (mOpenAuthorDialogViewModel != null)
            {
                mOpenAuthorDialogViewModel.GoBack();
                mOpenAuthorDialogViewModel = null;
                closed = true;
            }

            if (mCurrentBoxPopup != null)
            {
                mCurrentBoxPopup.Hide();
                mCurrentBoxPopup = null;
                closed = true;
            }

            if (all || !closed)
            {
                bool cls = _CurrentTool.CloseOpenDialog();
                closed |= cls;
                if (all)
                {
                    while (cls)
                    {
                        cls = _CurrentTool.CloseOpenDialog();
                    }
                }
            }

            return closed;
        }


        internal void ClearDelayRemoveList()
        {
            foreach (DelayRemoveTimer timer in mDelayRemoveTimers)
            {
                timer.Destroy();
            }
            mDelayRemoveTimers.Clear();
        }

        private ToolType _ToolModeAfterEditing = ToolType.e_none;
        internal ToolType ToolModeAfterEditing
        {
            get { return _ToolModeAfterEditing; }
            set { _ToolModeAfterEditing = value; }
        }

        private bool _ToolChangedInternally = false;
        internal bool ToolChangedInternally
        {
            get { return _ToolChangedInternally; }
            set { _ToolChangedInternally = value; }
        }

        #region Annotation Modification

        /// <summary>
        /// Lets the various tools raise the AnnotationAdded event from a unified location.
        /// </summary>
        /// <param name="annot"></param>
        internal void RaiseAnnotationAddedEvent(IAnnot annot)
        {
            if (AnnotationAdded != null)
            {
                AnnotationAdded(annot);
            }
        }

        /// <summary>
        /// Lets the various tools raise the AnnotationEdited event from a unified location.
        /// </summary>
        /// <param name="annot"></param>
        internal void RaiseAnnotationEditedEvent(IAnnot annot)
        {
            if (AnnotationEdited != null)
            {
                AnnotationEdited(annot);
            }
        }

        /// <summary>
        /// Lets the various tools raise the AnnotationRemoved event from a unified location.
        /// </summary>
        /// <param name="annot"></param>
        internal void RaiseAnnotationRemovedEvent(IAnnot annot)
        {
            if (AnnotationRemoved != null)
            {
                AnnotationRemoved(annot);
            }
        }

        #endregion Annotation Modification

        #region Event Handlers
        //////////////////////////////////////////////////////////////////////////
        // All events that the ToolManager gets follows the same pattern.
        // First, the event is forwarded to the current tool. 
        // Once the current tool is finished, we look to see if the next tool mode
        // is the same as the current tool mode. This gives the current tool a 
        // chance to process the event, and if it detects that another tool should
        // handle this event, it can set the next tool mode to the appropriate
        // tool.
        // This is done for example when the Pan tool handles a MouseLeftButtonDown
        // event and notices a form field at the current cursor location.
        //////////////////////////////////////////////////////////////////////////


        internal void OnScaleHandler()
        {
            PDFViewCtrlEventLoop(0, 0, (s, args) =>
            {
                return _CurrentTool.OnScale();
            });
        }

        /// <summary>
        /// Set this variable to true during any of PointerPressed or PointerReleased in the tools in order to make sure that this
        /// particular interaction does not generate a tap
        /// </summary>
        internal bool SuppressTap { get; set; }
        internal void PointerPressedHandler(Object sender, PointerRoutedEventArgs e)
        {
            SuppressTap = false;

            // double click checking
            _IsDoubleClick = false;
            if (_MouseFirstClick && e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                Windows.UI.Input.PointerPoint scDownPoint = e.GetCurrentPoint(mPDFView);
                double sqDist = (scDownPoint.Position.X - _scDoubleClickFirstDownPoint.X) * (scDownPoint.Position.X - _scDoubleClickFirstDownPoint.X)
                    + (scDownPoint.Position.Y - _scDoubleClickFirstDownPoint.Y) * (scDownPoint.Position.Y - _scDoubleClickFirstDownPoint.Y);
                TimeSpan timeDifference = DateTime.Now.Subtract(_MouseClickTime);
                if (scDownPoint.Properties.IsLeftButtonPressed && sqDist < 25 && timeDifference.TotalSeconds < 0.3) // we have a double click
                {
                    _IsDoubleClick = true;
                    _MouseFirstClick = false;
                    DoubleClickHandler(sender, e);
                    return;
                }
            }

            _MouseFirstClick = false;

            // regular touch handling
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerPressedHandler(s, args);
            });
            CurrentTool.AddPointer(e.Pointer);
        }

        internal void PointerReleasedHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerReleasedHandler(s, args);
            });
            CurrentTool.RemovePointer(e.Pointer);
        }

        internal void PointerMovedHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerMovedHandler(s, args);
            });
        }

        internal void PointerCanceledHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerCanceledHandler(s, args);
            });
            CurrentTool.RemovePointer(e.Pointer);
        }

        internal void PageNumberChangedHandler(int current_page, int num_pages)
        {
            PDFViewCtrlEventLoop(current_page, num_pages, (s, args) =>
            {
                return _CurrentTool.OnPageNumberChanged(s, args);
            });
        }

        internal void PointerWheelChangedHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerWheelChangedHandler(s, args);
            });
        }

        internal void PointerCaptureLostHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerCaptureLostHandler(s, args);
            });
            CurrentTool.RemovePointer(e.Pointer);
        }

        internal void PointerEnteredHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerEnteredHandler(s, args);
            });
        }

        internal void PointerExitedHandler(Object sender, PointerRoutedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.PointerExitedHandler(s, args);
            });
            CurrentTool.RemovePointer(e.Pointer);
        }

        internal void SetDocHandler()
        {
            if (_CurrentTool != null && _IsEnabled)
            {
                _CurrentTool.OnClose();
                CreateDefaultTool();
                _CurrentTool.OnSetDoc();
            }
        }

        internal void OnSizeHandler()
        {
            PDFViewCtrlEventLoop(0, 0, (s, args) =>
            {
                _CurrentTool.OnSize();
                return true;
            });
        }

        internal void OnFinishedRenderingHandler()
        {
            PDFViewCtrlEventLoop(0, 0, (s, args) =>
            {
                _CurrentTool.OnFinishedRendering();
                return true;
            });
        }

        internal void ManipulationStartedHandler(Object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.ManipulationStartedEventHandler(s, args);
            });
        }

        internal void ManipulationCompletedHandler(Object sender, ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.ManipulationCompletedEventHandler(s, args);
            });
        }

        internal void ManipulationDeltaHandler(Object sender, ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.ManipulationDeltaEventHandler(s, args);
            });
        }

        internal void ManipulationInertiaStartingHandler(Object sender, ManipulationInertiaStartingRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.ManipulationInertiaStartingEventHandler(s, args);
            });
        }

        internal void ManipulationStartingHandler(Object sender, ManipulationStartingRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.ManipulationStartingEventHandler(s, args);
            });
        }

        internal void TappedHandler(Object sender, TappedRoutedEventArgs e)
        {
            if (_IsDoubleClick || SuppressTap)
            {
                e.Handled = true;
                return;
            }
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                _MouseFirstClick = true;
                _MouseClickTime = DateTime.Now;
                _scDoubleClickFirstDownPoint = e.GetPosition(mPDFView);
            }
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) => 
            {
                return _CurrentTool.TappedHandler(s, args);
            });
        }


        internal void HoldingHandler(Object sender, HoldingRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.HoldingHandler(s, args);
            });
        }

        internal void RightTappedHandler(Object sender, RightTappedRoutedEventArgs e)

        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.RightTappedHandler(s, args);
            });
        }

        internal void DoubleTappedHandler(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.DoubleTappedHandler(s, args);
            });
        }

        internal void ViewChangedHandler(Object sender, Windows.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
        {
            PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.OnViewChanged(s, args);
            });
        }

        public void KeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            bool handling = false;
            handling = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.KeyDownHandler(s, args);
            });
            if (handling)
            {
                e.Handled = true;
                mPDFView.Focus(FocusState.Programmatic);
            }
        }

        public void KeyUpHandler(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.KeyUpHandler(s, args);
            });
        }

        private bool _MouseFirstClick = false;
        private bool _IsDoubleClick = false;
        private DateTime _MouseClickTime = DateTime.Now;
        private Windows.Foundation.Point _scDoubleClickFirstDownPoint;
        private void DoubleClickHandler(Object sender, PointerRoutedEventArgs e)
        {
            e.Handled = PDFViewCtrlEventLoop(sender, e, (s, args) =>
            {
                return _CurrentTool.DoubleClickedHandler(s, args);
            });
        }

        bool PDFViewCtrlEventLoop<S, T>(S sender, T args, Func<S, T, bool> act)
        {
            bool handled = false;
            if (_CurrentTool != null && _IsEnabled)
            {
                ToolType prev_tm = _CurrentTool.ToolMode;
                ToolType next_tm;
                while (true)
                {
                    handled |= act(sender, args);
                    next_tm = _CurrentTool.NextToolMode;
                    if (prev_tm != next_tm)
                    {
                        _ToolChangedInternally = true;
                        _CurrentTool = CreateTool(next_tm, _CurrentTool);
                        _ToolChangedInternally = false;
                        prev_tm = _CurrentTool.ToolMode;
                    }
                    else
                    {
                        _CurrentTool.JustSwitchedFromAnotherTool = false;
                        break;
                    }
                }
            }
            return handled;
        }

        #endregion Event Handlers
    }
}

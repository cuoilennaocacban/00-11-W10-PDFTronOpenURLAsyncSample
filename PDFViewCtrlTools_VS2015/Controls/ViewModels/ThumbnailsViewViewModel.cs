using System;
using System.Threading.Tasks;
using pdftron.PDF.Tools.Controls.ViewModels.Common;
using pdftron.PDF.Tools.Controls.ControlBase;
using Windows.UI.Xaml.Media;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

using UIRect = Windows.Foundation.Rect;
using System.IO;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI;


namespace pdftron.PDF.Tools.Controls.ViewModels
{
    public class ThumbnailItem : ViewModelBase
    {
        private int _PageNumber;
        public int PageNumber
        {
            get { return _PageNumber; }
            set
            {
                if (value != _PageNumber)
                {
                    _PageNumber = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ImageSource _Thumbnail;
        public ImageSource Thumbnail
        {
            get { return _Thumbnail; }
            set
            {
                if (value != _Thumbnail)
                {
                    _Thumbnail = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("HasThumbnail");
                }
            }
        }

        public bool IsCurrentPage { get; set; }

        public bool HasThumbnail { get { return Thumbnail != null; } }

        public SolidColorBrush DefaultBackground { get; set; }

        public ThumbnailItem(int pageNumber, Color color)
        {
            PageNumber = pageNumber;
            DefaultBackground = new SolidColorBrush(color);
        }
    }


    public class ThumbnailsViewViewModel : ViewModelBase
    {
        ICloseableControl _View;

        private bool _HasModifiedPageLayout = false;
        private int _MinPageInView = -1;
        private int _MaxPageInView = -1;
        private double _OldScrollPosition = -100;
        private double _OldScrollabelSpace = -100;
        private bool _IsResizng = true;
        private Dictionary<int, ThumbnailItem> _ThumbnailsAttached;
        private List<int> _ThumbnailRequestQueue;

        private bool _ClearingSelection = false;

        public ScrollViewer _CurrentScrollViewer;
        private ListViewBase _CurrentListViewBase;
        public ListViewBase CurrentListViewBase
        {
            get { return _CurrentListViewBase; }
            set
            {
                if (_CurrentListViewBase != value)
                {
                    _CurrentListViewBase = value;
                    _OldScrollPosition = -100;
                    _IsResizng = true;
                    if (CurrentListViewBase != null)
                    {
                        CurrentListViewBase.Opacity = 0;
                    }
                }
            }
        }

        private bool _WindowsEditingStyle = true;
        public bool WindowsEditingStyle
        {
            get { return _WindowsEditingStyle; }
            set
            {
                if (value != _WindowsEditingStyle)
                {
                    _WindowsEditingStyle = value;
                    RaisePropertyChanged();
                    if (_WindowsEditingStyle)
                    {
                        SelectionMode = ListViewSelectionMode.Single;
                    }
                    else
                    {
                        SelectionMode = ListViewSelectionMode.None;
                    }
                }
            }
        }

        private int _CurrentIndex = -1;

        private double _RowOrColumnDimension = -1;
        private int _MaximumRowsOrColumns = -1;
        private double _WidthOrHeightOfView = -1;

        private OnThumbnailGeneratedEventHandler _ThumbnailGeneratedHandler;
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler _CollectionChangedHandler;

        private ObservableCollection<ThumbnailItem> _ThumbnailsList;
        public ObservableCollection<ThumbnailItem> ThumbnailsList
        {
            get { return _ThumbnailsList; }
            private set
            {
                if (value != _ThumbnailsList)
                {
                    _ThumbnailsList = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ListViewSelectionMode _SelectionMode = ListViewSelectionMode.Multiple;
        public ListViewSelectionMode SelectionMode
        {
            get { return _SelectionMode; }
            set
            {
                if (value != _SelectionMode)
                {
                    _SelectionMode = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("IsInEditMode");
                }
            }
        }

        public bool IsInEditMode
        {
            get { return _SelectionMode != ListViewSelectionMode.None; }
        }

        private int _NumberOfSelectedItems = 0;
        public bool HasSelectedItems { get { return _NumberOfSelectedItems > 0; } }
        public int NumberOfSelectedItems
        {
            get { return _NumberOfSelectedItems; }
            set
            {
                if (value != _NumberOfSelectedItems)
                {
                    _NumberOfSelectedItems = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("HasSelectedItems");
                }
            }
        }

        private Color _PageDefaultColor = Colors.White;
        /// <summary>
        /// Set this immediately
        /// </summary>
        public Color PageDefaultColor
        {
            get { return _PageDefaultColor; }
            set
            {
                _PageDefaultColor = value;
                CreateCollection();
            }
        }

        private PDFViewCtrl _PDFViewCtrl;
        private string _DocumentTag;

        public ThumbnailsViewViewModel(PDFViewCtrl ctrl, string docTag, ICloseableControl view)
        {
            _PDFViewCtrl = ctrl;
            _View = view;
            PDFDoc doc = null;
            if (_PDFViewCtrl != null)
            {
                _DocumentTag = docTag;
                doc = _PDFViewCtrl.GetDoc();
                if (doc == null)
                {
                    return;
                }
            }

            SelectionChangedCommand = new RelayCommand(SelectionChangedCommandImpl);
            DeleteCommand = new RelayCommand(DeleteCommandImpl);
            CloseCommand = new RelayCommand(CloseCommandImpl);
            ClearSelectionCommand = new RelayCommand(ClearSelectionCommandImpl);
            ItemClickCommand = new RelayCommand(ItemClickCommandImpl);

            EditCommand = new RelayCommand(EditCommandImpl);
            DoneEditingCommand = new RelayCommand(DoneEditingCommandImpl);

            if (_PDFViewCtrl == null)
            {
                return;
            }


            if (_PDFViewCtrl == null)
            {
                return;
            }

            _ThumbnailsAttached = new Dictionary<int, ThumbnailItem>();
            _ThumbnailRequestQueue = new List<int>();
            _ThumbnailGeneratedHandler = new OnThumbnailGeneratedEventHandler(PDFViewCtrl_OnThumbnailGenerated);
            _PDFViewCtrl.OnThumbnailGenerated += _ThumbnailGeneratedHandler;
            CreateCollection();

            _CollectionChangedHandler = new System.Collections.Specialized.NotifyCollectionChangedEventHandler(_ThumbnailsList_CollectionChanged);
            _ThumbnailsList.CollectionChanged += _CollectionChangedHandler;
            GetBookmarks(doc);

            SelectionMode = ListViewSelectionMode.Single;
        }

        private async void GetBookmarks(PDFDoc doc)
        {
            try
            {
                await UserBookmarkManager.LoadBookmarksListAsync(_DocumentTag);
            }
            catch (Exception)
            {

            }
        }

        private void CreateCollection()
        {
            ObservableCollection<ThumbnailItem> items = new ObservableCollection<ThumbnailItem>();
            int pageCount = _PDFViewCtrl.GetPageCount();
            for (int i = 1; i <= pageCount; ++i)
            {
                items.Add(new ThumbnailItem(i, _PageDefaultColor));
            }
            items[_PDFViewCtrl.GetCurrentPage() - 1].IsCurrentPage = true;
            ThumbnailsList = items;
        }

        public async void CleanUp()
        {
            if (_ThumbnailGeneratedHandler != null)
            {
                _PDFViewCtrl.CancelAllThumbRequests();
                _PDFViewCtrl.OnThumbnailGenerated -= _ThumbnailGeneratedHandler;
                _ThumbnailGeneratedHandler = null;
                try
                {
                    PDFDoc doc = _PDFViewCtrl.GetDoc();
                    await UserBookmarkManager.SaveBookmarkListAsync(_DocumentTag, null);
                }
                catch (Exception) { }
            }
            if (_CollectionChangedHandler != null)
            {
                _ThumbnailsList.CollectionChanged -= _CollectionChangedHandler;
                _CollectionChangedHandler = null;
            }

            if (_CollectionChangedHandler != null)
            {
                _ThumbnailsList.CollectionChanged -= _CollectionChangedHandler;
                _CollectionChangedHandler = null;
            }

        }

        #region Events

        public delegate void PageDeletedDelegate(int pageNumber);
        public delegate void PageMovedDelegate(int pageNumber, int newLocation);

        public event PageDeletedDelegate PageDeleted = delegate { };
        public event PageMovedDelegate PageMoved = delegate { };

        #endregion Events

        #region Commands

        public RelayCommand DeleteCommand { get; private set; }
        public RelayCommand CloseCommand { get; private set; }
        public RelayCommand SelectionChangedCommand { get; private set; }
        public RelayCommand ClearSelectionCommand { get; private set; }
        public RelayCommand ItemClickCommand { get; private set; }
        public RelayCommand EditCommand { get; private set; }
        public RelayCommand DoneEditingCommand { get; private set; }

        private bool _Measuring = false;
        /// <summary>
        ///  When you ListView or GridView changes size, call this function with said FramworkElement
        /// </summary>
        /// <param name="element">The GridView or ListView</param>
        public async void UpdateSize(FrameworkElement element)
        {
            if (!double.IsNaN(element.ActualWidth) && !double.IsNaN(element.ActualHeight)
                && element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                if (_Measuring)
                {
                    return;
                }
                try
                {
                    _Measuring = true;
                    await CurrentListViewBase.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        _OldScrollPosition = -100;
                        MeasureControl();
                    });
                }
                catch (Exception)
                {
                }
                finally
                {
                    _Measuring = false;
                }
            }
        }

        private void DeleteCommandImpl(object sender)
        {
            DeleteItems();
        }

        private void CloseCommandImpl(object sender)
        {
            _View.CloseControl();
            if (_PDFViewCtrl != null)
            {
                if (_HasModifiedPageLayout)
                {
                    try
                    {
                        _PDFViewCtrl.UpdatePageLayout();
                    }
                    catch (Exception) { }
                }
                _PDFViewCtrl.RequestRendering();
            }
        }

        private void SelectionChangedCommandImpl(object sender)
        {
            if (_ClearingSelection)
            {
                return;
            }
            ListViewBase view = sender as ListViewBase;
            NumberOfSelectedItems = view.SelectedItems.Count;
        }

        private void ClearSelectionCommandImpl(object sender)
        {
            _ClearingSelection = true;
            CurrentListViewBase.SelectedItems.Clear();
            _ClearingSelection = false;
            NumberOfSelectedItems = 0;
        }

        private void ItemClickCommandImpl(object args)
        {
            ItemClickEventArgs eargs = args as ItemClickEventArgs;
            int index = CurrentListViewBase.Items.IndexOf(eargs.ClickedItem);
            _PDFViewCtrl.CancelAllThumbRequests();
            try
            {
                _PDFViewCtrl.UpdatePageLayout();
            }
            catch (Exception) { }
            _PDFViewCtrl.SetCurrentPage(index + 1);
            _PDFViewCtrl.RequestRendering();

            CleanUp();
            _View.CloseControl();
        }

        private void EditCommandImpl(object sender)
        {
            SelectionMode = ListViewSelectionMode.Multiple;
        }

        private void DoneEditingCommandImpl(object sender)
        {
            SelectionMode = ListViewSelectionMode.None;
        }

        #endregion Commands

        #region Public Interface

        /// <summary>
        /// User this function to notify the ViewModel when the ScrollViewer inside the GridView or ListView
        /// updates it's layout.
        /// </summary>
        /// <param name="scrollViewer">The Child ScrollViewer of the ListView or GridView </param>
        public void ScrollChanged(ScrollViewer scrollViewer)
        {
            if (_IsResizng)
            {
                return;
            }
            CalculatePagesToShow(scrollViewer);
        }

        public bool GoBack()
        {
            if (IsInEditMode)
            {
                SelectionMode = ListViewSelectionMode.None;
                return true;
            }
            _View.CloseControl();
            return true;
        }

        #endregion Public Interface

        #region Manage View

        private void CalculatePagesToShow(ScrollViewer scrollViewer)
        {
            if (CurrentListViewBase is GridView && Math.Abs(scrollViewer.HorizontalOffset - _OldScrollPosition) > 2)
            {
                if (_OldScrollabelSpace < 0)
                {
                    _OldScrollabelSpace = scrollViewer.ScrollableWidth;
                }
                else if (Math.Abs(_OldScrollabelSpace - scrollViewer.ScrollableWidth) > 1)
                {
                    return;
                }
                _OldScrollPosition = scrollViewer.HorizontalOffset;
                double centerOffset = scrollViewer.HorizontalOffset + (scrollViewer.ActualWidth / 2);
                double totalWidth = scrollViewer.ScrollableWidth + scrollViewer.ActualWidth;
                double offsetRatio = centerOffset / totalWidth;
                int currentIndex = (int)(ThumbnailsList.Count * offsetRatio);
                GetVisiblePageRange(currentIndex);
                RemoveThumbsOutsideRange();
                PopulateThumbnailQueue();

                _CurrentIndex = currentIndex;
            }
            if (CurrentListViewBase is ListView && Math.Abs(scrollViewer.VerticalOffset - _OldScrollPosition) > 2)
            {
                if (_OldScrollabelSpace < 0)
                {
                    _OldScrollabelSpace = scrollViewer.ScrollableHeight;
                }
                else if (Math.Abs(_OldScrollabelSpace - scrollViewer.ScrollableHeight) > 1)
                {
                    return;
                }
                _OldScrollPosition = scrollViewer.VerticalOffset;
                double centerOffset = scrollViewer.VerticalOffset + (scrollViewer.ActualHeight / 2);
                double totalHeight = scrollViewer.ScrollableHeight + scrollViewer.ActualHeight;
                double offsetRatio = centerOffset / totalHeight;
                int currentIndex = (int)(ThumbnailsList.Count * offsetRatio);
                GetVisiblePageRange(currentIndex);
                RemoveThumbsOutsideRange();
                PopulateThumbnailQueue();

                _CurrentIndex = currentIndex;
            }
        }

        private void GetVisiblePageRange(int centerIndex)
        {
            double rowsOrColumnsPerScreen = (_WidthOrHeightOfView / _RowOrColumnDimension) + 1;
            int rowsOrColumnsPerSide = (int)(rowsOrColumnsPerScreen / 2) + 1;

            _MinPageInView = centerIndex - (rowsOrColumnsPerSide * _MaximumRowsOrColumns);
            _MaxPageInView = centerIndex + ((rowsOrColumnsPerSide + 1) * _MaximumRowsOrColumns);

            if (_MinPageInView < 1)
            {
                _MinPageInView = 1;
            }
            PDFDoc doc = _PDFViewCtrl.GetDoc();
            if (_MaxPageInView > doc.GetPageCount())
            {
                _MaxPageInView = doc.GetPageCount();
            }
            return;
        }

        private void RePopulateThumbnailQueue()
        {
            _PDFViewCtrl.CancelAllThumbRequests();
            _ThumbnailRequestQueue.Clear();
            CalculatePagesToShow(_CurrentScrollViewer);
            PopulateThumbnailQueue();
        }

        private void PopulateThumbnailQueue()
        {
            bool shouldEmpty = _ThumbnailRequestQueue.Count > 0;
            foreach (int pgNum in _ThumbnailRequestQueue)
            {
                if (pgNum >= _MinPageInView && pgNum <= _MaxPageInView)
                {
                    shouldEmpty = false;
                }
            }
            if (shouldEmpty)
            {
                _PDFViewCtrl.CancelAllThumbRequests();
                _ThumbnailRequestQueue.Clear();
            }
            
            if (_ThumbnailRequestQueue.Count == 0)
            {
                List<int> thumbsToRequest = GetListOfPagesThatNeedThumbnails();
                
                foreach (int pgNum in thumbsToRequest)
                {
                    _PDFViewCtrl.GetThumbAsync(pgNum);
                    _ThumbnailRequestQueue.Add(pgNum);
                }
            }
        }

        private List<int> GetListOfPagesThatNeedThumbnails()
        {
            int maxThumbs = 5;
            List<int> pagesNeeded = new List<int>();
            for (int i = _MaxPageInView; i >= _MinPageInView && maxThumbs > 0; --i)
            {
                if (!_ThumbnailsAttached.ContainsKey(i))
                {
                    pagesNeeded.Add(i);
                    --maxThumbs;
                }
            }
            return pagesNeeded;
        }

        private void RemoveThumbsOutsideRange()
        {
            List<int> toRemove = new List<int>();
            foreach (int pagenumber in _ThumbnailsAttached.Keys)
            {
                if (pagenumber < _MinPageInView || pagenumber > _MaxPageInView)
                {
                    toRemove.Add(pagenumber);
                }
            }
            foreach (int pagenumber in toRemove)
            {
                _ThumbnailsAttached[pagenumber].Thumbnail = null;
                _ThumbnailsAttached.Remove(pagenumber);
            }
        }

        private async void PDFViewCtrl_OnThumbnailGenerated(int pageNumber, byte[] thumb, int w, int h)
        {
            if (_ThumbnailRequestQueue.Contains(pageNumber))
            {
                _ThumbnailRequestQueue.Remove(pageNumber);
            }
            if (pageNumber >= _MinPageInView && pageNumber <= _MaxPageInView && ThumbnailsList.Count >= pageNumber)
            {
                _ThumbnailsAttached[pageNumber] = ThumbnailsList[pageNumber - 1];
                if (_ThumbnailRequestQueue.Count == 0)
                {
                    PopulateThumbnailQueue();
                }
                Windows.UI.Xaml.Media.Imaging.WriteableBitmap wb = new Windows.UI.Xaml.Media.Imaging.WriteableBitmap(w, h);
                System.IO.Stream pixelStream = wb.PixelBuffer.AsStream();
                await Task<WriteableBitmap>.Run(() =>
                {
                    pixelStream.Seek(0, System.IO.SeekOrigin.Begin);
                    pixelStream.Write(thumb, 0, thumb.Length);
                });
                if (_ThumbnailsAttached.ContainsKey(pageNumber)) // this might have changed
                {
                    _ThumbnailsAttached[pageNumber].Thumbnail = wb;
                }
            }
            else
            {
                if (_ThumbnailRequestQueue.Count == 0)
                {
                    PopulateThumbnailQueue();
                }
            }
        }

        private void MeasureControl()
        {
            if (_PDFViewCtrl == null)
            {
                return;
            }
            _IsResizng = false;
            _OldScrollabelSpace = -100;
            bool isHorizontal = CurrentListViewBase is GridView;
            int currentIndex = -1;
            if (isHorizontal)
            {
                double centerOffset = _CurrentScrollViewer.HorizontalOffset + (_CurrentScrollViewer.ActualWidth / 2);
                double totalDistace = _CurrentScrollViewer.ScrollableWidth + _CurrentScrollViewer.ActualWidth;
                double offsetRatio = centerOffset / totalDistace;
                currentIndex = (int)(ThumbnailsList.Count * offsetRatio);
                _WidthOrHeightOfView = CurrentListViewBase.ActualWidth;
            }
            else
            {
                double centerOffset = _CurrentScrollViewer.VerticalOffset + (_CurrentScrollViewer.ActualHeight / 2);
                double totalDistace = _CurrentScrollViewer.ScrollableHeight + _CurrentScrollViewer.ActualHeight;
                double offsetRatio = centerOffset / totalDistace;
                currentIndex = (int)(ThumbnailsList.Count * offsetRatio);
                _WidthOrHeightOfView = CurrentListViewBase.ActualHeight;
            }

            if (currentIndex < 0)
            {
                return;
            }

            double centerLeft = -1;
            double centerTop = -1;
            int itemsPerRowOrColumn = 1;
            double itemDimension = -1;
            FrameworkElement containerElement = CurrentListViewBase.ContainerFromIndex(currentIndex) as FrameworkElement;
            if (containerElement != null)
            {
                UIRect rect = UtilityFunctions.GetElementRect(containerElement, CurrentListViewBase);
                centerLeft = rect.Left;
                centerTop = rect.Top;
                if (isHorizontal)
                {
                    itemDimension = rect.Width;
                }
                else
                {
                    itemDimension = rect.Height;
                }
            }

            for (int i = currentIndex + 1; i < ThumbnailsList.Count; ++i)
            {
                FrameworkElement container = CurrentListViewBase.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    Windows.Foundation.Rect rect = UtilityFunctions.GetElementRect(container, CurrentListViewBase);
                    if (isHorizontal)
                    {
                        if (Math.Abs(rect.Left - centerLeft) < 2)
                        {
                            itemsPerRowOrColumn++;
                        }
                        else
                        {
                            itemDimension = Math.Abs(rect.Left - centerLeft);
                            break;
                        }
                    }
                    else
                    {
                        if (Math.Abs(rect.Top - centerTop) < 2)
                        {
                            itemsPerRowOrColumn++;
                        }
                        else
                        {
                            itemDimension = Math.Abs(rect.Top - centerTop);
                            break;
                        }
                    }
                }
            }

            for (int i = currentIndex - 1; i >= 0; --i)
            {
                FrameworkElement container = CurrentListViewBase.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    Windows.Foundation.Rect rect = UtilityFunctions.GetElementRect(container, CurrentListViewBase);
                    if (isHorizontal)
                    {
                        if (Math.Abs(rect.Left - centerLeft) < 2)
                        {
                            itemsPerRowOrColumn++;
                        }
                        else
                        {
                            itemDimension = Math.Abs(rect.Left - centerLeft);
                            break;
                        }
                    }
                    else
                    {
                        if (Math.Abs(rect.Top - centerTop) < 2)
                        {
                            itemsPerRowOrColumn++;
                        }
                        else
                        {
                            itemDimension = Math.Abs(rect.Top - centerTop);
                            break;
                        }
                    }
                }
            }

            _RowOrColumnDimension = itemDimension;
            _MaximumRowsOrColumns = itemsPerRowOrColumn;
            if (_OldScrollPosition < 0)
            {
                if (_CurrentIndex < 0)
                {
                    _CurrentIndex = _PDFViewCtrl.GetCurrentPage() - 1;
                }
                CurrentListViewBase.ScrollIntoView(ThumbnailsList[_CurrentIndex], ScrollIntoViewAlignment.Default);
                CurrentListViewBase.Opacity = 1;
            }
            CalculatePagesToShow(_CurrentScrollViewer);
        }

        #endregion Manage View

        #region Editing Items

        object _ReorderItem;
        int _ReorderIndexFrom;

        void _ThumbnailsList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    _ReorderItem = e.OldItems[0];
                    _ReorderIndexFrom = e.OldStartingIndex;
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    if (_ReorderItem == null)
                    {
                        return;
                    }
                    HandleReorder(_ReorderItem, _ReorderIndexFrom, e.NewStartingIndex);
                    _ReorderItem = null;
                    break;
            }
        }

        void HandleReorder(object item, int indexFrom, int indexTo)
        {
            _HasModifiedPageLayout = true;
            _PDFViewCtrl.CancelAllThumbRequests();
            ThumbnailItem thumbItem = item as ThumbnailItem;
            try
            {
                _PDFViewCtrl.CancelRendering();
                _PDFViewCtrl.DocLock(true);
                PDFDoc doc = _PDFViewCtrl.GetDoc();

                Page pageToMove = doc.GetPage(indexFrom + 1);
                int oldNum = pageToMove.GetSDFObj().GetObjNum();
                if (indexFrom < indexTo)
                {
                    PageIterator moveTo = doc.GetPageIterator(indexTo + 2);
                    doc.PageInsert(moveTo, pageToMove);
                    PageIterator removeIter = doc.GetPageIterator(indexFrom + 1);
                    doc.PageRemove(removeIter);

                    Page newPage = doc.GetPage(indexTo + 1);
                    int newNum = newPage.GetSDFObj().GetObjNum();
                    UserBookmarkManager.PageMoved(_DocumentTag, oldNum, newNum, indexFrom + 1, indexTo + 1);
                }
                else
                {
                    PageIterator moveTo = doc.GetPageIterator(indexTo + 1);
                    doc.PageInsert(moveTo, pageToMove);
                    PageIterator removeIter = doc.GetPageIterator(indexFrom + 2);
                    doc.PageRemove(removeIter);

                    Page newPage = doc.GetPage(indexTo + 1);
                    int newNum = newPage.GetSDFObj().GetObjNum();
                    UserBookmarkManager.PageMoved(_DocumentTag, oldNum, newNum, indexFrom + 1, indexTo + 1);
                }
            }
            catch (Exception)
            { }
            finally
            {
                _PDFViewCtrl.DocUnlock();
            }
            _ThumbnailsAttached.Clear();
            RePopulateThumbnailQueue();
            PageMoved(indexFrom + 1, indexTo + 1);
        }

        Windows.Foundation.IAsyncOperation<Windows.UI.Popups.IUICommand> _DeleteErrorMessage;
        private async void DeleteItems()
        {
            if (NumberOfSelectedItems == ThumbnailsList.Count)
            {
                if (_DeleteErrorMessage == null)
                {
                    Windows.UI.Popups.MessageDialog deleteError = new Windows.UI.Popups.MessageDialog(ResourceHandler.GetString("ThumbnailsView_Delete_Error_Info"), ResourceHandler.GetString("ThumbnailsView_Delete_Error_Title"));
                    _DeleteErrorMessage = deleteError.ShowAsync();
                    try
                    {
                        await _DeleteErrorMessage;
                    }
                    catch (Exception) { }
                    _DeleteErrorMessage = null;
                }
                return;
            }

            _HasModifiedPageLayout = true;

            _PDFViewCtrl.CancelAllThumbRequests();
            List<ThumbnailItem> itemsToDelete = new List<ThumbnailItem>();
            List<int> pagesToDelete = new List<int>();
            foreach (Object item in CurrentListViewBase.SelectedItems)
            {
                itemsToDelete.Add(item as ThumbnailItem);
                pagesToDelete.Add(CurrentListViewBase.Items.IndexOf(item) + 1);
            }
            _ClearingSelection = true;
            CurrentListViewBase.SelectedItems.Clear();
            NumberOfSelectedItems = 0;
            _ClearingSelection = false;

            foreach (ThumbnailItem item in itemsToDelete)
            {
                ThumbnailsList.Remove(item);
            }

            try
            {
                _PDFViewCtrl.DocLock(false); // We shouldn't have to cancel rendering here. Leaving this as false means that 
                // unlocking won't trigger a rendering request.
                pagesToDelete.Sort();
                PDFDoc doc = _PDFViewCtrl.GetDoc();
                for (int i = pagesToDelete.Count - 1; i >= 0; --i)
                {
                    PageIterator itr = doc.GetPageIterator(pagesToDelete[i]);
                    UserBookmarkManager.PageDeleted(_DocumentTag, itr.Current().GetSDFObj().GetObjNum(), pagesToDelete[i], _PDFViewCtrl.GetPageCount());
                    doc.PageRemove(itr);
                }
                if (_MaxPageInView > doc.GetPageCount())
                {
                    _MaxPageInView = doc.GetPageCount();
                }
            }
            catch (Exception)
            { }
            finally
            {
                _PDFViewCtrl.DocUnlock();
            }
            _ThumbnailsAttached.Clear();

            _OldScrollabelSpace = -1;
            RePopulateThumbnailQueue();
            foreach (int pageNum in pagesToDelete)
            {
                PageDeleted(pageNum);
            }
        }


        #endregion Editing Items
    }
}
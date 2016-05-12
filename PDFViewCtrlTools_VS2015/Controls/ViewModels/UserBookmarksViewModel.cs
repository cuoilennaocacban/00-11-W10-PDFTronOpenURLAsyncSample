using pdftron.PDF.Tools.Controls.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.Storage;

namespace pdftron.PDF.Tools.Controls.ViewModels
{
    public class UserBookmarkManager
    {
        public class KeyValuePair // We're using this since using the built in KeyValuePair<string, string> seems to serialize to null values
        {
            public string Key { get; set; }
            public string Value { get; set; }

            public KeyValuePair() { }

            public KeyValuePair(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }



        private const string USER_BOOKMARK_SETTING_KEY_STRING_PREFIX = "User_Bookmarks_";
        private const string USER_BOOKMARK_FILE_NAME = "UserBookmarkMap";

        private static List<UserBookmarksViewModel.UserBookmarkItem> _CurrentBookmarkList = null;
        private static Dictionary<string, string> _FileDictionary = null;
        private static StorageFolder _BookmarksFolder = null;

        public static async Task<List<UserBookmarksViewModel.UserBookmarkItem>> GetBookmarkListAsync(string docTag)
        {
            await LoadBookmarksListAsync(docTag);
            return _CurrentBookmarkList;
        }

        public static async Task LoadBookmarksListAsync(string docTag)
        {
            _CurrentBookmarkList = null;
            if (_FileDictionary == null)
            {
                try
                {
                    _BookmarksFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("UserBookmarks", CreationCollisionOption.OpenIfExists);
                    if (_BookmarksFolder != null)
                    {
                        StorageFile file = await _BookmarksFolder.CreateFileAsync(USER_BOOKMARK_FILE_NAME, CreationCollisionOption.OpenIfExists);
                        if (file != null)
                        {
                            string dictString = await FileIO.ReadTextAsync(file);
                            _FileDictionary = DeSerializeFileDict(dictString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error creating file dictionary: " + ex.ToString());
                }
            }

            if (_FileDictionary == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(docTag))
            {
                if (_FileDictionary.ContainsKey(docTag))
                {
                    string bookmarkFileName = _FileDictionary[docTag];
                    IStorageItem fileItem = await _BookmarksFolder.TryGetItemAsync(bookmarkFileName);
                    if (fileItem != null)
                    {
                        StorageFile file = fileItem as StorageFile;
                        if (file != null)
                        {
                            string bookmarkString = await FileIO.ReadTextAsync(file);
                            if (!string.IsNullOrWhiteSpace(bookmarkString))
                            {
                                _CurrentBookmarkList = DeSerializeString(bookmarkString);
                            }
                        }
                    }
                }
            }

            return;
        }


        public static async Task SaveBookmarkListAsync(string docTag, List<UserBookmarksViewModel.UserBookmarkItem> items)
        {
            if (_FileDictionary == null)
            {
                return;
            }

            if (items == null)
            {
                items = _CurrentBookmarkList;
            }

            if (items == null)
            { 
                return;
            }

            if (!string.IsNullOrWhiteSpace(docTag) && items != null)
            {
                bool shouldSaveFiledict = false;
                if (!_FileDictionary.ContainsKey(docTag))
                {
                    Guid guid = Guid.NewGuid();
                    string fileName = guid.ToString();
                    _FileDictionary[docTag] = fileName;
                    shouldSaveFiledict = true;
                }
                try
                {
                    StorageFile bookmarkFile = await _BookmarksFolder.CreateFileAsync(_FileDictionary[docTag], CreationCollisionOption.OpenIfExists);
                    string bookmarkString = SerializeList(items);
                    await FileIO.WriteTextAsync(bookmarkFile, bookmarkString);
                    if (shouldSaveFiledict)
                    {
                        StorageFile file = await _BookmarksFolder.GetFileAsync(USER_BOOKMARK_FILE_NAME);
                        string dictString = SerializeFileDictionary(_FileDictionary);
                        await FileIO.WriteTextAsync(file, dictString);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error Creating or saving bookmarks: " + ex.ToString());
                }
            }
        }

        public static async Task EraseBookmarkListAsync(string docTag)
        {
            if (!string.IsNullOrWhiteSpace(docTag))
            {
                if (_FileDictionary.ContainsKey(docTag))
                {
                    try
                    {
                        _FileDictionary.Remove(docTag);
                        StorageFile file = await _BookmarksFolder.GetFileAsync(USER_BOOKMARK_FILE_NAME);
                        string dictString = SerializeFileDictionary(_FileDictionary);
                        await FileIO.WriteTextAsync(file, dictString);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error Erasing bookmarks from file: {0}: {1}", docTag, ex.ToString());
                    }
                }
            }
        }

        public static bool CanGetKeyFromDoc(PDFDoc doc)
        {
            return !string.IsNullOrWhiteSpace(GetKeyFromDoc(doc));
        }

        private static string GetKeyFromDoc(PDFDoc doc)
        {
            string docName = doc.GetFileName();
            if (string.IsNullOrWhiteSpace(docName))
            {
                return "";
            }
            return USER_BOOKMARK_SETTING_KEY_STRING_PREFIX + docName;
        }

        /// <summary>
        /// Indicates that the old page, previously being at oldPageNumber with object number objNumber,
        /// now is at page newPageNumber and has new object number newObjNumber
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="objNumber"></param>
        /// <param name="newObjNumber"></param>
        /// <param name="oldPageNumber"></param>
        /// <param name="newPageNumber"></param>
        public static void PageMoved(string docTag, int objNumber, int newObjNumber, int oldPageNumber, int newPageNumber)
        {
            if (_CurrentBookmarkList == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(docTag))
            {
                if (_CurrentBookmarkList.Count > 0)
                {
                    foreach (UserBookmarksViewModel.UserBookmarkItem item in _CurrentBookmarkList)
                    {
                        if (item.ObjectNumber == objNumber)
                        {
                            item.ObjectNumber = newObjNumber;
                            item.PageNumber = newPageNumber;
                        }
                    }
                    if (oldPageNumber < newPageNumber)
                    {
                        UpdateBookmarksAfterRearranging(docTag, _CurrentBookmarkList, oldPageNumber + 1, newPageNumber, false, newObjNumber);
                    }
                    else
                    {
                        UpdateBookmarksAfterRearranging(docTag, _CurrentBookmarkList, newPageNumber, oldPageNumber - 1, true, newObjNumber);
                    }
                }
            }
        }

        /// <summary>
        /// Page pageNumber with object number objNumber has been deleted.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="objNumber"></param>
        /// <param name="pageNumber"></param>
        public static void PageDeleted(string docTag, int objNumber, int pageNumber, int pageCount)
        {
            if (_CurrentBookmarkList == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(docTag))
            {
                if (_CurrentBookmarkList.Count > 0)
                {
                    List<UserBookmarksViewModel.UserBookmarkItem> newBookmarkList = new List<UserBookmarksViewModel.UserBookmarkItem>();
                    foreach (UserBookmarksViewModel.UserBookmarkItem item in _CurrentBookmarkList)
                    {
                        if (item.ObjectNumber != objNumber)
                        {
                            newBookmarkList.Add(item);
                        }
                    }
                    UpdateBookmarksAfterRearranging(docTag, newBookmarkList, pageNumber, pageCount, false);
                    _CurrentBookmarkList = newBookmarkList;
                }
            }
        }

        /// <summary>
        /// Updates all the bookmarks whose page number falls in the range fromPage to toPage. They will either be
        /// incremented by 1 or decremented by 1.
        /// 
        /// fromPage and toPage are inclusive.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="fromPage"></param>
        /// <param name="toPage"></param>
        /// <param name="increment"></param>
        public static void UpdateBookmarksAfterRearranging(string docTag, int fromPage, int toPage, bool increment, int ignoreObjNumber = -1)
        {
            if (_CurrentBookmarkList == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(docTag))
            {
                if (_CurrentBookmarkList.Count > 0)
                {
                    UpdateBookmarksAfterRearranging(docTag, _CurrentBookmarkList, fromPage, toPage, increment, ignoreObjNumber);
                }
            }
        }

        private static void UpdateBookmarksAfterRearranging(string docTag, List<UserBookmarksViewModel.UserBookmarkItem> bookmarkList,
            int fromPage, int toPage, bool increment, int ignoreObjNumber = -1)
        {
            if (_CurrentBookmarkList == null)
            {
                return;
            }
            if (fromPage > toPage)
            {
                int temp = fromPage;
                fromPage = toPage;
                toPage = temp;
            }
            int change = -1;
            if (increment)
            {
                change = 1;
            }
            foreach (UserBookmarksViewModel.UserBookmarkItem item in bookmarkList)
            {
                if (item.PageNumber >= fromPage && item.PageNumber <= toPage && item.ObjectNumber != ignoreObjNumber)
                {
                    item.PageNumber += change;
                }
            }
        }


        private static string SerializeList(List<UserBookmarksViewModel.UserBookmarkItem> items)
        {
            try
            {
                XmlSerializer xmlIzer = new XmlSerializer(typeof(List<UserBookmarksViewModel.UserBookmarkItem>));
                System.IO.StringWriter writer = new System.IO.StringWriter();
                xmlIzer.Serialize(writer, items);
                return writer.ToString();
            }
            catch (Exception exc)
            {
                System.Diagnostics.Debug.WriteLine(exc);
            }
            return string.Empty;
        }

        private static List<UserBookmarksViewModel.UserBookmarkItem> DeSerializeString(string itemsString)
        {
            List<UserBookmarksViewModel.UserBookmarkItem> items = new List<UserBookmarksViewModel.UserBookmarkItem>();
            try
            {
                XmlSerializer xmlIzer = new XmlSerializer(typeof(List<UserBookmarksViewModel.UserBookmarkItem>));
                using (System.IO.StringReader sr = new System.IO.StringReader(itemsString))
                {
                    items = (xmlIzer.Deserialize(sr)) as List<UserBookmarksViewModel.UserBookmarkItem>;
                    if (items != null)
                    {
                        return items;
                    }
                }
            }
            catch (Exception exc)
            {
                System.Diagnostics.Debug.WriteLine(exc);
            }
            return new List<UserBookmarksViewModel.UserBookmarkItem>();
        }

        private static string SerializeFileDictionary(Dictionary<string, string> fileDict)
        {
            try
            {
                List<KeyValuePair> tuples = new List<KeyValuePair>();
                foreach (KeyValuePair<string, string> keyValPair in fileDict)
                {
                    tuples.Add(new KeyValuePair(keyValPair.Key, keyValPair.Value));
                }
                XmlSerializer xmlIzer = new XmlSerializer(typeof(List<KeyValuePair>));
                System.IO.StringWriter writer = new System.IO.StringWriter();
                xmlIzer.Serialize(writer, tuples);
                return writer.ToString();
            }
            catch (Exception exc)
            {
                System.Diagnostics.Debug.WriteLine(exc);
            }
            return string.Empty;
        }

        private static Dictionary<string, string> DeSerializeFileDict(string fileDictString)
        {
            if (!string.IsNullOrWhiteSpace(fileDictString))
            {
                Dictionary<string, string> fileDict = new Dictionary<string, string>();
                try
                {
                    List<KeyValuePair> tuples = new List<KeyValuePair>();
                    XmlSerializer xmlIzer = new XmlSerializer(typeof(List<KeyValuePair>));
                    using (System.IO.StringReader sr = new System.IO.StringReader(fileDictString))
                    {
                        tuples = (xmlIzer.Deserialize(sr)) as List<KeyValuePair>;
                        if (tuples != null)
                        {
                            foreach (KeyValuePair tuple in tuples)
                            {
                                fileDict[tuple.Key] = tuple.Value;
                            }
                            return fileDict;
                        }
                    }
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Debug.WriteLine(exc);
                }
            }
            return new Dictionary<string, string>();
        }
    }

    public class UserBookmarksViewModel : ViewModelBase
    {
        public class UserBookmarkItem : ViewModelBase
        {
            private bool _IsEditing = false;
            [XmlIgnoreAttribute]
            public bool IsEditing
            {
                get { return _IsEditing; }
                set
                {
                     if (value != _IsEditing)
                     {
                         _IsEditing = value;
                         RaisePropertyChanged();
                     }
                }
            }

            private string _BookmarkName = "";
            public string BookmarkName
            {
                get { return _BookmarkName; }
                set
                {
                    if (value != null)
                    {
                        if (!value.Equals(_BookmarkName))
                        {
                            _BookmarkName = value;
                            RaisePropertyChanged();
                        }
                    }
                }
            }

            private int _PageNumber = 0;
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

            private int _ObjectNumber = -1;
            public int ObjectNumber
            {
                get { return _ObjectNumber; }
                set { _ObjectNumber = value; }
            }

            public UserBookmarkItem()
            {

            }

            public UserBookmarkItem(string itemName, int pageNumber, int objNum)
            {
                _BookmarkName = itemName;
                _PageNumber = pageNumber;
                ObjectNumber = objNum;
            }
        }

        public delegate void FocusOnSelectedItemEventHandler(UserBookmarkItem item);
        public event FocusOnSelectedItemEventHandler FocusOnSelectedItemRequested = delegate { };
        private PDFViewCtrl _PDFViewCtrl;
        private string _DocumentTag;

        private ObservableCollection<UserBookmarkItem> _BookmarkList;
        public ObservableCollection<UserBookmarkItem> BookmarkList
        {
            get { return _BookmarkList; }
            set 
            { 
                _BookmarkList = value; 
                RaisePropertyChanged(); 
            }
        }

        private UserBookmarkItem _SelectedBookmark;
        public UserBookmarkItem SelectedBookmark
        {
            get { return _SelectedBookmark; }
            set
            {
                if (value != _SelectedBookmark)
                {
                    DeselectCurrentBookmark(_SelectedBookmark);
                    _SelectedBookmark = value;
                    RaisePropertyChanged();
                    if (value != null && !IsInEditMode)
                    {
                        _SpontaneousEditMode = true;
                        IsInEditMode = true;
                    }
                    else if (value == null && _SpontaneousEditMode)
                    {
                        _SpontaneousEditMode = false;
                        IsInEditMode = false;
                    }
                    ResolveCanRename();
                }
            }
        }

        /// <summary>
        /// Due to some strange problem on Windows 10 (or VS 2015) we have removed
        /// UserBookmarksViewModel(PDFViewCtrl eshnfeshfeshof, string docTag)
        /// and replaced it with the empty constructor and an Init function.
        /// 
        /// Before, trying to include the tools in another project would cause some very strange
        /// error messages when building in release mode.
        /// </summary>
        public UserBookmarksViewModel()
        {
            BookmarkList = new ObservableCollection<UserBookmarkItem>();
            CreateCommands();
        }

        public void Init(PDFViewCtrl ctrl, string docTag)
        {
            _PDFViewCtrl = ctrl;
            _DocumentTag = docTag;
            PopulateList();
        }

        //public UserBookmarksViewModel(PDFViewCtrl control, string docTag)
        //{
        //    _PDFViewCtrl = control;
        //    _DocumentTag = docTag;
        //    BookmarkList = new ObservableCollection<UserBookmarkItem>();
        //    CreateCommands();
        //    PopulateList();
        //}

        private async void PopulateList()
        {
            PDFDoc doc = _PDFViewCtrl.GetDoc();
            if (doc != null)
            {
                IList<UserBookmarkItem> items = await UserBookmarkManager.GetBookmarkListAsync(_DocumentTag);
                if (items != null && items.Count > 0)
                {
                    foreach (UserBookmarkItem item in items)
                    {
                        BookmarkList.Add(item);
                    }
                }
            }
            RaisePropertyChanged("ListHasItems");
        }


        #region Properties

        private bool _SpontaneousEditMode = false;
        private bool _IsInEditMode = false;
        public bool IsInEditMode
        {
            get { return _IsInEditMode; }
            set
            {
                if (value != _IsInEditMode)
                {
                    _IsInEditMode = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged("SelectionMode");
                    if (!value)
                    {
                        _SpontaneousEditMode = false;
                    }
                }
            }
        }

        public Windows.UI.Xaml.Controls.ListViewSelectionMode SelectionMode
        {
            get
            {
                if (IsInEditMode)
                {
                    return Windows.UI.Xaml.Controls.ListViewSelectionMode.Single;
                }
                return Windows.UI.Xaml.Controls.ListViewSelectionMode.None;
            }
        }

        private bool _IsEditingText = false;
        public bool CanRenameItem
        {
            get { return SelectedBookmark != null && !_IsEditingText; }
        }
        private void ResolveCanRename()
        {
            RaisePropertyChanged("CanRenameItem");
        }

        public bool ListHasItems
        {
            get { return BookmarkList.Count > 0; }
        }

        #endregion Properties


        #region Commands

        public RelayCommand AddBookmarkCommand { get; private set; }
        public RelayCommand EditBookmarksCommand { get; private set; }
        public RelayCommand RemoveBookmarkCommand { get; private set; }
        public RelayCommand RenameBookmarkCommand { get; private set; }
        public RelayCommand DoneEditingCommand { get; private set; }

        public RelayCommand ListViewItemClickCommand { get; private set; }

        private void CreateCommands()
        {
            AddBookmarkCommand = new RelayCommand(AddBookmarkCommandImpl);
            EditBookmarksCommand = new RelayCommand(EditBookmarksCommandImpl);
            RemoveBookmarkCommand = new RelayCommand(RemoveBookmarkCommandImpl);
            RenameBookmarkCommand = new RelayCommand(RenameBookmarkCommandImpl);
            DoneEditingCommand = new RelayCommand(DoneEditingCommandImpl);

            ListViewItemClickCommand = new RelayCommand(ListViewItemClickCommandImpl);
        }

        private void AddBookmarkCommandImpl(object sender)
        {
            PDFDoc doc = _PDFViewCtrl.GetDoc();
            if (doc != null)
            {
                try
                {
                    _PDFViewCtrl.DocLockRead();
                    int pgNum = _PDFViewCtrl.GetCurrentPage();
                    string pgNumString = string.Format(ResourceHandler.GetString("DefaultBookmarkFormat"), pgNum);

                    Page page = doc.GetPage(pgNum);
                    int objNum = page.GetSDFObj().GetObjNum();

                    UserBookmarkItem bookmark = new UserBookmarkItem(pgNumString, pgNum, objNum);
                    BookmarkList.Add(bookmark);
                    RaisePropertyChanged("ListHasItems");
                    IsInEditMode = true;
                    _SpontaneousEditMode = true;
                    SelectedBookmark = bookmark;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
                finally
                {
                    _PDFViewCtrl.DocUnlockRead();
                }
            }
        }

        private void EditBookmarksCommandImpl(object sender)
        {
            IsInEditMode = true;
        }

        private void RemoveBookmarkCommandImpl(object sender)
        {
            if (SelectedBookmark != null)
            {
                BookmarkList.Remove(SelectedBookmark);
                RaisePropertyChanged("ListHasItems");
            }
        }

        private void RenameBookmarkCommandImpl(object sender)
        {
            EditCurrentBookmark();
        }

        private void DoneEditingCommandImpl(object sender)
        {
            _SpontaneousEditMode = false;
            IsInEditMode = false;
            SelectedBookmark = null;
        }

        private void ListViewItemClickCommandImpl(object sentArgs)
        {
            Windows.UI.Xaml.Controls.ItemClickEventArgs args = sentArgs as Windows.UI.Xaml.Controls.ItemClickEventArgs;
            if (args != null)
            {
                UserBookmarkItem item = args.ClickedItem as UserBookmarkItem;
                if (item != null)
                {
                    if (item.PageNumber > 0 && item.PageNumber <= _PDFViewCtrl.GetPageCount())
                    {
                        _PDFViewCtrl.SetCurrentPage(item.PageNumber);
                        _PDFViewCtrl.SetPageViewMode(PDFViewCtrlPageViewMode.e_fit_page);
                    }
                }
            }
        }


        #endregion Commands


        #region Behaviour

        private string _OriginalBookmarkName = "";

        private void DeselectCurrentBookmark(UserBookmarkItem item)
        {
            if (item != null)
            {
                item.IsEditing = false;
            }
        }

        private void EditCurrentBookmark()
        {
            if (_SelectedBookmark != null)
            {
                _SelectedBookmark.IsEditing = true;
                _OriginalBookmarkName = _SelectedBookmark.BookmarkName;
                FocusOnSelectedItemRequested(_SelectedBookmark);
            }
        }

        #endregion Behaviour

        #region Public Functions for the View

        public void CancelEditingText()
        {
            _SelectedBookmark.BookmarkName = _OriginalBookmarkName;
            FinishEditingText();
        }

        public void FinishEditingText()
        {
            _SelectedBookmark.IsEditing = false;
        }

        public void TextBoxGotFocus()
        {
            _IsEditingText = true;
            ResolveCanRename();
        }

        public void TextBoxLostFocus()
        {
            _IsEditingText = false;
            ResolveCanRename();
        }

        public async void SaveBookmarks()
        {
            PDFDoc doc = _PDFViewCtrl.GetDoc();
            if (doc != null && _BookmarkList != null)
            {
                if (_BookmarkList.Count > 0)
                {
                    List<UserBookmarkItem> items = new List<UserBookmarkItem>();
                    items.AddRange(_BookmarkList);
                    await UserBookmarkManager.SaveBookmarkListAsync(_DocumentTag, items);
                }
                else
                {
                    await UserBookmarkManager.EraseBookmarkListAsync(_DocumentTag);
                }
            }
        }

        #endregion Public Functions for the View

        #region Back Key
        public bool GoBack()
        {
            if (IsInEditMode)
            {
                IsInEditMode = false;
                return true;
            }
            return false;
        }
        #endregion Back Key

    }
}

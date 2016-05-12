using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

// This partial definition can be shared between Phone and Windows, allowing them to each specify their own styles and extra code behind

namespace pdftron.PDF.Tools.Controls
{
    public class BookmarkItem
    {
        public Bookmark SourceBookmark { get; set; }
        public string Title { get; set; }
        public bool HasChildren { get; set; }

        public BookmarkItem(Bookmark bookmark)
        {
            SourceBookmark = bookmark;
            Title = bookmark.GetTitle();
            HasChildren = bookmark.HasChildren();
        }
    }

    public class AnimationCollection
    {
        public Storyboard EnterFromLeft { get; set; }
        public Storyboard LeaveToTheLeft { get; set; }
        public Storyboard EnterFromRight { get; set; }
        public Storyboard LeaveToTheRight { get; set; }

        public AnimationCollection()
        { }

        public void Refresh(double newWidth)
        {
            DoubleAnimation da = EnterFromLeft.Children[0] as DoubleAnimation;
            da.From = -newWidth;
            da = LeaveToTheLeft.Children[0] as DoubleAnimation;
            da.To = -newWidth;
            da = EnterFromRight.Children[0] as DoubleAnimation;
            da.From = newWidth;
            da = LeaveToTheRight.Children[0] as DoubleAnimation;
            da.To = newWidth;
        }
    }

    partial class Outline
    {
        private PDFViewCtrl _PDFViewCtrl;
        private List<Grid> _BookmarkPages;
        private List<ScrollViewer> _ScrollViewerPages;
        private List<AnimationCollection> _Animations;
        private int _CurrentIndex = -1;

        private Size _CurrentSize;

        /// <summary>
        /// The PDFViewCtrl form which the outline should get the document with PDFs.
        /// </summary>
        public PDFViewCtrl PDFViewCtrl
        {
            get { return _PDFViewCtrl; }
            set
            {
                _PDFViewCtrl = value;
                if (_PDFViewCtrl != null)
                {

                    _BookmarkPages = new List<Grid>();
                    _ScrollViewerPages = new List<ScrollViewer>();
                    _Animations = new List<AnimationCollection>();

                    Show();
                }
            }
        }

        /// <summary>
        /// Creates a new outline (bookmark) control without any association with a PDFViewCtrl.
        /// Use the PDFViewCtrl property to associated the outline control with a document and populate it.
        /// </summary>
        public Outline()
        {
            this.InitializeComponent();
            this.SizeChanged += Outline_SizeChanged;
        }

        /// <summary>
        /// Creates a new outline (bookmark) control associated with a PDFViewCtrl. The control will show the bookmarks
        /// in the document that is open in the PDFViewCtrl.
        /// </summary>
        public Outline(PDFViewCtrl ctrl)
        {
            this.InitializeComponent();
            this.SizeChanged += Outline_SizeChanged;
            this.PDFViewCtrl = ctrl;
        }


        void Outline_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetSize(e.NewSize);
        }

        private void SetSize(Size size)
        {
            _CurrentSize = size;
            RectangleGeometry clipRect = new RectangleGeometry();
            clipRect.Rect = new Windows.Foundation.Rect(0, 0, size.Width, size.Height);
            ContentPanel.Clip = clipRect;

            foreach(AnimationCollection collection in _Animations)
            {
                collection.Refresh(size.Width);
            }

            if (_ScrollViewerPages != null)
            {
                foreach (ScrollViewer scroller in _ScrollViewerPages)
                {
                    FrameworkElement scrollContent = scroller.Content as FrameworkElement;
                    if (scrollContent != null)
                    {
                        scrollContent.Width = size.Width;
                    }
                }
            }
        }

        public event EventHandler<ItemClickEventArgs> ItemClicked;



        /// <summary>
        /// Resets the Bookmark display, so that next time it will create bookmarks from scratch.
        /// </summary>
        public void Reset()
        {
            _BookmarkPages.Clear();
            _ScrollViewerPages.Clear();
            _Animations.Clear();
            _CurrentIndex = -1;
            ContentPanel.Children.Clear();
        }

        /// <summary>
        /// Animates the Bookmarks panel onto the screen.
        /// </summary>
        public async void Show()
        {
            //ContentPanel.Opacity = 1;
            //SlideInPanel.Begin();
            IList<BookmarkItem> items;
            NoBookmarksTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            if (ContentPanel.Children.Count < 1)
            {
                items = await GetBookmarkListAsync(null);
                if (items.Count == 0)
                {
                    NoBookmarksTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    Grid ContentGrid = CreateBookmarkGrid(null, items);
                    _BookmarkPages.Add(ContentGrid);
                    ContentPanel.Children.Add(ContentGrid);
                    _CurrentIndex = 0;
                }
            }
        }

        /// <summary>
        /// Animates the Bookmarks panel of the screen.
        /// </summary>
        public void Hide()
        {
            //SlideOutPanel.Begin();
        }

        private void SlideOutCompleted(object sender, object e)
        {
            ContentPanel.Opacity = 0;
        }

        private IAsyncOperation<IList<BookmarkItem>> GetBookmarkListAsync(Bookmark root)
        {
            Task<IList<BookmarkItem>> t = new Task<IList<BookmarkItem>>(() =>
            {
                return GetBookmarkList(root);
            });
            t.Start();
            return t.AsAsyncOperation<IList<BookmarkItem>>();
        }

        private IList<BookmarkItem> GetBookmarkList(Bookmark root)
        {
            IList<BookmarkItem> items = new List<BookmarkItem>();

            Bookmark current;
            if (root == null)
            {
                current = this.PDFViewCtrl.GetDoc().GetFirstBookmark();
            }
            else
            {
                current = root;
            }

            int numbookmarks = 0;
            while (current.IsValid())
            {
                BookmarkItem item = new BookmarkItem(current);
                items.Add(item);
                current = current.GetNext();
                numbookmarks++;
            }
            if (root == null && numbookmarks == 1)
            {
                IList<BookmarkItem> nextLevel = GetBookmarkList(this.PDFViewCtrl.GetDoc().GetFirstBookmark().GetFirstChild());
                if (nextLevel != null && nextLevel.Count > 0)
                {
                    return nextLevel;
                }
            }

            return items;
        }

        private Grid CreateBookmarkGrid(Bookmark root, IList<BookmarkItem> items)
        {
            Grid hostGrid = new Grid();

            RowDefinition topRow = new RowDefinition();
            topRow.Height = new GridLength(1, GridUnitType.Auto);
            hostGrid.RowDefinitions.Add(topRow);
            RowDefinition mainRow = new RowDefinition();
            mainRow.Height = new GridLength(1, GridUnitType.Star);
            hostGrid.RowDefinitions.Add(mainRow);
            hostGrid.RenderTransform = new TranslateTransform();

            TextBlock title = new TextBlock();
            title.FontSize = 26;
            title.Margin = new Windows.UI.Xaml.Thickness(80, 20, 20, 30);
            title.HorizontalAlignment = HorizontalAlignment.Left;
            title.Style = this.Resources["HeaderTextBlockStyle"] as Style;
            hostGrid.Children.Add(title);

            if (root == null)
            {
                title.Text = ResourceHandler.GetString("Outline_DefaultHeader");
                title.Margin = new Thickness(20, 20, 20, 30);
            }
            else
            {
                title.Text = root.GetTitle();
                Button backButton = new Button();
                backButton.Style = this.Resources["BackwardNavigationButtonStyle"] as Style;
                backButton.HorizontalAlignment = HorizontalAlignment.Left;
                backButton.VerticalAlignment = VerticalAlignment.Top;
                backButton.Margin = new Thickness(10, 0, 0, 0);
                backButton.Click += backButton_Click;
                hostGrid.Children.Add(backButton);
            }

            ScrollViewer itemScroller = new ScrollViewer();
            itemScroller.Content = CreateBookmarkStack(items);
            itemScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            itemScroller.HorizontalScrollMode = ScrollMode.Disabled;
            itemScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            itemScroller.VerticalScrollMode = ScrollMode.Auto;
            itemScroller.ZoomMode = ZoomMode.Disabled;
            itemScroller.Padding = new Thickness(0, 0, 10, 0);
            _ScrollViewerPages.Add(itemScroller);

            // Animations
            AnimationCollection animations = new AnimationCollection();
            animations.EnterFromRight = EnterFromRight(hostGrid);
            animations.EnterFromLeft = EnterFromLeft(hostGrid);
            animations.LeaveToTheRight = LeaveToTheRight(hostGrid);
            animations.LeaveToTheLeft = LeaveToTheLeft(hostGrid);

            _Animations.Add(animations);
            itemScroller.SetValue(Grid.RowProperty, 1);
            hostGrid.Children.Add(itemScroller);

            return hostGrid;

        }

        private StackPanel CreateBookmarkStack(IList<BookmarkItem> items)
        {
            StackPanel bookmarkStack = new StackPanel();
            if (_CurrentSize != null && !double.IsNaN(_CurrentSize.Width))
            {
                bookmarkStack.Width = _CurrentSize.Width;
            }
            foreach (BookmarkItem item in items)
            {
                Grid itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                itemGrid.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch;
                itemGrid.Height = (double)this.Resources["BookmarksItemHeight"];
                Button itemButton = new Button();
                TextBlock title = new TextBlock();
                title.TextTrimming = TextTrimming.CharacterEllipsis;
                title.Text = item.Title;
                itemButton.Content = title;

                itemButton.Height = (double)this.Resources["BookmarksItemHeight"];
                itemButton.Tag = item.SourceBookmark;
                itemButton.BorderThickness = new Thickness(0);
                itemButton.Click += itemButton_Click;
                itemButton.VerticalAlignment = VerticalAlignment.Center;
                itemGrid.Children.Add(itemButton);
                if (item.HasChildren)
                {
                    //itemButton.Padding = new Thickness(10, 0, (double)this.Resources["NavigationButtonSize"], 0);
                    Button childButton = new Button();
                    childButton.HorizontalAlignment = HorizontalAlignment.Right;
                    childButton.Tag = item.SourceBookmark;
                    childButton.Style = this.Resources["ForwardNavigationButtonStyle"] as Style;
                    childButton.SetValue(Grid.ColumnProperty, 1);
                    itemGrid.Children.Add(childButton);
                    childButton.Click += childButton_Click;
                }
                bookmarkStack.Children.Add(itemGrid);
            }

            return bookmarkStack;
        }

        private void itemButton_Click(object sender, RoutedEventArgs e)
        {
            Utilities.AnalyticsHandlerBase.CURRENT.SendEvent("Bookmark", "Navigated by Outline List");

            Button button = sender as Button;
            Bookmark bookmark = button.Tag as Bookmark;
            pdftron.PDF.Action action = bookmark.GetAction();
            if (action.IsValid() && action.GetType() == pdftron.PDF.ActionType.e_GoTo || action.GetDest().IsValid())
            {
                this.PDFViewCtrl.ExecuteAction(action);
            }
            if (ItemClicked != null)
            {
                ItemClicked(this, new ItemClickEventArgs());
            }
        }

        private async void childButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Bookmark bookmark = button.Tag as Bookmark;
            IList<BookmarkItem> items = await GetBookmarkListAsync(bookmark.GetFirstChild());

            Grid ContentGrid = CreateBookmarkGrid(bookmark, items);
            _BookmarkPages.Add(ContentGrid);
            ContentPanel.Children.Add(ContentGrid);
            _Animations[_CurrentIndex].LeaveToTheLeft.Begin();
            _Animations.Last().EnterFromRight.Begin();
            _CurrentIndex++;
        }

        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            _BookmarkPages.RemoveAt(_CurrentIndex);
            _ScrollViewerPages.RemoveAt(_CurrentIndex);
            _ScrollViewerPages.Last().VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _Animations.Last().LeaveToTheRight.Begin();
            _Animations.RemoveAt(_CurrentIndex);
            _Animations.Last().EnterFromLeft.Begin();
            _CurrentIndex--;
            _BookmarkPages[_CurrentIndex].Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private Storyboard EnterFromRight(FrameworkElement target)
        {
            Storyboard sb = new Storyboard();
            String ID = target.Name.ToString();
            double from = _CurrentSize.Width;
            double to = 0;
            DoubleAnimation translate = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                RepeatBehavior = new RepeatBehavior { Count = 1 },
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut },
            };

            target.Resources.Add(ID + "EnterFromRightAnimation", sb);
            Storyboard.SetTarget(translate, target);
            Storyboard.SetTargetName(translate, target.Name);
            Storyboard.SetTargetProperty(translate, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Completed += EnterLeftCompleted;
            sb.Children.Add(translate);

            return sb;
        }

        private void EnterLeftCompleted(object sender, object e)
        {
        }

        private Storyboard EnterFromLeft(FrameworkElement target)
        {
            Storyboard sb = new Storyboard();
            String ID = target.Name.ToString();
            double from = -_CurrentSize.Width;
            double to = 0;
            DoubleAnimation translate = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                RepeatBehavior = new RepeatBehavior { Count = 1 },
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut },
            };

            target.Resources.Add(ID + "EnterFromLeftAnimation", sb);
            Storyboard.SetTarget(translate, target);
            Storyboard.SetTargetName(translate, target.Name);
            Storyboard.SetTargetProperty(translate, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Completed += EnterRightCompleted;
            sb.Children.Add(translate);

            return sb;
        }

        private void EnterRightCompleted(object sender, object e)
        {
            _ScrollViewerPages.Last().VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        private Storyboard LeaveToTheRight(FrameworkElement target)
        {
            Storyboard sb = new Storyboard();
            String ID = target.Name.ToString();
            double from = 0;
            double to = _CurrentSize.Width;
            DoubleAnimation translate = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                RepeatBehavior = new RepeatBehavior { Count = 1 },
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut },
            };

            target.Resources.Add(ID + "LeaveToTheRightAnimation", sb);
            Storyboard.SetTarget(translate, target);
            Storyboard.SetTargetName(translate, target.Name);
            Storyboard.SetTargetProperty(translate, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(translate);
            sb.Completed += LeaveRightCompleted;

            return sb;
        }

        private void LeaveRightCompleted(object sender, object e)
        {
            if (ContentPanel.Children.Count > 0)
            {
                ContentPanel.Children.RemoveAt(ContentPanel.Children.Count - 1);
            }
        }

        private Storyboard LeaveToTheLeft(FrameworkElement target)
        {
            Storyboard sb = new Storyboard();
            String ID = target.Name.ToString();
            double from = 0;
            double to = -_CurrentSize.Width;
            DoubleAnimation translate = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                RepeatBehavior = new RepeatBehavior { Count = 1 },
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut },
            };

            target.Resources.Add(ID + "LeaveToTheLeft", sb);
            Storyboard.SetTarget(translate, target);
            Storyboard.SetTargetName(translate, target.Name);
            Storyboard.SetTargetProperty(translate, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(translate);
            sb.Completed += LeaveLeftCompleted;

            return sb;
        }

        private void LeaveLeftCompleted(object sender, object e)
        {
            if (_BookmarkPages.Count > 1)
            {
                _BookmarkPages[_BookmarkPages.Count - 2].Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Will go back if possible.
        /// </summary>
        /// <returns>Return true if it went back, false otherwise</returns>
        public bool GoBack()
        {
            return false;
        }
    }
}

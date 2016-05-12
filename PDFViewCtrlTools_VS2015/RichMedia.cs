using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;
using UIPopup = Windows.UI.Xaml.Controls.Primitives.Popup;

using pdftron.PDF;
using pdftron.Common;
using pdftron.PDF.Annots;

using PDFPoint = pdftron.PDF.Point;
using PDFRect = pdftron.PDF.Rect;
using PDFPage = pdftron.PDF.Page;
using PDFDouble = pdftron.Common.DoubleRef;
using pdftron.SDF;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.Storage;




namespace pdftron.PDF.Tools
{
    /// <summary>
    /// 
    /// 
    /// Note: Currently, PointerEvents when the textbox for editing FreeText annotations is open do not fire
    /// Therefore, we will allow regular scroll and zoom, but we won't allow resize while in text edit mode.
    /// </summary>
    class RichMedia : Tool
    {
        protected static String[] SUPPORTED_FORMATS = { ".3gp", ".3gpp", ".3g2", ".3gp2", ".mp4", ".m4a", ".m4v", ".mp4v", ".mov", 
                                                          ".m2ts", ".asf", ".wm", ".wmv", ".wma", ".aac", ".adt", "adts", 
                                                          ".mp3", ".wav", ".avi", ".ac3", ".ec3"};

        protected bool mHasNoName = false;

        protected Canvas mViewerCanvas;

        protected Border mVideoBorder;
        protected MediaElement mVideoPlayer;
        protected bool mISPlaying = false;
        protected Button mStartPauseButton;

        IReadOnlyList<StorageFile> mListOfFiles;

        System.Text.RegularExpressions.Regex mFileNameRegex = null;

        public RichMedia(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_rich_media;
            mToolMode = ToolType.e_rich_media;
            mViewerCanvas = mPDFView.GetAnnotationCanvas();
            mViewerCanvas.Children.Add(this);

            string regexSearch = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            mFileNameRegex = new System.Text.RegularExpressions.Regex(string.Format("[{0}]", System.Text.RegularExpressions.Regex.Escape(regexSearch)));
        }

        internal override void OnClose()
        {
            base.OnClose();
            mVideoPlayer.Stop();
            mViewerCanvas.Children.Remove(this);
        }


        internal override bool TappedHandler(object sender, TappedRoutedEventArgs e)
        {
            if (mJustSwitchedFromAnotherTool)
            {
                mJustSwitchedFromAnotherTool = false;

                // We know that mAnnot is the Rich Media
                CreateVideoPlayer();
                ExtractVideo();
            }
            else
            {
                mNextToolMode = ToolType.e_pan;
            }

            return true;
        }

        internal override bool OnScale()
        {
            PositionVideoPlayer();
            return true;
        }

        internal override bool OnSize()
        {
            PositionVideoPlayer();
            return true;
        }

        protected void CreateVideoPlayer()
        {
            BuildAnnotBBox();

            mVideoBorder = new Border();
            mVideoBorder.PointerPressed += VideoBorder_PointerPressed;
            mVideoBorder.Tapped += VideoBorder_Tapped;
            mVideoBorder.MinWidth = 170;
            mVideoBorder.MinHeight = 120;

            mVideoBorder.BorderThickness = new Thickness(3);
            mVideoBorder.BorderBrush = new SolidColorBrush(Colors.Silver);
            mVideoBorder.Background = new SolidColorBrush(Colors.Black);
            this.Children.Add(mVideoBorder);

            Grid grid = new Grid();
            
            mVideoPlayer = new MediaElement();
            mVideoPlayer.AutoPlay = true;
            mVideoPlayer.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
            mVideoPlayer.Tapped += mVideoPlayer_Tapped;
            grid.Children.Add(mVideoPlayer);

            mStartPauseButton = new Button();
            mStartPauseButton.Content = "Pause";
            mStartPauseButton.Click += mStartPauseButton_Click;
            mStartPauseButton.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
            mStartPauseButton.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom;
            mStartPauseButton.Width = 80;
            grid.Children.Add(mStartPauseButton);

            Button stopButton = new Button();
            stopButton.Content = "Stop";
            stopButton.Click += stopButton_Click;
            stopButton.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
            stopButton.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom;
            stopButton.Width = 80;
            grid.Children.Add(stopButton);

            mVideoBorder.Child = grid;
            mISPlaying = true;

            PositionVideoPlayer();
        }

        void mVideoPlayer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (mISPlaying)
            {
                mVideoPlayer.Pause();
                mStartPauseButton.Content = "Play";
                mISPlaying = false;
            }
            else
            {
                mVideoPlayer.Play();
                mStartPauseButton.Content = "Pause";
                mISPlaying = true;
            }
        }

        void VideoBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        void VideoBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DisableScrolling();
            mVideoPlayer.Play();
            e.Handled = true;
        }

        void PositionVideoPlayer()
        {
            PDFRect rect = new Rect(mAnnotBBox.x1, mAnnotBBox.y1, mAnnotBBox.x2, mAnnotBBox.y2);
            rect = ConvertFromPageRectToCanvasRect(rect, mAnnotPageNum);
            rect.Normalize();

            mVideoBorder.SetValue(Canvas.LeftProperty, rect.x1);
            mVideoBorder.SetValue(Canvas.TopProperty, rect.y1);
            mVideoBorder.Width = rect.Width() + 6;
            mVideoBorder.Height = rect.Height() + 56;

            mVideoPlayer.Width = rect.Width();
            mVideoPlayer.Height = rect.Height();
        }



        void mStartPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (mISPlaying)
            {
                mVideoPlayer.Pause();
                mStartPauseButton.Content = "Play";
                mISPlaying = false;
            }
            else
            {
                mVideoPlayer.Play();
                mStartPauseButton.Content = "Pause";
                mISPlaying = true;
            }
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            mVideoPlayer.Stop();
            mStartPauseButton.Content = "Play";
            mISPlaying = false;
        }

        private async void ExtractVideo()
        {
            try
            {
                mListOfFiles = await Windows.Storage.ApplicationData.Current.TemporaryFolder.GetFilesAsync();
                string fileName = await CopyMediaToFileAsync();
                if (fileName.Equals(""))
                {
                    mNextToolMode = ToolType.e_pan;
                }
                StorageFile file = await Windows.Storage.ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);
                IRandomAccessStream iras = await file.OpenAsync(FileAccessMode.Read);
                mVideoPlayer.SetSource(iras, file.ContentType);
            }
            catch (Exception)
            {
                // log exception here
            }
        }


        private IAsyncOperation<String> CopyMediaToFileAsync()
        {
            Task<String> t = new Task<String>(() =>
            {
                string fileName = CopyMediaToFile();
                return fileName;
            });
            t.Start();
            return t.AsAsyncOperation<String>();
        }

        protected string CopyMediaToFile()
        {
            string fileName = "";

            mPDFView.DocLockRead();
            try
            {
                Obj ad = mAnnot.GetSDFObj();
                Obj mc = ad.FindObj("RichMediaContent");
                if (mc != null)
                {
                    NameTree assets = new NameTree(mc.FindObj("Assets"));
                    if (assets.IsValid())
                    {
                        NameTreeIterator nti = assets.GetIterator();
                        for (; nti.HasNext(); nti.Next())
                        {
                            String assetName = nti.Key().GetAsPDFText();

                            

                            // We want to make sure that the file type is supported. Sometimes a RichMedia annotaiton has more than
                            // one assets, some of which may not be the video we want to play.
                            fileName = GenerateFileName(assetName, System.IO.Path.GetFileName(mPDFView.GetDoc().GetFileName()), mAnnotPageNum);

                            if (!IsMediaFileSupported(fileName))
                            {
                                fileName = "";
                                continue;
                            }


                            // We want to check to make sure that the movie has a name at all. Otherwise, we risk having 2 movies in 2 different
                            // PDFDocs map to the same file (if the PDFDoc has no path, which might happen when the file comes from certain file picker
                            // services that don't set a path.
                            string movieName = mFileNameRegex.Replace(assetName, "");
                            string noExtensionName = System.IO.Path.GetFileNameWithoutExtension(movieName);
                            bool createNewFile = false;
                            if (string.IsNullOrWhiteSpace(noExtensionName))
                            {
                                createNewFile = true;
                            }

                            string fullFileName = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path + "\\" + fileName;

                            // Before going on with the extraction, let's check if the file already
                            // exists in our temp folder
                            if (createNewFile || !DoesFileExist(fileName))
                            {
                                FileSpec fileSpec = new FileSpec(nti.Value());
                                pdftron.Filters.IFilter stm = fileSpec.GetFileData();
                                if (stm != null)
                                {
                                    stm.WriteToFile(fullFileName, false);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                fileName = "";
            }
            finally
            {
                mPDFView.DocUnlockRead();
            }
            return fileName;
        }


        protected string GenerateFileName(string movieFileName, string docName, int pageNumber)
        {
            if (movieFileName.Length < 4)
            {
                return null;
            }

            string newName = docName + movieFileName.Substring(0, movieFileName.Length - 4) + "_" + pageNumber + movieFileName.Substring(movieFileName.Length - 4);

            newName = mFileNameRegex.Replace(newName, "");

            string justName = System.IO.Path.GetFileNameWithoutExtension(newName);


            if (string.IsNullOrWhiteSpace(justName))
            {
                return "";
            }

            return newName;

        }

        protected bool IsMediaFileSupported(string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName);
            return SUPPORTED_FORMATS.Contains(extension);
        }

        protected bool DoesFileExist(string fileName)
        {
            foreach (StorageFile file in mListOfFiles)
            {
                if (fileName.Equals(file.Name))
                {
                    return true;
                }
            }
            return false;
        }

        internal override bool KeyDownAction(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.P)
            {
                PositionVideoPlayer();
            }
            if (e.Key == Windows.System.VirtualKey.I)
            {
                RichMedia canv = mViewerCanvas.Children[0] as RichMedia;
                Border b = canv.Children[0] as Border;
                Grid g = b.Child as Grid;
                MediaElement medi = g.Children[0] as MediaElement;
            }

            return true;
        }

        internal bool IsEmptyFileName(string fileName)
        {
            System.Text.RegularExpressions.Regex emptySearch = new System.Text.RegularExpressions.Regex(@"^_{\d}*");
            System.Text.RegularExpressions.Match match = emptySearch.Match(fileName);
            return match.Success;
        }
    }
}

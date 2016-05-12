using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

using UIPoint = Windows.Foundation.Point;
using UIRect = Windows.Foundation.Rect;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace pdftron.PDF.Tools
{
    /// <summary>
    /// This class is used to let the user draw a shape (signature) and then produce a PDFPage which the user can use
    /// as a stamp.
    /// For this, we will create a document where a page can be used as a stamp.
    /// </summary>
    public sealed partial class SignatureDialog : UserControl
    {
        private const double START_DRAWING_THRESHOLD = 16;
        private static string SIGNATURE_FILE_NAME = "SignatureFile.CompleteReader";

        private Popup _Popup;

        // Drawing
        private List<uint> mPointerIDs = new List<uint>();
        private bool mIsStylus = false; // We don't want to do noise correction for stylus points.
        private UIPoint mDownPoint;
        private UIPoint mCurrentPoint;

        private UIPoint mLastPoint;
        private UIPoint mTwoPointsBack;
        private UIPoint mOnePointBack;
        private bool mStartedPathSmoothing = false;

        private Path mDrawingPath;
        private PathFigureCollection mPathPoints;
        private PathFigure mCurrentPathFigure;
        private bool mIsCurrentPathAdded;

        private double mStrokeThickness = 6;
        private Windows.UI.Color mStrokeColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);

        // storing the data
        private List<List<UIPoint>> mListOfPointLists;
        private List<UIPoint> mCurrentPointList;

        // borders
        private double mTop;
        private double mBottom;
        private double mLeft;
        private double mRight;

        private double mCurrentPathTop;
        private double mCurrentPathBottom;
        private double mCurrentPathLeft;
        private double mCurrentPathRight;

        // Handler for when done
        public delegate void OnSignatureDoneDelegate(bool wantItem);
        public event OnSignatureDoneDelegate SignatureDone;

        public delegate void OnUseDefaultDelegate();
        public event OnUseDefaultDelegate UseDefaultSelected;

        // properties

        /// <summary>
        /// Gets or sets whether to show the check box to overwrite the currently saved signature
        /// </summary>
        public bool ShowOptionToOverwriteSignature
        {
            get
            {
                return MakeDefaultCheckBox.Visibility == Windows.UI.Xaml.Visibility.Visible;
            }
            set
            {
                if (value)
                {
                    MakeDefaultCheckBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    MakeDefaultCheckBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }

        // Gets or sets whether or not the button to use the saved signature
        public bool ShowOptionToUseDefaultSignature
        {
            get
            {
                return UseDefaultButton.Visibility == Windows.UI.Xaml.Visibility.Visible;
            }
            set
            {
                if (value)
                {
                    UseDefaultButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    UseDefaultButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }

        public bool ShouldOverwriteOldSignature
        {
            get
            {
                return (MakeDefaultCheckBox.IsChecked == true);
            }
            set
            {
                MakeDefaultCheckBox.IsChecked = value;
            }
        }
        public double LeftMost
        { get { return mLeft; } }
        public double RightMost
        { get { return mRight; } }
        public double TopMost
        { get { return mTop; } }
        public double BottomMost
        { get { return mBottom; } }
        public Windows.UI.Color StrokeColor
        { get { return mStrokeColor; } }
        public List<List<UIPoint>> Strokes
        { get { return (List<List<UIPoint>>)mListOfPointLists; } }

        /// <summary>
        /// Gets whether the signature was created using a stylus
        /// </summary>
        public bool MadeWithStylus
        {
            get { return mIsStylus; }
        }

        public SignatureDialog()
        {
            this.InitializeComponent();
            Window.Current.SizeChanged += WindowSizeChangedHandler;
            this.Loaded += (s, e) => { HandleSizeChange(Window.Current.Bounds.Width, Window.Current.Bounds.Height); };

            this.Width = Window.Current.Bounds.Width;
            this.Height = Window.Current.Bounds.Height;

            _Popup = new Popup();
            _Popup.Child = this;

            _Popup.IsOpen = true;

            // Event handlers for drawing
            SigningArea.PointerPressed += SigningArea_PointerPressed;
            SigningArea.PointerMoved += SigningArea_PointerMoved;
            SigningArea.PointerReleased += SigningArea_PointerReleased;
            SigningArea.PointerCanceled += SigningArea_PointerCanceled;
            SigningArea.PointerCaptureLost += SigningArea_PointerCaptureLost;
            SigningArea.Tapped += SigningArea_Tapped;

            // Set up for drawing
            mDrawingPath = new Path();
            mDrawingPath.StrokeEndLineCap = PenLineCap.Round;
            mDrawingPath.StrokeStartLineCap = PenLineCap.Round;
            mDrawingPath.StrokeLineJoin = PenLineJoin.Round;
            mDrawingPath.StrokeThickness = mStrokeThickness;
            mDrawingPath.Stroke = new SolidColorBrush(mStrokeColor);

            mPathPoints = new PathFigureCollection();
            PathGeometry geom = new PathGeometry();
            geom.Figures = mPathPoints;
            mDrawingPath.Data = geom;

            SigningArea.Children.Add(mDrawingPath);

            mListOfPointLists = new List<List<UIPoint>>();

            // We want to keep track of the extreme positions in each direction.
            mTop = Int16.MaxValue;
            mBottom = -1;
            mLeft = Int16.MaxValue;
            mRight = -1;

            //CheckBoxIfNoFile();
        }

        public void Close()
        {
            _Popup.IsOpen = false;
        }

        /// <summary>
        ///  If there is no file saved, we want to make the checkbox checked by default.
        /// </summary>
        private async void CheckBoxIfNoFile()
        {
            Windows.Storage.IStorageItem signatureFile = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFileAsync(SIGNATURE_FILE_NAME);
            if (signatureFile == null)
            {
                MakeDefaultCheckBox.IsChecked = true;
            }
        }

        private void WindowSizeChangedHandler(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            HandleSizeChange(e.Size.Width, e.Size.Height);
        }

        private void HandleSizeChange(double width, double height)
        {
            this.Width = width;
            this.Height = height;
            ClearSignature();

            // see if all the options fit
            UIRect leftRect = GetElementRect(LeftSideOptionsStack);
            UIRect rightRect = GetElementRect(RightSideOptionsStack);
            if (leftRect.Right > rightRect.Left)
            {
                RightSideOptionsStack.SetValue(Grid.RowProperty, 1);
            }
            else
            {
                RightSideOptionsStack.SetValue(Grid.RowProperty, 0);
            }
        }

        private UIRect GetElementRect(Windows.UI.Xaml.FrameworkElement element)
        {
            GeneralTransform elementtransform = element.TransformToVisual(null);
            UIRect rect = elementtransform.TransformBounds(new UIRect(new UIPoint(0, 0), new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight)));
            return rect;
        }

        #region Drawing
        private void SigningArea_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            mPointerIDs.Add(e.Pointer.PointerId);
            SigningArea.CapturePointer(e.Pointer);

            Windows.UI.Input.PointerPoint pointerPoint = e.GetCurrentPoint(null);
            if (mPointerIDs.Count == 1 && !pointerPoint.Properties.IsRightButtonPressed)
            {
                mIsStylus = false;
                if (pointerPoint.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
                {
                    mIsStylus = true;
                }

                mDownPoint = e.GetCurrentPoint(SigningArea).Position;
                mTwoPointsBack = new UIPoint(mDownPoint.X, mDownPoint.Y);

                mCurrentPathFigure = new PathFigure();
                mCurrentPathFigure.StartPoint = new UIPoint(mDownPoint.X, mDownPoint.Y);

                mCurrentPointList = new List<UIPoint>();
                mCurrentPointList.Add(new UIPoint(mDownPoint.X, mDownPoint.Y));

                mIsCurrentPathAdded = false;

                mCurrentPathTop = mDownPoint.Y;
                mCurrentPathBottom = mDownPoint.Y;
                mCurrentPathLeft = mDownPoint.X;
                mCurrentPathRight = mDownPoint.X;
                mLastPoint = new UIPoint(mDownPoint.X, mDownPoint.Y);
            }
            else
            {
                if (mIsCurrentPathAdded)
                {
                    mPathPoints.Remove(mCurrentPathFigure);
                    //mSecondaryPathPoints.Remove(mSecondaryCurrentPathFigure);
                    mListOfPointLists.Remove(mCurrentPointList);
                    mIsCurrentPathAdded = false;
                }
            }
        }
        void SigningArea_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerIDs.Count == 1)
            {


                //mCurrentPoint = e.GetCurrentPoint(SigningArea).Position;
                IList<Windows.UI.Input.PointerPoint> pointerPoints = e.GetIntermediatePoints(SigningArea);
                for (int i = pointerPoints.Count - 1; i >= 0; i--)
                {
                    mCurrentPoint = pointerPoints[i].Position;

                    // check bounds
                    if (mCurrentPoint.X < mStrokeThickness)
                    {
                        mCurrentPoint.X = 0 + mStrokeThickness;
                    }
                    if (mCurrentPoint.X > SigningArea.ActualWidth - mStrokeThickness)
                    {
                        mCurrentPoint.X = SigningArea.ActualWidth - mStrokeThickness;
                    }
                    if (mCurrentPoint.Y < mStrokeThickness)
                    {
                        mCurrentPoint.Y = mStrokeThickness;
                    }
                    if (mCurrentPoint.Y > SigningArea.ActualHeight - mStrokeThickness)
                    {
                        mCurrentPoint.Y = SigningArea.ActualHeight - mStrokeThickness;
                    }

                    // make sure we've actually moved...
                    if (Math.Abs(mCurrentPoint.X - mLastPoint.X) < 0.1 && Math.Abs(mCurrentPoint.Y - mLastPoint.Y) < 0.1)
                    {
                        continue;
                    }

                    if (mIsStylus)
                    {
                        AddPointToPath(mCurrentPoint);
                    }
                    else
                    {
                        if (!mStartedPathSmoothing)
                        {
                            mStartedPathSmoothing = true;
                            mOnePointBack = new UIPoint(mCurrentPoint.X, mCurrentPoint.Y);
                        }
                        else
                        {
                            UIPoint newPoint = UtilityFunctions.GetPointCloserToLine(mTwoPointsBack, mCurrentPoint, mOnePointBack);
                            mTwoPointsBack.X = mOnePointBack.X;
                            mTwoPointsBack.Y = mOnePointBack.Y;
                            mOnePointBack.X = mCurrentPoint.X;
                            mOnePointBack.Y = mCurrentPoint.Y;
                            AddPointToPath(newPoint);
                        }
                    }
                    //AddPointToPath(mCurrentPoint);
                }
                if (mIsCurrentPathAdded)
                {
                    (mDrawingPath.Data as PathGeometry).Figures = mPathPoints;
                    //(mSecondaryDrawingPath.Data as PathGeometry).Figures = mSecondaryPathPoints;
                }
            }
        }

        private void AddPointToPath(UIPoint point)
        {
            QuadraticBezierSegment qbc = new QuadraticBezierSegment();
            double oX = mLastPoint.X;
            double oY = mLastPoint.Y;
            double nX = point.X;
            double nY = point.Y;
            qbc.Point1 = new UIPoint(oX, oY);
            qbc.Point2 = new UIPoint((oX + nX) / 2, (oY + nY) / 2);
            mCurrentPathFigure.Segments.Add(qbc);

            mLastPoint.X = point.X;
            mLastPoint.Y = point.Y;

            mCurrentPointList.Add(mCurrentPoint);

            if (mCurrentPoint.Y < mCurrentPathTop)
            {
                mCurrentPathTop = mCurrentPoint.Y;
            }
            if (mCurrentPoint.Y > mCurrentPathBottom)
            {
                mCurrentPathBottom = mCurrentPoint.Y;
            }
            if (mCurrentPoint.X < mCurrentPathLeft)
            {
                mCurrentPathLeft = mCurrentPoint.X;
            }
            if (mCurrentPoint.X > mCurrentPathRight)
            {
                mCurrentPathRight = mCurrentPoint.X;
            }

            if (!mIsCurrentPathAdded)
            {
                double xDist = mDownPoint.X - point.X;
                double yDist = mDownPoint.Y - point.Y;

                if ((xDist * xDist) + (yDist * yDist) >= START_DRAWING_THRESHOLD)
                {
                    mIsCurrentPathAdded = true;
                    mPathPoints.Add(mCurrentPathFigure);
                    mListOfPointLists.Add(mCurrentPointList);
                }
            }
        }

        private void SigningArea_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (mPointerIDs.Count == 1 && mPointerIDs[0] == e.Pointer.PointerId)
            {
                if (mCurrentPathTop < mTop)
                {
                    mTop = mCurrentPathTop;
                }
                if (mCurrentPathBottom > mBottom)
                {
                    mBottom = mCurrentPathBottom;
                }
                if (mCurrentPathLeft < mLeft)
                {
                    mLeft = mCurrentPathLeft;
                }
                if (mCurrentPathRight > mRight)
                {
                    mRight = mCurrentPathRight;
                }
            }

            if (mIsCurrentPathAdded && !mIsStylus)
            {
                // check bounds
                if (mCurrentPoint.X < mStrokeThickness)
                {
                    mCurrentPoint.X = 0 + mStrokeThickness;
                }
                if (mCurrentPoint.X > SigningArea.ActualWidth - mStrokeThickness)
                {
                    mCurrentPoint.X = SigningArea.ActualWidth - mStrokeThickness;
                }
                if (mCurrentPoint.Y < mStrokeThickness)
                {
                    mCurrentPoint.Y = mStrokeThickness;
                }
                if (mCurrentPoint.Y > SigningArea.ActualHeight - mStrokeThickness)
                {
                    mCurrentPoint.Y = SigningArea.ActualHeight - mStrokeThickness;
                }

                // make sure we've actually moved...
                if (!(Math.Abs(mCurrentPoint.X - mLastPoint.X) < 0.1 && Math.Abs(mCurrentPoint.Y - mLastPoint.Y) < 0.1))
                {
                    AddPointToPath(mCurrentPoint);
                }
                
            }

            mPointerIDs.Remove(e.Pointer.PointerId);
            SigningArea.ReleasePointerCapture(e.Pointer);
            ReleasePointerCapture(e.Pointer);
        }

        private void SigningArea_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            mPointerIDs.Remove(e.Pointer.PointerId);
            SigningArea.ReleasePointerCapture(e.Pointer);
            mStartedPathSmoothing = false;
        }

        void SigningArea_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            mPointerIDs.Remove(e.Pointer.PointerId);
            mStartedPathSmoothing = false;
        }

        /// <summary>
        /// If tapped, we want to add a dot. We do this by creating an ink path with only 2 really close points.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SigningArea_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (mPointerIDs.Count == 0)
            {
                UIPoint tapPoint = e.GetPosition(SigningArea);

                mCurrentPathFigure = new PathFigure();
                mCurrentPathFigure.StartPoint = new UIPoint(tapPoint.X, tapPoint.Y);
                mCurrentPathFigure.Segments.Add(new LineSegment() { Point = new UIPoint(tapPoint.X + 0.5, tapPoint.Y + 0.5)});
                mPathPoints.Add(mCurrentPathFigure);

                mCurrentPointList = new List<UIPoint>();
                mCurrentPointList.Add(new UIPoint(tapPoint.X, tapPoint.Y));
                mCurrentPointList.Add(new UIPoint(tapPoint.X + 0.5, tapPoint.Y + 0.5));
                mListOfPointLists.Add(mCurrentPointList);

                if (tapPoint.Y < mTop)
                {
                    mTop = tapPoint.Y;
                }
                if (tapPoint.Y > mBottom)
                {
                    mBottom = tapPoint.Y;
                }
                if (tapPoint.X < mLeft)
                {
                    mLeft = tapPoint.X;
                }
                if (tapPoint.X > mRight)
                {
                    mRight = tapPoint.X;
                }
            }
        }


        #endregion Drawing

        #region Button Clicks

        private void CancelButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (SignatureDone != null)
            {
                SignatureDone(false);
            }
            _Popup.IsOpen = false;
        }

        private void ClearButton_Clicked(object sender, RoutedEventArgs e)
        {
            ClearSignature();
        }

        private void ClearSignature()
        {
            mPathPoints.Clear();
            mListOfPointLists.Clear();

            mTop = Int16.MaxValue;
            mBottom = -1;
            mLeft = Int16.MaxValue;
            mRight = -1;
        }

        private void AddSignatureButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (SignatureDone != null)
            {
                SignatureDone(true);
            }
            return;
        }

        private void UseDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (UseDefaultSelected != null)
            {
                UseDefaultSelected();
            }
        }
       
        #endregion Button Clicks
    }
}

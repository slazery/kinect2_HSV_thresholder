//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.ObjectRecognition
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using OpenCvSharp;
    using OpenCvSharp.CPlusPlus;
    using OpenCvSharp.Extensions;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for depth/color/body index frames
        /// </summary>
        private MultiSourceFrameReader multiFrameSourceReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// The size in bytes of the bitmap back buffer
        /// </summary>
        private uint bitmapBackBufferSize = 0;

        /// <summary>
        /// Intermediate storage for the color to depth mapping
        /// </summary>
        private CameraSpacePoint[] colorMappedToCameraPoints = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// HSV thresholds for color segmentation
        /// </summary>
        private int lowerH = 112;
        private int upperH = 120;
        private int lowerS = 100;
        private int upperS = 255;
        private int lowerV = 100;
        private int upperV = 255;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // open the reader for the frames
            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);

            // wire handler for frame arrival
            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

            // create the depthFrameDescription from the DepthFrameSource
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            
            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;

            // initialize intermediate array storing mapping from color to camera space
            this.colorMappedToCameraPoints = new CameraSpacePoint[colorWidth * colorHeight];

            // create the bitmap to display
            this.bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            // Calculate the WriteableBitmap back buffer size
            this.bitmapBackBufferSize = (uint)((this.bitmap.BackBufferStride * (this.bitmap.PixelHeight - 1)) + (this.bitmap.PixelWidth * this.bytesPerPixel));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            // initialize OpenCV window to display info from object segmentation
            this.InitializeCv();
        }

        private void InitializeCv()
        {
            CvWindow objectSeg = new CvWindow("Thresholded View");
            CvWindow colorPick = new CvWindow("Color Picker");
            CvTrackbarCallback lowerHCallback = delegate(int pos)
            {
                if (this.upperH > pos)
                {
                    this.lowerH = pos;
                }
            };
            CvTrackbarCallback upperHCallback = delegate(int pos)
            {
                if (pos > this.lowerH)
                {
                    this.upperH = pos;
                }
            };
            CvTrackbarCallback lowerSCallback = delegate(int pos)
            {
                if (this.upperS > pos)
                {
                    this.lowerS = pos;
                }
            };
            CvTrackbarCallback upperSCallback = delegate(int pos)
            {
                if (pos > this.lowerS)
                {
                    this.upperS = pos;
                }
            };
            CvTrackbarCallback lowerVCallback = delegate(int pos)
            {
                if (this.upperV > pos)
                {
                    this.lowerV = pos;
                }
            };
            CvTrackbarCallback upperVCallback = delegate(int pos)
            {
                if (pos > this.lowerV)
                {
                    this.upperV = pos;
                }
            };
            CvTrackbar lowerH = colorPick.CreateTrackbar("lower H", this.lowerH, 255, lowerHCallback);
            CvTrackbar upperH = colorPick.CreateTrackbar("upper H", this.upperH, 255, upperHCallback);
            CvTrackbar lowerS = colorPick.CreateTrackbar("lower S", this.lowerS, 255, lowerSCallback);
            CvTrackbar upperS = colorPick.CreateTrackbar("upper S", this.upperS, 255, upperSCallback);
            CvTrackbar lowerV = colorPick.CreateTrackbar("lower V", this.lowerH, 255, lowerVCallback);
            CvTrackbar upperV = colorPick.CreateTrackbar("upper V", this.upperH, 255, upperVCallback);
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.multiFrameSourceReader != null)
            {
                // multiFrameSourceReader is IDisposable
                this.multiFrameSourceReader.Dispose();
                this.multiFrameSourceReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.bitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.bitmap));

                string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = Path.Combine(myPhotos, "KinectScreenshot-Color-" + time + ".png");

                // write the new file to disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.StatusText = string.Format(Properties.Resources.SavedScreenshotStatusTextFormat, path);
                }
                catch (IOException)
                {
                    this.StatusText = string.Format(Properties.Resources.FailedScreenshotStatusTextFormat, path);
                }
            }
        }

        /// <summary>
        /// Handles color and depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            int depthWidth = 0;
            int depthHeight = 0;

            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            bool isBitmapLocked = false;

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            // if the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }

            try
            {
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();

                // if any frame has expired by the time we process this event, return.
                // the "finally" statement will Dispose any that are not null.
                if ((depthFrame == null) || (colorFrame == null))
                {
                    return;
                }

                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;

                // access the depth frame data directly via LockImageBuffer to avoid making a copy
                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    this.coordinateMapper.MapColorFrameToCameraSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.colorMappedToCameraPoints);
                }

                // we're done with the DepthFrame 
                depthFrame.Dispose();
                depthFrame = null;

                // Process Color

                // lock the bitmap for writing
                this.bitmap.Lock();
                isBitmapLocked = true;

                colorFrame.CopyConvertedFrameDataToIntPtr(this.bitmap.BackBuffer, this.bitmapBackBufferSize, ColorImageFormat.Bgra);

                // we're done with the ColorFrame 
                colorFrame.Dispose();
                colorFrame = null;

                this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));

                this.bitmap.Unlock();
                isBitmapLocked = false;

                // Segment Object

                // grab frame from Kinect and convert to HSV format
                IplImage imgOriginal = this.bitmap.ToIplImage();
                IplImage imgHsv = Cv.CreateImage(imgOriginal.Size, BitDepth.U8, 3);
                Cv.CvtColor(imgOriginal, imgHsv, ColorConversion.RgbToHsv);
                imgOriginal.Dispose();

                // set color thresholds and create IplImage to store thresholded frame
                CvScalar lower = new CvScalar(this.lowerH, this.lowerS, this.lowerV);
                CvScalar upper = new CvScalar(this.upperH, this.upperS, this.upperV);
                IplImage imgThreshed = Cv.CreateImage(imgHsv.Size, BitDepth.U8, 1);
                Cv.InRangeS(imgHsv, lower, upper, imgThreshed);

                // show image
                CvSize size = new CvSize(imgThreshed.Width / 2, imgThreshed.Height / 2);
                IplImage imgResized = new IplImage(size, BitDepth.U8, 1);
                imgThreshed.Resize(imgResized, Interpolation.Linear);
                Cv.ShowImage("Thresholded", imgResized);

                // clean up
                imgHsv.Dispose();
                imgThreshed.Dispose();
                imgResized.Dispose();
            }
            finally
            {
                if (isBitmapLocked)
                {
                    this.bitmap.Unlock();
                }

                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
    }
}

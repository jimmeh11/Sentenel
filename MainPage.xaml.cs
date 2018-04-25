using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Kinect;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.Storage.Pickers;
using Windows.Graphics.Imaging;
using Windows.Graphics.Display;
using Windows.Storage;

namespace SentenalProto_1
{
    public enum DisplayFrameType{
        Depth,
        JointsOnDepth,
        Colour,
        JointsOnColour
    }

    public sealed partial class MainPage
    {
        private const DisplayFrameType DefaultDisplayframetype = DisplayFrameType.JointsOnColour;

        private const int BytesPerPixel = 4;

        // Generic variables
        private readonly KinectSensor _kinectSensor;
        private string _statusText;
        private WriteableBitmap _bitmap;
        private FrameDescription _currentFrameDescription;
        private DisplayFrameType _currentDisplayFrameType;
        private readonly CoordinateMapper _coordinateMapper;
        private BodiesManager _bodiesManager;
        private BodyFrame _bodyFrame;
        private Body[] _bodies;
        private Vector4 _floorPlane;
        private string _systemEvent = "None";
        private string _eventTime = "None";

        // Gesture timestamp stacks
        private readonly Stack<DateTime> _pickUpMedication = new Stack<DateTime>();
        private readonly Stack<DateTime> _pickUpPantry = new Stack<DateTime>();
        private readonly Stack<DateTime> _pickUpFridge = new Stack<DateTime>();
        private readonly Stack<DateTime> _pickUpBowlCupboard = new Stack<DateTime>();
        private readonly Stack<DateTime> _pickUpFoodPrep = new Stack<DateTime>();
        private readonly Stack<DateTime> _putDownFoodPrep = new Stack<DateTime>();
        private readonly Stack<DateTime> _openDoorPantry = new Stack<DateTime>();
        private readonly Stack<DateTime> _openDoorFridge = new Stack<DateTime>();
        private readonly Stack<DateTime> _openDoorBowlCupboard = new Stack<DateTime>();
        private readonly Stack<DateTime> _handToMouthDining = new Stack<DateTime>();
        private readonly Stack<DateTime> _pourFoodPrep = new Stack<DateTime>();
        
        // Depth Frame variables
        private ushort[] _depthFrameData;
        private byte[] _depthPixels;

        /// List of gesture detectors; there will be one detector created for each potential body (max of 6)
        private readonly List<GestureDetector> _gestureDetectorList;

        // Screenshot variables
        //private bool _isTakingScreenshot = false;

        //Body Joints are drawn here
        private Canvas _drawingCanvas;

        // Float array declarations for coordinates [x,y,z] of key locations in meters, normalised for ground plane
        private readonly float[] _medication = { 1.11f, 0.86f, 1.68f};
        private readonly float[] _pantry = { -0.45f, 1.25f, 1.40f };
        private readonly float[] _fridge = { 1.75f, 1.15f, 4.40f };
        private readonly float[] _bowlCupboard = { 0.75f, 1.10f, 2.50f };
        private readonly float[] _foodPrep = { -0.10f, 1.22f, 2.83f };
        private readonly float[] _dining = { -0.31f, 0.78f, 1.98f };

        // Location flags
        private Rectangle _medicationLocation;
        private Rectangle _pantryLocation;
        private Rectangle _fridgeLocation;
        private Rectangle _bowlLocation;
        private Rectangle _prepLocation;
        private Rectangle _diningLocation;

        public event PropertyChangedEventHandler PropertyChanged;

        private string StatusText
        {
            get 
            { 
                return _statusText; 
            }

            set
            {
                if (_statusText == value) return;

                _statusText = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                }
            }
        }

        private FrameDescription CurrentFrameDescription
        {
            get { return _currentFrameDescription; }
            set
            {
                if (_currentFrameDescription == value) return;

                _currentFrameDescription = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("CurrentFrameDescription"));
                }
            }
        }

        private DisplayFrameType CurrentDisplayFrameType
        {
            get { return _currentDisplayFrameType; }
            set
            {
                if (_currentDisplayFrameType == value) return;

                _currentDisplayFrameType = value;

                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("CurrentDisplayFrameType"));
                }
            }
        }

        public MainPage()
        {
            // one sensor is currently supported
            _kinectSensor = KinectSensor.GetDefault();

            _coordinateMapper = _kinectSensor.CoordinateMapper;

            var multiSourceFrameReader = _kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Infrared | FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);

            multiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

            // set IsAvailableChanged event notifier
            _kinectSensor.IsAvailableChanged += Sensor_IsAvailableChanged;

            // use the window object as the view model in this simple example
            DataContext = this;

            // open the sensor
            _kinectSensor.Open();

            InitializeComponent();

            // new
            Loaded += MainPage_Loaded;

            // Initialize the gesture detection objects for our gestures
            _gestureDetectorList = new List<GestureDetector>();

            // Create a gesture detector for each body (6 bodies => 6 detectors)
            int maxBodies = _kinectSensor.BodyFrameSource.BodyCount;
            for (int i = 0; i < maxBodies; ++i)
            {
                GestureResultView result = new GestureResultView(i, false, false, 0.0f, null);
                GestureDetector detector = new GestureDetector(_kinectSensor, result);
                result.PropertyChanged += GestureResult_PropertyChanged;
                _gestureDetectorList.Add(detector);
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DefaultDisplayframetype);
        }
        
        private void SetupCurrentDisplay(DisplayFrameType newDisplayFrameType)
        {
            CurrentDisplayFrameType = newDisplayFrameType;

            // Frames used by more than one type are declared outside the switch
            FrameDescription colorFrameDescription;
            FrameDescription depthFrameDescription;

            // Instantiate a new canvas
            _drawingCanvas = new Canvas();
            
            if (BodyJointsGrid != null)
            {
                BodyJointsGrid.Visibility = Visibility.Collapsed;
            }

            if (FrameDisplayImage != null)
            {
                FrameDisplayImage.Source = null;
            }

            switch (CurrentDisplayFrameType)
            {
                case DisplayFrameType.Colour:
                    colorFrameDescription = _kinectSensor.ColorFrameSource.FrameDescription;
                    CurrentFrameDescription = colorFrameDescription;
                    // create the bitmap to display
                    _bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);
                    break;

                case DisplayFrameType.Depth:
                    depthFrameDescription = _kinectSensor.DepthFrameSource.FrameDescription;
                    CurrentFrameDescription = depthFrameDescription;
                    // allocate space to put the pixels being received and converted
                    _depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                    _depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];
                    _bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height);
                    break;

                 case DisplayFrameType.JointsOnColour:
                    // Colour data
                    colorFrameDescription = _kinectSensor.ColorFrameSource.FrameDescription;
                    CurrentFrameDescription = colorFrameDescription;
                    // create the bitmap to display
                    _bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);

                    // Joints data
                     // set the clip rectangle to prevent rendering outside the canvas
                    _drawingCanvas.Clip = new RectangleGeometry();
                     if (BodyJointsGrid != null)
                     {
                         _drawingCanvas.Clip.Rect = new Rect(0.0, 0.0, BodyJointsGrid.Width, BodyJointsGrid.Height);
                         _drawingCanvas.Width = BodyJointsGrid.Width;
                         _drawingCanvas.Height = BodyJointsGrid.Height;
                         // reset the body joints grid
                         BodyJointsGrid.Visibility = Visibility.Visible;
                         BodyJointsGrid.Children.Clear();
                         // add canvas to DisplayGrid
                         BodyJointsGrid.Children.Add(_drawingCanvas);
                     }

                     _bodiesManager = new BodiesManager(_coordinateMapper, _drawingCanvas, _kinectSensor.BodyFrameSource.BodyCount);
                    break;

                case DisplayFrameType.JointsOnDepth:

                    //Depth data
                    depthFrameDescription = _kinectSensor.DepthFrameSource.FrameDescription;
                    CurrentFrameDescription = depthFrameDescription;

                    // allocate space to put the pixels being received and converted
                    _depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                    _depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];
                    _bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height);
                    
                    // set the clip rectangle to prevent rendering outside the canvas
                    _drawingCanvas.Clip = new RectangleGeometry();
                    if (BodyJointsGrid != null)
                    {
                        _drawingCanvas.Clip.Rect = new Rect(0.0, 0.0, BodyJointsGrid.Width, BodyJointsGrid.Height);
                        _drawingCanvas.Width = BodyJointsGrid.Width;
                        _drawingCanvas.Height = BodyJointsGrid.Height;

                        // reset the body joints grid
                        BodyJointsGrid.Visibility = Visibility.Visible;
                        BodyJointsGrid.Children.Clear();

                        // add canvas to DisplayGrid
                        BodyJointsGrid.Children.Add(_drawingCanvas);
                    }

                    _bodiesManager = new BodiesManager(_coordinateMapper, _drawingCanvas, _kinectSensor.BodyFrameSource.BodyCount);
                    break;
                    
                default:
                    break;
            }

            SetupLocationFlags();
        }

        /// <summary>
        /// Updates status text based on sensor status
        /// </summary>
        private void Sensor_IsAvailableChanged(KinectSensor sender, IsAvailableChangedEventArgs args)
        {
            StatusText = _kinectSensor.IsAvailable ? "Running" : "Not Available";
        }
        
        /// <summary>
        /// Create the location flags (rectangles) within the GUI to indicate where the locations are in the frame
        /// </summary>
        private void SetupLocationFlags()
        {
            // Create new reectangles for each location
            _medicationLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Purple),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            _pantryLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Green),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            _fridgeLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Yellow),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            _bowlLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Orange),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            _prepLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.LightBlue),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            _diningLocation = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Blue),
                Width = 20,
                Height = 20,
                Opacity = 0.5,
                Visibility = Visibility.Visible
            };

            // Add the location flags to the frame
            _drawingCanvas.Children.Add(_medicationLocation);
            _drawingCanvas.Children.Add(_pantryLocation);
            _drawingCanvas.Children.Add(_fridgeLocation);
            _drawingCanvas.Children.Add(_bowlLocation);
            _drawingCanvas.Children.Add(_prepLocation);
            _drawingCanvas.Children.Add(_diningLocation);

            // Position the location flags according to their location arrays, corrected for frame size
            Canvas.SetLeft(_medicationLocation, 246 + (_medication[0] / (_medication[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_medicationLocation, 215);

            Canvas.SetLeft(_pantryLocation, 246 + (_pantry[0] / (_pantry[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_pantryLocation, 180);

            Canvas.SetLeft(_fridgeLocation, 246 + (_fridge[0] / (_fridge[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_fridgeLocation, 200);

            Canvas.SetLeft(_bowlLocation, 246 + (_bowlCupboard[0] / (_bowlCupboard[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_bowlLocation, 300);

            Canvas.SetLeft(_prepLocation, 246 + (_foodPrep[0] / (_foodPrep[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_prepLocation, 200);

            Canvas.SetLeft(_diningLocation, 246 + (_dining[0] / (_dining[2] * Math.Tan(35.3 * 3.1419f / 180))) * 256);
            Canvas.SetTop(_diningLocation, 250);
        }

        private void Reader_MultiSourceFrameArrived(MultiSourceFrameReader sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }
            DepthFrame depthFrame;
            ColorFrame colorFrame;

            using (_bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                if (Equals(_bodyFrame.FloorClipPlane, _floorPlane)) { }
                else
                {
                    _floorPlane = _bodyFrame.FloorClipPlane;
                }

                RegisterGesture(_bodyFrame);
            }

            switch (CurrentDisplayFrameType)
            {
                case DisplayFrameType.Colour:
                    using (colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);
                    }
                    break;

                case DisplayFrameType.Depth:
                    using (depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
                    {
                        ShowDepthFrame(depthFrame);
                    }
                    break;

                case DisplayFrameType.JointsOnColour:
                    using (colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);

                        using (_bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
                        {
                            ShowBodyJoints(_bodyFrame);
                        }
                    }
                    break;

                case DisplayFrameType.JointsOnDepth:
                    using (depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
                    {
                        ShowDepthFrame(depthFrame);
                    }

                    using (_bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
                    {
                        ShowBodyJoints(_bodyFrame);
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void GestureResult_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            GestureResultView result = sender as GestureResultView;
            if (result == null) return;

            var detectedGestureName = result.GestureName;
            GestureVisual.Opacity = result.Confidence;
            BodyTracked.Text = result.BodyIndex.ToString();
            GestureName.FontSize = 30;
            GestureName.Foreground = new SolidColorBrush(Colors.White);

            // Using the array bodies[], the specific body recording the gesture is found.
            // The location of the left and right fingertips, as well as the midpoint of the spine are found and stored as [x,y,z] coordinates.
            // These coordinates correspond to the location of the camera
            Body body = _bodies[result.BodyIndex];

            if (body != null)
            {
                // FloorClipPlane results, used to normalise the coordinate system of the detector to the floor of the room
                float floorX = _floorPlane.X;
                float floorY = _floorPlane.Y;
                float floorZ = _floorPlane.Z;
                float floorW = _floorPlane.W;

                float cameraAngleRadians = (float)Math.Atan(floorZ / floorY);
                float cosCameraAngle = (float)Math.Cos(cameraAngleRadians);
                float sinCameraAngle = (float)Math.Sin(cameraAngleRadians);

                // Output the FloorClipPlane coordinates to the GUI for debug purposes
                FloorPosition.Text = "X: " + floorX + ", Y: " + floorY + ", Z: " + floorZ + ", W: " + floorW;
                
                // Collect the coordinates of the left and right finger tips and the spine midpoint
                Joint fingerTipLeft = body.Joints[JointType.HandTipLeft];
                Joint fingerTipRight = body.Joints[JointType.HandTipRight];
                Joint spineMid = body.Joints[JointType.SpineMid];

                // Location of the left and right finger tips and the spine midpoint, after trigonometric correction for the FloorClipPlane.
                // Gives the coordinate locations with the origin on the floor at the base of the camera, with y-axis normal to the floor.
                float[] fingerTipLeftXyz = { fingerTipLeft.Position.X * floorX, (fingerTipLeft.Position.Y * floorY) + floorW, fingerTipLeft.Position.Z * floorZ};
                float[] fingerTipRightXyz =  { fingerTipRight.Position.X, (fingerTipRight.Position.Y * cosCameraAngle) + (fingerTipRight.Position.Z * sinCameraAngle) + floorW, (fingerTipRight.Position.Z * cosCameraAngle) + (fingerTipRight.Position.Y * sinCameraAngle) };
                float[] spineMidXyz = { spineMid.Position.X, (spineMid.Position.Y * cosCameraAngle) + (spineMid.Position.Z * sinCameraAngle) + floorW, (spineMid.Position.Z * cosCameraAngle) + (spineMid.Position.Y * sinCameraAngle) };

                // Output the coordinates of the spine and the left fingertip to the GUI for debug purposes
                BodyPosition.Text = "X: " + spineMidXyz[0].ToString("n4") + ", Y: " + spineMidXyz[1].ToString("n4") + ", Z: " + spineMidXyz[2].ToString("n4");
                LHandPosition.Text = "X: " + fingerTipLeftXyz[0].ToString("n4") + ", Y: " + fingerTipLeftXyz[1].ToString("n4") + ", Z: " + fingerTipLeftXyz[2].ToString("n4");

                // Check whether the location where the gesture is performed matches the key locations 
                string gestureLocation = GestureLocationChecker(fingerTipLeftXyz, fingerTipRightXyz, spineMidXyz, detectedGestureName);

                GestureLocation.Text = gestureLocation;

                if (detectedGestureName == null || !(result.Confidence > 0.1f) || GestureLocation.Text == null) return;

                GestureConfidence.Text = result.Confidence < 0.3f ? "Low" : (result.Confidence * 100f).ToString();

                // Output gesture name to GUI
                GestureName.Text = detectedGestureName;

                if (result.Confidence < 0.4f) return;

                // if confidence over 0.4 change gesture name in GUI to green
                GestureName.Foreground = new SolidColorBrush(Colors.Green);

                switch (detectedGestureName)
                {
                    case "PickUp":
                        switch (GestureLocation.Text)
                        {
                            case "Medication":
                                _pickUpMedication.Push(DateTime.Now);
                                break;
                            case "Pantry":
                                _pickUpPantry.Push(DateTime.Now);
                                break;
                            case "Fridge":
                                _pickUpFridge.Push(DateTime.Now);
                                break;
                            case "BowlCupboard":
                                _pickUpBowlCupboard.Push(DateTime.Now);
                                break;
                            case "FoodPrep":
                                _pickUpFoodPrep.Push(DateTime.Now);
                                break;
                            default:
                                break;
                        }
                        break;

                    case "PutDown":
                        if (GestureLocation.Text == "FoodPrep")
                        {
                            _putDownFoodPrep.Push(DateTime.Now);
                        }
                        break;

                    case "OpenDoor":
                        switch (GestureLocation.Text)
                        {
                            case "Pantry":
                                _openDoorPantry.Push(DateTime.Now);
                                break;
                            case "Fridge":
                                _openDoorFridge.Push(DateTime.Now);
                                break;
                            case "BowlCupboard":
                                _openDoorBowlCupboard.Push(DateTime.Now);
                                break;
                            default:
                                break; 
                        }
                        break;

                    case "HandToMouth":
                        if (GestureLocation.Text == "Dining")
                        {
                            _handToMouthDining.Push(DateTime.Now);
                        }
                        break;

                    case "Pour":
                        if (GestureLocation.Text == "FoodPrep")
                        {
                            _pourFoodPrep.Push(DateTime.Now);
                        }
                        break;

                    case "No Gesture":
                        GestureName.Foreground = new SolidColorBrush(Colors.Red);
                        GestureConfidence.Text = "";
                        break;

                    default:
                        break;
                }

                //Check for an event, such as taking medication or eatin meal
                EventChecker();
            }
            else
            {
                BodyPosition.Text = "body = null";
            }
        }

        /// <summary>
        /// Renders depth frames to the GUI 
        /// </summary>
        private void ShowDepthFrame(DepthFrame depthFrame)
        {
            var depthFrameProcessed = false;
            ushort minDepth = 0;
            ushort maxDepth = 0;

            if (depthFrame != null)
            {
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                // verify data and write the new infrared frame data to the display bitmap
                if ((depthFrameDescription.Width == _bitmap.PixelWidth) && (depthFrameDescription.Height == _bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyFrameDataToArray(_depthFrameData);

                    minDepth = depthFrame.DepthMinReliableDistance;
                    maxDepth = depthFrame.DepthMaxReliableDistance;

                    depthFrameProcessed = true;
                }
            }

            // we got a frame, convert and render
            if (depthFrameProcessed)
            {
                ConvertDepthDataToPixels(minDepth, maxDepth);
                RenderPixelArray(_depthPixels);
            }
        }

        /// <summary>
        /// Renders colour frames to the bitmap image in the GUI
        /// </summary>
        private void ShowColorFrame(ColorFrame colorFrame)
        {
            bool colorFrameProcessed = false;

            if (colorFrame != null)
            {
                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                // verify data and write the new color frame data to the Writeable bitmap
                if ((colorFrameDescription.Width == _bitmap.PixelWidth) && (colorFrameDescription.Height == _bitmap.PixelHeight))
                {
                    if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        colorFrame.CopyRawFrameDataToBuffer(_bitmap.PixelBuffer);
                    }
                    else
                    {
                        colorFrame.CopyConvertedFrameDataToBuffer(_bitmap.PixelBuffer, ColorImageFormat.Bgra);
                    }

                    colorFrameProcessed = true;
                }
            }

            if (colorFrameProcessed)
            {
                _bitmap.Invalidate();
                FrameDisplayImage.Source = _bitmap;
            }
        }

        /// <summary>
        /// If a bodyFrame has been received, renders the body, joints and frame edges to the GUI using BodiesManager class
        /// </summary>
        private void ShowBodyJoints(BodyFrame bodyFrame)
        {
            Body[] bodies = new Body[_kinectSensor.BodyFrameSource.BodyCount];
            bool dataReceived = false;
            if (bodyFrame != null)
            {
                bodyFrame.GetAndRefreshBodyData(bodies);
                dataReceived = true;
            }

            if (dataReceived)
            {
                _bodiesManager.UpdateBodiesAndEdges(bodies);
            }
        }

        /// <summary>
        /// Gets the body data, updates the gesture detectors
        /// </summary>
        private void RegisterGesture(BodyFrame bodyFrame)
        {
            bool dataReceived = false;

            if (bodyFrame != null)
            {
                if (_bodies == null)
                {
                    // Creates an array of 6 bodies, which is the max number of bodies the Kinect can track simultaneously
                    _bodies = new Body[bodyFrame.BodyCount];
                }

                // The first time GetAndRefreshBodyData is called, allocate each Body in the array.
                // As long as those body objects are not disposed and not set to null in the array, those body objects will be re-used.
                bodyFrame.GetAndRefreshBodyData(_bodies);
                dataReceived = true;
            }

            if (dataReceived)
            {
                // We may have lost/acquired bodies, so update the corresponding gesture detectors
                if (_bodies != null)
                {
                    // Loop through all bodies to see if any of the gesture detectors need to be updated
                    for (int i = 0; i < bodyFrame.BodyCount; ++i)
                    {
                        Body body = _bodies[i];
                        var trackingId = body.TrackingId;

                        // If the current body TrackingId changed, update the corresponding gesture detector with the new value
                        if (trackingId != _gestureDetectorList[i].TrackingId)
                        {
                            _gestureDetectorList[i].TrackingId = trackingId;

                            // If the current body is tracked, unpause its detector to get VisualGestureBuilderFrameArrived events.
                            // If the current body is NOT tracked, pause its detector so we don't waste resources trying to get invalid gesture results
                            _gestureDetectorList[i].IsPaused = trackingId == 0;
                        }
                    }
                }
            }
        }
                
/*
        // Screenshot method, used for debuggin purposes. When a gesture is detected, or another event occurs, calling Screenshot() will save a 
        // screen image to the chosen file location
        private async void Screenshot()
        {
            // Thread protetction on FileIO actions
            if (!_isTakingScreenshot)
            {
                _isTakingScreenshot = true;
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap();
                await renderTargetBitmap.RenderAsync(RootGrid);
                var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();

                var savePicker = new FileSavePicker();
                savePicker.DefaultFileExtension = ".png";
                savePicker.FileTypeChoices.Add(".png", new List<string> { ".png" });
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                savePicker.SuggestedFileName = "snapshot.png";

                // Prompt the user to select a file
                var saveFile = await savePicker.PickSaveFileAsync();

                // Verify the user selected a file
                if (saveFile != null)
                {
                    // Encode the image to the selected file on disk
                    using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)renderTargetBitmap.PixelWidth, (uint)renderTargetBitmap.PixelHeight,
                            DisplayInformation.GetForCurrentView().LogicalDpi, DisplayInformation.GetForCurrentView().LogicalDpi, pixelBuffer.ToArray());
                        await encoder.FlushAsync();
                    }
                }
                _isTakingScreenshot = false;
            }
        }
*/

        private void RenderPixelArray(byte[] pixels)
        {
            pixels.CopyTo(_bitmap.PixelBuffer);
            _bitmap.Invalidate();
            FrameDisplayImage.Source = _bitmap;
        }

        private void ConvertDepthDataToPixels(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;

            // Shape the depth to the range of a byte
            int mapDepthToByte = maxDepth / 256;

            foreach (var depth in _depthFrameData)
            {
                // To convert to a byte, we're mapping the depth value
                // to the byte range.
                // Values outside the reliable depth range are 
                // mapped to 0 (black).
                var intensity = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / mapDepthToByte) : 0);

                _depthPixels[colorPixelIndex++] = intensity; //Blue
                _depthPixels[colorPixelIndex++] = intensity; //Green
                _depthPixels[colorPixelIndex++] = intensity; //Red
                _depthPixels[colorPixelIndex++] = 255; //Alpha
            }
        }

        /// <summary>
        /// Determines whether the currently detected gesture is being performed at one of the listed key locations
        /// </summary>
        private string GestureLocationChecker(float[] fingerLeftXyz, float[] fingerRightXyz, float[] spineXyz, string gestureName)  
        {
            switch (gestureName)
            {
                case "PickUp":
                    // Check both finger tip locations against "Medication" [+/- 0.1m]
                    // Check X coordinate
                    if ((fingerLeftXyz[0] <= (_medication[0] + 0.1f) && fingerLeftXyz[0] >= (_medication[0] - 0.1f)) ||
                        (fingerRightXyz[0] <= (_medication[0] + 0.1f) && fingerRightXyz[0] >= (_medication[0] - 0.1f)))
                    {
                        // Check Y coordinate
                        if ((fingerLeftXyz[1] <= (_medication[1] + 0.1f) && fingerLeftXyz[1] >= (_medication[1] - 0.1f)) ||
                            (fingerRightXyz[1] <= (_medication[1] + 0.1f) && fingerRightXyz[1] >= (_medication[1] - 0.1f)))
                        {
                            // Check Z coordinate
                            if ((fingerLeftXyz[2] <= (_medication[2] + 0.1f) && fingerLeftXyz[2] >= (_medication[2] - 0.1f)) ||
                                (fingerRightXyz[2] <= (_medication[2] + 0.1f) && fingerRightXyz[2] >= (_medication[2] - 0.1f)))
                            {
                                return "Medication";
                            }
                        }
                    }
                    // Check spine location against "Pantry" [x: +/- 0.15m, z: +/-0.3m]
                    else if (spineXyz[0] <= (_pantry[0] + 0.15f) && spineXyz[0] >= (_pantry[0] - 0.15f) && 
                             spineXyz[2] <= (_pantry[2] + 0.3f) && spineXyz[2] >= (_pantry[2] - 0.3f))
                    {
                        return "Pantry";
                    }
                    // Check spine location against "Fridge" +/- 0.5m
                    else if (spineXyz[0] <= (_fridge[0] + 0.5f) && spineXyz[0] >= (_fridge[0] - 0.5f) && 
                             spineXyz[2] <= (_fridge[2] + 0.5f) && spineXyz[2] >= (_fridge[2] - 0.5f))
                    {
                        return "Fridge";
                    }
                    // Check spine location against "BowlCupboard" [x: +/- 0.15m, z: +/-0.3m]
                    else if (spineXyz[0] <= (_bowlCupboard[0] + 0.15f) && spineXyz[0] >= (_bowlCupboard[0] - 0.15f) && 
                             spineXyz[2] <= (_bowlCupboard[2] + 0.3f) && spineXyz[2] >= (_bowlCupboard[2] - 0.3f))
                    {
                        return "BowlCupboard";
                    }
                    // Check spine location against "FoodPrep" [x: +/- 0.15m, z: +/-0.3m]
                    else if (spineXyz[0] <= (_foodPrep[0] + 0.15f) && spineXyz[0] >= (_foodPrep[0] - 0.15f) &&
                             spineXyz[2] <= (_foodPrep[2] + 0.3f) && spineXyz[2] >= (_foodPrep[2] - 0.3f))
                    {
                        return "FoodPrep";
                    }
                    break;

                case "PutDown":
                    // Check spine location against "FoodPrep" +/- 0.4m
                    if (spineXyz[0] <= (_foodPrep[0] + 0.4f) && spineXyz[0] >= (_foodPrep[0] - 0.4f) &&
                        spineXyz[2] <= (_foodPrep[2] + 0.4f) && spineXyz[2] >= (_foodPrep[2] - 0.4f))
                    {
                        return "FoodPrep";
                    }

                    break;

                case "OpenDoor":
                    // Check spine location against "Pantry" [x: +/- 0.15m, z: +/-0.3m]
                    if (spineXyz[0] <= (_pantry[0] + 0.15f) && spineXyz[0] >= (_pantry[0] - 0.15f) && 
                        spineXyz[2] <= (_pantry[2] + 0.3f) && spineXyz[2] >= (_pantry[2] - 0.3f))
                    {
                        return "Pantry";
                    }
                    // Check spine location against "Fridge" +/- 0.5m
                    else if (spineXyz[0] <= (_fridge[0] + 0.5f) && spineXyz[0] >= (_fridge[0] - 0.5f) && 
                             spineXyz[2] <= (_fridge[2] + 0.5f) && spineXyz[2] >= (_fridge[2] - 0.5f))
                    {
                        return "Fridge";
                    }
                    // Check spine location against "BowlCupboard" [x: +/- 0.15m, z: +/-0.3m]
                    else if (spineXyz[0] <= (_bowlCupboard[0] + 0.15f) && spineXyz[0] >= (_bowlCupboard[0] - 0.15f) && 
                             spineXyz[2] <= (_bowlCupboard[2] + 0.3f) && spineXyz[2] >= (_bowlCupboard[2] - 0.3f))
                    {
                        return "BowlCupboard";
                    }
                    break;

                case "HandToMouth":
                    // Check spine location against "Dining" [x: +/- 0.15m, z: +/-0.3m]
                    if (spineXyz[0] <= (_dining[0] + 0.15f) && spineXyz[0] >= (_dining[0] - 0.15f) &&
                        spineXyz[2] <= (_dining[2] + 0.3f) && spineXyz[2] >= (_dining[2] - 0.3f))
                    {
                        return "Dining";
                    }

                    break;

                case "Pour":
                    // Check spine location against "FoodPrep" [x: +/- 0.15m, z: +/-0.3m]
                    if (spineXyz[0] <= (_foodPrep[0] + 0.15f) && spineXyz[0] >= (_foodPrep[0] - 0.15f) &&
                        spineXyz[2] <= (_foodPrep[2] + 0.3f) && spineXyz[2] >= (_foodPrep[2] - 0.3f))
                    {
                        return "FoodPrep";
                    }

                    break;

                default:
                    break;
            }

            return "None";
        }

        /// <summary>
        /// Checks the stacks for each gesture-location combionation to determine whether medication has been taken or a meal eaten
        /// </summary>
        private void EventChecker()
        {
            // Check for Taking Medication
            if (GestureName.Text == "HandToMouth" && _pickUpMedication.Count != 0)
            {
                // Check the most recent time the medication was picked up and convert that to a time 

                    var y = DateTime.Now.Subtract(_pickUpMedication.Peek()).TotalSeconds;

                    if (y < 120)
                    {
                        _systemEvent = "Medication Taken";
                        _eventTime = DateTime.Now.ToString();

                        _pickUpMedication.Clear();
                    }
            }

            //Check for Making Breakfast
            if (GestureName.Text == "HandToMouth" && GestureLocation.Text == "Dining")
            {
                // Check the most recent time the medication was picked up and convert that to a time 
                var openPantryT = DateTime.Now.Subtract(_openDoorPantry.Peek()).TotalSeconds;
                var putFoodT = DateTime.Now.Subtract(_putDownFoodPrep.Peek()).TotalSeconds;
                var pourFoodT = DateTime.Now.Subtract(_pourFoodPrep.Peek()).TotalSeconds;
                var pickFoodT = DateTime.Now.Subtract(_pickUpFoodPrep.Peek()).TotalSeconds;

                //Check whether the relevant gestures 
                if ( openPantryT < 150 && openPantryT < putFoodT && putFoodT < 120 && pourFoodT < 100 && pickFoodT < 60 && pickFoodT < pourFoodT )
                {
                    _systemEvent = "Eating Meal";
                    _eventTime = DateTime.Now.ToString();

                    //Clear all relevant stacks
                    _pickUpPantry.Clear();
                    _putDownFoodPrep.Clear();
                    _pourFoodPrep.Clear();
                    _pickUpFoodPrep.Clear();
                    _handToMouthDining.Clear();
                }
            }

            // Update these values in the GUI
            SystemEvent.Text = _systemEvent;
            EventTime.Text = _eventTime;
        }

        /// <summary>
        /// Handles Depth Button in GUI
        /// </summary>
        private void DepthButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Depth);
        }

        /// <summary>
        /// Handles Joints on Button in GUI
        /// </summary>
        private void JointsOnDepthButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.JointsOnDepth);
        }

        /// <summary>
        /// Handles Colour Button in GUI
        /// </summary>
        private void ColourButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Colour);
        }

        /// <summary>
        /// Handles Joints on Colour Button in GUI
        /// </summary>
        private void JointsOnColourButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.JointsOnColour);
        }
    }
}

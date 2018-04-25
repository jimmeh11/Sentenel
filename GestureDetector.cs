//------------------------------------------------------------------------------
// <copyright file="GestureDetector.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace SentenalProto_1
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Kinect.VisualGestureBuilder;
    using WindowsPreview.Kinect;

    /// <summary>
    /// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
    /// and updates the associated GestureResultView object with the latest results for the gesture
    /// </summary>
    public class GestureDetector : IDisposable
    {
        //public RoutedEventHandler GestureRecognized { get; set; }

        /// Path to the gesture database that was trained with VGB
        private readonly string _gestureDatabase = @"Database\SentenelGestures.gbd";

        // Name of the discrete gestures in the database that we want to track 
        private readonly string _pickUp = "PickUp";

        private readonly string _putDown = "PutDown";

        private readonly string _openDoor = "OpenDoor";

        private readonly string _handToMouth = "HandToMouth";

        private readonly string _pour = "Pour";

        /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
        private VisualGestureBuilderFrameSource _vgbFrameSource = null;

        /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
        private VisualGestureBuilderFrameReader _vgbFrameReader = null;

        private static string _gestureName;
        
        /// <summary>
        /// Initializes a new instance of the GestureDetector class along with the gesture frame source and reader
        /// </summary>
        /// <param name="kinectSensor">Active sensor to initialize the VisualGestureBuilderFrameSource object with</param>
        /// <param name="gestureResultView">GestureResultView object to store gesture results of a single body to</param>
        public GestureDetector(KinectSensor kinectSensor, GestureResultView gestureResultView)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }

            if (gestureResultView == null)
            {
                throw new ArgumentNullException("gestureResultView");
            }

            GestureResultView = gestureResultView;

            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            _vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            _vgbFrameSource.TrackingIdLost += Source_TrackingIdLost;

            // open the reader for the vgb frames
            _vgbFrameReader = _vgbFrameSource.OpenReader();
            if (_vgbFrameReader != null)
            {
                _vgbFrameReader.IsPaused = true;
                _vgbFrameReader.FrameArrived += Reader_GestureFrameArrived;
            }

            // load the gestures from the gesture database
            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(_gestureDatabase))
            {
                _vgbFrameSource.AddGestures(database.AvailableGestures);
            }
        }

        /// <summary> Gets the GestureResultView object which stores the detector results for display in the UI </summary>
        public GestureResultView GestureResultView { get; private set; }

        /// <summary>
        /// Gets or sets the body tracking ID associated with the current detector
        /// The tracking ID can change whenever a body comes in/out of scope
        /// </summary>
        public ulong TrackingId
        {
            get
            {
                return _vgbFrameSource.TrackingId;
            }

            set
            {
                if (_vgbFrameSource.TrackingId != value)
                {
                    _vgbFrameSource.TrackingId = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the detector is currently paused
        /// If the body tracking ID associated with the detector is not valid, then the detector should be paused
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return _vgbFrameReader.IsPaused;
            }

            set
            {
                if (_vgbFrameReader.IsPaused != value)
                {
                    _vgbFrameReader.IsPaused = value;
                }
            }
        }

        /// <summary>
        /// Disposes all unmanaged resources for the class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader objects
        /// </summary>
        /// <param name="disposing">True if Dispose was called directly, false if the GC handles the disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_vgbFrameReader != null)
                {
                    _vgbFrameReader.FrameArrived -= Reader_GestureFrameArrived;
                    _vgbFrameReader.Dispose();
                    _vgbFrameReader = null;
                }

                if (_vgbFrameSource != null)
                {
                    _vgbFrameSource.TrackingIdLost -= Source_TrackingIdLost;
                    _vgbFrameSource.Dispose();
                    _vgbFrameSource = null;
                }
            }
        }

       /// <summary>
        /// Handles gesture detection results arriving from the sensor for the associated body tracking Id
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            _gestureName = null;

            using (VisualGestureBuilderFrame frame = _vgbFrameReader.CalculateAndAcquireLatestFrame())
            {
                if (frame != null)
                {
                    // get the discrete gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;

                    if (discreteResults != null)
                    {
                        foreach (Gesture gesture in _vgbFrameSource.Gestures)
                        {
                            
                            DiscreteGestureResult result;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                if (gesture.Name.Equals(_pickUp) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    _gestureName = "PickUp";
                                }
                                else if (gesture.Name.Equals(_putDown) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    _gestureName = "PutDown";
                                }
                                else if (gesture.Name.Equals(_openDoor) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    _gestureName = "OpenDoor";
                                }
                                else if (gesture.Name.Equals(_handToMouth) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    _gestureName = "HandToMouth";
                                }
                                else if (gesture.Name.Equals(_pour) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    _gestureName = "Pour";
                                }
                                else
                                {
                                    _gestureName = "No Gesture";
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the TrackingIdLost event for the VisualGestureBuilderSource object
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            // update the GestureResultView object to show the 'Not Tracked' image in the UI
            GestureResultView.UpdateGestureResult(false, false, 0.0f, null);
        }
    }
}

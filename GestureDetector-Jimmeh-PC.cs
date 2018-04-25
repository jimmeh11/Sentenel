//------------------------------------------------------------------------------
// <copyright file="GestureDetector.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Diagnostics.Tracing;

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
        private readonly string gestureDatabase = @"Database\SentenelGestures.gbd";

        // Name of the discrete gestures in the database that we want to track 
        private readonly string PickUp = "PickUp";

        private readonly string PutDown = "PutDown";

        private readonly string OpenDoor = "OpenDoor";

        private readonly string HandToMouth = "HandToMouth";

        private readonly string Pour = "Pour";

        /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
        private VisualGestureBuilderFrameSource vgbFrameSource = null;

        /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
        private VisualGestureBuilderFrameReader vgbFrameReader = null;

        public static string gestureName = null;
        
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

            this.GestureResultView = gestureResultView;

            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            this.vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            this.vgbFrameSource.TrackingIdLost += this.Source_TrackingIdLost;

            // open the reader for the vgb frames
            this.vgbFrameReader = this.vgbFrameSource.OpenReader();
            if (this.vgbFrameReader != null)
            {
                this.vgbFrameReader.IsPaused = true;
                this.vgbFrameReader.FrameArrived += this.Reader_GestureFrameArrived;
            }

            // load the gestures from the gesture database
            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(this.gestureDatabase))
            {
                this.vgbFrameSource.AddGestures(database.AvailableGestures);
                //foreach (Gesture gesture in database.AvailableGestures)
                //{
                //    if (gesture.Name.Equals(this.PickUp))
                //    {
                //        this.vgbFrameSource.AddGesture(gesture);
                //    }
                //}
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
                return this.vgbFrameSource.TrackingId;
            }

            set
            {
                if (this.vgbFrameSource.TrackingId != value)
                {
                    this.vgbFrameSource.TrackingId = value;
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
                return this.vgbFrameReader.IsPaused;
            }

            set
            {
                if (this.vgbFrameReader.IsPaused != value)
                {
                    this.vgbFrameReader.IsPaused = value;
                }
            }
        }

        /// <summary>
        /// Disposes all unmanaged resources for the class
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
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
                if (this.vgbFrameReader != null)
                {
                    this.vgbFrameReader.FrameArrived -= this.Reader_GestureFrameArrived;
                    this.vgbFrameReader.Dispose();
                    this.vgbFrameReader = null;
                }

                if (this.vgbFrameSource != null)
                {
                    this.vgbFrameSource.TrackingIdLost -= this.Source_TrackingIdLost;
                    this.vgbFrameSource.Dispose();
                    this.vgbFrameSource = null;
                }
            }
        }

        ///// <summary>
        ///// Retrieves the latest gesture detection results from the sensor
        ///// </summary>
        //public void UpdateGestureData()
        //{
        //    string gestureName = null;

        //    using (var frame = this.vgbFrameReader.CalculateAndAcquireLatestFrame())
        //    {
        //        if (frame != null)
        //        {
        //            // get all discrete and continuous gesture results that arrived with the latest frame
        //            var discreteResults = frame.DiscreteGestureResults;
        //            // var continuousResults = frame.ContinuousGestureResults;
        //            bool isPickUp = false;
        //            bool isPutDown = false;
        //            bool isOpenDoor = false;
        //            bool isHandToMouth = false;
        //            bool isPour = false;
                    
        //            if (discreteResults != null)
        //            {
        //                foreach (var gesture in this.vgbFrameSource.Gestures)
        //                {
        //                    if (gesture.GestureType == GestureType.Discrete)
        //                    {
        //                        DiscreteGestureResult result = null;
        //                        discreteResults.TryGetValue(gesture, out result);

        //                        if (result != null)
        //                        {
        //                            if (gesture.Name.Equals(this.PickUp))
        //                            {
        //                                isPickUp = result.Detected;
        //                                gestureName = PickUp;
        //                            }
        //                            else if (gesture.Name.Equals(this.PutDown))
        //                            {
        //                                isPutDown = result.Detected;
        //                                gestureName = PutDown;
        //                            }
        //                            else if (gesture.Name.Equals(this.OpenDoor))
        //                            {
        //                                isOpenDoor = result.Detected;
        //                                gestureName = OpenDoor;
        //                            }
        //                            else if (gesture.Name.Equals(this.HandToMouth))
        //                            {
        //                                isHandToMouth = result.Detected;
        //                                gestureName = HandToMouth;
        //                            }
        //                            else if (gesture.Name.Equals(this.Pour))
        //                            {
        //                                isPour = result.Detected;
        //                                gestureName = Pour;
        //                            }
        //                        }

        //                        if (gestureName == null)
        //                        {
        //                            this.GestureResultView.UpdateGestureResult(true, false, 0.0f, null);
        //                        }
        //                        else
        //                        {
        //                            // update the UI with the latest gesture detection results
        //                            this.GestureResultView.UpdateGestureResult(true, true, result.Confidence, gestureName);
        //                        }
        //                    }
        //                    //if (continuousResults != null)
        //                }
        //            }
        //        }
        //    }
        //}
        
        /// <summary>
        /// Handles gesture detection results arriving from the sensor for the associated body tracking Id
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            gestureName = null;

            VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (VisualGestureBuilderFrame frame = vgbFrameReader.CalculateAndAcquireLatestFrame())
            {
                if (frame != null)
                {
                    // get the discrete gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;

                    if (discreteResults != null)
                    {
                        foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                        {
                            
                            DiscreteGestureResult result = null;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                if (gesture.Name.Equals(this.PickUp) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.05)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    gestureName = "PickUp";
                                }
                                else if (gesture.Name.Equals(this.PutDown) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.2)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    gestureName = "PutDown";
                                }
                                else if (gesture.Name.Equals(this.OpenDoor) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.2)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    gestureName = "OpenDoor";
                                }
                                else if (gesture.Name.Equals(this.HandToMouth) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.2)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    gestureName = "HandToMouth";
                                }
                                else if (gesture.Name.Equals(this.Pour) && gesture.GestureType == GestureType.Discrete && result.Confidence > 0.2)
                                {
                                    // update the GestureResultView object with new gesture result values
                                    this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence, gesture.Name);
                                    gestureName = "Pour";
                                }
                                else
                                {
                                    gestureName = "No Gesture";
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
            this.GestureResultView.UpdateGestureResult(false, false, 0.0f, null);
        }
    }
}

//------------------------------------------------------------------------------
// <copyright file="GestureResultView.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace SentenalProto_1
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Stores discrete gesture results for the GestureDetector.
    /// Properties are stored/updated for display in the UI.
    /// </summary>
    public sealed class GestureResultView : INotifyPropertyChanged
    {
        /// <summary> The body index (0-5) associated with the current gesture detector </summary>
        private int _bodyIndex = 0;

        // Name of the gesture currently being detected
        private string _gestureName = null;

        /// <summary> Current confidence value reported by the discrete gesture </summary>
        private float _confidence = 0.0f;

        /// <summary> True, if the discrete gesture is currently being detected </summary>
        private bool _detected = false;

        /// <summary> True, if the body is currently being tracked </summary>
        private bool _isTracked = false;


        /// <summary>
        /// Initializes a new instance of the GestureResultView class and sets initial property values
        /// </summary>
        /// <param name="bodyIndex">Body Index associated with the current gesture detector</param>
        /// <param name="isTracked">True, if the body is currently tracked</param>
        /// <param name="detected">True, if the gesture is currently detected for the associated body</param>
        /// <param name="confidence">Confidence value for detection of the 'Seated' gesture</param>
        /// <param name="gestureName">Name of the gesture that was detected. USed to differentiate gestures in the GUI</param>
        public GestureResultView(int bodyIndex, bool isTracked, bool detected, float confidence, string gestureName)
        {
            BodyIndex = bodyIndex;
            IsTracked = isTracked;
            Detected = detected;
            Confidence = confidence;
            GestureName = gestureName;
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary> 
        /// Gets the body index associated with the current gesture detector result 
        /// </summary>
        public int BodyIndex
        {
            get
            {
                return _bodyIndex;
            }

            private set
            {
                if (_bodyIndex == value) return;

                _bodyIndex = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary> 
        /// Gets a value indicating whether or not the body associated with the gesture detector is currently being tracked 
        /// </summary>
        public bool IsTracked
        {
            get
            {
                return _isTracked;
            }

            private set
            {
                if (IsTracked == value) return;

                _isTracked = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary> 
        /// Gets a value indicating whether or not the discrete gesture has been detected
        /// </summary>
        public bool Detected
        {
            get
            {
                return _detected;
            }

            private set
            {
                if (_detected == value) return;

                _detected = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary> 
        /// Gets a float value which indicates the detector's confidence that the gesture is occurring for the associated body 
        /// </summary>
        public float Confidence
        {
            get
            {
                return _confidence;
            }

            private set
            {
                if (_confidence == value) return;

                _confidence = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary> 
        /// Gets a string value which indicates the name of the gesture  that is currently being detected for the associated body 
        /// </summary>
        public string GestureName
        {
            get
            {
                return _gestureName;
            }

            private set
            {
                if (_gestureName == value) return;

                _gestureName = value;
                NotifyPropertyChanged();
            }
        }



        /// <summary>
        /// Updates the values associated with the discrete gesture detection result
        /// </summary>
        /// <param name="isBodyTrackingIdValid">True, if the body associated with the GestureResultView object is still being tracked</param>
        /// <param name="isGestureDetected">True, if the discrete gesture is currently detected for the associated body</param>
        /// <param name="detectionConfidence">Confidence value for detection of the discrete gesture</param>
        public void UpdateGestureResult(bool isBodyTrackingIdValid, bool isGestureDetected, float detectionConfidence, string currentGestureName)
        {
            IsTracked = isBodyTrackingIdValid;
            Confidence = 0.0f;

            if (!IsTracked)
            {
                Detected = false;
                GestureName = null;
            }
            else
            {
                Detected = isGestureDetected;

                if (Detected)
                {
                    Confidence = detectionConfidence;
                    GestureName = currentGestureName;
                }
            }
        }

        /// <summary>
        /// Notifies UI that a property has changed
        /// </summary>
        /// <param name="propertyName">Name of property that has changed</param> 
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

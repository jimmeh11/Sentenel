﻿using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using WindowsPreview.Kinect;

namespace SentenalProto_1
{
    public class BodiesManager
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HighConfidenceHandSize = 40;

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double LowConfidenceHandSize = 20;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 8.0;

        /// <summary>
        /// Thickness of seen bone lines
        /// </summary>
        private const double TrackedBoneThickness = 4.0;

        /// <summary>
        /// Thickness of inferred joint lines
        /// </summary>
        private const double InferredBoneThickness = 1.0;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 5;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        private readonly CoordinateMapper _coordinateMapper;
        private readonly Canvas _drawingCanvas;

        private Rectangle _leftClipEdge;
        private Rectangle _rightClipEdge;
        private Rectangle _topClipEdge;
        private Rectangle _bottomClipEdge;

        /// <summary>
        /// List of BodyInfo objects for each potential body
        /// </summary>
        private BodyInfo[] _bodyInfos;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private readonly List<Color> _bodyColors;

        private int BodyCount
        {
            set
            {
                if (value == 0)
                {
                    _bodyInfos = null;
                    return;
                }

                // creates instances of BodyInfo objects for potential number of bodies
                if (_bodyInfos != null && _bodyInfos.Length == value) return;

                _bodyInfos = new BodyInfo[value];

                for (var bodyIndex = 0; bodyIndex < value; bodyIndex++)
                {
                    _bodyInfos[bodyIndex] = new BodyInfo(_bodyColors[bodyIndex], JointThickness);
                }
            }

            get { return _bodyInfos == null ? 0 : _bodyInfos.Length; }
        }

        /// <summary>
        /// Maps bodies and joints to render them in a canvas.
        /// </summary>
        /// <param name="coordMapper">The current co-ordinate mapper from the Kinect Sensor</param>
        /// <param name="drawableCanvas">The canvas upon which the joints and bones are drawn</param>
        /// <param name="bodyCount">The amount of bodies concurrently drawable</param>
        public BodiesManager(CoordinateMapper coordMapper, Canvas drawableCanvas, int bodyCount)
        {
            _coordinateMapper = coordMapper;
            _drawingCanvas = drawableCanvas;
            // populate body colors, one for each BodyIndex
            _bodyColors = new List<Color>
            {
                Colors.Red,
                Colors.Orange,
                Colors.Green,
                Colors.Blue,
                Colors.Indigo,
                Colors.Violet
            };

            // sets total number of possible tracked bodies
            // create ellipses and lines for drawing bodies
            BodyCount = bodyCount;

            PopulateVisualJoints();
        }

        /// <summary>
        /// Instantiate new objects for joints, bone lines, and clipped edge rectangles
        /// </summary>
        private void PopulateVisualJoints()
        {
            // create clipped edges and set to collapsed initially
            _leftClipEdge = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Red),
                Width = ClipBoundsThickness,
                Height = _drawingCanvas.Height,
                Visibility = Windows.UI.Xaml.Visibility.Collapsed
            };

            _rightClipEdge = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Red),
                Width = ClipBoundsThickness,
                Height = _drawingCanvas.Height,
                Visibility = Windows.UI.Xaml.Visibility.Collapsed
            };

            _topClipEdge = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Red),
                Width = _drawingCanvas.Width,
                Height = ClipBoundsThickness,
                Visibility = Windows.UI.Xaml.Visibility.Collapsed
            };

            _bottomClipEdge = new Rectangle()
            {
                Fill = new SolidColorBrush(Colors.Red),
                Width = _drawingCanvas.Width,
                Height = ClipBoundsThickness,
                Visibility = Windows.UI.Xaml.Visibility.Collapsed
            };

            foreach (var bodyInfo in _bodyInfos)
            {
                // add left and right hand ellipses of all bodies to canvas
                _drawingCanvas.Children.Add(bodyInfo.HandLeftEllipse);
                _drawingCanvas.Children.Add(bodyInfo.HandRightEllipse);

                // add joint ellipses of all bodies to canvas
                foreach (var joint in bodyInfo.JointPoints)
                {
                    _drawingCanvas.Children.Add(joint.Value);
                }

                // add bone lines of all bodies to canvas
                foreach (var bone in bodyInfo.Bones)
                {
                    _drawingCanvas.Children.Add(bodyInfo.BoneLines[bone]);
                }
            }

            // add clipped edges rectanges to main canvas
            _drawingCanvas.Children.Add(_leftClipEdge);
            _drawingCanvas.Children.Add(_rightClipEdge);
            _drawingCanvas.Children.Add(_topClipEdge);
            _drawingCanvas.Children.Add(_bottomClipEdge);

            // position the clipped edges
            Canvas.SetLeft(_leftClipEdge, 0);
            Canvas.SetTop(_leftClipEdge, 0);
            Canvas.SetLeft(_rightClipEdge, _drawingCanvas.Width - ClipBoundsThickness);
            Canvas.SetTop(_rightClipEdge, 0);
            Canvas.SetLeft(_topClipEdge, 0);
            Canvas.SetTop(_topClipEdge, 0);
            Canvas.SetLeft(_bottomClipEdge, 0);
            Canvas.SetTop(_bottomClipEdge, _drawingCanvas.Height - ClipBoundsThickness);
        }

        /// <summary>
        /// Updates all elipses and lines representing joints and bones
        /// with the latest tracked bodies. 
        /// </summary>
        /// <param name="bodies">An array containing body data.</param>
        internal void UpdateBodiesAndEdges(Body[] bodies)
        {
            bool hasTrackedBody = false;
            // iterate through each body
            for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
            {
                Body body = bodies[bodyIndex];

                if (body.IsTracked)
                {
                    // check if this body clips an edge
                    UpdateClippedEdges(body, hasTrackedBody);

                    UpdateBody(body, bodyIndex);

                    hasTrackedBody = true;
                }
                else
                {
                    // collapse this body from canvas as it goes out of view
                    ClearBody(bodyIndex);
                }
            }

            if (!hasTrackedBody)
            {
                // clear clipped edges if no bodies are tracked
                ClearClippedEdges();
            }
        }

        /// <summary>
        /// Update body data for each body that is tracked.
        /// </summary>
        /// <param name="body">body for getting joint info</param>
        /// <param name="bodyIndex">index for body we are currently updating</param>
        internal void UpdateBody(Body body, int bodyIndex)
        {
            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
            var jointPointsInDepthSpace = new Dictionary<JointType, Point>();

            var bodyInfo = _bodyInfos[bodyIndex];
            
            // update all joints
            foreach (var jointType in body.Joints.Keys)
            {
                // sometimes the depth(Z) of an inferred joint may show as negative
                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                CameraSpacePoint position = body.Joints[jointType].Position;
                if (position.Z < 0)
                {
                    position.Z = InferredZPositionClamp;
                }

                // map joint position to depth space
                DepthSpacePoint depthSpacePoint = _coordinateMapper.MapCameraPointToDepthSpace(position);
                jointPointsInDepthSpace[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);

                // modify the joint's visibility and location
                UpdateJoint(bodyInfo.JointPoints[jointType], joints[jointType], jointPointsInDepthSpace[jointType]);

                // modify hand ellipse colors based on hand states
                // modity hand ellipse sizes based on tracking confidences
                if (jointType == JointType.HandRight)
                {
                    UpdateHand(bodyInfo.HandRightEllipse, body.HandRightState, body.HandRightConfidence, jointPointsInDepthSpace[jointType]);
                }

                if (jointType == JointType.HandLeft)
                {
                    UpdateHand(bodyInfo.HandLeftEllipse, body.HandLeftState, body.HandLeftConfidence, jointPointsInDepthSpace[jointType]);
                }
            }

            // update all bones
            foreach (var bone in bodyInfo.Bones)
            {
                UpdateBone(bodyInfo.BoneLines[bone], joints[bone.Item1], joints[bone.Item2],
                                jointPointsInDepthSpace[bone.Item1],
                                jointPointsInDepthSpace[bone.Item2]);
            }
        }
        /// <summary>
        /// Updates hand state ellipses depending on tracking state and it's confidence.
        /// </summary>
        /// <param name="ellipse">ellipse representing handstate</param>
        /// <param name="handState">open, closed, or lasso</param>
        /// <param name="trackingConfidence">confidence of handstate</param>
        /// <param name="point">location of handjoint</param>
        private void UpdateHand(Ellipse ellipse, HandState handState, TrackingConfidence trackingConfidence, Point point)
        {
            ellipse.Fill = new SolidColorBrush(HandStateToColor(handState));

            // draw handstate ellipse based on tracking confidence
            ellipse.Width = ellipse.Height = (trackingConfidence == TrackingConfidence.Low) ? LowConfidenceHandSize : HighConfidenceHandSize;

            ellipse.Visibility = Windows.UI.Xaml.Visibility.Visible;

            // don't draw handstate if hand joints are not tracked
            if (!Double.IsInfinity(point.X) && !Double.IsInfinity(point.Y))
            {
                Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, point.Y - ellipse.Width / 2);
            }
        }

        /// <summary>
        /// Update a joint.
        /// </summary>
        /// <param name="ellipse"></param>
        /// <param name="joint"></param>
        /// <param name="point"></param>
        private void UpdateJoint(Ellipse ellipse, Joint joint, Point point)
        {
            TrackingState trackingState = joint.TrackingState;

            // only draw if joint is tracked or inferred
            if (trackingState != TrackingState.NotTracked)
            {
                if (trackingState == TrackingState.Tracked)
                {
                    ellipse.Fill = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    // inferred joints are yellow
                    ellipse.Fill = new SolidColorBrush(Colors.Yellow);
                }

                Canvas.SetLeft(ellipse, point.X - JointThickness / 2);
                Canvas.SetTop(ellipse, point.Y - JointThickness / 2);

                ellipse.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                ellipse.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update a bone line.
        /// </summary>
        /// <param name="line">line representing a bone line</param>
        /// <param name="startJoint">start joint of bone line</param>
        /// <param name="endJoint">end joint of bone line</param>
        /// <param name="startPoint">location of start joint</param>
        /// <param name="endPoint">location of end joint</param>
        private void UpdateBone(Line line, Joint startJoint, Joint endJoint, Point startPoint, Point endPoint)
        {
            // don't draw if neither joints are tracked
            if (startJoint.TrackingState == TrackingState.NotTracked || endJoint.TrackingState == TrackingState.NotTracked)
            {
                line.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            // all lines are inferred thickness unless both joints are tracked
            line.StrokeThickness = InferredBoneThickness;

            if (startJoint.TrackingState == TrackingState.Tracked &&
                endJoint.TrackingState == TrackingState.Tracked)
            {
                line.StrokeThickness = TrackedBoneThickness;
            }

            line.Visibility = Windows.UI.Xaml.Visibility.Visible;

            line.X1 = startPoint.X;
            line.Y1 = startPoint.Y;
            line.X2 = endPoint.X;
            line.Y2 = endPoint.Y;
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data.
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="hasTrackedBody">bool to determine if another body is triggering a clipped edge</param>
        private void UpdateClippedEdges(Body body, bool hasTrackedBody)
        {
            // BUG (waiting for confirmation): 
            // Clip dectection works differently for top and right edges compared to left and bottom edges
            // due to the current joint confidence model. This is an ST issue.
            // Joints become inferred immediately as they touch the left/bottom edges and clip detection triggers.
            // Joints squish on the right/top edges and clip detection doesn't trigger until more joints of 
            // the body goes out of view (e.g all hand joints vs only handtip).

            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                _leftClipEdge.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else if (!hasTrackedBody)
            {
                // don't clear this edge if another body is triggering clipped edge
                _leftClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                _rightClipEdge.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else if (!hasTrackedBody)
            {
                _rightClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                _topClipEdge.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else if (!hasTrackedBody)
            {
                _topClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                _bottomClipEdge.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else if (!hasTrackedBody)
            {
                _bottomClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Select color of hand state
        /// </summary>
        /// <param name="handState"></param>
        /// <returns></returns>
        private Color HandStateToColor(HandState handState)
        {
            switch (handState)
            {
                case HandState.Open:
                    return Colors.Green;

                case HandState.Closed:
                    return Colors.Red;

                case HandState.Lasso:
                    return Colors.Blue;
            }

            return Colors.Transparent;
        }

        /// <summary>
        /// Collapse the body from the canvas.
        /// </summary>
        /// <param name="bodyIndex"></param>
        private void ClearBody(int bodyIndex)
        {
            var bodyInfo = _bodyInfos[bodyIndex];

            // collapse all joint ellipses
            foreach (var joint in bodyInfo.JointPoints)
            {
                joint.Value.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            // collapse all bone lines
            foreach (var bone in bodyInfo.Bones)
            {
                bodyInfo.BoneLines[bone].Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            // collapse handstate ellipses
            bodyInfo.HandLeftEllipse.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            bodyInfo.HandRightEllipse.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        /// <summary>
        /// Clear all clipped edges.
        /// </summary>
        private void ClearClippedEdges()
        {
            _leftClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            _rightClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            _topClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            _bottomClipEdge.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }
    }
}

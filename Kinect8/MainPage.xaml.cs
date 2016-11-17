using System;
using System.Collections.Generic;
using System.IO;
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
using WindowsPreview.Kinect;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Kinect.Face;

using Windows.Storage.Streams;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Shapes;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Kinect8
{

    public enum DisplayFrameType {
        Infrared,
        Color,
        Depth,
        BodyMask,
        BodyJoints,
        Draw,
        Blob,
        Sound,
        Faces
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public string StatusText { get; set; }

        private const DisplayFrameType DEFAULT_DISPLAY_TYPE = DisplayFrameType.Color;

        private const int BytesPerPixel = 4;
        private WriteableBitmap bitmap;

        private FrameDescription currentFrameDescription;
        private DisplayFrameType currentDisplayFrameType;

        private MultiSourceFrameReader multiSourceFrameReader = null;
        private CoordinateMapper coordinateMapper = null;
        private BodiesManager bodiesManager = null;
        private FaceFrameReader faceFrameReader = null;

        private ushort[] ifFrameData;
        private byte[] irPixels = null;

        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
        private const float InfraredOutputValueMinimum = 0.01f;
        private const float InfraredOutputValueMaximum = 1.0f;

        private const float InfraredSceneValueAverage = 0.08f;
        private const float InfraredSceneStandardDeviations = 3.0f;


        private Random rand = new Random();

        //Depth shit
        private ushort[] depthFrameData = null;
        private byte[] depthPixels = null;

        //body mask shit
        private DepthSpacePoint[] colorMappedToDepthPoints = null;

        //body joint shit
        private Canvas canvas;
        KinectSensor sensor;


        //draw shit
        static JointType[] selectJoints = new JointType[] {
            JointType.HandRight,
            JointType.HandLeft,
            JointType.ElbowLeft,
            JointType.ElbowRight,
            JointType.ShoulderRight,
            JointType.ShoulderLeft,
            JointType.WristLeft,
            JointType.WristRight
        };

        static int numPoints = 100;
        Ellipse[] points = new Ellipse[numPoints];

        private void SetupCurrentDisplay(DisplayFrameType newDisplayFrameType)
        {
            currentDisplayFrameType = newDisplayFrameType;

            FrameDescription colorFrameDescription = null;
            if(this.BodyJointsGrid != null)
            {
                this.BodyJointsGrid.Visibility = Visibility.Collapsed;
            }

            if(this.FrameDisplayImage != null)
            {
                this.FrameDisplayImage.Source = null;
            }

            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:

                    FrameDescription irFrameDescription = this.sensor.InfraredFrameSource.FrameDescription;

                    this.currentFrameDescription = irFrameDescription;

                    this.ifFrameData = new ushort[irFrameDescription.Width * irFrameDescription.Height];
                    this.irPixels = new byte[irFrameDescription.Width * irFrameDescription.Height * BytesPerPixel];
                    this.bitmap = new WriteableBitmap(irFrameDescription.Width, irFrameDescription.Height);

                    break;
                case DisplayFrameType.Color:
                    colorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
                    this.currentFrameDescription = colorFrameDescription;

                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);
                    break;

                case DisplayFrameType.Depth:
                    FrameDescription depthFrameDescription = this.sensor.DepthFrameSource.FrameDescription;
                    this.currentFrameDescription = depthFrameDescription;

                    this.depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                    this.depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];

                    this.bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height);
                    break;

                case DisplayFrameType.BodyMask:
                    colorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
                    this.currentFrameDescription = colorFrameDescription;
                    this.colorMappedToDepthPoints = new DepthSpacePoint[colorFrameDescription.Width * colorFrameDescription.Height];
                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);
                    break;

                case DisplayFrameType.BodyJoints:
                    this.canvas = new Canvas();

                    this.canvas.Clip = new RectangleGeometry();
                    this.canvas.Clip.Rect = new Rect(0.0, 0.0, this.BodyJointsGrid.Width, this.BodyJointsGrid.Height);
                    this.canvas.Width = this.BodyJointsGrid.Width;
                    this.canvas.Height = this.BodyJointsGrid.Height;

                    this.BodyJointsGrid.Visibility = Visibility.Visible;
                    this.BodyJointsGrid.Children.Clear();
                    this.BodyJointsGrid.Children.Add(this.canvas);
                    bodiesManager = new BodiesManager(this.coordinateMapper, this.canvas, this.sensor.BodyFrameSource.BodyCount);
                    break;

                case DisplayFrameType.Draw:
                    this.canvas = new Canvas();
                    this.canvas.Width = this.BodyJointsGrid.Width;
                    this.canvas.Height = this.BodyJointsGrid.Height;

                    this.BodyJointsGrid.Visibility = Visibility.Visible;
                    this.BodyJointsGrid.Children.Clear();
                    this.BodyJointsGrid.Children.Add(this.canvas);
                    break;

                case DisplayFrameType.Sound:
                    colorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
                    this.currentFrameDescription = colorFrameDescription;
                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);

                    this.canvas = new Canvas();
                    this.canvas.Width = this.BodyJointsGrid.Width;
                    this.canvas.Height = this.BodyJointsGrid.Height;

                    this.BodyJointsGrid.Visibility = Visibility.Visible;
                    this.BodyJointsGrid.Children.Clear();
                    this.BodyJointsGrid.Children.Add(this.canvas);

                    break;

                case DisplayFrameType.Faces:

                    colorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
                    this.currentFrameDescription = colorFrameDescription;
                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);
                    this.canvas = new Canvas();
                    this.canvas.Width = this.BodyJointsGrid.Width;
                    this.canvas.Height = this.BodyJointsGrid.Height;

                    this.BodyJointsGrid.Visibility = Visibility.Visible;
                    this.BodyJointsGrid.Children.Clear();
                    this.BodyJointsGrid.Children.Add(this.canvas);
                    break;
             
                default:
                    break;
            }
        }

        public MainPage()
        {
            this.sensor = KinectSensor.GetDefault();
            this.sensor.Open();

            SetupCurrentDisplay(DEFAULT_DISPLAY_TYPE);

            this.coordinateMapper = this.sensor.CoordinateMapper;

            FaceFrameSource face_source = new FaceFrameSource(sensor, 0, FaceFrameFeatures.BoundingBoxInColorSpace | 
                FaceFrameFeatures.PointsInColorSpace | FaceFrameFeatures.FaceEngagement | FaceFrameFeatures.Glasses 
                | FaceFrameFeatures.Happy | FaceFrameFeatures.LeftEyeClosed  | FaceFrameFeatures.MouthOpen
                );

            faceFrameReader = face_source.OpenReader();

            faceFrameReader.FrameArrived += FaceFrameReader_FrameArrived;

            this.multiSourceFrameReader = this.sensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Infrared | 
                FrameSourceTypes.Color | 
                FrameSourceTypes.Depth | 
                FrameSourceTypes.BodyIndex | 
                FrameSourceTypes.Body );

            this.multiSourceFrameReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;
            this.sensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            this.InitializeComponent();
        }

        private void FaceFrameReader_FrameArrived(FaceFrameReader sender, FaceFrameArrivedEventArgs args)
        {

            using (var frame = args.FrameReference.AcquireFrame())
            {
                if(currentDisplayFrameType != DisplayFrameType.Faces)
                {
                    return;
                }

                if(frame == null)
                {
                    return;
                }

                FaceFrameResult res = frame.FaceFrameResult;

                if(res == null)
                {
                    return;
                }

                var bound = res.FaceBoundingBoxInColorSpace;
                Rectangle rect;
                if(canvas.Children.Count > 0)
                {
                    rect = (Rectangle)canvas.Children[0];
                } 
                else
                {
                    rect = new Rectangle()
                    {
                        Width = bound.Right - bound.Left,
                        Height = bound.Top - bound.Bottom,
                        Visibility = Visibility.Visible,
                        Stroke = new SolidColorBrush(Colors.Yellow),
                        Fill = new SolidColorBrush(Colors.Yellow),
                        StrokeThickness = 10
                    };

                    canvas.Children.Add(rect);
                }

                Canvas.SetLeft(rect, bound.Left);
                Canvas.SetTop(rect, bound.Top);
            }
        }

        private void Sensor_IsAvailableChanged(KinectSensor sender, IsAvailableChangedEventArgs args)
        {
            this.StatusText = this.sensor.IsAvailable ? "Running" : "Not available";
        }

        private void Reader_MultiSourceFrameArrived(MultiSourceFrameReader sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame msFrame = e.FrameReference.AcquireFrame();


            if (msFrame == null)
            {
                return;
            }

            InfraredFrame infraredFrame = null;
            ColorFrame colorFrame = null;
            DepthFrame df = null;
            BodyIndexFrame biframe = null;
            BodyFrame bodyFrame = null;

            IBuffer depthFrameDataBuffer = null;
            IBuffer bodyIndexFrameData = null;
            IBufferByteAccess bodyIndexByteAccess = null;
            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:
                    using (infraredFrame = msFrame.InfraredFrameReference.AcquireFrame())
                    {
                        ShowInfraredFrame(infraredFrame);
                    }
                    break;
                case DisplayFrameType.Color:
                    using (colorFrame = msFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);
                    }
                    break;
                case DisplayFrameType.Depth:
                    using(df = msFrame.DepthFrameReference.AcquireFrame())
                    {
                        ShowDepthFrame(df);
                    }
                    break;
                case DisplayFrameType.BodyJoints:
                    using(bodyFrame = msFrame.BodyFrameReference.AcquireFrame())
                    {
                        ShowBodyJoints(bodyFrame);
                    }
                    break;
                case DisplayFrameType.Draw:
                    using(bodyFrame = msFrame.BodyFrameReference.AcquireFrame())
                    {
                        DrawShit(bodyFrame);
                    }
                    break;
                case DisplayFrameType.Sound:
                    using (colorFrame = msFrame.ColorFrameReference.AcquireFrame())
                    {
                        DrawSound(colorFrame);
                    }
                    break;
                case DisplayFrameType.Faces:
                    using(colorFrame = msFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);
                    }
                    break;
                case DisplayFrameType.BodyMask:
                    try
                    {
                        df = msFrame.DepthFrameReference.AcquireFrame();
                        biframe = msFrame.BodyIndexFrameReference.AcquireFrame();
                        colorFrame = msFrame.ColorFrameReference.AcquireFrame();

                        if (df == null || colorFrame == null || biframe == null)
                            return;
                        depthFrameDataBuffer = df.LockImageBuffer();
                        this.coordinateMapper.MapColorFrameToDepthSpaceUsingIBuffer(depthFrameDataBuffer, this.colorMappedToDepthPoints);
                        colorFrame.CopyConvertedFrameDataToBuffer(this.bitmap.PixelBuffer, ColorImageFormat.Bgra);

                        bodyIndexFrameData = biframe.LockImageBuffer();
                        ShowMappedBodyFrame(df.FrameDescription.Width, df.FrameDescription.Height, bodyIndexFrameData, bodyIndexByteAccess);
                    }
                    finally
                    {
                        if (df != null)
                            df.Dispose();
                        if (colorFrame != null)
                            colorFrame.Dispose();
                        if (biframe != null)
                            biframe.Dispose();

                        if (bodyIndexByteAccess != null)
                        {
                            Marshal.ReleaseComObject(bodyIndexByteAccess);
                        }
                        if (depthFrameDataBuffer != null)
                        {
                            Marshal.ReleaseComObject(depthFrameDataBuffer);
                        }
                        if(bodyIndexFrameData != null)
                        {
                            Marshal.ReleaseComObject(bodyIndexFrameData);
                        }
                    }
                    break;
                default:
                    break;
            }


        }

        unsafe private void ShowMappedBodyFrame(int depthWidth, int depthHeight, IBuffer bodyIndexFrameData, IBufferByteAccess bodyIndexByteAccess)
        {

            bodyIndexByteAccess = (IBufferByteAccess)bodyIndexFrameData;
            byte* bodyIndexBytes = null;
            bodyIndexByteAccess.Buffer(out bodyIndexBytes);

            fixed(DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
            {
                IBufferByteAccess bitmapBackBufferByteAccess = (IBufferByteAccess)this.bitmap.PixelBuffer;

                byte* bitmapBackBufferBytes = null;
                bitmapBackBufferByteAccess.Buffer(out bitmapBackBufferBytes);

                uint* bitmapPixelsPointer = (uint*)bitmapBackBufferBytes;

                int colorMappedLength = this.colorMappedToDepthPoints.Length;
                for(int colorIndex = 0; colorIndex < colorMappedLength; ++colorIndex)
                {
                    float colorMappedToDepthX = colorMappedToDepthPointsPointer[colorIndex].X;
                    float colorMappedToDepthY = colorMappedToDepthPointsPointer[colorIndex].Y;

                    if(!float.IsNegativeInfinity(colorMappedToDepthX) && !float.IsNegativeInfinity(colorMappedToDepthY))
                    {
                        int depthX = (int)(colorMappedToDepthX + 0.5f);
                        int depthY = (int)(colorMappedToDepthY + 0.5f);

                        if(depthX >= 0 && depthX < depthWidth && depthY >= 0 && depthY < depthHeight)
                        {
                            int depthIndex = (depthY * depthWidth) + depthX;

                            if (bodyIndexBytes[depthIndex] != 0xff)
                            {
                                // bitmapPixelsPointer[colorIndex] *= bitmapPixelsPointer[colorIndex] > .7 ? (uint)30: (uint)1;
                                continue;
                            }
                        }
                    }
                    bitmapPixelsPointer[colorIndex] *= (uint)0;
                }
            }

            this.bitmap.Invalidate();
            FrameDisplayImage.Source = this.bitmap;
        }

        int numEllipse = 0;
        int maxChildren = 900 * selectJoints.Length;
        int thing = 0;
        SolidColorBrush[] colors = new SolidColorBrush[]
        {
            new SolidColorBrush(Colors.LightBlue),
            new SolidColorBrush(Colors.LightGreen),
            new SolidColorBrush(Colors.PaleVioletRed),
            new SolidColorBrush(Colors.Azure),
            new SolidColorBrush(Colors.Crimson),
            new SolidColorBrush(Colors.DarkOliveGreen),
            new SolidColorBrush(Colors.Gold)
        };

        int numSoundEllipses = 0;
        private void DrawSound(ColorFrame colorFrame)
        {
            if(colorFrame == null)
            {
                return;
            }

            var beams = sensor.AudioSource.AudioBeams;
            var maxEllipses = 1;
            foreach(var beam in beams)
            {
                var angle = beam.BeamAngle;
                Ellipse ellipse = null;
                TextBlock tb = null;

                if(numSoundEllipses >= maxEllipses)
                {
                    ellipse = (Ellipse)canvas.Children[0];
                    tb = (TextBlock)canvas.Children[1];
                }
                else
                {
                    ellipse = new Ellipse()
                    {
                        Visibility = Visibility.Visible,
                        Height = 30,
                        Width = 30,
                        Fill = new SolidColorBrush(Colors.White)
                    };
                    canvas.Children.Add(ellipse);
                    numSoundEllipses++;

                    tb = new TextBlock()
                    {
                        Visibility = Visibility.Visible,
                        Text = "yo",
                        Height = 50,
                        Width = 200
                    };
                    canvas.Children.Add(tb);
                }

                if (beam.BeamAngleConfidence < .5)
                    ellipse.Fill = new SolidColorBrush(Colors.Blue);
                else
                    ellipse.Fill = new SolidColorBrush(Colors.White);

                ellipse.Width = 40 * beam.BeamAngleConfidence;
                ellipse.Height = 40 * beam.BeamAngleConfidence;

                var x = (angle + 1.0) / 2.0 * canvas.Width;
                tb.Text = x.ToString();

                Canvas.SetLeft(ellipse, x + ellipse.Width / 2);
                Canvas.SetTop(ellipse, canvas.ActualHeight / 2);

                Canvas.SetLeft(tb, colorFrame.FrameDescription.Width / 2);
                Canvas.SetTop(tb, canvas.ActualHeight / 3);
                
            }

            bool processed = false;

            FrameDescription colorFrameDescription = colorFrame.FrameDescription;

            if((colorFrameDescription.Width == this.bitmap.PixelWidth) && (colorFrameDescription.Height == this.bitmap.PixelHeight))
            {
                if(colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                {
                    colorFrame.CopyRawFrameDataToBuffer(this.bitmap.PixelBuffer);
                }
                else
                {
                    colorFrame.CopyConvertedFrameDataToBuffer(this.bitmap.PixelBuffer, ColorImageFormat.Bgra);
                }

                processed = true;
            }

            if(processed)
            {
                this.bitmap.Invalidate();
                FrameDisplayImage.Source = this.bitmap;
            }

        }

        private void DrawShit(BodyFrame bf)
        {
            if (bf == null)
                return;
            Body[] bodies = new Body[this.sensor.BodyFrameSource.BodyCount];

            bf.GetAndRefreshBodyData(bodies);
            var color = colors[(thing++ / 20) % colors.Length];

            for(int x = 0; x < bodies.Length; x++)
            {
                var body = bodies[x];
                //var color = Colors.LightBlue;
                foreach(var joint in body.Joints.Keys)
                {
                    var hand = body.Joints[joint];

                    var point = this.coordinateMapper.MapCameraPointToDepthSpace(hand.Position);

                    if(this.canvas.Children.Count >= maxChildren)
                    {
                        var ellipse = (Ellipse)this.canvas.Children[numEllipse++ % maxChildren];
                        // compare to previous joint position, calculate speed, if below threshold make it transparent
                        var prevX = Canvas.GetLeft(ellipse);
                        var prevY = Canvas.GetLeft(ellipse);

                        if (Math.Abs(prevX - point.X) + Math.Abs(prevY - point.Y) > 0)
                            ellipse.Fill = new SolidColorBrush(Colors.White);
                        else
                            ellipse.Fill = new SolidColorBrush(Colors.Transparent);

                        Canvas.SetLeft(ellipse, point.X);
                        Canvas.SetTop(ellipse, point.Y);
                    }
                    else
                    {
                        var ellipse = new Ellipse()
                        {
                            Visibility = Visibility.Visible,
                            Height = 3.0,
                            Width = 3.0,
                            Fill = new SolidColorBrush(Colors.White) 
                        };

                        this.canvas.Children.Add(ellipse);
                        
                        Canvas.SetLeft(ellipse, point.X);
                        Canvas.SetTop(ellipse, point.Y);
                    }
                }
            }
        }

        private void ShowBodyJoints(BodyFrame bodyFrame)
        {
            Body[] bodies = new Body[this.sensor.BodyFrameSource.BodyCount];
            bool dataReceived = false;
            if (bodyFrame == null)
                return;

            bodyFrame.GetAndRefreshBodyData(bodies);
            this.bodiesManager.UpdateBodiesAndEdges(bodies);
        }

        private void ShowDepthFrame(DepthFrame df)
        {
            bool processed = false;
            ushort minDepth = 0;
            ushort maxDepth = 0;

            if (df == null)
                return;

            FrameDescription dfDescription = df.FrameDescription;

            if(((dfDescription.Width * dfDescription.Height) == this.depthFrameData.Length) &&
                (dfDescription.Width == this.bitmap.PixelWidth) && (dfDescription.Height == this.bitmap.PixelHeight))
            {
                df.CopyFrameDataToArray(this.depthFrameData);

                minDepth = df.DepthMinReliableDistance;
                maxDepth = df.DepthMaxReliableDistance;

                processed = true;
            }

            if (processed)
            {
                ConvertDepthDataToPixels(minDepth, maxDepth);
                RenderPixelArray(this.depthPixels);
            }
        }

        private void ShowInfraredFrame(InfraredFrame infraredFrame)
        {
            bool processed = false;
            if (infraredFrame == null)
                return;

            FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;

            if(((infraredFrameDescription.Width * infraredFrameDescription.Height) == this.ifFrameData.Length) && (infraredFrameDescription.Width == this.bitmap.PixelWidth) && (infraredFrameDescription.Height == this.bitmap.PixelHeight))
            {
                infraredFrame.CopyFrameDataToArray(this.ifFrameData);
                processed = true;
            }

            if(processed)
            {
                this.ConvertInfraredDataToPixels();
                this.RenderPixelArray(this.irPixels);
            }
        }

        private void ShowColorFrame(ColorFrame colorFrame)
        {
            bool processed = false;
            if(colorFrame == null)
            {
                return;
            }

            FrameDescription colorFrameDescription = colorFrame.FrameDescription;

            if((colorFrameDescription.Width == this.bitmap.PixelWidth) && (colorFrameDescription.Height == this.bitmap.PixelHeight))
            {
                if(colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                {
                    colorFrame.CopyRawFrameDataToBuffer(this.bitmap.PixelBuffer);
                }
                else
                {
                    colorFrame.CopyConvertedFrameDataToBuffer(this.bitmap.PixelBuffer, ColorImageFormat.Bgra);
                }

                processed = true;
            }

            if(processed)
            {
                this.bitmap.Invalidate();
                FrameDisplayImage.Source = this.bitmap;
            }
        }

        private void SoundButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Sound);
        }

        private void FacesButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Faces);
        }

        private void InfraredButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Infrared);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Color);
        }

        private void DepthButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Depth);
        }

        private void BodyMask_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.BodyMask);
        }

        private void BodyJoints_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.BodyJoints);
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Draw);
        }



        private void ConvertInfraredDataToPixels()
        {
            int colorPixelIndex = 0;
            double r = rand.NextDouble();
            double g = rand.NextDouble();
            double b = rand.NextDouble();
            float c = 255;
            // var t = DateTime.Now.Second / 60.0;

            for (int i = 0; i < this.ifFrameData.Length; i++)
            {
                float intensityRatio = (float)this.ifFrameData[i] / InfraredSourceValueMaximum;

                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                //intensityRatio = Math.Max(InfraredOutputValueMaximum, intensityRatio);

                double intensity;
                if (intensityRatio > 0.05)
                {
                    intensity = intensityRatio * 255.0f;
                    // intensity = 254.0f; 
                }
                else
                {
                    intensity = 0;
                }


                this.irPixels[colorPixelIndex++] = (byte)(intensity % 255.0f);
                this.irPixels[colorPixelIndex++] = (byte)(intensity % 255.0f);
                this.irPixels[colorPixelIndex++] = (byte)(intensity % 255.0f);
                this.irPixels[colorPixelIndex++] = (byte)(1 * 255.0f);
            }
        }

        private void ConvertDepthDataToPixels(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;

            int mapDepthToByte = maxDepth / 256;

            for(int i = 0; i < this.depthFrameData.Length; ++i)
            {
                ushort depth = this.depthFrameData[i];

                int intensity = (depth >= 0 && depth <= maxDepth ? (depth / mapDepthToByte) : 0);

                /*if(depth > 100)
                {
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                    this.depthPixels[colorPixelIndex++] = 255;
                    continue;
                }*/

                var bandWidth = 50;

                int zone = depth / bandWidth;


                if (depth > 3000 || depth <= minDepth)
                {
                    this.depthPixels[colorPixelIndex++] = (byte)(0);
                    this.depthPixels[colorPixelIndex++] = (byte)(0);
                    this.depthPixels[colorPixelIndex++] = (byte)(0);
                    this.depthPixels[colorPixelIndex++] = (byte)(255);
                    continue;
                }
 
                switch(zone % 4)
                {
                    case 0:
                        this.depthPixels[colorPixelIndex++] = (byte)(10);
                        this.depthPixels[colorPixelIndex++] = (byte)(200);
                        this.depthPixels[colorPixelIndex++] = (byte)(40);
                        break;
                    case 1:
                        this.depthPixels[colorPixelIndex++] = (byte)(200);
                        this.depthPixels[colorPixelIndex++] = (byte)(200);
                        this.depthPixels[colorPixelIndex++] = (byte)(40);
                        break;
                    case 2:
                        this.depthPixels[colorPixelIndex++] = (byte)(0);
                        this.depthPixels[colorPixelIndex++] = (byte)(10);
                        this.depthPixels[colorPixelIndex++] = (byte)(240);
                        break;
                    case 3:
                        this.depthPixels[colorPixelIndex++] = (byte)(10);
                        this.depthPixels[colorPixelIndex++] = (byte)(200);
                        this.depthPixels[colorPixelIndex++] = (byte)(200);
                        break;
                }

                /*{ 
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                    this.depthPixels[colorPixelIndex++] = (byte)(intensity);
                }*/
                    this.depthPixels[colorPixelIndex++] = 255;
            }
        }

        private void RenderPixelArray(byte[] pixels)
        {
            pixels.CopyTo(this.bitmap.PixelBuffer);
            this.bitmap.Invalidate();
            FrameDisplayImage.Source = this.bitmap;
        }

        [Guid("905a0fef-bc53-11df-8c49-001e4fc686da"), 
            InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IBufferByteAccess
        {
            unsafe void Buffer(out byte* pByte);
        }

    }
}

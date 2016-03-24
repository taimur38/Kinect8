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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Kinect8
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int BytesPerPixel = 4;
        private WriteableBitmap bitmap;

        private InfraredFrameReader ifReader;
        private ushort[] ifFrameData;
        private byte[] irPixels = null;


        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
        private const float InfraredOutputValueMinimum = 0.01f;
        private const float InfraredOutputValueMaximum = 1.0f;

        private const float InfraredSceneValueAverage = 0.08f;
        private const float InfraredSceneStandardDeviations = 3.0f;

        KinectSensor sensor;

        public MainPage()
        {
            this.sensor = KinectSensor.GetDefault();

            FrameDescription irFrameDescription = this.sensor.InfraredFrameSource.FrameDescription;

            this.ifReader = this.sensor.InfraredFrameSource.OpenReader();

            this.ifReader.FrameArrived += this.Reader_InfraredFrameArrived;

            this.ifFrameData = new ushort[irFrameDescription.Width * irFrameDescription.Height];
            this.irPixels = new byte[irFrameDescription.Width * irFrameDescription.Height * BytesPerPixel];

            this.bitmap = new WriteableBitmap(irFrameDescription.Width, irFrameDescription.Height);

            this.sensor.Open();
            this.InitializeComponent();
        }

        private void Reader_InfraredFrameArrived(InfraredFrameReader sender, InfraredFrameArrivedEventArgs e)
        {
            bool irFrameProcessed = false;

            using (var irFrame = e.FrameReference.AcquireFrame())
            {
                if (irFrame == null)
                    return;

                var irFrameDescription = irFrame.FrameDescription;

                if(((irFrameDescription.Width * irFrameDescription.Height) == this.ifFrameData.Length) && (irFrameDescription.Width == this.bitmap.PixelWidth) && (irFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    irFrame.CopyFrameDataToArray(this.ifFrameData);

                    irFrameProcessed = true;
                }
                
            }

            if (irFrameProcessed)
            {
                ConvertInfraredDataToPixels();
                RenderPixelArray(this.irPixels);
            }
        }

        private void ConvertInfraredDataToPixels()
        {
            int colorPixelIndex = 0;
            for (int i = 0; i < this.ifFrameData.Length; i++)
            {
                float intensityRatio = (float)this.ifFrameData[i] / InfraredSourceValueMaximum;

                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                intensityRatio = Math.Max(InfraredOutputValueMaximum, intensityRatio);

                byte intensity = (byte)(intensityRatio * 255.0f);
                this.irPixels[colorPixelIndex++] = intensity;
                this.irPixels[colorPixelIndex++] = intensity;
                this.irPixels[colorPixelIndex++] = intensity;
                this.irPixels[colorPixelIndex++] = intensity;
            }
        }

        private void RenderPixelArray(byte[] pixels)
        {
            pixels.CopyTo(this.bitmap.PixelBuffer);
            this.bitmap.Invalidate();
            FrameDisplayImage.Source = this.bitmap;
        }
    }
}

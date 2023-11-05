using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace HistogramsAndBinarization
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int globalWidth, globalHeight;
        byte[] globalPixels;
        int[] globalPixelsAsGray;
        WriteableBitmap modifiedBitmap;
        List<Color> globalPixelsAsColors = new List<Color>();
        int bytesPerPixel, stride;

        public MainWindow()
        {
            InitializeComponent();
            SizeChanged += MainWindow_SizeChanged;

        }
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            histogramCanvas.Width = e.NewSize.Width;
            histogramCanvas.Height = e.NewSize.Height;
        }

        private void readJpeg()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JPEG files (*.jpeg, *.jpg)|*.jpeg; *.jpg";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(openFileDialog.FileName);
                    bitmapImage.EndInit();

                    globalWidth = bitmapImage.PixelWidth;
                    globalHeight = bitmapImage.PixelHeight;
                    bytesPerPixel = (bitmapImage.Format.BitsPerPixel + 7) / 8;
                    stride = globalWidth * bytesPerPixel;

                    byte[] pixelData = new byte[globalHeight * stride];
                    bitmapImage.CopyPixels(pixelData, stride, 0);
                    globalPixels = pixelData;

                    setGlobalColorsFromPixelsArray();
                    modifiedBitmap = new WriteableBitmap(bitmapImage);
                    displayedImage.Source = bitmapImage;

                    setGlobalGrayPixels();
                    drawHistogram(calculateGrayPixels(), false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while processing the image: " + ex.Message);
                }
            }
        }

        private int[] calculateGrayPixels()
        {
            int[] grayscaleBytes = new int[256];
            int average;


            for (int i = 0; i < globalPixelsAsColors.Count; i++)
            {
                average = ((globalPixelsAsColors[i].R + globalPixelsAsColors[i].B + globalPixelsAsColors[i].G) / 3);
                grayscaleBytes[average]++;
            }
            return grayscaleBytes;
        }

        private void setGlobalGrayPixels()
        {
            int average;

            globalPixelsAsGray = new int[globalPixels.Length];
            for (int i = 0; i < globalPixels.Length; i += 4)
            {
                average = (globalPixels[i] + globalPixels[i + 1] + globalPixels[i + 2]) / 3;
                globalPixelsAsGray[i] = globalPixelsAsGray[i + 1] = globalPixelsAsGray[i + 2] = average;
                globalPixelsAsGray[i + 3] = 255;
            }
        }

        private int[] returnHistogramArrayFromPixelsArray(byte[] pixels)
        {
            int[] grayscaleBytes = new int[256];
            int average;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                average = ((pixels[i] + pixels[i + 2] + pixels[i + 1]) / 3); //skip alpha channnel
                grayscaleBytes[average]++;
            }
            return grayscaleBytes;
        }

        private void stretchHistogram(object sender, RoutedEventArgs e)
        {

            int min, max;
            int[] grayscalePixels = new int[globalPixelsAsColors.Count];
            int[] histogramPixels = new int[256];

            for (int i = 0; i < globalPixelsAsColors.Count; i++)
            {
                grayscalePixels[i] = ((globalPixelsAsColors[i].R + globalPixelsAsColors[i].B + globalPixelsAsColors[i].G) / 3);
            }

            min = grayscalePixels.Min();
            max = grayscalePixels.Max();

            for (int i = 0; i < grayscalePixels.Length; i++)
            {
                grayscalePixels[i] = (int)((double)(grayscalePixels[i] - min) / (max - min) * 255);
                histogramPixels[grayscalePixels[i]]++;
            }

            drawImageFromBytes(convertPixelsArrayToDrawableArray(grayscalePixels));
            drawHistogram(histogramPixels, false);

        }
        private void equalizeHistogram(object sender, RoutedEventArgs e)
        {
            int[] grayscalePixels = calculateGrayPixels(); //it contains number of partciular gray pixels
            int numberOfPixels = globalPixels.Length;

            int[] cdf = new int[256];
            cdf[0] = grayscalePixels[0];
            for (int i = 1; i < 256; i++)
            {
                cdf[i] = cdf[i - 1] + grayscalePixels[i];
            }

            byte[] equalizedPixels = new byte[numberOfPixels];
            for (int i = 0; i < numberOfPixels; i += 4)
            {
                int gray = (globalPixels[i] + globalPixels[i + 1] + globalPixels[i + 2]) / 3;
                int newGray = (int)(255.0 * (cdf[gray] - cdf.Min()) / (cdf.Max() - cdf.Min()));
                equalizedPixels[i] = equalizedPixels[i + 1] = equalizedPixels[i + 2] = (byte)newGray;
                equalizedPixels[i + 3] = 255;
            }
            drawImageFromBytes(equalizedPixels);
            setGlobalColorsFromPixelsArray(equalizedPixels);
            drawHistogram(calculateGrayPixels(), false);

        }
        private byte[] convertPixelsArrayToDrawableArray(int[] array)
        {
            byte[] bytes = new byte[globalPixels.Length];
            int index = 0;
            for (int i = 0; i < globalPixels.Length; i += 4)
            {
                bytes[i] = (byte)array[index];
                bytes[i + 1] = (byte)array[index];
                bytes[i + 2] = (byte)array[index];
                bytes[i + 3] = 255;
                index++;
            }
            return bytes;
        }

        private void drawHistogram(int[] histogramData, bool isBinarizated)
        {
            int maxCount = histogramData.Max();
            double canvasWidth = histogramCanvas.ActualWidth;
            double canvasHeight = histogramCanvas.ActualHeight;

            histogramCanvas.Children.Clear();

            if (!isBinarizated)
            {
                for (int i = 0; i < 256; i++)
                {
                    double normalizedValue = (double)histogramData[i] / maxCount;
                    Rectangle rect = new Rectangle
                    {
                        Width = canvasWidth / 256,
                        Height = normalizedValue * canvasHeight,
                        Fill = Brushes.Black
                    };

                    Canvas.SetLeft(rect, i * (canvasWidth / 256));
                    Canvas.SetTop(rect, canvasHeight - (normalizedValue * canvasHeight));
                    histogramCanvas.Children.Add(rect);
                }
            }
            else
            {
                int black = histogramData[0];
                int white = histogramData[255];
                double normalizedValue1 = (double)black / maxCount;
                double normalizedValue2 = (double)white / maxCount;

                Rectangle rect1 = new Rectangle
                {
                    Width = canvasWidth / 5,
                    Height = normalizedValue1 * canvasHeight,
                    Fill = Brushes.Black
                };
                Rectangle rect2 = new Rectangle
                {
                    Width = canvasWidth / 5,
                    Height = normalizedValue2 * canvasHeight,
                    Fill = Brushes.Black
                };

                Canvas.SetLeft(rect1, (canvasWidth / 4));
                Canvas.SetTop(rect1, canvasHeight - (normalizedValue1 * canvasHeight));
                histogramCanvas.Children.Add(rect1);
                Canvas.SetLeft(rect2, (canvasWidth / 2));
                Canvas.SetTop(rect2, canvasHeight - (normalizedValue2 * canvasHeight));
                histogramCanvas.Children.Add(rect2);
            }

        }

        private void readJpeg(object sender, RoutedEventArgs e)
        {
            readJpeg();
        }

        private void drawImageFromBytes(byte[] bytes)
        {
            modifiedBitmap.WritePixels(new Int32Rect(0, 0, globalWidth, globalHeight), bytes, stride, 0);

            displayedImage.Source = modifiedBitmap;
        }



        private void setGlobalColorsFromPixelsArray()
        {
            globalPixelsAsColors.Clear();
            for (int i = 0; i + 3 < globalPixels.Length; i += 4)
            {
                globalPixelsAsColors.Add(Color.FromArgb(255, globalPixels[i + 2], globalPixels[i + 1], globalPixels[i]));
            }
        }

        private void binarizationFromUserInput(object sender, RoutedEventArgs e)
        {
            int threshold = Int32.Parse(userThreshold.Text);
            byte[] binarizedPixels = makeBinarizationFromThreshold(threshold);

            drawImageFromBytes(binarizedPixels);
            drawHistogram(returnHistogramArrayFromPixelsArray(binarizedPixels), true);
        }

        private byte[] makeBinarizationFromThreshold(int threshold)
        {
            byte[] binarized = new byte[globalPixels.Length];
            for (int i = 0; i < globalPixels.Length; i += 4)
            {
                binarized[i] = (byte)(globalPixelsAsGray[i] < threshold ? 0 : 255);
                binarized[i + 1] = (byte)(globalPixelsAsGray[i + 1] < threshold ? 0 : 255);
                binarized[i + 2] = (byte)(globalPixelsAsGray[i + 2] < threshold ? 0 : 255);
                binarized[i + 3] = (byte)255;
            }

            return binarized;
        }

        private void binarizationFromBlackSelection(object sender, RoutedEventArgs e)
        {
            int[] histogramArray = calculateGrayPixels();
            int numberOfPixels = globalPixelsAsColors.Count;
            int seekedThreshold = 0, percentage = 40;
            bool foundThreshold = false;
            int cumulativeNumber = 0, index = 0;

            while (!foundThreshold)
            {
                cumulativeNumber += histogramArray[index];  //sum particular qty
                double currentPercentage = ((double)cumulativeNumber / (double)numberOfPixels) * 100;
                if (currentPercentage < percentage)   //if qty is lower than 40% -> continue
                    index++;
                else
                    foundThreshold = true;

                seekedThreshold = index;
            }
            byte[] binarized = makeBinarizationFromThreshold(seekedThreshold);

            drawImageFromBytes(binarized);
            drawHistogram(returnHistogramArrayFromPixelsArray(binarized), true);
        }


        private void binarizationFromIterativeMethod(object sender, RoutedEventArgs e)
        {
            int threshold = 128;
            int[] histogramArray = calculateGrayPixels();
            while (true)
            {
                int sumBelow = 0;
                int countBelow = 0;
                int sumAbove = 0;
                int countAbove = 0;

                for (int i = 0; i < histogramArray.Length; i++)
                {
                    if (i < threshold)
                    {
                        sumBelow += i * histogramArray[i];
                        countBelow += histogramArray[i];
                    }
                    else
                    {
                        sumAbove += i * histogramArray[i];
                        countAbove += histogramArray[i];
                    }
                }

                int newThreshold = (sumBelow / countBelow + sumAbove / countAbove) / 2;
                if (newThreshold == threshold)
                {
                    break;
                }

                threshold = newThreshold;
            }

            byte[] binarizedPixels = makeBinarizationFromThreshold(threshold);

            drawImageFromBytes(binarizedPixels);
            drawHistogram(returnHistogramArrayFromPixelsArray(binarizedPixels), true);
        }

        private void setGlobalColorsFromPixelsArray(byte[] array)
        {
            globalPixelsAsColors.Clear();
            for (int i = 0; i + 3 < array.Length; i += 4)
            {
                globalPixelsAsColors.Add(Color.FromArgb(255, array[i + 2], array[i + 1], array[i]));
            }
        }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    public static class ImageCompressor
    {
        public static BitmapSource LoadBitmapFromFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            if (frame.CanFreeze)
            {
                frame.Freeze();
            }
            return frame;
        }

        public static byte[] CompressByQuality(BitmapSource source, string format, int percent)
        {
            format = format.ToLowerInvariant().TrimStart('.');
            if (format == "jpg" || format == "jpeg")
            {
                var prepared = PrepareForJpeg(source);
                int quality = percent >= 100 ? 100 : Math.Clamp(percent, 5, 98);
                return EncodeJpeg(prepared, quality);
            }
            else
            {
                if (percent >= 100)
                {
                    return EncodePng(source);
                }

                // Map percent (10 - 99) to quantization step (2 - 28)
                int step = Math.Clamp(2 + (int)Math.Round((99 - percent) * 0.29), 2, 28);
                return EncodePngWithStep(source, step);
            }
        }

        public static byte[] CompressToTargetSize(BitmapSource source, string format, long targetBytes, long originalBytes)
        {
            format = format.ToLowerInvariant().TrimStart('.');
            if (targetBytes <= 0)
            {
                targetBytes = originalBytes;
            }

            if (format == "jpg" || format == "jpeg")
            {
                var prepared = PrepareForJpeg(source);

                // Quick check at 95 quality
                byte[] bestData = EncodeJpeg(prepared, 95);
                if (bestData.Length <= targetBytes)
                {
                    // Can we do 98 or 100?
                    byte[] maxData = EncodeJpeg(prepared, 100);
                    if (maxData.Length <= targetBytes)
                    {
                        return maxData;
                    }
                    return bestData;
                }

                // Binary search quality between 5 and 95
                int low = 5;
                int high = 95;
                byte[]? closestUnderTarget = null;
                byte[] lowestResult = bestData;

                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    byte[] candidate = EncodeJpeg(prepared, mid);
                    lowestResult = candidate;

                    if (candidate.Length <= targetBytes)
                    {
                        closestUnderTarget = candidate;
                        low = mid + 1; // Try higher quality
                    }
                    else
                    {
                        high = mid - 1; // Must reduce quality
                    }
                }

                return closestUnderTarget ?? lowestResult;
            }
            else
            {
                // PNG binary search via quantization steps:
                // step 2 = highest quality / largest size
                // step 28 = highest compression / smallest size
                byte[] uncompressed = EncodePng(source);
                if (uncompressed.Length <= targetBytes)
                {
                    return uncompressed;
                }

                int stepLow = 2;
                int stepHigh = 28;
                byte[]? closestUnderTarget = null;
                byte[] lowestResult = uncompressed;

                while (stepLow <= stepHigh)
                {
                    int stepMid = (stepLow + stepHigh) / 2;
                    byte[] candidate = EncodePngWithStep(source, stepMid);
                    lowestResult = candidate;

                    if (candidate.Length <= targetBytes)
                    {
                        closestUnderTarget = candidate;
                        stepHigh = stepMid - 1; // Try smaller step (higher quality)
                    }
                    else
                    {
                        stepLow = stepMid + 1; // Needs more quantization
                    }
                }

                return closestUnderTarget ?? lowestResult;
            }
        }

        public static byte[] CompressToPercentageOfSize(
            BitmapSource source,
            string format,
            double targetPercent,
            long originalBytes)
        {
            if (originalBytes <= 0)
            {
                originalBytes = 1024 * 500;
            }
            long targetBytes = (long)Math.Round(originalBytes * (Math.Clamp(targetPercent, 1.0, 100.0) / 100.0));
            return CompressToTargetSize(source, format, targetBytes, originalBytes);
        }

        public static byte[] EncodeJpeg(BitmapSource source, int quality)
        {
            using var ms = new MemoryStream();
            var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 1, 100) };
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return ms.ToArray();
        }

        public static byte[] EncodePng(BitmapSource source)
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return ms.ToArray();
        }

        public static byte[] EncodePngWithStep(BitmapSource source, int step)
        {
            if (step <= 1)
            {
                return EncodePng(source);
            }

            BitmapSource bgraSource = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Bgr32)
            {
                bgraSource = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            int width = bgraSource.PixelWidth;
            int height = bgraSource.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bgraSource.CopyPixels(pixels, stride, 0);

            // Precompute lookup table for quantization step
            byte[] lut = new byte[256];
            for (int c = 0; c < 256; c++)
            {
                int val = (int)Math.Round((double)c / step) * step;
                lut[c] = (byte)Math.Clamp(val, 0, 255);
            }

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = lut[pixels[i]];         // B
                pixels[i + 1] = lut[pixels[i + 1]]; // G
                pixels[i + 2] = lut[pixels[i + 2]]; // R
            }

            var resultSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            return EncodePng(resultSource);
        }

        public static BitmapSource PrepareForJpeg(BitmapSource source)
        {
            // If format supports transparency, composite over white background to avoid black borders
            bool hasAlpha = source.Format == PixelFormats.Bgra32 ||
                            source.Format == PixelFormats.Pbgra32 ||
                            source.Format == PixelFormats.Prgba64 ||
                            source.Format == PixelFormats.Rgba64;

            if (!hasAlpha)
            {
                return source;
            }

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            }
            var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            if (rtb.CanFreeze)
            {
                rtb.Freeze();
            }
            return rtb;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }
            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}

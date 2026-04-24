using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            OcrOutput output = RunAsync(options).GetAwaiter().GetResult();

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OcrOutput));
            using (Stream target = options.OutputPath == null
                ? Console.OpenStandardOutput()
                : File.Create(options.OutputPath))
            {
                serializer.WriteObject(target, output);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async System.Threading.Tasks.Task<OcrOutput> RunAsync(Options options)
    {
        string fullInputPath = Path.GetFullPath(options.InputPath);

        Bitmap source = (Bitmap)Image.FromFile(fullInputPath);
        Bitmap prepared = null;
        string tempPath = Path.Combine(Path.GetTempPath(), "ocr-" + Guid.NewGuid().ToString("N") + ".png");

        try
        {
            prepared = PrepareBitmap(source, options);
            prepared.Save(tempPath, ImageFormat.Png);

            StorageFile file = await StorageFile.GetFileFromPathAsync(tempPath);
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null)
                {
                    throw new InvalidOperationException("Windows OCR engine could not be created.");
                }

                OcrResult result = await engine.RecognizeAsync(bitmap);

                List<LineOutput> lines = new List<LineOutput>();
                foreach (OcrLine line in result.Lines)
                {
                    List<WordOutput> words = new List<WordOutput>();
                    foreach (OcrWord word in line.Words)
                    {
                        words.Add(new WordOutput
                        {
                            Text = word.Text,
                            Left = (int)Math.Round(word.BoundingRect.X),
                            Top = (int)Math.Round(word.BoundingRect.Y),
                            Width = (int)Math.Round(word.BoundingRect.Width),
                            Height = (int)Math.Round(word.BoundingRect.Height),
                        });
                    }

                    lines.Add(new LineOutput
                    {
                        Text = line.Text,
                        Left = words.Count > 0 ? words[0].Left : 0,
                        Top = words.Count > 0 ? words[0].Top : 0,
                        Words = words,
                    });
                }

                return new OcrOutput
                {
                    InputPath = fullInputPath,
                    Crop = new CropOutput
                    {
                        X = options.X,
                        Y = options.Y,
                        Width = prepared.Width,
                        Height = prepared.Height,
                        Scale = options.Scale,
                        Threshold = options.Threshold,
                    },
                    Text = result.Text,
                    Lines = lines,
                };
            }
        }
        finally
        {
            if (prepared != null)
            {
                prepared.Dispose();
            }

            source.Dispose();

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Bitmap PrepareBitmap(Bitmap source, Options options)
    {
        int cropWidth = options.Width > 0 ? options.Width : source.Width - options.X;
        int cropHeight = options.Height > 0 ? options.Height : source.Height - options.Y;

        cropWidth = Math.Min(cropWidth, source.Width - options.X);
        cropHeight = Math.Min(cropHeight, source.Height - options.Y);

        Rectangle sourceRectangle = new Rectangle(options.X, options.Y, cropWidth, cropHeight);
        int targetWidth = Math.Max(1, (int)Math.Round(cropWidth * options.Scale));
        int targetHeight = Math.Max(1, (int)Math.Round(cropHeight * options.Scale));

        Bitmap target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);

        using (Graphics graphics = Graphics.FromImage(target))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight), sourceRectangle, GraphicsUnit.Pixel);
        }

        if (options.Threshold >= 0)
        {
            for (int x = 0; x < target.Width; x++)
            {
                for (int y = 0; y < target.Height; y++)
                {
                    Color pixel = target.GetPixel(x, y);
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    target.SetPixel(x, y, brightness >= options.Threshold ? Color.White : Color.Black);
                }
            }
        }

        return target;
    }

    private sealed class Options
    {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Scale { get; set; }
        public int Threshold { get; set; }

        public static Options Parse(string[] args)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Arguments must be provided as --name value pairs.");
                }

                values[args[i]] = args[i + 1];
            }

            string inputPath;
            if (!values.TryGetValue("--input", out inputPath))
            {
                throw new ArgumentException("Missing required argument --input.");
            }

            string outputPath;
            return new Options
            {
                InputPath = inputPath,
                OutputPath = values.TryGetValue("--output", out outputPath) ? outputPath : null,
                X = GetInt(values, "--x"),
                Y = GetInt(values, "--y"),
                Width = GetInt(values, "--width"),
                Height = GetInt(values, "--height"),
                Scale = GetDouble(values, "--scale", 1.0),
                Threshold = GetInt(values, "--threshold", -1),
            };
        }

        private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue = 0)
        {
            string value;
            return values.TryGetValue(key, out value) ? int.Parse(value) : defaultValue;
        }

        private static double GetDouble(IReadOnlyDictionary<string, string> values, string key, double defaultValue)
        {
            string value;
            return values.TryGetValue(key, out value) ? double.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : defaultValue;
        }
    }
}

[DataContract]
internal sealed class OcrOutput
{
    [DataMember(Name = "inputPath", Order = 1)]
    public string InputPath { get; set; } = string.Empty;

    [DataMember(Name = "crop", Order = 2)]
    public CropOutput Crop { get; set; } = new CropOutput();

    [DataMember(Name = "text", Order = 3)]
    public string Text { get; set; } = string.Empty;

    [DataMember(Name = "lines", Order = 4)]
    public List<LineOutput> Lines { get; set; } = new List<LineOutput>();
}

[DataContract]
internal sealed class CropOutput
{
    [DataMember(Name = "x", Order = 1)]
    public int X { get; set; }

    [DataMember(Name = "y", Order = 2)]
    public int Y { get; set; }

    [DataMember(Name = "width", Order = 3)]
    public int Width { get; set; }

    [DataMember(Name = "height", Order = 4)]
    public int Height { get; set; }

    [DataMember(Name = "scale", Order = 5)]
    public double Scale { get; set; }

    [DataMember(Name = "threshold", Order = 6)]
    public int Threshold { get; set; }
}

[DataContract]
internal sealed class LineOutput
{
    [DataMember(Name = "text", Order = 1)]
    public string Text { get; set; } = string.Empty;

    [DataMember(Name = "left", Order = 2)]
    public int Left { get; set; }

    [DataMember(Name = "top", Order = 3)]
    public int Top { get; set; }

    [DataMember(Name = "words", Order = 4)]
    public List<WordOutput> Words { get; set; } = new List<WordOutput>();
}

[DataContract]
internal sealed class WordOutput
{
    [DataMember(Name = "text", Order = 1)]
    public string Text { get; set; } = string.Empty;

    [DataMember(Name = "left", Order = 2)]
    public int Left { get; set; }

    [DataMember(Name = "top", Order = 3)]
    public int Top { get; set; }

    [DataMember(Name = "width", Order = 4)]
    public int Width { get; set; }

    [DataMember(Name = "height", Order = 5)]
    public int Height { get; set; }
}

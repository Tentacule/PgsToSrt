using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tesseract.Internal;
using Tesseract.Interop;

namespace Tesseract
{
    public sealed class Page : DisposableBase
    {
        private static readonly TraceSource trace = new TraceSource("Tesseract");

        private bool runRecognitionPhase;
        private Rect _regionOfInterest;

        public TesseractEngine Engine { get; private set; }

        /// <summary>
        /// Gets the <see cref="Pix"/> that is being ocr'd.
        /// </summary>
        public Pix Image { get; }

        /// <summary>
        /// Gets the name of the image being ocr'd.
        /// </summary>
        /// <remarks>
        /// This is also used for some of the more advanced functionality such as identifying the associated UZN file if present.
        /// </remarks>
        public string ImageName { get; private set; }

        /// <summary>
        /// Gets the page segmentation mode used to OCR the specified image.
        /// </summary>
        public PageSegMode PageSegmentMode { get; private set; }

        internal Page(TesseractEngine engine, Pix image, string imageName, Rect regionOfInterest, PageSegMode pageSegmentMode)
        {
            Engine = engine;
            Image = image;
            ImageName = imageName;
            RegionOfInterest = regionOfInterest;
            PageSegmentMode = pageSegmentMode;
        }

        /// <summary>
        /// The current region of interest being parsed.
        /// </summary>
        public Rect RegionOfInterest
        {
            get
            {
                return _regionOfInterest;
            }
            set
            {
                if (value.X1 < 0 || value.Y1 < 0 || value.X2 > Image.Width || value.Y2 > Image.Height)
                    throw new ArgumentException("The region of interest to be processed must be within the image bounds.", "value");

                if (_regionOfInterest != value)
                {
                    _regionOfInterest = value;

                    // update region of interest in image
                    TessApi.Native.BaseApiSetRectangle(Engine.Handle, _regionOfInterest.X1, _regionOfInterest.Y1, _regionOfInterest.Width, _regionOfInterest.Height);

                    // request rerun of recognition on the next call that requires recognition
                    runRecognitionPhase = false;
                }
            }
        }

        /// <summary>
        /// Gets the thresholded image that was OCR'd.
        /// </summary>
        /// <returns></returns>
        public Pix GetThresholdedImage()
        {
            Recognize();

            var pixHandle = TessApi.Native.BaseAPIGetThresholdedImage(Engine.Handle);
            if (pixHandle == IntPtr.Zero)
            {
                throw new TesseractException("Failed to get thresholded image.");
            }

            return Pix.Create(pixHandle);
        }

        /// <summary>
        /// Gets the page's content as plain text.
        /// </summary>
        /// <returns></returns>
        public string GetText()
        {
            Recognize();
            return TessApi.BaseAPIGetUTF8Text(Engine.Handle);
        }

        internal void Recognize()
        {
            Guard.Verify(PageSegmentMode != PageSegMode.OsdOnly, "Cannot OCR image when using OSD only page segmentation, please use DetectBestOrientation instead.");
            if (!runRecognitionPhase)
            {
                if (TessApi.Native.BaseApiRecognize(Engine.Handle, new HandleRef(this, IntPtr.Zero)) != 0)
                {
                    throw new InvalidOperationException("Recognition of image failed.");
                }

                runRecognitionPhase = true;

                // now write out the thresholded image if required to do so
                bool tesseditWriteImages;
                if (Engine.TryGetBoolVariable("tessedit_write_images", out tesseditWriteImages) && tesseditWriteImages)
                {
                    using (Pix thresholdedImage = GetThresholdedImage())
                    {
                        string filePath = Path.Combine(Environment.CurrentDirectory, "tessinput.tif");
                        try
                        {
                            thresholdedImage.Save(filePath, ImageFormat.TiffG4);
                            trace.TraceEvent(TraceEventType.Information, 2, "Successfully saved the thresholded image to '{0}'", filePath);
                        }
                        catch (Exception error)
                        {
                            trace.TraceEvent(TraceEventType.Error, 2, "Failed to save the thresholded image to '{0}'.\nError: {1}", filePath, error.Message);
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TessApi.Native.BaseAPIClear(Engine.Handle);
            }
        }
    }
}
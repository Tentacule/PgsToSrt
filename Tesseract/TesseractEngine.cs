using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tesseract.Internal;
using Tesseract.Interop;
using Tncl.NativeLoader;

namespace Tesseract
{
    /// <summary>
    /// The tesseract OCR engine.
    /// </summary>
    public class TesseractEngine : DisposableBase
    {
        private readonly NativeLoader loader;
        private HandleRef handle;
        private int processCount = 0;

        /// <summary>
        /// Creates a new instance of <see cref="TesseractEngine"/> with the specified <paramref name="engineMode"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <paramref name="datapath"/> parameter should point to the directory that contains the 'tessdata' folder
        /// for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath should
        /// be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment variable if defined,
        /// effectively ignoring the value of <paramref name="datapath"/> parameter.
        /// </para>
        /// </remarks>
        /// <param name="datapath">The path to the parent directory that contains the 'tessdata' directory, ignored if the <c>TESSDATA_PREFIX</c> environment variable is defined.</param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode"/> value to use when initialising the tesseract engine.</param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode)
            : this(datapath, language, engineMode, new string[0], new Dictionary<string, object>(), false)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="TesseractEngine"/> with the specified <paramref name="engineMode"/> and <paramref name="configFiles"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <paramref name="datapath"/> parameter should point to the directory that contains the 'tessdata' folder
        /// for example if your tesseract language data is installed in <c>C:\Tesseract\tessdata</c> the value of datapath should
        /// be <c>C:\Tesseract</c>. Note that tesseract will use the value of the <c>TESSDATA_PREFIX</c> environment variable if defined,
        /// effectively ignoring the value of <paramref name="datapath"/> parameter.
        /// </para>
        /// </remarks>
        /// <param name="datapath">The path to the parent directory that contains the 'tessdata' directory, ignored if the <c>TESSDATA_PREFIX</c> environment variable is defined.</param>
        /// <param name="language">The language to load, for example 'eng' for English.</param>
        /// <param name="engineMode">The <see cref="EngineMode"/> value to use when initialising the tesseract engine.</param>
        /// <param name="configFiles">
        /// An optional sequence of tesseract configuration files to load, encoded using UTF8 without BOM
        /// with Unix end of line characters you can use an advanced text editor such as Notepad++ to accomplish this.
        /// </param>
        public TesseractEngine(string datapath, string language, EngineMode engineMode, IEnumerable<string> configFiles, IDictionary<string, object> initialOptions, bool setOnlyNonDebugVariables)
        {
            Guard.RequireNotNullOrEmpty("language", language);

            DefaultPageSegMode = PageSegMode.Auto;

            loader = new NativeLoader();
            loader.WindowsOptions.UseSetDllDirectory = true;

            TessApi.Initialize(loader);

            handle = new HandleRef(this, TessApi.Native.BaseApiCreate());

            Initialise(datapath, language, engineMode, configFiles, initialOptions, setOnlyNonDebugVariables);
        }

        internal HandleRef Handle
        {
            get { return handle; }
        }

        /// <summary>
        /// Processes the specific image.
        /// </summary>
        /// <remarks>
        /// You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        public Page Process(Pix image, PageSegMode? pageSegMode = null)
        {
            return Process(image, null, new Rect(0, 0, image.Width, image.Height), pageSegMode);
        }

        /// <summary>
        /// Processes a specified region in the image using the specified page layout analysis mode.
        /// </summary>
        /// <remarks>
        /// You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="region">The image region to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        /// <returns>A result iterator</returns>
        public Page Process(Pix image, Rect region, PageSegMode? pageSegMode = null)
        {
            return Process(image, null, region, pageSegMode);
        }

        /// <summary>
        /// Processes the specific image.
        /// </summary>
        /// <remarks>
        /// You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="inputName">Sets the input file's name, only needed for training or loading a uzn file.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        public Page Process(Pix image, string inputName, PageSegMode? pageSegMode = null)
        {
            return Process(image, inputName, new Rect(0, 0, image.Width, image.Height), pageSegMode);
        }

        /// <summary>
        /// Processes a specified region in the image using the specified page layout analysis mode.
        /// </summary>
        /// <remarks>
        /// You can only have one result iterator open at any one time.
        /// </remarks>
        /// <param name="image">The image to process.</param>
        /// <param name="inputName">Sets the input file's name, only needed for training or loading a uzn file.</param>
        /// <param name="region">The image region to process.</param>
        /// <param name="pageSegMode">The page layout analyasis method to use.</param>
        /// <returns>A result iterator</returns>
        public Page Process(Pix image, string inputName, Rect region, PageSegMode? pageSegMode = null)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (region.X1 < 0 || region.Y1 < 0 || region.X2 > image.Width || region.Y2 > image.Height)
                throw new ArgumentException("The image region to be processed must be within the image bounds.", "region");
            if (processCount > 0) throw new InvalidOperationException("Only one image can be processed at once. Please make sure you dispose of the page once your finished with it.");

            processCount++;

            var actualPageSegmentMode = pageSegMode.HasValue ? pageSegMode.Value : DefaultPageSegMode;
            TessApi.Native.BaseAPISetPageSegMode(handle, actualPageSegmentMode);
            TessApi.Native.BaseApiSetImage(handle, image.Handle);
            if (!String.IsNullOrEmpty(inputName))
            {
                TessApi.Native.BaseApiSetInputName(handle, inputName);
            }
            var page = new Page(this, image, inputName, region, actualPageSegmentMode);
            page.Disposed += OnIteratorDisposed;
            return page;
        }

        protected override void Dispose(bool disposing)
        {
            if (handle.Handle != IntPtr.Zero)
            {
                TessApi.Native.BaseApiDelete(handle);
                handle = new HandleRef(this, IntPtr.Zero);
            }

            loader.FreeAll();
        }

        private void Initialise(string datapath, string language, EngineMode engineMode, IEnumerable<string> configFiles, IDictionary<string, object> initialValues, bool setOnlyNonDebugVariables)
        {
            const string TessDataDirectory = "tessdata";
            Guard.RequireNotNullOrEmpty("language", language);

            // do some minor processing on datapath to fix some common errors (this basically mirrors what tesseract does as of 3.04)
            if (!String.IsNullOrEmpty(datapath))
            {
                // remove any excess whitespace
                datapath = datapath.Trim();

                // remove any trialing '\' or '/' characters
                if (datapath.EndsWith("\\", StringComparison.Ordinal) || datapath.EndsWith("/", StringComparison.Ordinal))
                {
                    datapath = datapath.Substring(0, datapath.Length - 1);
                }
                // remove 'tessdata', if it exists, tesseract will add it when building up the tesseract path
                if (datapath.EndsWith("tessdata", StringComparison.OrdinalIgnoreCase))
                {
                    datapath = datapath.Substring(0, datapath.Length - TessDataDirectory.Length);
                }
            }

            if (TessApi.BaseApiInit(handle, datapath, language, (int)engineMode, configFiles ?? new List<string>(), initialValues ?? new Dictionary<string, object>(), setOnlyNonDebugVariables) != 0)
            {
                // Special case logic to handle cleaning up as init has already released the handle if it fails.
                handle = new HandleRef(this, IntPtr.Zero);
                GC.SuppressFinalize(this);

                throw new TesseractException(ErrorMessage.Format(1, "Failed to initialise tesseract engine."));
            }
        }

        /// <summary>
        /// Ties the specified pix to the lifecycle of a page.
        /// </summary>
        private class PageDisposalHandle
        {
            private readonly Page page;
            private readonly Pix pix;

            public PageDisposalHandle(Page page, Pix pix)
            {
                this.page = page;
                this.pix = pix;
                page.Disposed += OnPageDisposed;
            }

            private void OnPageDisposed(object sender, System.EventArgs e)
            {
                page.Disposed -= OnPageDisposed;
                // dispose the pix when the page is disposed.
                pix.Dispose();
            }
        }

        #region Config

        /// <summary>
        /// Gets or sets default <see cref="PageSegMode" /> mode used by <see cref="Tesseract.TesseractEngine.Process(Pix, Rect, PageSegMode?)" />.
        /// </summary>
        public PageSegMode DefaultPageSegMode
        {
            get;
            set;
        }

        /// <summary>
        /// Attempts to retrieve the value for a boolean variable.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The current value of the variable.</param>
        /// <returns>Returns <c>True</c> if successful; otherwise <c>False</c>.</returns>
        public bool TryGetBoolVariable(string name, out bool value)
        {
            if (TessApi.Native.BaseApiGetBoolVariable(handle, name, out var val) != 0)
            {
                value = (val != 0);
                return true;
            }
            else
            {
                value = false;
                return false;
            }
        }

        #endregion Config

        #region Event Handlers

        private void OnIteratorDisposed(object sender, EventArgs e)
        {
            processCount--;
        }

        #endregion Event Handlers
    }
}

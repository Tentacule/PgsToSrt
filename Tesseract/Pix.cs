using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Tesseract.Internal;

namespace Tesseract
{
    public sealed unsafe class Pix : DisposableBase, IEquatable<Pix>
    {
        #region Constants

        public const float Deg2Rad = (float)(Math.PI / 180.0);

        // Skew Defaults
        public const int DefaultBinarySearchReduction = 2; // binary search part

        public const int DefaultBinaryThreshold = 130;

        /// <summary>
        /// A small angle, in radians, for threshold checking. Equal to about 0.06 degrees.
        /// </summary>
        private const float VerySmallAngle = 0.001F;

        private static readonly List<int> AllowedDepths = new List<int> { 1, 2, 4, 8, 16, 32 };

        /// <summary>
        /// Used to lookup image formats by extension.
        /// </summary>
        private static readonly Dictionary<string, ImageFormat> imageFomatLookup = new Dictionary<string, ImageFormat>
        {
            { ".jpg", ImageFormat.JfifJpeg },
            { ".jpeg", ImageFormat.JfifJpeg },
            { ".gif", ImageFormat.Gif },
            { ".tif", ImageFormat.Tiff },
            { ".tiff", ImageFormat.Tiff },
            { ".png", ImageFormat.Png },
            { ".bmp", ImageFormat.Bmp }
        };

        #endregion Constants

        #region Fields

        private readonly int depth;
        private readonly int height;
        private readonly int width;
        private PixColormap colormap;
        private HandleRef handle;

        #endregion Fields

        #region Create\Load methods

        /// <summary>
        /// Creates a new pix instance using an existing handle to a pix structure.
        /// </summary>
        /// <remarks>
        /// Note that the resulting instance takes ownership of the data structure.
        /// </remarks>
        /// <param name="handle"></param>
        private Pix(IntPtr handle)
        {
            if (handle == IntPtr.Zero) throw new ArgumentNullException("handle");

            this.handle = new HandleRef(this, handle);
            this.width = Interop.LeptonicaApi.Native.pixGetWidth(this.handle);
            this.height = Interop.LeptonicaApi.Native.pixGetHeight(this.handle);
            this.depth = Interop.LeptonicaApi.Native.pixGetDepth(this.handle);

            var colorMapHandle = Interop.LeptonicaApi.Native.pixGetColormap(this.handle);
            if (colorMapHandle != IntPtr.Zero)
            {
                this.colormap = new PixColormap(colorMapHandle);
            }
        }

        public static Pix Create(int width, int height, int depth)
        {
            if (!AllowedDepths.Contains(depth))
                throw new ArgumentException("Depth must be 1, 2, 4, 8, 16, or 32 bits.", "depth");

            if (width <= 0) throw new ArgumentException("Width must be greater than zero", "width");
            if (height <= 0) throw new ArgumentException("Height must be greater than zero", "height");

            var handle = Interop.LeptonicaApi.Native.pixCreate(width, height, depth);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("Failed to create pix, this normally occurs because the requested image size is too large, please check Standard Error Output.");

            return Create(handle);
        }

        public static Pix Create(IntPtr handle)
        {
            if (handle == IntPtr.Zero) throw new ArgumentException("Pix handle must not be zero (null).", "handle");

            return new Pix(handle);
        }
        
        public static Pix LoadFromMemory(byte[] bytes)
        {
            IntPtr handle;
            fixed (byte* ptr = bytes)
            {
                handle = Interop.LeptonicaApi.Native.pixReadMem(ptr, bytes.Length);
            }
            if (handle == IntPtr.Zero)
            {
                throw new IOException("Failed to load image from memory.");
            }
            return Create(handle);
        }
        
        #endregion Create\Load methods

        #region Properties

        public PixColormap Colormap
        {
            get { return colormap; }
            set
            {
                if (value != null)
                {
                    if (Interop.LeptonicaApi.Native.pixSetColormap(handle, value.Handle) == 0)
                    {
                        colormap = value;
                    }
                }
                else
                {
                    if (Interop.LeptonicaApi.Native.pixDestroyColormap(handle) == 0)
                    {
                        colormap = null;
                    }
                }
            }
        }

        public int Depth
        {
            get { return depth; }
        }

        public int Height
        {
            get { return height; }
        }

        public int Width
        {
            get { return width; }
        }

        public int XRes
        {
            get { return Interop.LeptonicaApi.Native.pixGetXRes(this.handle); }
            set { Interop.LeptonicaApi.Native.pixSetXRes(this.handle, value); }
        }

        public int YRes
        {
            get { return Interop.LeptonicaApi.Native.pixGetYRes(this.handle); }
            set { Interop.LeptonicaApi.Native.pixSetYRes(this.handle, value); }
        }

        internal HandleRef Handle
        {
            get { return handle; }
        }

        public PixData GetData()
        {
            return new PixData(this);
        }

        #endregion Properties

        #region Equals

        public override bool Equals(object obj)
        {
            // Check for null values and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
                return false;

            return Equals((Pix)obj);
        }

        public bool Equals(Pix other)
        {
            if (other == null)
            {
                return false;
            }

            int same;
            if (Interop.LeptonicaApi.Native.pixEqual(Handle, other.Handle, out same) != 0)
            {
                throw new TesseractException("Failed to compare pix");
            }
            return same != 0;
        }

        #endregion

        #region Save methods

        /// <summary>
        /// Saves the image to the specified file.
        /// </summary>
        /// <param name="filename">The path to the file.</param>
        /// <param name="format">The format to use when saving the image, if not specified the file extension is used to guess the format.</param>
        public void Save(string filename, ImageFormat? format = null)
        {
            ImageFormat actualFormat;
            if (!format.HasValue)
            {
                var extension = Path.GetExtension(filename).ToLowerInvariant();
                if (!imageFomatLookup.TryGetValue(extension, out actualFormat))
                {
                    // couldn't find matching format, perhaps there is no extension or it's not recognised, fallback to default.
                    actualFormat = ImageFormat.Default;
                }
            }
            else
            {
                actualFormat = format.Value;
            }

            if (Interop.LeptonicaApi.Native.pixWrite(filename, handle, actualFormat) != 0)
            {
                throw new IOException(String.Format("Failed to save image '{0}'.", filename));
            }
        }

        #endregion Save methods

        #region Clone

        /// <summary>
        /// Increments this pix's reference count and returns a reference to the same pix data.
        /// </summary>
        /// <remarks>
        /// A "clone" is simply a reference to an existing pix. It is implemented this way because
        /// image can be large and hence expensive to copy and extra handles need to be made with a simple
        /// policy to avoid double frees and memory leaks.
        ///
        /// The general usage protocol is:
        /// <list type="number">
        /// 	<item>Whenever you want a new reference to an existing <see cref="Pix" /> call <see cref="Pix.Clone" />.</item>
        ///     <item>
        /// 		Always call <see cref="Pix.Dispose" /> on all references. This decrements the reference count and
        /// 		will destroy the pix when the reference count reaches zero.
        /// 	</item>
        /// </list>
        /// </remarks>
        /// <returns>The pix with it's reference count incremented.</returns>
        public Pix Clone()
        {
            var clonedHandle = Interop.LeptonicaApi.Native.pixClone(handle);
            return new Pix(clonedHandle);
        }

        #endregion Clone

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            var tmpHandle = handle.Handle;
            Interop.LeptonicaApi.Native.pixDestroy(ref tmpHandle);
            this.handle = new HandleRef(this, IntPtr.Zero);
        }

        #endregion Disposal
    }
}
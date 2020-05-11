using System;
using System.Runtime.InteropServices;
using Tesseract.Interop;

namespace Tesseract
{
    /// <summary>
    /// Represents a colormap.
    /// </summary>
    /// <remarks>
    /// Once the colormap is assigned to a pix it is owned by that pix and will be disposed off automatically 
    /// when the pix is disposed off.
    /// </remarks>
    public sealed class PixColormap : IDisposable
    {
        private HandleRef handle;

        internal PixColormap(IntPtr handle)
        {
            this.handle = new HandleRef(this, handle);
        }

        internal HandleRef Handle
        {
            get { return handle; }
        }

        public void Dispose()
        {
            IntPtr tmpHandle = Handle.Handle;
            TessApi.Leptonica.pixcmapDestroy(ref tmpHandle);
            this.handle = new HandleRef(this, IntPtr.Zero);
        }
    }
}

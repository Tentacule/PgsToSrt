using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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
            Interop.LeptonicaApi.Native.pixcmapDestroy(ref tmpHandle);
            this.handle = new HandleRef(this, IntPtr.Zero);
        }
    }
}

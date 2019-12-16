//  Copyright (c) 2014 Andrey Akinshin
//  Project URL: https://github.com/AndreyAkinshin/InteropDotNet
//  Distributed under the MIT License: http://opensource.org/licenses/MIT
using System;
using System.Runtime.InteropServices;

namespace InteropDotNet
{
    [ComVisible(true)]
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class RuntimeDllImportAttribute : Attribute
    {
        public string EntryPoint;

        public CallingConvention CallingConvention;

        public CharSet CharSet;

        public bool SetLastError;        

        public bool BestFitMapping;

        public bool ThrowOnUnmappableChar;

        public string LibraryFileNameWindows { get; private set; }

        public string LibraryFileNameUnix { get; private set; }

        public RuntimeDllImportAttribute(string libraryFileNameWindows, string libraryFileNameUnix)
        {
            LibraryFileNameWindows = libraryFileNameWindows;
            LibraryFileNameUnix = libraryFileNameUnix;
        }

        public string GetLibraryFileName()
        {
            var operatingSystem = SystemManager.GetOperatingSystem();
            if (operatingSystem == OperatingSystem.Windows)
            {
                return LibraryFileNameWindows;
            }
            else
            {
                return LibraryFileNameUnix;
            }
        }
    }
}
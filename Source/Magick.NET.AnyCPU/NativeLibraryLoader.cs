﻿//=================================================================================================
// Copyright 2013-2016 Dirk Lemstra <https://magick.codeplex.com/>
//
// Licensed under the ImageMagick License (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
//
//   http://www.imagemagick.org/script/license.php
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
// express or implied. See the License for the specific language governing permissions and
// limitations under the License.
//=================================================================================================

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ImageMagick
{
  internal static class NativeLibraryLoader
  {
    private static volatile bool _Loaded;

    private static class NativeMethods
    {
      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool SetDllDirectory(string lpPathName);
    }

    private static Assembly Assembly
    {
      get
      {
        return typeof(NativeLibraryLoader).Assembly;
      }
    }

    public static void Copy(Stream source, Stream destination)
    {
#if NET20
      byte[] buffer = new byte[16384];
      int bytesRead;

      while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
      {
        destination.Write(buffer, 0, bytesRead);
      }
#else
      source.CopyTo(destination);
#endif
    }

    private static string CreateCacheDirectory()
    {
      AssemblyFileVersionAttribute version = (AssemblyFileVersionAttribute)Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false)[0];

#if NET20
      string path = Path.Combine(MagickAnyCPU.CacheDirectory, "Magick.NET.net20." + version.Version);
#else
      string path = Path.Combine(MagickAnyCPU.CacheDirectory, "Magick.NET.net40-client." + version.Version);
#endif
      if (!Directory.Exists(path))
      {
        Directory.CreateDirectory(path);
        GrantEveryoneReadAndExecuteAccess(path);
      }

      return path;
    }

    private static void ExtractLibrary()
    {
#if Q8
      string name = "Magick.NET-Q8-" + (NativeLibrary.Is64Bit ? "x64" : "x86");
#elif Q16
      string name = "Magick.NET-Q16-" + (NativeLibrary.Is64Bit ? "x64" : "x86");
#elif Q16HDRI
      string name = "Magick.NET-Q16-HDRI-" + (NativeLibrary.Is64Bit ? "x64" : "x86");
#else
#error Not implemented!
#endif
      string cacheDirectory = CreateCacheDirectory();
      string tempFile = Path.Combine(cacheDirectory, name + ".Native.dll");

      WriteAssembly(tempFile);
      WriteXmlResources(cacheDirectory);

      NativeMethods.SetDllDirectory(cacheDirectory);

      MagickNET.Initialize(cacheDirectory);
    }

    private static void GrantEveryoneReadAndExecuteAccess(string cacheDirectory)
    {
      if (!MagickAnyCPU.HasSharedCacheDirectory || !MagickAnyCPU.UsesDefaultCacheDirectory)
        return;

      DirectoryInfo directoryInfo = new DirectoryInfo(cacheDirectory);
      DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();
      SecurityIdentifier identity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
      InheritanceFlags inheritanceFlags = InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit;
      directorySecurity.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.ReadAndExecute, inheritanceFlags, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
      directoryInfo.SetAccessControl(directorySecurity);
    }

    [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "No harm in doing that here.")]
    private static void WriteAssembly(string tempFile)
    {
      if (File.Exists(tempFile))
        return;

      string resourceName = "ImageMagick.Resources.Library.Magick.NET.Native_" + (NativeLibrary.Is64Bit ? "x64" : "x86") + ".gz";

      using (Stream stream = Assembly.GetManifestResourceStream(resourceName))
      {
        using (GZipStream compressedStream = new GZipStream(stream, CompressionMode.Decompress, false))
        {
          using (FileStream fileStream = File.Open(tempFile, FileMode.CreateNew))
          {
            Copy(compressedStream, fileStream);
          }
        }
      }
    }

    private static void WriteXmlResources(string cacheDirectory)
    {
      string[] xmlFiles =
      {
        "coder.xml", "colors.xml", "configure.xml", "delegates.xml", "english.xml", "locale.xml",
        "log.xml", "magic.xml", "policy.xml", "thresholds.xml", "type.xml", "type-ghostscript.xml",
      };

      foreach (string xmlFile in xmlFiles)
      {
        string outputFile = Path.Combine(cacheDirectory, xmlFile);
        if (File.Exists(outputFile))
          continue;

        string resourceName = "ImageMagick.Resources.Xml." + xmlFile;
        using (Stream stream = Assembly.GetManifestResourceStream(resourceName))
        {
          using (FileStream fileStream = File.Open(outputFile, FileMode.CreateNew))
          {
            Copy(stream, fileStream);
          }
        }
      }
    }

    public static void Load()
    {
      if (_Loaded)
        return;

      _Loaded = true;
      ExtractLibrary();
    }
  }
}

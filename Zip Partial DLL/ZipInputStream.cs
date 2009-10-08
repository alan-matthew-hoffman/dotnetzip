// ZipInputStream.cs
//
// ------------------------------------------------------------------
//
// Copyright (c) 2009 Dino Chiesa.  
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License. 
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs): 
// Time-stamp: <2009-October-08 01:37:05>
//
// ------------------------------------------------------------------
//
// This module defines the ZipInputStream class, which is a stream metaphor for
// reading zip files.  This class does not depend on Ionic.Zip.ZipFile, but rather
// stands alongside it as an alternative "container" for ZipEntry, when reading zips. 
//
// It adds one interesting method to the normal "stream" interface: GetNextEntry.  
//
// ------------------------------------------------------------------
//

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

namespace  Ionic.Zip
{
    /// <summary>
    ///   Provides a stream metaphor for reading zip files. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    ///   This class provides an alternative programming model for reading zip files to
    ///   the one enabled by the <see cref="ZipFile"/> class.  Use this when reading zip
    ///   files, as an alternative to the <see cref="ZipFile"/> class, when you would
    ///   like to use a Stream class to read the file.
    /// </para>
    ///
    /// <para>
    ///   Some application designs require a readable stream for input. This stream can
    ///   be used to read a zip file, and extract entries.
    /// </para>
    ///
    /// <para>
    ///   Both the <c>ZipOutputStream</c> class and the <c>ZipFile</c> class can be used
    ///   to read and extract zip files.  Both of them support many of the common zip
    ///   features, including Unicode, different compression levels, and ZIP64.  The
    ///   programming models differ. For example, when extracting entries via calls to
    ///   the <c>GetNextEntry()</c> and <c>Read()</c> methods on the
    ///   <c>ZipInputStream</c> class, the caller is responsible for creating the file,
    ///   writing the bytes into the file, setting the attributes on the file, and
    ///   setting the created, last modified, and last accessed timestamps on the
    ///   file. All of these things are done automatically by a call to <see
    ///   cref="ZipEntry.Extract()">ZipEntry.Extract()</see>.  For this reason, the
    ///   <c>ZipInputStream</c> is generally recommended for when your application wants
    ///   to extract the data, but not create a file with that data.
    /// </para>
    ///
    /// <para>
    ///   Aside from the obvious differences in programming model, there are some
    ///   differences in capability between the <c>ZipFile</c> class and the
    ///   <c>ZipInputStream</c> class.
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item>
    ///   <c>ZipFile</c> can be used to create or update zip files, or read and extract
    ///   zip files. <c>ZipInputStream</c> can be used only to read and extract zip
    ///   files.
    ///   </item>
    ///
    ///   <item>
    ///     <c>ZipInputStream</c> cannot read segmented or spanned
    ///     zip files.
    ///   </item>
    ///
    ///   <item>
    ///     <c>ZipInputStream</c> will not read Zip file comments.
    ///     zip files.
    ///   </item>
    ///
    /// </list>
    ///
    /// </remarks>
    public class ZipInputStream : Stream
    {
        /// <summary>
        ///   Create a ZipInputStream.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        ///
        /// <param name="stream">
        /// The stream to read. It must be readable. This stream will be closed at
        /// the time the ZipInputStream is closed.
        /// </param>
        ///
        /// <example>
        ///
        ///   This example shows how to read a zip file, and extract entries, using the
        ///   ZipInputStream class.
        ///
        /// <code>
        /// private void Unzip()
        /// {
        ///     byte[] buffer= new byte[2048];
        ///     int n;
        ///     using (var raw = File.Open(_outputFileName, FileMode.Open, FileAccess.Read))
        ///     {
        ///         using (var input= new ZipInputStream(raw))
        ///         {
        ///             ZipEntry e;
        ///             while (( e = input.GetNextEntry()) != null)
        ///             {
        ///                 if (e.IsDirectory) continue;
        ///                 string outputPath = Path.Combine(_extractDir, e.FileName);
        ///                 using (var output = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite))
        ///                 {
        ///                     while ((n= input.Read(buffer,0,buffer.Length)) > 0)
        ///                     {
        ///                         output.Write(buffer,0,n);
        ///                     }
        ///                 }
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public ZipInputStream(Stream stream)  : this (stream, false) { }


        
        /// <summary>
        ///   Create a ZipInputStream.
        /// </summary>
        ///
        /// <remarks>
        ///   See the documentation for the <see
        ///   cref="ZipInputStream(Stream)">ZipInputStream(Stream)</see>
        ///   constructor for an example of how to use the class.
        /// </remarks>
        ///
        /// <param name="stream">
        ///   The stream to read from. It must be readable.
        /// </param>
        ///
        /// <param name="leaveOpen">
        ///   true if the application would like the stream
        ///   to remain open after the <c>ZipInputStream</c> has been closed.
        /// </param>
        public ZipInputStream(Stream stream, bool leaveOpen)
        {
            _inputStream = stream;
            _container= new ZipContainer(this);
            _provisionalAlternateEncoding = System.Text.Encoding.GetEncoding("IBM437");
            _leaveUnderlyingStreamOpen = leaveOpen;
        }


        /// <summary>
        ///   The text encoding to use when reading entries into the zip archive, for
        ///   those entries whose filenames or comments cannot be encoded with the
        ///   default (IBM437) encoding.
        /// </summary>
        /// 
        /// <remarks>
        /// <para>
        ///   In <see href="http://www.pkware.com/documents/casestudies/APPNOTE.TXT">its
        ///   zip specification</see>, PKWare describes two options for encoding
        ///   filenames and comments: using IBM437 or UTF-8.  But, some archiving tools
        ///   or libraries do not follow the specification, and instead encode
        ///   characters using the system default code page.  For example, WinRAR when
        ///   run on a machine in Shanghai may encode filenames with the Big-5 Chinese
        ///   (950) code page.  This behavior is contrary to the Zip specification, but
        ///   it occurs anyway.
        /// </para>
        ///
        /// <para>
        ///   When using DotNetZip to read zip archives that use something other than UTF-8 or IBM437, 
        ///   set this property to specify the code page to use
        ///   when reading encoded filenames and comments for each
        ///   <c>ZipEntry</c> in the zip file.
        /// </para>
        ///
        /// <para>
        ///   This property is "provisional".  IBM437 is used to decode filenames and
        ///   comments where possible, in other words, where no loss of data would
        ///   result (when encoding and decoding is reflexive). This codepage is used
        ///   when that encoding is not sufficient. It is possible, therefore, to have a
        ///   given entry with a <c>Comment</c> encoded in IBM437 and a <c>FileName</c>
        ///   encoded with the specified "provisional" codepage.
        /// </para>
        ///
        /// <para>
        ///   When a zip file uses an arbitrary, non-UTF8 code page for encoding, there is no
        ///   standard way for the reader application - whether DotNetZip, WinZip,
        ///   WinRar, or something else - to know which
        ///   codepage has been used for the entries. Readers of zip files
        ///   are not able to inspect the zip file and determine the codepage that was
        ///   used for the entries contained within it.  It is left to the application
        ///   or user to determine the necessary codepage when reading zip files encoded
        ///   this way.  If you use an incorrect codepage when reading a zipfile, you
        ///   will get entries with filenames that are incorrect, and the incorrect
        ///   filenames may even contain characters that are not legal for use within
        ///   filenames in Windows. Extracting entries with illegal characters in the
        ///   filenames will lead to exceptions. It's too bad, but this is just the way
        ///   things are with code pages in zip files. Caveat Emptor.
        /// </para>
        ///
        /// </remarks>
        public System.Text.Encoding ProvisionalAlternateEncoding
        {
            get
            {
                return _provisionalAlternateEncoding;
            }
            set
            {
                _provisionalAlternateEncoding = value;
            }
        }
        
        
        /// <summary>
        ///   Size of the work buffer to use for the ZLIB codec during decompression.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this may affect performance.  For larger files, setting this to a
        ///   larger size may improve performance, but I'm not sure.  Sorry, I don't
        ///   currently have good recommendations on how to set it.  You can test it if
        ///   you like.
        /// </remarks>
        public int CodecBufferSize
        {
            get;
            set;
        }

        
        /// <summary>
        ///   Sets the password to be used on the <c>ZipInputStream</c> instance.
        /// </summary>
        /// 
        /// <remarks>
        /// 
        /// <para>
        ///   When reading a zip archive, this password is used to read and decrypt the
        ///   entries that are encrypted within the zip file. When entries within a zip
        ///   file use different passwords, set the appropriate password for the entry
        ///   before the first call to <c>Read()</c> for each entry.
        /// </para>
        /// 
        /// </remarks>
        ///
        /// <example>
        ///
        ///   This example uses the ZipInputStream to read and extract entries from a
        ///   zip file, using a potentially different password for each entry.
        /// 
        /// <code lang="C#">
        /// byte[] buffer= new byte[2048];
        /// int n;
        /// using (var raw = File.Open(_inputFileName, FileMode.Open, FileAccess.Read ))
        /// {
        ///     using (var input= new ZipInputStream(raw))
        ///     {
        ///         ZipEntry e;
        ///         while (( e = input.GetNextEntry()) != null)
        ///         {
        ///             input.Password = PasswordForEntry(e.FileName);
        ///             if (e.IsDirectory) continue;
        ///             string outputPath = Path.Combine(_extractDir, e.FileName);
        ///             using (var output = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite))
        ///             {
        ///                 while ((n= input.Read(buffer,0,buffer.Length)) > 0)
        ///                 {
        ///                     output.Write(buffer,0,n);
        ///                 }
        ///             }
        ///         }
        ///     }
        /// }
        ///
        /// </code>
        /// </example>
        public String Password
        {
            set
            {
                if (_closed)
                {
                    _exceptionPending = true;
                    throw new System.InvalidOperationException("The stream has been closed.");
                }
                _password = value;
            }
        }


        private void _SetupStream()
        {
            _crcStream= _currentEntry.InternalOpenReader(_password);
            _LeftToRead = _crcStream.Length;
            _needSetup = false;
        }



        internal Stream ReadStream
        {
            get
            {
                return _inputStream;    
            }
        }

        
        /// <summary>
        ///   Read the data from the stream into the buffer.
        /// </summary>
        ///
        /// <remarks>
        ///   As the application reads data into the buffer, the data may be
        ///   decrypted and uncompressed, as necessary, before being copied to the buffer, 
        /// </remarks>
        ///
        /// <param name="buffer">The buffer to hold the data read from the stream.</param>
        /// <param name="offset">the offset within the buffer to copy the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes read, after decryption and decompression.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_closed)
            {
                _exceptionPending = true;
                throw new System.InvalidOperationException("The stream has been closed.");
            }
            
            if (_needSetup)
                _SetupStream();

            if (_LeftToRead == 0) return 0;

            int len = (_LeftToRead > count) ? count : (int)_LeftToRead;
            int n = _crcStream.Read(buffer, offset, len);

            _LeftToRead -= n;

            if (_LeftToRead == 0)
            {
                _inputStream.Seek(_endOfEntry, SeekOrigin.Begin);
                int CrcResult = _crcStream.Crc;
                _currentEntry.VerifyCrc(CrcResult);
            }
            
            return n;
        }

        

        /// <summary>
        ///   Read the next entry from the zip file.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   Call this method just before calling <see cref="Read(byte[], int, int)"/>,
        ///   to position the pointer in the zip file to the next entry that can be
        ///   read.  Subsequent calls to <c>Read()</c>, will decrypt and decompress the
        ///   data in the zip file, until <c>Read()</c> returns 0.
        /// </para>
        ///
        /// <para>
        ///   Each time you call <c>GetNextEntry()</c>, the pointer in the wrapped
        ///   stream is moved to the next entry in the zip file.
        /// </para>
        ///
        /// <para>
        ///   This method returns the <c>ZipEntry</c>.  In addition to reading the raw
        ///   bytes, You can extract an entry by calling <see
        ///   cref="ZipEntry.Extract()"/>, or one of its siblings.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <returns>
        ///   The ZipEntry read. Returns null (or Nothing in VB) if there are no more
        ///   entries in the zip file.
        /// </returns>
        ///
        public ZipEntry GetNextEntry()
        {
            _currentEntry = ZipEntry.ReadEntry(_container, !_firstEntry);
            _endOfEntry = _inputStream.Position;
            _firstEntry = true;
            _needSetup = true;
            return _currentEntry;
        }
        

        /// <summary>
        ///   Close the stream.
        /// </summary>
        /// 
        /// <remarks>
        /// <para>
        ///   This method closes the ZipInputStream.  It may also close the underlying stream, depending
        ///   on which constructor was used. 
        /// </para>
        ///
        /// <para>
        ///   Typically the application will call <c>Close()</c> implicitly, via a <c>using</c>
        ///   statement in C#, or a <c>Using</c> statement in VB.
        /// </para>
        /// 
        /// </remarks>
        ///
        public override void Close()
        {
            if (_closed) return;

            // When ZipInputStream is used within a using clause, and an exception is thrown,
            // Close() is invoked.  But we don't want to try to write anything in that case.
            // Eventually the exception will be propagated to the application.
            if (_exceptionPending) return;
            
            if (!_leaveUnderlyingStreamOpen)
                _inputStream.Close();
            
            _closed= true;
        }


        /// <summary>
        /// Always returns false.
        /// </summary>
        public override bool CanRead  { get { return true; } }
        
        /// <summary>
        /// Always returns false.
        /// </summary>
        public override bool CanSeek  { get { return true; } }
        
        /// <summary>
        /// Always returns true.
        /// </summary>
        public override bool CanWrite { get { return false; } }
        
        /// <summary>
        /// Always returns a NotSupportedException.
        /// </summary>
        public override long Length   { get { throw new NotSupportedException(); }}

        /// <summary>
        /// Always returns a NotSupportedException.
        /// </summary>
        public override long Position
        {
            get { throw new NotSupportedException();}
            set { throw new NotSupportedException();}
        }

        /// <summary>
        /// This is a no-op.
        /// </summary>
        public override void Flush()
        {
            throw new NotSupportedException("Flush");
        }

                
        /// <summary>
        /// This method always throws a NotSupportedException.
        /// </summary>
        /// <param name="buffer">ignored</param>
        /// <param name="offset">ignored</param>
        /// <param name="count">ignored</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Write");
        }

        
        /// <summary>
        ///   this method always throws.
        /// </summary>
        ///
        /// <param name="offset">the offset point to seek to</param>
        /// <param name="origin">the reference point from which to seek</param>
        /// <returns>the new position</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.InvalidOperationException("Seek");
        }

        /// <summary>
        /// This method always throws a NotSupportedException.
        /// </summary>
        /// <param name="value">ignored</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        
        private Stream _inputStream;
        private System.Text.Encoding _provisionalAlternateEncoding;
        private ZipEntry _currentEntry;
        private bool _firstEntry;
        private bool _needSetup;
        private ZipContainer _container;
        private Ionic.Zlib.CrcCalculatorStream _crcStream;
        private Int64 _LeftToRead;
        private String _password;
        private Int64 _endOfEntry;
        
        private bool _leaveUnderlyingStreamOpen;
        private bool _closed;
        private bool _exceptionPending;
    }


   
}
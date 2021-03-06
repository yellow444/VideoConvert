﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GenHelper.cs" company="JT-Soft (https://github.com/UniqProject/VideoConvert)">
//   This file is part of the VideoConvert.Interop source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Generic Helper Class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace VideoConvert.Interop.Utilities
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using VideoConvert.Interop.Model.MediaInfo;

    /// <summary>
    /// Generic Helper Class
    /// </summary>
    public static class GenHelper
    {
        /// <summary>
        /// Gets the Description for enum Types
        /// </summary>
        /// <param name="value"></param>
        /// <returns>string containing the description</returns>
        public static string StringValueOf(Enum value)
        {
            var fi = value.GetType().GetField(value.ToString("F"));
            var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString("F");
        }

        /// <summary>
        /// Gets File size
        /// </summary>
        /// <param name="fName">File name</param>
        /// <returns></returns>
        public static ulong GetFileSize(string fName)
        {
            return (ulong)new FileInfo(fName).Length;
        }

        // Get media information with an 10 sec timeout
        private delegate MediaInfoContainer MiWorkDelegate(string fileName);

        /// <summary>
        /// Gets Media info
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public static MediaInfoContainer GetMediaInfo(string fileName)
        {
            MiWorkDelegate d = DoWorkHandler;
            var res = d.BeginInvoke(fileName, null, null);

            if (res.IsCompleted) return d.EndInvoke(res);

            res.AsyncWaitHandle.WaitOne(10000, false);
            if (res.IsCompleted == false)
                throw new TimeoutException("Could not open media file!");
            return d.EndInvoke(res);
        }

        private static MediaInfoContainer DoWorkHandler(string fileName)
        {
            return MediaInfoContainer.GetMediaInfo(fileName);
        }
    }
}

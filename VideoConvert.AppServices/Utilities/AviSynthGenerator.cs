﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AviSynthGenerator.cs" company="JT-Soft (https://github.com/UniqProject/VideoConvert)">
//   This file is part of the VideoConvert.AppServices source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   The AviSynth Generator
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace VideoConvert.AppServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using log4net;
    using VideoConvert.AppServices.Services.Interfaces;
    using VideoConvert.AppServices.Utilities.Interfaces;
    using VideoConvert.Interop.Model;
    using VideoConvert.Interop.Model.MediaInfo;
    using VideoConvert.Interop.Utilities;

    /// <summary>
    /// The AviSynth Generator
    /// </summary>
    public class AviSynthGenerator : IAviSynthGenerator
    {
        /// <summary>
        /// Errorlog
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(AviSynthGenerator));
        private static readonly CultureInfo CInfo = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");

        private static IAppConfigService _appConfig;

        /// <summary>
        /// Contains path to config file used by h264StereoSource plugin for AviSynth
        /// </summary>
        public string StereoConfigFile = string.Empty;

        /// <summary>
        /// The AviSynth Generator Constructor
        /// </summary>
        /// <param name="appConfig"></param>
        public AviSynthGenerator(IAppConfigService appConfig)
        {
            _appConfig = appConfig;
        }

        /// <summary>
        /// Generates AviSynth script used for video encoding
        /// </summary>
        /// <param name="videoInfo">All video properties</param>
        /// <param name="changeFps">Defines whether framerate should be changed</param>
        /// <param name="targetFps">Sets target framerate</param>
        /// <param name="resizeTo">Sets target video resolution</param>
        /// <param name="stereoEncoding">Defines, which stereo encoding mode should be used</param>
        /// <param name="stereoVideoInfo">Sets all parameters for stereo encoding</param>
        /// <param name="isDvdResolution">Defines whether target resolution is used for DVD encoding</param>
        /// <param name="subtitleFile">Sets subtitle file for hardcoding into video</param>
        /// <param name="subtitleOnlyForced">Defines whether only forced captions should be hardcoded</param>
        /// <param name="skipScaling"></param>
        /// <returns>Path to AviSynth script</returns>
        public string Generate(VideoInfo videoInfo, bool changeFps, float targetFps, Size resizeTo,
                               StereoEncoding stereoEncoding, StereoVideoInfo stereoVideoInfo, bool isDvdResolution,
                               string subtitleFile, bool subtitleOnlyForced, bool skipScaling)
        {
            var sb = new StringBuilder();

            var mtUseful = (videoInfo.Interlaced && _appConfig.UseHQDeinterlace) || changeFps;

            var useStereo = stereoEncoding != StereoEncoding.None && stereoVideoInfo.RightStreamId > -1;

            // support for multithreaded AviSynth
            if (_appConfig.UseAviSynthMT && mtUseful)
            {
                sb.AppendLine("SetMTMode(2,0)");
                sb.AppendLine("SetMemoryMax(512)");
            }

            var pluginList = new List<string> {"ffms2.dll"};
            var scriptList = new List<string>();

            //loading plugins

            if (changeFps || (videoInfo.Interlaced && _appConfig.UseHQDeinterlace))
                pluginList.Add("mvtools2.dll");

            if (videoInfo.Interlaced && _appConfig.UseHQDeinterlace)
            {
                pluginList.Add(_appConfig.LastAviSynthVer.StartsWith("2.5")
                                    ? "mt_masktools-25.dll"
                                    : "mt_masktools-26.dll");
                pluginList.Add("nnedi3.dll");
                pluginList.Add("RemoveGrainSSE2.dll");
                pluginList.Add("RepairSSE2.dll");
                
                scriptList.Add("QTGMC-3.32.avsi");
            }
            else if (videoInfo.Interlaced)
            {
                pluginList.Add("Decomb.dll");
            }

            if (useStereo)
                pluginList.Add("H264StereoSource.dll");

            if (!string.IsNullOrEmpty(subtitleFile) && File.Exists(subtitleFile))
            {
                switch (Path.GetExtension(subtitleFile))
                {
                    case "sup":
                        pluginList.Add("SupTitle.dll");
                        break;
                    case "ass":
                    case "ssa":
                    case "srt":
                        pluginList.Add("VSFilter.dll");
                        break;
                }
            }

            // generate plugin and script loading
            foreach (var plugin in pluginList)
            {
                sb.AppendLine($"LoadPlugin(\"{Path.Combine(_appConfig.AvsPluginsPath, plugin)}\")");
            }
            foreach (var script in scriptList)
            {
                sb.AppendLine($"Import(\"{Path.Combine(_appConfig.AvsPluginsPath, script)}\")");
            }

            //generate rest of the script

            // calculate framerate numerator & denominator
            if (videoInfo.Fps <= 0)
            {
                var mi = new MediaInfoContainer();
                try
                {
                     mi = GenHelper.GetMediaInfo(videoInfo.TempFile);
                }
                catch (TimeoutException ex)
                {
                    Log.Error(ex);
                    mi = new MediaInfoContainer();
                }
                finally
                {
                    if (mi.Video.Count > 0)
                    {
                        videoInfo.Fps = mi.Video[0].FrameRate;
                        VideoHelper.GetFpsNumDenom(videoInfo.Fps, out videoInfo.FrameRateEnumerator,
                                                  out videoInfo.FrameRateDenominator);

                        if (videoInfo.FrameRateEnumerator == 0)
                        {
                            videoInfo.FrameRateEnumerator = (int)Math.Round(videoInfo.Fps) * 1000;
                            videoInfo.FrameRateDenominator =
                                (int)(Math.Round(Math.Ceiling(videoInfo.Fps) - Math.Floor(videoInfo.Fps)) + 1000);
                        }
                    }
                }
                
            }
            sb.Append($"FFVideoSource(\"{videoInfo.TempFile}\",");

            if (videoInfo.FrameRateEnumerator > 0 && videoInfo.FrameRateDenominator > 0)
                sb.Append($"fpsnum={videoInfo.FrameRateEnumerator:0},fpsden={videoInfo.FrameRateDenominator:0},");

            var threadCount = _appConfig.LimitDecoderThreads ? 1 : 0;
            sb.Append($"threads={threadCount:0})");
            sb.AppendLine();

            var stereoVar = string.Empty;

            if (useStereo)
            {
                var configFile = GenerateStereoSourceConfig(stereoVideoInfo);
                sb.AppendLine($"VideoRight = H264StereoSource(\"{configFile}\",{videoInfo.FrameCount - 50:0})");
                StereoConfigFile = configFile;
                stereoVar = "VideoRight";
            }

            // deinterlace video source
            if (videoInfo.Interlaced)
            {
                if (_appConfig.UseHQDeinterlace)
                    sb.AppendLine("QTGMC(Preset=\"Slower\")");
                else
                {
                    sb.AppendLine("ConvertToYUY2(interlaced=true)");
                    sb.AppendLine("Telecide(post=4)");
                    sb.AppendLine("Crop(4, 0, -4, 0)");
                    sb.AppendLine("AddBorders(4, 0, 4, 0)");
                    sb.AppendLine("ConvertToYV12()");
                }
            }

            // hardcode subtitles
            if (!string.IsNullOrEmpty(subtitleFile) && File.Exists(subtitleFile))
            {
                switch (Path.GetExtension(subtitleFile))
                {
                    case "sup":
                        var subForced = subtitleOnlyForced ? "true" : "false";
                        sb.Append($"SupTitle(\"{subtitleFile}\", forcedOnly={subForced})");
                        break;
                    case "ass":
                    case "ssa":
                    case "srt":
                        sb.Append($"TextSub(\"{subtitleFile}\")");
                        break;
                }
                
                sb.AppendLine();
            }

            // video cropping
            if (!videoInfo.CropRect.IsEmpty && !skipScaling)
            {
                int temp;

                Math.DivRem(videoInfo.CropRect.X, 2, out temp);
                videoInfo.CropRect.X += temp;
                Math.DivRem(videoInfo.CropRect.Y, 2, out temp);
                videoInfo.CropRect.Y += temp;
                Math.DivRem(videoInfo.CropRect.Width, 2, out temp);
                videoInfo.CropRect.Width += temp;
                Math.DivRem(videoInfo.CropRect.Height, 2, out temp);
                videoInfo.CropRect.Height += temp;

                videoInfo.Height = videoInfo.CropRect.Height;
                videoInfo.Width = videoInfo.CropRect.Width;

                if ((videoInfo.CropRect.X > 0) || (videoInfo.CropRect.Y > 0) || (videoInfo.CropRect.Width < videoInfo.Width) ||
                    (videoInfo.CropRect.Height < videoInfo.Height))
                {
                    sb.Append(useStereo ? "CroppedVideoRight = Crop(VideoRight," : "Crop(");

                    sb.Append($"{videoInfo.CropRect.Left:0},{videoInfo.CropRect.Top:0},");
                    sb.Append($"{videoInfo.CropRect.Width:0},{videoInfo.CropRect.Height:0})");
                    sb.AppendLine();

                    if (useStereo)
                    {
                        stereoVar = "CroppedVideoRight";
                    }
                }
            }

            // Side-By-Side stereo encoding
            if (!string.IsNullOrEmpty(stereoVar))
            {
                switch(stereoEncoding)
                {
                    case StereoEncoding.FullSideBySideLeft:
                    case StereoEncoding.HalfSideBySideLeft:
                        sb.AppendLine($"StackHorizontal(last,{stereoVar})");
                        break;
                    case StereoEncoding.FullSideBySideRight:
                    case StereoEncoding.HalfSideBySideRight:
                        sb.AppendLine($"StackHorizontal({stereoVar},last)");
                        break;
                }
                sb.AppendLine("ConvertToYV12()");
            }

            var calculatedHeight = videoInfo.Height;
            var calculatedWidth = videoInfo.Width;

            var borderRight = 0;
            var borderLeft = 0;
            var borderBottom = 0;
            var borderTop = 0;
            var addBorders = false;

            // video resizing
            if (!resizeTo.IsEmpty && (resizeTo.Height != videoInfo.Height || resizeTo.Width != videoInfo.Width) && !skipScaling)
            {
                // aspect ratios

                var toAr = (float) Math.Round(resizeTo.Width / (float)resizeTo.Height, 3);
                var fromAr = videoInfo.AspectRatio;
                var mod = 1f;

                calculatedWidth = resizeTo.Width;

                if (fromAr > toAr) // source aspectratio higher than target aspectratio
                {
                    if (isDvdResolution)
                    {
                        calculatedHeight = (int)(calculatedWidth / fromAr);
                        if (calculatedHeight > resizeTo.Height)
                            calculatedHeight = resizeTo.Height;
                        calculatedWidth = 720;
                    }
                    else
                    {
                        calculatedWidth = resizeTo.Width;
                        calculatedHeight = (int)(calculatedWidth / fromAr);
                    }

                    int temp;

                    Math.DivRem(calculatedWidth, 2, out temp);
                    calculatedWidth += temp;
                    Math.DivRem(calculatedHeight, 2, out temp);
                    calculatedHeight += temp;

                    if (calculatedHeight != resizeTo.Height)
                    {
                        addBorders = true;
                        var borderHeight = resizeTo.Height - calculatedHeight;
                        borderTop = borderHeight/2;
                        Math.DivRem(borderTop, 2, out temp);
                        borderTop += temp;
                        borderBottom = borderHeight - borderTop;
                    }
                }
                else if (Math.Abs(fromAr - toAr) <= 0)  // source and target aspectratio equals
                {
                    if (isDvdResolution)
                    {
                        calculatedHeight = (int)(calculatedWidth / fromAr);
                        calculatedWidth = 720;
                        if (calculatedHeight > resizeTo.Height)
                            calculatedHeight = resizeTo.Height;
                    }
                    else
                    {
                        calculatedWidth = resizeTo.Width;
                        calculatedHeight = (int) (calculatedWidth/toAr);
                    }

                    int temp;

                    Math.DivRem(calculatedWidth, 2, out temp);
                    calculatedWidth += temp;
                    Math.DivRem(calculatedHeight, 2, out temp);
                    calculatedHeight += temp;

                    if (calculatedHeight != resizeTo.Height)
                    {
                        addBorders = true;
                        var borderHeight = resizeTo.Height - calculatedHeight;
                        borderTop = borderHeight/2;
                        Math.DivRem(borderTop, 2, out temp);
                        borderTop += temp;
                        borderBottom = borderHeight - borderTop;
                    }
                }
                else
                {
                    if (fromAr > 1.4f && isDvdResolution)  // source aspectratio not 4:3, encoding for dvd resolution
                    {
                        mod = 720f/resizeTo.Width;

                        calculatedHeight = (int)(calculatedWidth / fromAr);
                        if (calculatedHeight > resizeTo.Height)
                        {
                            calculatedHeight = resizeTo.Height;
                            calculatedWidth = (int)(calculatedHeight * fromAr * mod);
                        }
                        else
                            calculatedWidth = 720;
                    }
                    else if (isDvdResolution)
                    {
                        calculatedHeight = resizeTo.Height;
                        calculatedWidth = (int) (calculatedHeight*fromAr);
                    }
                    else
                        calculatedHeight = resizeTo.Height;

                    int temp;
                    Math.DivRem(calculatedWidth, 2, out temp);
                    calculatedWidth += temp;
                    Math.DivRem(calculatedHeight, 2, out temp);
                    calculatedHeight += temp;

                    if (Math.Abs(toAr - 1.778f) <= 0)     // aspectratio 16:9
                    {
                        addBorders = true;
                        var borderHeight = resizeTo.Height - calculatedHeight;
                        borderTop = borderHeight/2;
                        Math.DivRem(borderTop, 2, out temp);
                        borderTop += temp;
                        borderBottom = borderHeight - borderTop;

                        var borderWidth = (int) ((resizeTo.Width*mod) - calculatedWidth);
                        borderLeft = borderWidth/2;
                        Math.DivRem(borderLeft, 2, out temp);
                        borderLeft += temp;
                        borderRight = borderWidth - borderLeft;
                    }
                    else if (calculatedWidth != resizeTo.Width)
                    {
                        addBorders = true;
                        var borderWidth = resizeTo.Width - calculatedWidth;
                        borderLeft = borderWidth/2;
                        Math.DivRem(borderLeft, 2, out temp);
                        borderLeft += temp;
                        borderRight = borderWidth - borderLeft;

                        var borderHeight = resizeTo.Height - calculatedHeight;
                        borderTop = borderHeight/2;
                        Math.DivRem(borderTop, 2, out temp);
                        borderTop += temp;
                        borderBottom = borderHeight - borderTop;
                    }
                }
            }

            // apply resize filter
            if (calculatedHeight != videoInfo.Height || calculatedWidth != videoInfo.Width ||
                (stereoEncoding == StereoEncoding.HalfSideBySideLeft ||
                 stereoEncoding == StereoEncoding.HalfSideBySideRight
                && useStereo) && !skipScaling)
            {
                if (calculatedHeight < videoInfo.Height || calculatedWidth < videoInfo.Width ||
                    (stereoEncoding == StereoEncoding.HalfSideBySideLeft ||
                     stereoEncoding == StereoEncoding.HalfSideBySideRight
                     && useStereo))
                    sb.Append("BicubicResize");
                else
                    sb.Append("Lanczos4Resize");

                sb.Append($"({calculatedWidth:0},{calculatedHeight:0})");
                sb.AppendLine();
            }

            // add borders if needed
            if (addBorders && (borderLeft > 0 || borderRight > 0 || borderTop > 0 || borderBottom > 0) && !skipScaling)
                sb.AppendLine($"AddBorders({borderLeft:0},{borderTop:0},{borderRight:0},{borderBottom:0})");

            // change framerate
            if (changeFps)
            {
                int fpsnum;
                int fpsden;

                // get framerate numerator & denominator for target framerate
                VideoHelper.GetFpsNumDenom(targetFps, out fpsnum, out fpsden);

                // source is 23.976 or 24 fps
                if (videoInfo.FrameRateEnumerator == 24000 && (videoInfo.FrameRateDenominator == 1001 || videoInfo.FrameRateDenominator == 1000))
                {
                    if (fpsnum == 30000 && fpsden == 1001)
                    {
                        // 3:2 pulldown / telecine
                        sb.AppendLine("AssumeFrameBased()");
                        sb.AppendLine("SeparateFields()");
                        sb.AppendLine("SelectEvery(8, 0, 1, 2, 3, 2, 5, 4, 7, 6, 7)");
                        sb.AppendLine("Weave()");
                    }
                    else if (fpsnum == 25000 && fpsden == 1000)
                    {
                        // convert to 25 fps
                        sb.AppendLine("ConvertToYUY2()");
                        sb.AppendLine("ConvertFPS(50)");
                        sb.AppendLine("AssumeTFF()");
                        sb.AppendLine("SeparateFields()");
                        sb.AppendLine("SelectEvery(4,0,3)");
                        sb.AppendLine("Weave()");
                        sb.AppendLine("ConvertToYV12()");
                    }
                }
                // source is 30fps
                else if (videoInfo.FrameRateEnumerator == 30000)
                {
                    sb.AppendLine("ConvertToYUY2()");
                    sb.AppendLine("DoubleWeave()");
                    sb.AppendLine($"ConvertFPS(numerator={fpsnum * 2:0},denominator={fpsden:0})");
                    sb.AppendLine("SelectEven()");
                    sb.AppendLine("ConvertToYV12()");
                }
                // source is 25fps
                else if (videoInfo.FrameRateEnumerator == 25000 && videoInfo.FrameRateDenominator == 1000)
                {
                    if ((fpsnum == 30000 || fpsnum == 24000) && fpsden == 1001)
                    {
                        sb.AppendLine("ConvertToYUY2()");
                        sb.AppendLine("ConvertFPS(numerator=48000,denominator=1001");
                        if (fpsnum == 30000)
                        {
                            sb.AppendLine("AssumeFrameBased()");
                            sb.AppendLine("SeparateFields()");
                            sb.AppendLine("SelectEvery(8, 0, 1, 2, 3, 2, 5, 4, 7, 6, 7)");
                        }
                        else
                        {
                            sb.AppendLine("AssumeTFF()");
                            sb.AppendLine("SeparateFields()");
                            sb.AppendLine("SelectEven()");
                        }
                        sb.AppendLine("Weave()");
                        sb.AppendLine("ConvertToYV12()");
                    }
                }
                // every other framerate
                else
                {
                    // very slow framerate interpolation
                    sb.AppendLine("super = MSuper(pel=2)");
                    sb.AppendLine("backward_vec = MAnalyse(super, overlap=4, isb = true, search=3)");
                    sb.AppendLine("forward_vec = MAnalyse(super, overlap=4, isb = false, search=3)");
                    sb.Append($"MFlowFps(super, backward_vec, forward_vec, num={fpsnum:0}, den={fpsden:0})");
                }

                sb.AppendLine();
            }

            // multithreaded avisynth
            if (!_appConfig.UseAviSynthMT || !mtUseful) return WriteScript(sb.ToString());

            sb.AppendLine("SetMTMode(1)");
            sb.AppendLine("GetMTMode(false) > 0 ? distributor() : last");

            return WriteScript(sb.ToString());
        }

        /// <summary>
        /// Generates configuration file used by H264StereoSource plugin
        /// </summary>
        /// <param name="stereoVideoInfo"></param>
        /// <returns></returns>
        private static string GenerateStereoSourceConfig(StereoVideoInfo stereoVideoInfo)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"InputFile = \"{stereoVideoInfo.LeftTempFile}\"");
            sb.AppendLine($"InputFile2 = \"{stereoVideoInfo.RightTempFile}\"");
            sb.AppendLine("FileFormat = 0");
            sb.AppendLine("POCScale = 1");
            sb.AppendLine("DisplayDecParams = 1");
            sb.AppendLine("ConcealMode = 0");
            sb.AppendLine("RefPOCGap = 2");
            sb.AppendLine("POCGap = 2");
            sb.AppendLine("IntraProfileDeblocking = 1");
            sb.AppendLine("DecFrmNum = 0");

            return (WriteScript(sb.ToString(), "cfg"));
        }

        /// <summary>
        /// Creates AviSynth script used to determine black borders for cropping
        /// </summary>
        /// <param name="inputFile">Path to source file</param>
        /// <param name="targetFps">Sets framerate of the source file</param>
        /// <param name="streamLength">Sets duration of the source file, in seconds</param>
        /// <param name="videoSize"></param>
        /// <param name="aspectRatio"></param>
        /// <param name="frameCount">Calculated amount of frames</param>
        /// <returns>Path to AviSynth script</returns>
        public string GenerateCropDetect(string inputFile, float targetFps, double streamLength, Size videoSize,
                                         float aspectRatio, out int frameCount)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"LoadPlugin(\"{Path.Combine(_appConfig.AvsPluginsPath, "ffms2.dll")}\")");

            int fpsnum;
            int fpsden;
            VideoHelper.GetFpsNumDenom(targetFps, out fpsnum, out fpsden);

            if (fpsnum == 0)
            {
                fpsnum = (int)Math.Round(targetFps) * 1000;
                fpsden = (int)(Math.Round(Math.Ceiling(targetFps) - Math.Floor(targetFps)) + 1000);
            }

            sb.AppendLine($"inStream=FFVideoSource(\"{inputFile}\",fpsnum={fpsnum:0},fpsden={fpsden:0},threads=1)");

            var randomList = new List<int>();

            var rand = new Random();
            for (var i = 0; i < 5; i++)
                randomList.Add(rand.Next((int) Math.Round(streamLength*targetFps, 0)));

            randomList.Sort();

            frameCount = 0;

            var frameList = new List<string>();
            foreach (var frame in randomList)
            {
                var endFrame = frame + (int)Math.Round(targetFps * 5f, 0);
                sb.AppendLine($"Frame{frame:0}=inStream.Trim({frame:0},{endFrame:0})");
                frameList.Add("Frame" + frame.ToString(CInfo));
                frameCount += (endFrame - frame);
            }

            var concString = "combined=" + string.Join("+", frameList);

            sb.AppendLine(concString);

            if (videoSize.Height*aspectRatio > videoSize.Width)
            {
                videoSize.Width = (int)(videoSize.Height * aspectRatio);
                int temp;
                Math.DivRem(videoSize.Width, 2, out temp);
                videoSize.Width += temp;
            }

            sb.AppendLine($"BicubicResize(combined,{videoSize.Width:0},{videoSize.Height:0})");

            return WriteScript(sb.ToString());
        }

        /// <summary>
        /// Generates simple script used to check whether AviSynth is installed on system
        /// </summary>
        /// <returns>Path to AviSynth script</returns>
        public string GenerateTestFile()
        {
            return WriteScript("Version()");
        }

        /// <summary>
        /// Writes script content to file and returns the path of written file
        /// </summary>
        /// <param name="script">Script content</param>
        /// <param name="extension">File extension of the file, default "avs"</param>
        /// <returns>Path of written file</returns>
        private static string WriteScript(string script, string extension = "avs")
        {
            Log.Info($"Writing AviSynth script: {Environment.NewLine}{script}");

            var avsFile = FileSystemHelper.CreateTempFile(_appConfig.DemuxLocation, extension);
            using (var sw = new StreamWriter(avsFile, false, Encoding.ASCII))
                sw.WriteLine(script);

            return avsFile;
        }

        /// <summary>
        /// Generates AviSynth script used for audio encoding
        /// </summary>
        /// <param name="inputFile">Path to input file</param>
        /// <param name="inFormat">Format of input file</param>
        /// <param name="inFormatProfile">Format profile of input file</param>
        /// <param name="inChannels">Channel count of input file</param>
        /// <param name="outChannels">Target channel count</param>
        /// <param name="inSampleRate">Samplerate of input file</param>
        /// <param name="outSampleRate">Target samplerate</param>
        /// <returns>Path to AviSynth script</returns>
        public string GenerateAudioScript(string inputFile, string inFormat, string inFormatProfile, 
                                          int inChannels, int outChannels, int inSampleRate, 
                                          int outSampleRate)
        {
            var sb = new StringBuilder();

            var ext = StreamFormat.GetFormatExtension(inFormat, inFormatProfile, false);

            sb.AppendLine($"LoadPlugin(\"{Path.Combine(_appConfig.AvsPluginsPath, "ffms2.dll")}\")");
            sb.AppendLine($"FFAudioSource(\"{inputFile}\")");

            if (inChannels > outChannels && outChannels > 0)
            {
                sb.AppendLine($"Import(\"{Path.Combine(_appConfig.AvsPluginsPath, "audio", "ChannelDownMix.avsi")}\")");

                switch (inChannels)
                {
                    case 3:
                        switch (outChannels)
                        {
                            case 2:
                                sb.AppendLine("Dmix3Stereo()");
                                break;
                            case 4:
                            case 3:
                                sb.AppendLine("Dmix3Dpl()");
                                break;
                            case 1:
                                sb.AppendLine("ConvertToMono()");
                                break;
                        }
                        break;
                    case 4:
                        switch (outChannels)
                        {
                            case 2:
                                sb.AppendLine("Dmix4qStereo()");
                                break;
                            case 3:
                                sb.AppendLine("Dmix4qDpl()");
                                break;
                            case 4:
                                sb.AppendLine("Dmix4qDpl2()");
                                break;
                            case 1:
                                sb.AppendLine("ConvertToMono()");
                                break;
                        }
                        break;
                    case 5:
                        switch (outChannels)
                        {
                            case 2:
                                sb.AppendLine("Dmix5Stereo()");
                                break;
                            case 3:
                                sb.AppendLine("Dmix5Dpl()");
                                break;
                            case 4:
                                sb.AppendLine("Dmix5Dpl2()");
                                break;
                            case 1:
                                sb.AppendLine("ConvertToMono()");
                                break;
                        }
                        break;
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        switch (outChannels)
                        {
                            case 2:
                                sb.AppendLine("Dmix6StereoLfe()");
                                break;
                            case 3:
                                sb.AppendLine("Dmix6DplLfe()");
                                break;
                            case 4:
                                sb.AppendLine("Dmix6Dpl2Lfe()");
                                break;
                            case 1:
                                sb.AppendLine("ConvertToMono()");
                                break;
                            case 6:
                                sb.AppendLine("GetChannel(1,2,3,4,5,6)");
                                break;
                        }
                        break;
                }

            }

            if (inSampleRate != outSampleRate && outSampleRate > 0)
            {
                sb.Append($"SSRC({outSampleRate},fast=False)");
                sb.AppendLine();
            }

            sb.AppendLine("return last");

            return WriteScript(sb.ToString());
        }

    }
}

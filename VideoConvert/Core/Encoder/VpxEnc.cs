﻿//============================================================================
// VideoConvert - Fast Video & Audio Conversion Tool
// Copyright © 2012 JT-Soft
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using VideoConvert.Core.CommandLine;
using VideoConvert.Core.Helpers;
using VideoConvert.Core.Profiles;
using log4net;

namespace VideoConvert.Core.Encoder
{
    class VpxEnc
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(VpxEnc));

        private EncodeInfo _jobInfo;
        private const string Executable = "vpxenc.exe";

        private long _frameCount;
        private DateTime _startTime;
        private TimeSpan _remaining = new TimeSpan(0, 0, 0);
        private string _pass = string.Empty;

        private readonly string _progressFormat = Processing.GetResourceString("vp8_encoding_progress");
        private readonly Regex _frameInformation = new Regex(@"^.*Pass\s\d\/\d frame \s*\d*\/(\d*).*$",
                                           RegexOptions.Singleline | RegexOptions.Multiline);
        private BackgroundWorker _bw;

        public void SetJob(EncodeInfo job)
        {
            _jobInfo = job;
        }

        public string GetVersionInfo()
        {
            return GetVersionInfo(AppSettings.ToolsPath);
        }

        public string GetVersionInfo(string encPath)
        {
            string verInfo = string.Empty;

            string localExecutable = Path.Combine(encPath, Executable);

            using (Process encoder = new Process())
            {
                ProcessStartInfo parameter = new ProcessStartInfo(localExecutable)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                encoder.StartInfo = parameter;

                bool started;
                try
                {
                    started = encoder.Start();
                }
                catch (Exception ex)
                {
                    started = false;
                    Log.ErrorFormat("vpxenc exception: {0}", ex);
                }

                if (started)
                {
                    string output = encoder.StandardError.ReadToEnd();
                    Regex regObj = new Regex(@".*VP8 Encoder (v[-.\w\d]*)", RegexOptions.Singleline | RegexOptions.Multiline);
                    Match result = regObj.Match(output);
                    if (result.Success)
                        verInfo = result.Groups[1].Value.Trim();

                    encoder.WaitForExit(10000);
                    if (!encoder.HasExited)
                        encoder.Kill();
                }
            }

            // Debug info
            if (Log.IsDebugEnabled)
                Log.DebugFormat("VpxEnc \"{0:s}\" found", verInfo);

            return verInfo;
        }

        public void DoEncode(object sender, DoWorkEventArgs e)
        {
            _bw = (BackgroundWorker)sender;

            string passStr = Processing.GetResourceString("vp8_pass");
            string status = Processing.GetResourceString("vp8_encoding_status");

            VP8Profile encProfile = (VP8Profile)_jobInfo.VideoProfile;

            if (!_jobInfo.EncodingProfile.Deinterlace && _jobInfo.VideoStream.Interlaced)
                _jobInfo.VideoStream.Interlaced = false;

            Size resizeTo = VideoHelper.GetTargetSize(_jobInfo);

            if (string.IsNullOrEmpty(_jobInfo.AviSynthScript))
                GenerateAviSynthScript(resizeTo);

            string inputFile = _jobInfo.AviSynthScript;
            string outFile =
                Processing.CreateTempFile(
                    string.IsNullOrEmpty(_jobInfo.TempOutput) ? _jobInfo.BaseName : _jobInfo.TempOutput, "encoded.webm");

            _frameCount = _jobInfo.VideoStream.FrameCount;

            int targetBitrate = 0;
            if (_jobInfo.EncodingProfile.TargetFileSize > 0)
                targetBitrate = Processing.CalculateVideoBitrate(_jobInfo);

            int encodeMode = encProfile.EncodingMode;
            if (encodeMode == 1)
                _pass = string.Format(" {1} {0:0}; ", _jobInfo.StreamId, passStr);

            _bw.ReportProgress(-10, status + _pass.Replace("; ", string.Empty));
            _bw.ReportProgress(0, status);

            string argument = VP8CommandLineGenerator.Generate(encProfile,
                                                                targetBitrate,
                                                                resizeTo.Width,
                                                                resizeTo.Height,
                                                                _jobInfo.StreamId,
                                                                _jobInfo.VideoStream.FrameRateEnumerator,
                                                                _jobInfo.VideoStream.FrameRateDenominator,
                                                                outFile);

            string localExecutable = Path.Combine(AppSettings.ToolsPath, Executable);

            using (Process encoder = new Process(),
                decoder = FfMpeg.GenerateDecodeProcess(inputFile,
                    AppSettings.Use64BitEncoders && AppSettings.UseFfmpegScaling,
                    new Size(_jobInfo.VideoStream.Width, _jobInfo.VideoStream.Height), 
                    _jobInfo.VideoStream.AspectRatio,
                    _jobInfo.VideoStream.CropRect, resizeTo))
            {
                ProcessStartInfo parameter = new ProcessStartInfo(localExecutable)
                {
                    WorkingDirectory = AppSettings.DemuxLocation,
                    Arguments = argument,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };
                encoder.StartInfo = parameter;

                encoder.ErrorDataReceived += OnDataReceived;

                Log.InfoFormat("start parameter: vpxenc {0:s}", argument);

                bool started;
                bool decStarted;
                try
                {
                    started = encoder.Start();
                }
                catch (Exception ex)
                {
                    started = false;
                    Log.ErrorFormat("vpxenc exception: {0}", ex);
                    _jobInfo.ExitCode = -1;
                }

                NamedPipeServerStream decodePipe = new NamedPipeServerStream(AppSettings.DecodeNamedPipeName,
                    PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.None);

                try
                {
                    decStarted = decoder.Start();
                }
                catch (Exception ex)
                {
                    decStarted = false;
                    Log.ErrorFormat("avconv exception: {0}", ex);
                    _jobInfo.ExitCode = -1;
                }

                _startTime = DateTime.Now;

                if (started && decStarted)
                {
                    encoder.PriorityClass = AppSettings.GetProcessPriority();
                    encoder.BeginErrorReadLine();
                    decoder.PriorityClass = AppSettings.GetProcessPriority();
                    decoder.BeginErrorReadLine();

                    Thread pipeReadThread = new Thread(() =>
                    {
                        try
                        {
                            ReadThreadStart(decodePipe, encoder);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    });
                    pipeReadThread.Start();
                    pipeReadThread.Priority = ThreadPriority.BelowNormal;
                    encoder.Exited += (o, args) => pipeReadThread.Abort();

                    while (!encoder.HasExited)
                    {
                        if (_bw.CancellationPending)
                        {
                            encoder.Kill();
                            decoder.Kill();
                        }
                        Thread.Sleep(200);
                    }

                    encoder.WaitForExit(10000);
                    encoder.CancelErrorRead();

                    if (decodePipe.IsConnected)
                        try
                        {
                            decodePipe.Disconnect();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }

                    try
                    {
                        decodePipe.Close();
                        decodePipe.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }

                    decoder.WaitForExit(10000);
                    decoder.CancelErrorRead();

                    _jobInfo.ExitCode = encoder.ExitCode;
                    Log.InfoFormat("Exit Code: {0:g}", _jobInfo.ExitCode);
                }
            }

            if (_jobInfo.ExitCode == 0)
            {
                if ((encProfile.EncodingMode == 1 && _jobInfo.StreamId == 2) ||
                    encProfile.EncodingMode == 0)
                {
                    _jobInfo.VideoStream.Encoded = true;
                    _jobInfo.VideoStream.IsRawStream = false;

                    _jobInfo.TempFiles.Add(_jobInfo.VideoStream.TempFile);
                    _jobInfo.VideoStream.TempFile = outFile;

                    try
                    {
                        _jobInfo.MediaInfo = Processing.GetMediaInfo(_jobInfo.VideoStream.TempFile);
                    }
                    catch (TimeoutException ex)
                    {
                        Log.Error(ex);
                    }
                    _jobInfo.VideoStream = VideoHelper.GetStreamInfo(_jobInfo.MediaInfo, _jobInfo.VideoStream, _jobInfo.EncodingProfile.OutFormat == OutputType.OutputBluRay);

                    string statsFile = Processing.CreateTempFile(outFile, "stats");
                    _jobInfo.TempFiles.Add(statsFile);
                    _jobInfo.TempFiles.Add(_jobInfo.AviSynthScript);
                    _jobInfo.TempFiles.Add(_jobInfo.FfIndexFile);
                    _jobInfo.TempFiles.Add(_jobInfo.AviSynthStereoConfig);
                }
            }

            _bw.ReportProgress(100);
            _jobInfo.CompletedStep = _jobInfo.NextStep;
            e.Result = _jobInfo;
        }

        private void OnDataReceived(object outputSender, DataReceivedEventArgs outputEvent)
        {
            string line = outputEvent.Data;

            if (string.IsNullOrEmpty(line)) return;

            Match result = _frameInformation.Match(line);

            TimeSpan eta = DateTime.Now.Subtract(_startTime);
            long secRemaining = 0;

            if (result.Success)
            {
                long current;
                Int64.TryParse(result.Groups[1].Value, NumberStyles.Number,
                    AppSettings.CInfo, out current);
                long framesRemaining = _frameCount - current;
                float fps = 0f;
                if (eta.Seconds != 0)
                {
                    //Frames per Second
                    double codingFPS = Math.Round(current / eta.TotalSeconds, 2);

                    if (codingFPS > 1)
                    {
                        secRemaining = framesRemaining / (int)codingFPS;
                        fps = (float)codingFPS;
                    }
                    else
                        secRemaining = 0;
                }

                if (secRemaining > 0)
                    _remaining = new TimeSpan(0, 0, (int)secRemaining);

                DateTime ticks = new DateTime(eta.Ticks);

                string progress = string.Format(_progressFormat,
                    current, _frameCount,
                    fps,
                    _remaining, ticks, _pass);
                _bw.ReportProgress((int)(((float)current / _frameCount) * 100),
                    progress);
            }
            else
            {
                Log.InfoFormat("vpxenc: {0:s}", line);
            }
        }

        private void ReadThreadStart(NamedPipeServerStream decodePipe, Process encoder)
        {
            const int bufSize = 1024 * 1024;
            byte[] buffer = new byte[bufSize];

            decodePipe.WaitForConnection();
            while (decodePipe.IsConnected && !encoder.HasExited)
            {
                int readCnt = decodePipe.Read(buffer, 0, bufSize);
                encoder.StandardInput.BaseStream.Write(buffer, 0, readCnt);
            }
            encoder.StandardInput.BaseStream.Close();
        }

        private void GenerateAviSynthScript(Size resizeTo)
        {
            SubtitleInfo sub = _jobInfo.SubtitleStreams.FirstOrDefault(item => item.HardSubIntoVideo);
            string subFile = string.Empty;
            bool keepOnlyForced = false;
            if (sub != null)
            {
                subFile = sub.TempFile;
                keepOnlyForced = sub.KeepOnlyForcedCaptions;
            }
            _jobInfo.AviSynthScript = AviSynthGenerator.Generate(_jobInfo.VideoStream,
                                                                    false,
                                                                    0f,
                                                                    resizeTo, 
                                                                    _jobInfo.EncodingProfile.StereoType,
                                                                    _jobInfo.StereoVideoStream,
                                                                    false,
                                                                    subFile,
                                                                    keepOnlyForced,
                                                                    AppSettings.Use64BitEncoders && AppSettings.UseFfmpegScaling);
            if (!string.IsNullOrEmpty(AviSynthGenerator.StereoConfigFile))
                _jobInfo.AviSynthStereoConfig = AviSynthGenerator.StereoConfigFile;
        }
    }
}
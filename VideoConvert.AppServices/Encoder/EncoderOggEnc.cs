﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncoderOggEnc.cs" company="JT-Soft (https://github.com/UniqProject/VideoConvert)">
//   This file is part of the VideoConvert.AppServices source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   The EncoderOggEnc
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace VideoConvert.AppServices.Encoder
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using log4net;
    using VideoConvert.AppServices.Decoder;
    using VideoConvert.AppServices.Encoder.Interfaces;
    using VideoConvert.AppServices.Services.Base;
    using VideoConvert.AppServices.Services.Interfaces;
    using VideoConvert.AppServices.Utilities;
    using VideoConvert.Interop.EventArgs;
    using VideoConvert.Interop.Model;
    using VideoConvert.Interop.Model.Profiles;
    using VideoConvert.Interop.Utilities;

    /// <summary>
    /// The EncoderOggEnc
    /// </summary>
    public class EncoderOggEnc : EncodeBase, IEncoderOggEnc
    {
        /// <summary>
        /// Errorlog
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof (EncoderOggEnc));

        #region Private Variables

        private readonly IAppConfigService _appConfig;

        /// <summary>
        /// Gets the Encoder Process ID
        /// </summary>
        private int _encoderProcessId;

        /// <summary>
        /// Get the Decoder Process ID
        /// </summary>
        private int _decoderProcessId;

        private NamedPipeServerStream _encodePipe;
        private IAsyncResult _encodePipeState;
        private Thread _pipeReadThread;

        /// <summary>
        /// Start time of the current Encode;
        /// </summary>
        private DateTime _startTime;

        /// <summary>
        /// The Current Task
        /// </summary>
        private EncodeInfo _currentTask;

        private string _inputFile;
        private string _outputFile;

        private AudioInfo _audio;

        private readonly Regex _encObj = new Regex(@"^.*Encoding\s*?\[.*\].*$",
            RegexOptions.Singleline | RegexOptions.Multiline);

        private readonly Regex _pipeObj = new Regex(@"^([\d\,\.]*?)%.*$",
            RegexOptions.Singleline | RegexOptions.Multiline);

        private bool _dataWriteStarted;
        private bool _decoderIsRunning;
        private bool _encoderIsRunning;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EncoderOggEnc"/> class.
        /// </summary>
        /// <param name="appConfig">
        /// The user Setting Service.
        /// </param>
        public EncoderOggEnc(IAppConfigService appConfig) : base(appConfig)
        {
            _appConfig = appConfig;
        }

        #region Properties

        /// <summary>
        /// Gets or sets the OggEnc Process
        /// </summary>
        protected Process EncodeProcess { get; set; }

        /// <summary>
        /// Gets or sets the BePipe decoding Process
        /// </summary>
        protected Process DecodeProcess { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads encoder version from its output, use path settings from parameters
        /// </summary>
        /// <param name="encPath">Path to encoder</param>
        /// <param name="optimized"></param>
        /// <param name="appConfig"></param>
        /// <returns>Encoder version</returns>
        public static string GetVersionInfo(string encPath, bool optimized, IAppConfigService appConfig)
        {
            var verInfo = string.Empty;

            var localExecutable = Path.Combine(encPath, BuildExecutable(optimized, appConfig));

            using (var encoder = new Process())
            {
                var parameter = new ProcessStartInfo(localExecutable)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = "-h"
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
                    Log.Error($"OggEnc exception: {ex}");
                }

                if (started)
                {
                    var output = encoder.StandardOutput.ReadToEnd();
                    const string regexNonOptimized = @"^OggEnc\s+?v([\w\d\.\(\)\s]+?)$";
                    const string regexOptimized = @"^OggEnc\s+?v([\d\.]*?)\s+?\(.+?based on (.+?)\s+?\[.+?\]\).*?$";

                    var regObj = new Regex(regexNonOptimized, RegexOptions.Singleline | RegexOptions.Multiline);
                    var result = regObj.Match(output);
                    if (result.Success)
                        verInfo = result.Groups[1].Value.Trim();
                    else
                    {
                        regObj = new Regex(regexOptimized, RegexOptions.Singleline | RegexOptions.Multiline);
                        result = regObj.Match(output);
                        if (result.Success)
                            verInfo = $"{result.Groups[1].Value} ({result.Groups[2].Value})";
                    }

                    encoder.WaitForExit(10000);
                    if (!encoder.HasExited)
                        encoder.Kill();
                }
            }

            // Debug info
            if (!Log.IsDebugEnabled) return verInfo;

            if (optimized)
                Log.Debug("Optimized encoder");
            Log.Debug($"OggEnc \"{verInfo}\" found");

            return verInfo;
        }

        /// <summary>
        /// Starts encoding process with given Encode Job
        /// </summary>
        /// <param name="encodeQueueTask">Job to encode</param>
        public override void Start(EncodeInfo encodeQueueTask)
        {
            try
            {
                if (IsEncoding)
                {
                    encodeQueueTask.ExitCode = -1;
                    throw new Exception("OggEnc is already running");
                }

                IsEncoding = true;
                _currentTask = encodeQueueTask;

                var query = GenerateCommandLine();
                var cliPath = Path.Combine(_appConfig.ToolsPath, BuildExecutable(_appConfig.UseOptimizedEncoders, _appConfig));

                var cliStart = new ProcessStartInfo(cliPath, query)
                {
                    WorkingDirectory = _appConfig.DemuxLocation,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                EncodeProcess = new Process {StartInfo = cliStart};
                Log.Info($"start parameter: OggEnc {query}");

                DecodeProcess = DecoderBePipe.CreateDecodingProcess(_inputFile, _appConfig.AvsPluginsPath);

                _encodePipe = new NamedPipeServerStream(_appConfig.EncodeNamedPipeName,
                                                             PipeDirection.InOut,
                                                             3,
                                                             PipeTransmissionMode.Byte,
                                                             PipeOptions.Asynchronous);

                _encodePipeState = _encodePipe.BeginWaitForConnection(EncoderConnected, null);

                EncodeProcess.Start();
                DecodeProcess.Start();

                _startTime = DateTime.Now;

                EncodeProcess.ErrorDataReceived += EncodeProcessDataReceived;
                EncodeProcess.BeginErrorReadLine();

                DecodeProcess.ErrorDataReceived += DecodeProcessDataReceived;
                DecodeProcess.BeginErrorReadLine();

                _encoderProcessId = EncodeProcess.Id;
                _decoderProcessId = DecodeProcess.Id;

                if (_encoderProcessId != -1)
                {
                    EncodeProcess.EnableRaisingEvents = true;
                    EncodeProcess.Exited += EncodeProcessExited;
                    _encoderIsRunning = true;
                }

                if (_decoderProcessId != -1)
                {
                    DecodeProcess.EnableRaisingEvents = true;
                    DecodeProcess.Exited += DecodeProcessExited;
                    _decoderIsRunning = true;
                }

                EncodeProcess.PriorityClass = _appConfig.GetProcessPriority();
                DecodeProcess.PriorityClass = _appConfig.GetProcessPriority();

                // Fire the Encode Started Event
                InvokeEncodeStarted(EventArgs.Empty);
            }
            catch (Exception exc)
            {
                Log.Error(exc);
                _currentTask.ExitCode = -1;
                IsEncoding = false;
                _encoderIsRunning = false;
                _decoderIsRunning = false;
                InvokeEncodeCompleted(new EncodeCompletedEventArgs(false, exc, exc.Message));
            }
        }

        /// <summary>
        /// Kill the CLI process
        /// </summary>
        public override void Stop()
        {
            try
            {
                if (EncodeProcess != null && !EncodeProcess.HasExited)
                {
                    _encoderIsRunning = false;
                    Thread.Sleep(200);
                    EncodeProcess.Kill();
                }
                if (DecodeProcess != null && !DecodeProcess.HasExited)
                {
                    _decoderIsRunning = false;
                    Thread.Sleep(200);
                    DecodeProcess.Kill();
                }
            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }
            IsEncoding = false;
        }

        /// <summary>
        /// Shutdown the service.
        /// </summary>
        public void Shutdown()
        {
            // Nothing to do.
        }

        #endregion

        #region Private Helper Methods

        private void DecodeProcessExited(object sender, EventArgs e)
        {
            if (_encodePipe != null)
            {
                try
                {
                    if (!_encodePipeState.IsCompleted)
                        _encodePipe.EndWaitForConnection(_encodePipeState);
                }
                catch (Exception exc)
                {
                    Log.Error(exc);
                }
            }

            _decoderIsRunning = false;
        }

        /// <summary>
        /// The OggEnc process has exited.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The EventArgs.
        /// </param>
        private void EncodeProcessExited(object sender, EventArgs e)
        {
            if (_encodePipe != null)
            {
                try
                {
                    if (!_encodePipeState.IsCompleted)
                        _encodePipe.EndWaitForConnection(_encodePipeState);
                }
                catch (Exception exc)
                {
                    Log.Error(exc);
                }
            }

            try
            {
                EncodeProcess.CancelErrorRead();
            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }

            _encoderIsRunning = false;

            _currentTask.ExitCode = EncodeProcess.ExitCode;
            Log.Info($"Exit Code: {_currentTask.ExitCode:0}");

            if (_currentTask.ExitCode == 0)
            {
                _currentTask.TempFiles.Add(_inputFile);
                _currentTask.TempFiles.Add(_audio.TempFile);
                _currentTask.TempFiles.Add(_audio.TempFile + ".d2a");
                _currentTask.TempFiles.Add(_audio.TempFile + ".ffindex");
                _audio.TempFile = _outputFile;
                AudioHelper.GetStreamInfo(_audio);
            }

            _currentTask.CompletedStep = _currentTask.NextStep;
            IsEncoding = false;
            InvokeEncodeCompleted(new EncodeCompletedEventArgs(true, null, string.Empty));
        }

        private void DecodeProcessDataReceived(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (string.IsNullOrEmpty(line) || !IsEncoding) return;

            if (line.Contains("Writing Data..."))
                _dataWriteStarted = true;

            var bePipeMatch = _pipeObj.Match(line);
            if (bePipeMatch.Success)
            {
                float progress;
                var tempProgress = bePipeMatch.Groups[1].Value.Replace(",", ".");
                float.TryParse(tempProgress, NumberStyles.Number, _appConfig.CInfo, out progress);

                var progressRemaining = 100f - progress;
                var elapsedTime = DateTime.Now - _startTime;

                long secRemaining = 0;
                if (elapsedTime.TotalSeconds > 0)
                {
                    var speed = Math.Round(progress / elapsedTime.TotalSeconds, 6);

                    if (speed > 0)
                        secRemaining = (long)Math.Round(progressRemaining / speed, 0);
                    else
                        secRemaining = 0;
                }
                if (secRemaining < 0)
                    secRemaining = 0;

                var remainingTime = TimeSpan.FromSeconds(secRemaining);

                var eventArgs = new EncodeProgressEventArgs
                {
                    AverageFrameRate = 0,
                    CurrentFrameRate = 0,
                    EstimatedTimeLeft = remainingTime,
                    PercentComplete = progress,
                    ElapsedTime = elapsedTime,
                };
                InvokeEncodeStatusChanged(eventArgs);
            }
            else
                Log.Info($"bepipe: {line}");
        }

        /// <summary>
        /// process received data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncodeProcessDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && IsEncoding)
            {
                ProcessLogMessage(e.Data);
            }
        }

        private void ProcessLogMessage(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            var result = _encObj.Match(line);
            if (!result.Success)
                Log.Info($"OggEnc: {line}");
        }

        private void EncoderConnected(IAsyncResult ar)
        {
            Log.Info("Encoder Pipe connected");
            lock (_encodePipe)
            {
                _encodePipe.EndWaitForConnection(ar);
            }

            _pipeReadThread = new Thread(PipeReadThreadStart);
            _pipeReadThread.Start();
            _pipeReadThread.Priority = _appConfig.GetThreadPriority();
        }

        private void PipeReadThreadStart()
        {
            try
            {
                if (DecodeProcess != null && EncodeProcess != null)
                    ReadThreadStart();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void ReadThreadStart()
        {
            try
            {
                // wait for decoder to start writing
                while (!_dataWriteStarted || !_decoderIsRunning || !_encoderIsRunning)
                {
                    Thread.Sleep(100);
                }

                var buffer = new byte[0xA00000]; // 10 MB

                var read = 0;
                do
                {
                    if (_decoderIsRunning)
                        read = DecodeProcess.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length);

                    if (_encoderIsRunning)
                        _encodePipe.Write(buffer, 0, read);

                } while (read > 0 && _decoderIsRunning && _encoderIsRunning);

                _encodePipe.Close();
            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }
        }

        private static string BuildExecutable(bool optimized, IAppConfigService appConfig)
        {
            var fName = "oggenc2";
            if (optimized)
            {
                if (appConfig.SupportedCpuExtensions.SSE3 == 1)
                {
                    fName += "_SSE3";
                    if (Environment.Is64BitOperatingSystem && appConfig.Use64BitEncoders)
                        fName += "_64";
                }
                else if (appConfig.SupportedCpuExtensions.SSE2 == 1)
                    fName += "_SSE2";
                else if (appConfig.SupportedCpuExtensions.SSE == 1)
                    fName += "_SSE";
            }
            fName += ".exe";

            return fName;
        }

        private string GenerateCommandLine()
        {
            var sb = new StringBuilder();

            _audio = _currentTask.AudioStreams[_currentTask.StreamId];

            var outChannels = ((OggProfile)_currentTask.AudioProfile).OutputChannels;
            switch (outChannels)
            {
                case 1:
                    outChannels = 2;
                    break;
                case 2:
                    outChannels = 1;
                    break;
            }
            var outSampleRate = ((OggProfile)_currentTask.AudioProfile).SampleRate;
            switch (outSampleRate)
            {
                case 1:
                    outSampleRate = 8000;
                    break;
                case 2:
                    outSampleRate = 11025;
                    break;
                case 3:
                    outSampleRate = 22050;
                    break;
                case 4:
                    outSampleRate = 44100;
                    break;
                case 5:
                    outSampleRate = 48000;
                    break;
                default:
                    outSampleRate = 0;
                    break;
            }

            var encMode = ((OggProfile)_currentTask.AudioProfile).EncodingMode;
            var bitrate = ((OggProfile)_currentTask.AudioProfile).Bitrate * 1000;
            var quality = ((OggProfile)_currentTask.AudioProfile).Quality;

            var avs = new AviSynthGenerator(_appConfig);

            _inputFile = avs.GenerateAudioScript(_audio.TempFile, _audio.Format, _audio.FormatProfile,
                                                      _audio.ChannelCount, outChannels, _audio.SampleRate,
                                                      outSampleRate);

            _outputFile = FileSystemHelper.CreateTempFile(_appConfig.DemuxLocation, _audio.TempFile, "encoded.ogg");

            if (encMode == 2)
                sb.Append($"-q {quality:0.00} ".ToString(_appConfig.CInfo));
            else
            {
                if (encMode == 1)
                    sb.Append("--managed ");
                sb.Append($"-b {bitrate:0} ");
            }

            sb.Append($"-o \"{_outputFile}\" --ignorelength \"{_appConfig.EncodeNamedPipeFullName}\" ");

            return sb.ToString();
        }

        #endregion
    }
}
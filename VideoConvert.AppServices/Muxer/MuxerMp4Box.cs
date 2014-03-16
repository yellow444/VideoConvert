﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MuxerMp4Box.cs" company="JT-Soft (https://github.com/UniqProject/VideoConvert)">
//   This file is part of the VideoConvert.AppServices source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   The MuxerMp4Box
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace VideoConvert.AppServices.Muxer
{
    using Interfaces;
    using Interop.EventArgs;
    using Interop.Model;
    using Interop.Utilities;
    using log4net;
    using Services;
    using Services.Base;
    using Services.Interfaces;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    /// <summary>
    /// The MuxerMp4Box
    /// </summary>
    public class MuxerMp4Box : EncodeBase, IMuxerMp4Box
    {
        /// <summary>
        /// Errorlog
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof (MuxerMp4Box));

        #region Private Variables

        private readonly IAppConfigService _appConfig;
        private const string Executable = "mp4box.exe";

        /// <summary>
        /// Gets the Encoder Process ID
        /// </summary>
        private int _encoderProcessId;

        /// <summary>
        /// Start time of the current Encode;
        /// </summary>
        private DateTime _startTime;

        /// <summary>
        /// The Current Task
        /// </summary>
        private EncodeInfo _currentTask;

        private string _outputFile;
        private int _lastImportPercent;
        private int _streamImportCount;
        private int _muxStepCount;
        private int _muxStep;
        private float _singleStep;

        private readonly Regex _importingRegex = new Regex(@"^Importing ([\w-\d\(\) ]*|ISO File): \|.+?\| \((\d+?)\/\d+?\)$",
                                                           RegexOptions.Singleline | RegexOptions.Multiline);
        private readonly Regex _progressRegex = new Regex(@"^ISO File Writing: \|.+?\| \((\d+?)\/\d+?\)$",
                                                        RegexOptions.Singleline | RegexOptions.Multiline);

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MuxerMp4Box"/> class.
        /// </summary>
        /// <param name="appConfig">
        /// The user Setting Service.
        /// </param>
        public MuxerMp4Box(IAppConfigService appConfig) : base(appConfig)
        {
            this._appConfig = appConfig;
        }

        #region Properties

        /// <summary>
        /// Gets or sets the mp4box Process
        /// </summary>
        protected Process EncodeProcess { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads encoder version from its output, use path settings from parameters
        /// </summary>
        /// <param name="encPath">Path to encoder</param>
        /// <returns>Encoder version</returns>
        public static string GetVersionInfo(string encPath)
        {
            var verInfo = string.Empty;

            var localExecutable = Path.Combine(encPath, Executable);

            using (var encoder = new Process())
            {
                var parameter = new ProcessStartInfo(localExecutable)
                {
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
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
                    Log.ErrorFormat("mp4box exception: {0}", ex);
                }

                if (started)
                {
                    var output = encoder.StandardOutput.ReadToEnd();
                    if (string.IsNullOrEmpty(output))
                        output = encoder.StandardError.ReadToEnd();
                    var regObj = new Regex(@"^MP4Box.+?version ([\d\w\.\-\ \(\)]+)\s*$",
                        RegexOptions.Singleline | RegexOptions.Multiline);
                    var result = regObj.Match(output);
                    if (result.Success)
                        verInfo = result.Groups[1].Value;

                    encoder.WaitForExit(10000);
                    if (!encoder.HasExited)
                        encoder.Kill();
                }
            }

            // Debug info
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("mp4box \"{0}\" found", verInfo);
            }
            return verInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encodeQueueTask"></param>
        /// <exception cref="Exception"></exception>
        public override void Start(EncodeInfo encodeQueueTask)
        {
            try
            {
                if (this.IsEncoding)
                {
                    encodeQueueTask.ExitCode = -1;
                    throw new Exception("mp4box is already running");
                }

                this.IsEncoding = true;
                this._currentTask = encodeQueueTask;
                
                this._lastImportPercent = 0;
                this._streamImportCount = 0;
                this._muxStep = 0;
                
                var query = GenerateCommandLine();

                this._muxStepCount = this._streamImportCount + 1;
                this._singleStep = 1f / this._muxStepCount;

                var cliPath = Path.Combine(this._appConfig.ToolsPath, Executable);

                var cliStart = new ProcessStartInfo(cliPath, query)
                {
                    WorkingDirectory = this._appConfig.DemuxLocation,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                this.EncodeProcess = new Process {StartInfo = cliStart};
                Log.InfoFormat("start parameter: mp4box {0}", query);

                this.EncodeProcess.Start();

                this._startTime = DateTime.Now;
                
                this.EncodeProcess.ErrorDataReceived += EncodeProcessDataReceived;
                this.EncodeProcess.BeginErrorReadLine();

                this.EncodeProcess.OutputDataReceived += EncodeProcessDataReceived;
                this.EncodeProcess.BeginOutputReadLine();

                this._encoderProcessId = this.EncodeProcess.Id;

                if (this._encoderProcessId != -1)
                {
                    this.EncodeProcess.EnableRaisingEvents = true;
                    this.EncodeProcess.Exited += EncodeProcessExited;
                }

                this.EncodeProcess.PriorityClass = this._appConfig.GetProcessPriority();

                // Fire the Encode Started Event
                this.InvokeEncodeStarted(EventArgs.Empty);
            }
            catch (Exception exc)
            {
                Log.Error(exc);
                this._currentTask.ExitCode = -1;
                this.IsEncoding = false;
                this.InvokeEncodeCompleted(new EncodeCompletedEventArgs(false, exc, exc.Message));
            }
        }

        /// <summary>
        /// Kill the CLI process
        /// </summary>
        public override void Stop()
        {
            try
            {
                if (this.EncodeProcess != null && !this.EncodeProcess.HasExited)
                {
                    this.EncodeProcess.Kill();
                }
            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }
            this.IsEncoding = false;
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

        private string GenerateCommandLine()
        {
            var sb = new StringBuilder();

            var fps = this._currentTask.VideoStream.Fps;
            int vidStream;

            const string chapterName = "Chapter {0:g}";

            var tempExt = Path.GetExtension(this._currentTask.VideoStream.TempFile);
            if (this._currentTask.VideoStream.IsRawStream)
                vidStream = 0;
            else if ((this._currentTask.Input == InputType.InputAvi) && (!this._currentTask.VideoStream.Encoded))
                vidStream = 0;
            else if ((this._currentTask.VideoStream.Encoded) || (tempExt == ".mp4"))
                vidStream = 0;
            else
                vidStream = this._currentTask.VideoStream.StreamId;

            _outputFile = !string.IsNullOrEmpty(this._currentTask.TempOutput)
                        ? this._currentTask.TempOutput
                        : this._currentTask.OutputFile;

            var fpsStr = string.Empty;
            if (this._currentTask.VideoStream.IsRawStream)
            {
                if (this._currentTask.VideoStream.FrameRateEnumerator == 0 || this._appConfig.LastMp4BoxVer.StartsWith("0.5"))
                    fpsStr = string.Format(this._appConfig.CInfo, ":fps={0:0.000}", fps);
                else
                    fpsStr = string.Format(":fps={0:g}/{1:g}",
                                           this._currentTask.VideoStream.FrameRateEnumerator,
                                           this._currentTask.VideoStream.FrameRateDenominator);
            }

            sb.AppendFormat(this._appConfig.CInfo,
                            "-add \"{0}#video:trackID={1:g}{2}:lang=eng\" -keep-sys ",
                            this._currentTask.VideoStream.TempFile, vidStream, fpsStr);

            this._streamImportCount = 1;

            foreach (var item in this._currentTask.AudioStreams)
            {
                var itemlang = item.LangCode;

                if ((itemlang == "xx") || (string.IsNullOrEmpty(itemlang)))
                    itemlang = "und";

                var delayString = string.Empty;

                if (item.Delay != 0)
                    delayString = string.Format(this._appConfig.CInfo, ":delay={0:#}", item.Delay);

                sb.AppendFormat(this._appConfig.CInfo,
                                "-add \"{0}#audio:lang={1:s}{2:s}\" -keep-sys ",
                                item.TempFile,
                                itemlang,
                                delayString);
                this._streamImportCount++;
            }

            foreach (var item in this._currentTask.SubtitleStreams)
            {
                if (item.Format.ToLowerInvariant() != "utf-8") continue;
                if (!File.Exists(item.TempFile)) continue;

                var itemlang = item.LangCode;

                if ((itemlang == "xx") || (string.IsNullOrEmpty(itemlang)))
                    itemlang = "und";

                var delayString = string.Empty;

                if (item.Delay != 0)
                    delayString = string.Format(this._appConfig.CInfo, ":delay={0:#}", item.Delay);

                sb.AppendFormat(this._appConfig.CInfo,
                                "-add \"{0}#lang={1:s}{2:s}:name={3:s}\" -keep-sys ",
                                item.TempFile,
                                itemlang,
                                delayString,
                                LanguageHelper.GetLanguage(itemlang).FullLang);
                this._streamImportCount++;
            }

            if (this._currentTask.Chapters.Count > 1)
            {
                var chapterFile = FileSystemHelper.CreateTempFile(
                                                        this._appConfig.DemuxLocation,
                                                        !string.IsNullOrEmpty(this._currentTask.TempOutput)
                                                            ? this._currentTask.TempOutput
                                                            : this._currentTask.OutputFile,
                                                        "chapters.ttxt");

                var xmlSettings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "    ",
                    Encoding = Encoding.UTF8,
                    NewLineHandling = NewLineHandling.Entitize,
                    ConformanceLevel = ConformanceLevel.Auto,
                    CloseOutput = true
                };
                var writer = XmlWriter.Create(chapterFile, xmlSettings);
                writer.WriteStartDocument(true);
                writer.WriteStartElement("TextStream");
                writer.WriteAttributeString("version", "1.1");

                int temp;
                var subHeight = Math.DivRem(this._currentTask.VideoStream.Height, 3, out temp);
                subHeight += temp;

                writer.WriteStartElement("TextStreamHeader");
                writer.WriteAttributeString("width", this._currentTask.VideoStream.Width.ToString("G"));
                writer.WriteAttributeString("height", subHeight.ToString("G"));
                writer.WriteAttributeString("layer", "0");
                writer.WriteAttributeString("translation_x", "0");
                writer.WriteAttributeString("translation_y", "0");

                writer.WriteStartElement("TextSampleDescription");
                writer.WriteAttributeString("horizontalJustification", "center");
                writer.WriteAttributeString("verticalJustification", "bottom");
                writer.WriteAttributeString("backColor", "0 0 0 0");
                writer.WriteAttributeString("verticalText", "no");
                writer.WriteAttributeString("fillTextRegion", "no");
                writer.WriteAttributeString("continousKaraoke", "no");
                writer.WriteAttributeString("scroll", "None");

                writer.WriteStartElement("FontTable");

                writer.WriteStartElement("FontTableEntry");
                writer.WriteAttributeString("fontName", "Arial");
                writer.WriteAttributeString("fontID", "1");
                writer.WriteEndElement(); // FontTableEntry

                writer.WriteEndElement(); // FontTable

                writer.WriteStartElement("TextBox");
                writer.WriteAttributeString("top", "0");
                writer.WriteAttributeString("left", "0");
                writer.WriteAttributeString("bottom", this._currentTask.VideoStream.Height.ToString("G"));
                writer.WriteAttributeString("right", this._currentTask.VideoStream.Width.ToString("G"));
                writer.WriteEndElement(); // TextBox

                writer.WriteStartElement("Style");
                writer.WriteAttributeString("styles", "Normal");
                writer.WriteAttributeString("fontID", "1");
                writer.WriteAttributeString("fontSize", "32");
                writer.WriteAttributeString("color", "ff ff ff ff");
                writer.WriteEndElement(); // Style

                writer.WriteEndElement(); // TextSampleDescription

                writer.WriteEndElement(); // TextStreamHeader

                for (var index = 0; index < this._currentTask.Chapters.Count; index++)
                {
                    var dt = DateTime.MinValue.Add(this._currentTask.Chapters[index]);
                    writer.WriteStartElement("TextSample");
                    writer.WriteAttributeString("sampleTime", dt.ToString("HH:mm:ss.fff"));
                    writer.WriteValue(string.Format(chapterName, index + 1));
                    writer.WriteEndElement(); // TextSample
                }

                writer.WriteEndElement(); // TextStream
                writer.WriteEndDocument();

                writer.Flush();
                writer.Close();

                sb.AppendFormat(" -add \"{0}:chap\"", chapterFile);
                this._currentTask.TempFiles.Add(chapterFile);
                this._streamImportCount++;
            }

            var tool = string.Format("{0} v{1}", AppConfigService.GetProductName(),
                                        AppConfigService.GetAppVersion().ToString(4));
            var tempPath = this._appConfig.DemuxLocation;

            sb.AppendFormat("-itags tool=\"{0}\" -tmp \"{1}\" -new \"{2}\"", tool, tempPath, _outputFile);

            return sb.ToString();
        }

        /// <summary>
        /// The mp4box process has exited.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The EventArgs.
        /// </param>
        private void EncodeProcessExited(object sender, EventArgs e)
        {
            try
            {
                this.EncodeProcess.CancelErrorRead();
                this.EncodeProcess.CancelOutputRead();
            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }

            this._currentTask.ExitCode = EncodeProcess.ExitCode;
            Log.InfoFormat("Exit Code: {0:g}", this._currentTask.ExitCode);

            if (this._currentTask.ExitCode == 0)
            {
                this._currentTask.TempFiles.Add(this._currentTask.VideoStream.TempFile);
                foreach (var stream in this._currentTask.AudioStreams)
                    this._currentTask.TempFiles.Add(stream.TempFile);
                foreach (var stream in this._currentTask.SubtitleStreams)
                    this._currentTask.TempFiles.Add(stream.TempFile);
            }

            this._currentTask.CompletedStep = this._currentTask.NextStep;
            this.IsEncoding = false;
            this.InvokeEncodeCompleted(new EncodeCompletedEventArgs(true, null, string.Empty));
        }

        /// <summary>
        /// process received data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncodeProcessDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && this.IsEncoding)
            {
                this.ProcessLogMessage(e.Data);
            }
        }

        private void ProcessLogMessage(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) return;

            var progressResult = _progressRegex.Match(line);
            var importResult = _importingRegex.Match(line);
            var elapsedTime = DateTime.Now - this._startTime;

            var progress = 0f;
            double processingSpeed = 0f;
            var secRemaining = 0;

            if (progressResult.Success)
            {
                int progressTmp;
                Int32.TryParse(progressResult.Groups[1].Value, NumberStyles.Number, this._appConfig.CInfo,
                               out progressTmp);
                progress = ((this._muxStepCount - 1)*100*this._singleStep) + (progressTmp*this._singleStep);
            }
            else if (importResult.Success)
            {
                int progressTmp;
                Int32.TryParse(importResult.Groups[2].Value, NumberStyles.Number, this._appConfig.CInfo,
                               out progressTmp);
                if (progressTmp < this._lastImportPercent)
                    this._muxStep++;
                this._lastImportPercent = progressTmp;

                progress = (this._muxStep * 100 * this._singleStep) + (progressTmp * this._singleStep);
            }
            else
                Log.InfoFormat("mp4box: {0}", line);

            if (progressResult.Success || importResult.Success)
            {
                if (elapsedTime.TotalSeconds > 0)
                    processingSpeed = progress / elapsedTime.TotalSeconds;

                if (processingSpeed > 0)
                    secRemaining = (int)Math.Round((100D - progress) / processingSpeed, MidpointRounding.ToEven);

                var remainingTime = new TimeSpan(0, 0, secRemaining);

                var eventArgs = new EncodeProgressEventArgs
                {
                    AverageFrameRate = 0,
                    CurrentFrameRate = 0,
                    EstimatedTimeLeft = remainingTime,
                    PercentComplete = progress,
                    ElapsedTime = elapsedTime,
                };
                this.InvokeEncodeStatusChanged(eventArgs);
            }
        }

        #endregion
    }
}
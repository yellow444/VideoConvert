﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamSelectViewModel.cs" company="JT-Soft (https://github.com/UniqProject/VideoConvert)">
//   This file is part of the VideoConvertWPF source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace VideoConvertWPF.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using BDInfoLib.BDROM;
    using Caliburn.Micro;
    using SharpDvdInfo;
    using SharpDvdInfo.DvdTypes;
    using VideoConvert.AppServices.Model.Profiles;
    using VideoConvert.AppServices.Services.Interfaces;
    using VideoConvert.Interop.Model;
    using VideoConvert.Interop.Model.MediaInfo;
    using VideoConvert.Interop.Model.Profiles;
    using VideoConvert.Interop.Utilities;
    using VideoConvertWPF.ViewModels.Interfaces;
    using ILog = log4net.ILog;
    using LogManager = log4net.LogManager;

    public class StreamSelectViewModel : ViewModelBase, IStreamSelectViewModel
    {
        private readonly IShellViewModel _shellViewModel;
        private static readonly ILog Log = LogManager.GetLogger(typeof(StreamSelectViewModel));

        #region private properties

        private EncodeInfo _jobInfo;
        private List<EncoderProfile> _profiles;
        private List<StreamTreeNode> _tree;

        private BDROM _bdInfo;
        private int _treeNodeID;
        private int _defaultSelection;
        private MovieEntry _resultMovieData;
        private EpisodeEntry _resultEpisodeData;
        private int _selectedIndex;
        private StreamTreeNode _selectedNode;
        private StreamTreeNode _selectedTitleInfo;
        private EncoderProfile _selectedProfile;
        private string _selectedProfileName;

        private readonly IAppConfigService _configService;
        private readonly IProcessingService _processingService;

        #endregion

        #region public properties

        public bool? DialogResult { get; set; }

        public EncodeInfo JobInfo
        {
            get
            {
                return _jobInfo;
            }
            set
            {
                _jobInfo = value;
                NotifyOfPropertyChange(()=>JobInfo);
            }
        }

        public string JobTitle
        {
            get
            {
                return JobInfo.JobName; 
            }
            set
            {
                JobInfo.JobName = value;
                NotifyOfPropertyChange(()=>JobTitle);
            }
        }

        public List<EncoderProfile> Profiles
        {
            get
            {
                return _profiles;
            }
            set
            {
                _profiles = value;
                NotifyOfPropertyChange(()=>Profiles);
            }
        }

        public EncoderProfile SelectedProfile
        {
            get
            {
                return _selectedProfile;
            }
            set
            {
                _selectedProfile = value;
                NotifyOfPropertyChange(()=>SelectedProfile);
            }
        }

        public string SelectedProfileName
        {
            get
            {
                return _configService.LastSelectedProfile; 
            }
            set { _selectedProfileName = value; }
        }

        public List<StreamTreeNode> Tree
        {
            get
            {
                return _tree;
            }
            set
            {
                _tree = value;
                NotifyOfPropertyChange(()=>Tree);
            }
        }

        public StreamTreeNode SelectedTitleInfo
        {
            get
            {
                return _selectedTitleInfo;
            }
            set
            {
                _selectedTitleInfo = value;
                NotifyOfPropertyChange(()=>SelectedTitleInfo);
            }
        }

        public BDROM BdRom
        {
            get
            {
                return _bdInfo;
            }
            set
            {
                _bdInfo = value;
                NotifyOfPropertyChange(()=>BdRom);
            }
        }

        public int TreeNodeID
        {
            get
            {
                return _treeNodeID;
            }
            set
            {
                _treeNodeID = value;
                NotifyOfPropertyChange(()=>TreeNodeID);
            }
        }

        public int DefaultSelection
        {
            get
            {
                return _defaultSelection;
            }
            set
            {
                _defaultSelection = value;
                NotifyOfPropertyChange(()=>DefaultSelection);
            }
        }

        public MovieEntry ResultMovieData
        {
            get
            {
                return _resultMovieData;
            }
            set
            {
                _resultMovieData = value;
                NotifyOfPropertyChange(()=>ResultMovieData);
            }
        }

        public EpisodeEntry ResultEpisodeData
        {
            get
            {
                return _resultEpisodeData;
            }
            set
            {
                _resultEpisodeData = value;
                NotifyOfPropertyChange(()=>ResultEpisodeData);
            }
        }

        public int SelectedIndex
        {
            get
            {
                return _selectedIndex;
            }
            set
            {
                _selectedIndex = value;
                NotifyOfPropertyChange(()=>SelectedIndex);
            }
        }

        public StreamTreeNode SelectedNode
        {
            get
            {
                return _selectedNode;
            }
            set
            {
                _selectedNode = value;
                NotifyOfPropertyChange(() => SelectedNode);
                NotifyOfPropertyChange(() => SelectedNodeData);
                NotifyOfPropertyChange(() => MatroskaDefault);
                NotifyOfPropertyChange(() => HardcodeIntoVideo);
                NotifyOfPropertyChange(() => KeepOnlyForced);
            }
        }

        public object SelectedNodeData => _selectedNode?.Data;

        public bool MatroskaDefault
        {
            get
            {
                return _selectedNode != null && _selectedNode.MatroskaDefault;
            }
            set
            {
                if (_selectedNode == null) return;
                _selectedNode.MatroskaDefault = value;
                NotifyOfPropertyChange(()=>MatroskaDefault);
            }
        }

        public bool HardcodeIntoVideo
        {
            get
            {
                return _selectedNode != null && _selectedNode.HardcodeIntoVideo;
            }
            set
            {
                if (_selectedNode == null) return;
                _selectedNode.HardcodeIntoVideo = value;
                NotifyOfPropertyChange(() => HardcodeIntoVideo);
            }
        }

        public bool KeepOnlyForced
        {
            get
            {
                return _selectedNode != null && _selectedNode.KeepOnlyForced;
            }
            set
            {
                if (_selectedNode == null) return;
                _selectedNode.KeepOnlyForced = value;
                NotifyOfPropertyChange(() => KeepOnlyForced);
            }
        }

        #endregion

        #region constructors

        public StreamSelectViewModel(IAppConfigService config, IShellViewModel shellViewModel,
            IWindowManager windowManager, IProcessingService processing)
        {
            _shellViewModel = shellViewModel;
            WindowManager = windowManager;
            _configService = config;
            _processingService = processing;
        }

        public override void OnLoad()
        {
            base.OnLoad();
            Tree = new List<StreamTreeNode>();
            Profiles = new List<EncoderProfile>();
            DefaultSelection = 0;
            LoadStreams();
        }

        public void ShowAbout()
        {
            
        }

        #endregion

        #region background stream info loading

        private void BgWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (JobInfo.Input == InputType.InputBluRay)
            {
                JobTitle = _bdInfo.VolumeLabel;
            }
            else if (JobInfo.Input != InputType.InputDvd)
            {
                JobTitle = JobInfo.MediaInfo.General.Title.Length > 0
                                     ? JobInfo.MediaInfo.General.Title
                                     : JobInfo.MediaInfo.General.FileName;
            }
            else
            {
                var dir = new DirectoryInfo(JobInfo.InputFile);
                JobTitle = GetVolumeLabel(dir);

                if (string.IsNullOrEmpty(JobTitle))
                {
                    JobTitle = Path.GetFileName(JobInfo.InputFile);
                }
            }

            NotifyOfPropertyChange(() => JobInfo);
            NotifyOfPropertyChange(() => Tree);
            SelectedIndex = DefaultSelection;
        }

        private void BgWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            switch (JobInfo.Input)
            {
                case InputType.InputUndefined:
                    DialogResult = false;
                    return;
                case InputType.InputBluRay:
                case InputType.InputAvchd:
                case InputType.InputHddvd:
                    GetBdInfo();
                    break;
                case InputType.InputDvd:
                    GetDvdTitleList();
                    break;
                default:
                    GetFileInfo();
                    break;
            }
        }

        #endregion

        #region background profile loading

        private void ProfilesWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            NotifyOfPropertyChange(()=>Profiles);
            NotifyOfPropertyChange(()=> SelectedProfileName);
        }

        private void ProfilesWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var profHandler = new ProfilesHandler(_configService);
            _profiles = profHandler.FilteredList.Where(p => p.Type == ProfileType.QuickSelect).ToList();
        }

        #endregion

        #region control and property events

        private void TreeNodePropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            var myStreamTree = sender as StreamTreeNode;
            if (myStreamTree == null) return;
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "IsChecked":
                    if (myStreamTree.Children != null && myStreamTree.Children.Count > 0)
                    {
                        CheckSubItems(myStreamTree);
                    }

                    NotifyOfPropertyChange(() => Tree);

                    break;

                case "MatroskaDefault":
                    switch (myStreamTree.Data.GetType().Name)
                    {
                        case "SubtitleInfo":
                            ((SubtitleInfo)myStreamTree.Data).MkvDefault = myStreamTree.MatroskaDefault;
                            break;
                        case "AudioInfo":
                            ((AudioInfo)myStreamTree.Data).MkvDefault = myStreamTree.MatroskaDefault;
                            break;
                    }

                    NotifyOfPropertyChange(() => MatroskaDefault);

                    break;

                case "HardcodeIntoVideo":
                    if (myStreamTree.Data.GetType().Name == "SubtitleInfo")
                        ((SubtitleInfo)myStreamTree.Data).HardSubIntoVideo = myStreamTree.HardcodeIntoVideo;

                    NotifyOfPropertyChange(() => HardcodeIntoVideo);

                    break;

                case "KeepOnlyForced":
                    if (myStreamTree.Data.GetType().Name == "SubtitleInfo")
                        ((SubtitleInfo)myStreamTree.Data).KeepOnlyForcedCaptions = myStreamTree.KeepOnlyForced;

                    NotifyOfPropertyChange(() => KeepOnlyForced);

                    break;
            }
        }

        public void SetSelectedItem(StreamTreeNode myNode)
        {
            SelectedNode = myNode;
            NotifyOfPropertyChange(() => SelectedNode);
            NotifyOfPropertyChange(() => SelectedNodeData);
        }

        public void ClickOk()
        {
            if (SelectedTitleInfo == null) return;
            if (SelectedProfile == null) return;
            if (string.IsNullOrEmpty(JobTitle)) return;

            var sortedList = GetCheckedItems(SelectedTitleInfo);

            var videoSet = false;

            foreach (var item in sortedList)
            {
                if (item.Data == null) continue;

                var dataType = item.Data.GetType();

                if (dataType == typeof(string))
                    JobInfo.InputFile = (string)item.Data;
                else if (dataType == typeof(VideoInfo))
                {
                    if (videoSet) continue;
                    JobInfo.VideoStream = (VideoInfo)item.Data;
                    videoSet = true;
                }
                else if (dataType == typeof(StereoVideoInfo))
                    JobInfo.StereoVideoStream = (StereoVideoInfo)item.Data;
                else if (dataType == typeof(AudioInfo))
                    JobInfo.AudioStreams.Add((AudioInfo)item.Data);
                else if (dataType == typeof(SubtitleInfo))
                {
                    var sub = (SubtitleInfo)item.Data;
                    if (sub.Format == "PGS" || sub.Format == "VobSub" || sub.Format == "UTF-8" || sub.Format == "ASS" || sub.Format == "SSA")
                        JobInfo.SubtitleStreams.Add(sub);
                }
                else if (dataType == typeof(List<TimeSpan>))
                    JobInfo.Chapters.AddRange((List<TimeSpan>)item.Data);
                else if (dataType == typeof(Dictionary<string, object>))
                {
                    object itemData;
                    ((Dictionary<string, object>) item.Data).TryGetValue("Name", out itemData);
                    if (itemData != null)
                        JobInfo.InputFile = (string)itemData;
                    ((Dictionary<string, object>) item.Data).TryGetValue("PlaylistIndex", out itemData);
                    if (itemData != null)
                        JobInfo.StreamId = (int)itemData;
                    ((Dictionary<string, object>) item.Data).TryGetValue("TrackID", out itemData);
                    if (itemData != null)
                        JobInfo.TrackId = (int)itemData;
                }
            }
            JobInfo.StreamId = -1;

            if (Profiles != null && SelectedProfile != null)
            {
                JobInfo.EncodingProfile = (QuickSelectProfile)SelectedProfile;
                JobInfo.AudioProfile = GetProfile(JobInfo.EncodingProfile.AudioProfile,
                    JobInfo.EncodingProfile.AudioProfileType);
                JobInfo.VideoProfile = GetProfile(JobInfo.EncodingProfile.VideoProfile,
                    JobInfo.EncodingProfile.VideoProfileType);
            }

            _configService.LastSelectedProfile = SelectedProfile.Name;

            if (_configService.CreateXbmcInfoFile)
            {
                if (_resultMovieData != null)
                    JobInfo.MovieInfo = ResultMovieData;
                else if (_resultEpisodeData != null)
                    JobInfo.EpisodeInfo = ResultEpisodeData;
            }

            _bdInfo = null;
            TryClose(true);
        }

        #endregion

        #region model logic

        public void LoadStreams()
        {
            var bgWorker = new BackgroundWorker();
            bgWorker.DoWork += BgWorkerDoWork;
            bgWorker.RunWorkerCompleted += BgWorkerRunWorkerCompleted;
            bgWorker.RunWorkerAsync();

            var profilesWorker = new BackgroundWorker();
            profilesWorker.DoWork += ProfilesWorkerDoWork;
            profilesWorker.RunWorkerCompleted += ProfilesWorkerRunWorkerCompleted;
            profilesWorker.RunWorkerAsync();
        }

        public void GetFileInfo()
        {
            var mi = new MediaInfoContainer();
            try
            {
                mi = GenHelper.GetMediaInfo(JobInfo.InputFile);
            }
            catch (TimeoutException ex)
            {
                Log.Error(ex);
            }

            JobInfo.MediaInfo = mi;

            const string strChapters = "Chapters";
            const string strVideo = "Video";
            const string strAudio = "Audio";
            const string strSubtitles = "Subtitles";

            var containerFormat = mi.General.Format;
            var duration = mi.General.DurationTime.ToString("H:mm:ss.fff");
            var shortFileName = mi.General.FileName + "." + mi.General.FileExtension;

            var treeRoot = $"{shortFileName} / {containerFormat} / Length: {duration}";

            var root = new StreamTreeNode
            {
                ID = _treeNodeID++,
                Name = treeRoot,
                Data = JobInfo.InputFile,
                IsChecked = true,
                IsExpanded = true,
                Children = new List<StreamTreeNode>()
            };
            root.PropertyChanged += TreeNodePropertyChanged;
            _tree.Add(root);

            var chaptersStreamTree = CreateNode(root, strChapters, null);
            var videoStreamTree = CreateNode(root, strVideo, null);
            var audioStreamTree = CreateNode(root, strAudio, null);
            var subStreamTree = CreateNode(root, strSubtitles, null);

            if (mi.Chapters.Count > 0)
            {
                var chaptersTitle = $"{mi.Chapters.Count:0} {strChapters}";

                CreateNode(chaptersStreamTree, chaptersTitle, mi.Chapters);
            }
            else
                chaptersStreamTree.IsChecked = false;

            var streamIndex = 0;

            foreach (var clip in mi.Video)
            {
                streamIndex++;
                var videoPid = clip.ID;
                var videoCodec = clip.FormatInfo;
                var videoCodecShort = clip.Format;

                var videoDesc = $"{clip.Width:0}x{clip.Height:0} {clip.ScanType} / Profile: {clip.FormatProfile} / {clip.FrameRate:0.000}fps";
                var videoStreamTitle = $"{streamIndex:0}: {videoCodec} ({videoCodecShort}), {videoDesc}";

                var vid = new VideoInfo();
                if (JobInfo.Input == InputType.InputAvi)
                    vid.StreamId = 0;
                else
                    vid.StreamId = videoPid == 0 ? streamIndex : videoPid;
                vid.StreamKindID = clip.StreamKindID;
                vid.Fps = clip.FrameRate;
                vid.PicSize = clip.VideoSize;
                vid.Interlaced = clip.ScanType == "Interlaced";
                vid.Format = clip.Format;
                vid.FormatProfile = clip.FormatProfile;
                vid.Height = clip.Height;
                vid.Width = clip.Width;
                vid.FrameCount = clip.FrameCount;
                vid.StreamSize = clip.StreamSize;
                vid.Length = mi.General.DurationTime.TimeOfDay.TotalSeconds;
                float.TryParse(clip.DisplayAspectRatio, NumberStyles.Number, _configService.CInfo, out vid.AspectRatio);
                vid.FrameRateEnumerator = clip.FrameRateEnumerator;
                vid.FrameRateDenominator = clip.FrameRateDenominator;
                vid.FrameMode = clip.FormatFrameMode;

                CreateNode(videoStreamTree, videoStreamTitle, vid);
            }

            videoStreamTree.IsChecked = videoStreamTree.Children.Count > 0;

            foreach (var audio in mi.Audio)
            {
                streamIndex++;
                var audioPid = audio.ID;
                var audioCodec = audio.FormatInfo;
                var audioCodecShort = audio.Format;
                var audioLangCode = audio.LanguageIso6392;
                var audioLanguage = audio.LanguageFull;
                var audioStreamKindID = audio.StreamKindID;

                var audioDesc = $"{audio.Channels:0} Channels ({audio.ChannelPositions}) / {audio.SamplingRate:0}Hz / ";
                audioDesc +=    $"{audio.BitDepth:0} bit / {audio.BitRate/1000:0} kbit/s";

                var audioStreamTitle = $"{streamIndex:0}: {audioCodec} ({audioCodecShort}) / {audioLangCode} ({audioLanguage}) / {audioDesc}";

                if (JobInfo.Input == InputType.InputAvi)
                    audioPid += 1;
                else
                    audioPid = audioPid == 0 ? streamIndex : audioPid;

                var aud = new AudioInfo
                {
                    Id = audioPid,
                    Format = audioCodecShort,
                    FormatProfile = audio.FormatProfile,
                    StreamId = streamIndex,
                    LangCode = audioLangCode,
                    OriginalId = audioPid,
                    StreamKindId = audioStreamKindID,
                    Delay = audio.Delay,
                    Bitrate = audio.BitRate,
                    SampleRate = audio.SamplingRate,
                    ChannelCount = audio.Channels,
                    BitDepth = audio.BitDepth,
                    ShortLang = audio.LanguageIso6391,
                    StreamSize = audio.StreamSize,
                    Length = mi.General.DurationTime.TimeOfDay.TotalSeconds,
                    IsHdStream = audio.CompressionMode == "Lossless"
                };

                CreateNode(audioStreamTree, audioStreamTitle, aud);
            }

            audioStreamTree.IsChecked = audioStreamTree.Children.Count > 0;

            foreach (var sub in mi.Text)
            {
                streamIndex++;
                var subCodec = sub.CodecIDInfo;
                var subCodecShort = sub.Format;
                var subLangCode = sub.LanguageIso6392;
                var subLanguage = sub.LanguageFull;
                var subStreamKindID = sub.StreamKindID;

                var subStreamTitle = $"{streamIndex:0}: {subCodec} ({subCodecShort}) / {subLangCode} ({subLanguage})";

                var subInfo = new SubtitleInfo
                {
                    Id = sub.ID,
                    StreamId = streamIndex,
                    LangCode = subLangCode,
                    Format = subCodecShort,
                    StreamKindId = subStreamKindID,
                    Delay = sub.Delay,
                    StreamSize = sub.StreamSize
                };

                CreateNode(subStreamTree, subStreamTitle, subInfo);
            }

            foreach (var sub in mi.Image)
            {
                streamIndex++;
                var subCodec = sub.CodecIDInfo;
                var subCodecShort = sub.Format;
                var subLangCode = sub.LanguageIso6392;
                var subLanguage = sub.LanguageFull;
                var subStreamKindID = sub.StreamKindID;

                var subStreamTitle = $"{streamIndex:0}: {subCodec} ({subCodecShort}) / {subLangCode} ({subLanguage})";
                var subInfo = new SubtitleInfo
                {
                    Id = sub.ID,
                    StreamId = streamIndex,
                    LangCode = subLangCode,
                    Format = subCodecShort,
                    StreamKindId = subStreamKindID,
                    Delay = 0,
                    StreamSize = sub.StreamSize
                };

                CreateNode(subStreamTree, subStreamTitle, subInfo);
            }

            subStreamTree.IsChecked = subStreamTree.Children.Count > 0;

            NotifyOfPropertyChange(() => Tree);
        }

        public void GetBdInfo()
        {
            const string strChapters = "Chapters";    //ProcessingService.GetResourceString("streamselect_chapters");
            const string strVideo = "Video";    //ProcessingService.GetResourceString("streamselect_video");
            const string strAudio = "Audio";    //ProcessingService.GetResourceString("streamselect_audio");
            const string strSubtitles = "Subtitles";    //ProcessingService.GetResourceString("streamselect_subtitles");

            _bdInfo = new BDROM(JobInfo.InputFile);
            _bdInfo.Scan();

            var longestClip = GetLongestBdPlaylist();

            var playlistIndex = 1;

            foreach (var item in _bdInfo.PlaylistFiles.Values)
            {
                if (!item.IsValid)
                {
                    playlistIndex++;
                    continue;
                }

                var streamIndex = 0;

                var duration = new DateTime();

                duration = duration.AddSeconds(item.TotalLength);

                var treeRoot = $"Title: {playlistIndex:0} ({item.Name}), Length: {duration.ToString("H:mm:ss.fff")}";

                var treeData = new Dictionary<string, object>
                    {
                        {
                            "Name",
                            Path.Combine(_bdInfo.DirectoryPLAYLIST.FullName, item.Name)
                        },
                        {"PlaylistIndex", playlistIndex}
                    };

                var root = new StreamTreeNode
                {
                    ID = _treeNodeID++,
                    Name = treeRoot,
                    Data = treeData,
                    Children = new List<StreamTreeNode>(),
                    IsChecked = true,
                    IsExpanded = true
                };
                root.PropertyChanged += TreeNodePropertyChanged;
                _tree.Add(root);

                var chaptersStreamTree = CreateNode(root, strChapters, null);
                var videoStreamTree = CreateNode(root, strVideo, null);
                var audioStreamTree = CreateNode(root, strAudio, null);
                var subStreamTree = CreateNode(root, strSubtitles, null);

                var streamChapters = new List<TimeSpan>();
                if (item.Chapters.Count > 1)
                {
                    streamIndex++;

                    streamChapters.AddRange(item.Chapters.Select(TimeSpan.FromSeconds));

                    var chaptersFormat = $"{streamChapters.Count:0} {strChapters}";

                    CreateNode(chaptersStreamTree, chaptersFormat, streamChapters);
                }

                var videoDescStereo = string.Empty;
                var leftVideoStreamID = -1;
                var leftVideoDiscStreamID = -1;
                foreach (var clip in item.VideoStreams)
                {
                    streamIndex++;
                    var videoCodec = clip.CodecName;
                    var videoCodecShort = clip.CodecShortName;
                    var videoDesc = clip.Description;

                    if ((clip.StreamType == TSStreamType.AVC_VIDEO) && (item.VideoStreams.Count > 1)
                        && (item.VideoStreams[0].PID == clip.PID)
                        && (item.VideoStreams[item.VideoStreams.Count - 1].StreamType == TSStreamType.MVC_VIDEO))
                    {
                        videoDescStereo = videoDesc;
                        videoCodec += item.MVCBaseViewR ? " (right eye)" : " (left eye)";

                        leftVideoStreamID = streamIndex;
                        leftVideoDiscStreamID = clip.PID;
                    }
                    if ((clip.StreamType == TSStreamType.MVC_VIDEO) && (item.VideoStreams.Count > 1)
                        && (item.VideoStreams[item.VideoStreams.Count - 1].PID == clip.PID)
                        && (item.VideoStreams[0].StreamType == TSStreamType.AVC_VIDEO))
                    {
                        videoDesc = videoDescStereo;
                        videoCodec = "MPEG-4 MVC Video";
                        videoCodec += item.MVCBaseViewR ? " (right eye)" : " (left eye)";
                    }

                    var videoStreamFormat = $"{streamIndex:0}: {videoCodec} ({videoCodecShort}), {videoDesc}";
                    switch (clip.StreamType)
                    {
                        case TSStreamType.AVC_VIDEO:
                        case TSStreamType.MPEG2_VIDEO:
                        case TSStreamType.MPEG1_VIDEO:
                        case TSStreamType.VC1_VIDEO:
                            {
                                var vid = new VideoInfo
                                {
                                    StreamId = streamIndex,
                                    TrackId = playlistIndex,
                                    Fps = (float)clip.FrameRateEnumerator / clip.FrameRateDenominator,
                                    PicSize = (VideoFormat)clip.VideoFormat,
                                    Interlaced = clip.IsInterlaced,
                                    Format = clip.CodecShortName,
                                    DemuxStreamId = clip.PID,
                                    FrameCount = 0,
                                    Encoded = false,
                                    IsRawStream = false,
                                    StreamSize = 0,
                                    Length = item.TotalLength,
                                    FrameRateEnumerator = clip.FrameRateEnumerator,
                                    FrameRateDenominator = clip.FrameRateDenominator,
                                    Height = clip.Height
                                };

                                int.TryParse(item.Name.Substring(0, item.Name.LastIndexOf('.')), NumberStyles.Number,
                                               _configService.CInfo, out vid.DemuxPlayList);

                                foreach (var streamClip in item.StreamClips)
                                    vid.DemuxStreamNames.Add(streamClip.StreamFile.FileInfo.FullName);

                                float mod;
                                switch (clip.AspectRatio)
                                {
                                    case TSAspectRatio.ASPECT_16_9:
                                        mod = (float)1.777778;
                                        break;
                                    default:
                                        mod = (float)1.333333;
                                        break;
                                }
                                vid.Width = (int)(vid.Height * mod);
                                vid.AspectRatio = mod;

                                CreateNode(videoStreamTree, videoStreamFormat, vid);
                            }
                            break;
                        case TSStreamType.MVC_VIDEO:
                            {
                                var vid = new StereoVideoInfo
                                {
                                    RightStreamId = streamIndex,
                                    DemuxRightStreamId = clip.PID,
                                    LeftStreamId = leftVideoStreamID,
                                    DemuxLeftStreamId = leftVideoDiscStreamID
                                };
                                CreateNode(videoStreamTree, videoStreamFormat, vid);
                            }
                            break;
                    }
                }

                foreach (var audio in item.AudioStreams)
                {
                    streamIndex++;
                    var audioCodec = audio.CodecName;
                    var audioCodecShort = audio.CodecShortName;
                    var audioDesc = audio.Description;
                    var audioLangCode = audio.LanguageCode;
                    var audioLanguage = audio.LanguageName;

                    var audioStreamFormat = $"{streamIndex:0}: {audioCodec} ({audioCodecShort}) / {audioLangCode} ({audioLanguage}) / {audioDesc}";

                    var aud = new AudioInfo
                    {
                        Format = audioCodecShort,
                        FormatProfile = string.Empty,
                        Id = streamIndex,
                        StreamId = streamIndex,
                        LangCode = audioLangCode,
                        TempFile = string.Empty,
                        OriginalId = streamIndex,
                        Delay = 0,
                        Bitrate = audio.BitRate,
                        DemuxStreamId = audio.PID,
                        SampleRate = audio.SampleRate,
                        ChannelCount = audio.ChannelCount + audio.LFE,
                        BitDepth = audio.BitDepth,
                        ShortLang = audio.LanguageCode,
                        StreamSize = 0,
                        Length = item.TotalLength,
                        IsHdStream = audio.CoreStream != null
                    };

                    CreateNode(audioStreamTree, audioStreamFormat, aud);
                }

                foreach (var sub in item.TextStreams)
                {
                    streamIndex++;
                    var subCodecShort = sub.CodecShortName;
                    var subDesc = sub.Description;
                    var subLangCode = sub.LanguageCode;
                    var subLanguage = sub.LanguageName;

                    var subStreamFormat = $"{streamIndex:0}: {subCodecShort} / {subLangCode} ({subLanguage}); {subDesc}";

                    var subInfo = new SubtitleInfo
                    {
                        Id = streamIndex,
                        StreamId = streamIndex,
                        TempFile = string.Empty,
                        LangCode = subLangCode,
                        Format = subCodecShort,
                        Delay = 0,
                        DemuxStreamId = sub.PID,
                        StreamSize = 0
                    };

                    CreateNode(subStreamTree, subStreamFormat, subInfo);
                }

                foreach (var sub in item.GraphicsStreams)
                {
                    streamIndex++;
                    var subCodecShort = sub.CodecShortName;
                    var subDesc = sub.Description;
                    var subLangCode = sub.LanguageCode;
                    var subLanguage = sub.LanguageName;

                    var subStreamFormat = $"{streamIndex:0}: {subCodecShort} / {subLangCode} ({subLanguage}); {subDesc}";

                    var subInfo = new SubtitleInfo
                    {
                        Id = streamIndex,
                        StreamId = streamIndex,
                        TempFile = string.Empty,
                        LangCode = subLangCode,
                        Format = subCodecShort,
                        DemuxStreamId = sub.PID,
                        StreamSize = 0
                    };

                    CreateNode(subStreamTree, subStreamFormat, subInfo);
                }
                playlistIndex++;
            }
            _defaultSelection = longestClip - 1;
        }

        public int GetLongestBdPlaylist()
        {
            var longest = 0;
            var longestClip = 0;

            var playlistIndex = 1;

            foreach (var clipLength in from item in _bdInfo.PlaylistFiles.Values where item.IsValid select (int)Math.Truncate(item.TotalLength))
            {
                if (clipLength > longest)
                {
                    longest = clipLength;
                    longestClip = playlistIndex;
                }
                playlistIndex++;
            }

            return longestClip;
        }

        public void GetDvdTitleList()
        {
            const string strChapters = "Chapters";
            const string strVideo = "Video";
            const string strAudio = "Audio";
            const string strSubtitles = "Subtitles";

            var dvd = new DvdInfoContainer(JobInfo.InputFile);

            foreach (var info in dvd.Titles)
            {
                int videoId = info.TitleNumber;
                var fps = info.VideoStream.Framerate;
                var videoFormat = info.VideoStream.VideoStandard.ToString();
                var codec = _processingService.StringValueOf(info.VideoStream.CodingMode);
                var aspect = _processingService.StringValueOf(info.VideoStream.AspectRatio);

                var resolution = _processingService.StringValueOf(info.VideoStream.VideoResolution);

                var resolutionArray = resolution.Split(new[] {"x"}, StringSplitOptions.RemoveEmptyEntries);
                int width = 0, height = 0;

                try
                {
                    int.TryParse(resolutionArray[0], out width);
                    int.TryParse(resolutionArray[1], out height);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                var letterboxed = _processingService.StringValueOf(info.VideoStream.DisplayFormat);
                int vtsID = info.TitleSetNumber;

                var duration = DateTime.MinValue.Add(info.VideoStream.Runtime);
                var treeRoot = $"Title: {videoId:0}, Length: {duration.ToString("H:mm:ss.fff")}";

                var treeData = new Dictionary<string, object>
                {
                    {"Name", JobInfo.InputFile},
                    {"TrackID", videoId}
                };
                var root = new StreamTreeNode
                {
                    ID = _treeNodeID++,
                    Name = treeRoot,
                    Data = treeData,
                    Children = new List<StreamTreeNode>(),
                    IsChecked = true,
                    IsExpanded = true
                };
                _tree.Add(root);

                var chaptersTree = CreateNode(root, strChapters, null);
                var videoTree = CreateNode(root, strVideo, null);
                var audioTree = CreateNode(root, strAudio, null);
                var subTree = CreateNode(root, strSubtitles, null);

                if (info.Chapters != null && info.Chapters.Count > 0)
                {
                    var chaptersFormat = $"{info.Chapters.Count:0} {strChapters}";
                    CreateNode(chaptersTree, chaptersFormat, info.Chapters);
                }

                var videoStream = $"{codec} {resolution} {aspect} ({letterboxed}) {videoFormat} {fps:0.000} fps";
                var vid = new VideoInfo
                {
                    VtsId = vtsID,
                    TrackId = videoId,
                    StreamId = 1,
                    Fps = fps,
                    Interlaced = true,
                    Format = codec,
                    FrameCount = 0,
                    Width = width,
                    Height = height,
                    Encoded = false,
                    IsRawStream = false,
                    DemuxStreamNames = new List<string>(),
                    StreamSize = 0,
                    Length = info.VideoStream.Runtime.TotalSeconds,
                    FrameRateEnumerator = info.VideoStream.FrameRateNumerator,
                    FrameRateDenominator = info.VideoStream.FrameRateDenominator,
                    AspectRatio = info.VideoStream.AspectRatio == DvdVideoAspectRatio.Aspect4By3 ? 4f / 3f : 16f / 9f
                };

                CreateNode(videoTree, videoStream, vid);

                foreach (var stream in info.AudioStreams)
                {
                    var audioID = stream.StreamIndex;
                    var langCode = stream.Language.Code;
                    var language = stream.Language.Name;
                    var format = _processingService.StringValueOf(stream.CodingMode);
                    var frequency = stream.SampleRate;
                    var quantization = _processingService.StringValueOf(stream.Quantization);
                    var channels = stream.Channels;
                    var content = _processingService.StringValueOf(stream.Extension);
                    var streamID = stream.StreamId;

                    var audioStream =
                        $"Track {audioID:0} ({streamID:0}), {langCode} ({language} - {content}), {format} {channels:0} Channels {frequency:0} Hz, {quantization}";

                    var aud = new AudioInfo
                    {
                        Format = format,
                        FormatProfile = string.Empty,
                        Id = audioID,
                        StreamId = streamID,
                        LangCode = langCode,
                        TempFile = string.Empty,
                        OriginalId = audioID,
                        Delay = 0,
                        Bitrate = 0,
                        SampleRate = frequency,
                        ChannelCount = channels,
                        ShortLang = langCode,
                        StreamSize = 0,
                        IsHdStream = false
                    };

                    CreateNode(audioTree, audioStream, aud);
                }

                foreach (var stream in info.SubtitleStreams)
                {
                    var subID = stream.StreamIndex;
                    var langCode = stream.Language.Code;
                    var language = stream.Language.Name;
                    var content = _processingService.StringValueOf(stream.Extension);
                    var streamID = stream.StreamId;

                    var subtitleStream = $"Track {subID:0} ({streamID:0}), {langCode} ({language} - {content})";

                    var subInfo = new SubtitleInfo
                    {
                        Id = subID + info.AudioStreams.Count,
                        StreamId = streamID,
                        TempFile = string.Empty,
                        LangCode = langCode,
                        Format = "VobSub",
                        Delay = 0,
                        StreamSize = 0
                    };

                    CreateNode(subTree, subtitleStream, subInfo);
                }

            }

            int longestTrack =
                dvd.Titles.Single(
                    info => info.VideoStream.Runtime == dvd.Titles.Max(infoLocl => infoLocl.VideoStream.Runtime))
                    .TitleNumber;

            _defaultSelection = longestTrack - 1;
        }

        public string GetVolumeLabel(FileSystemInfo dir)
        {
            uint serialNumber = 0;
            uint maxLength = 0;
            var volumeFlags = new uint();
            var volumeLabel = new StringBuilder(256);
            var fileSystemName = new StringBuilder(256);
            var label = string.Empty;

            try
            {
                GetVolumeInformation(dir.Name, volumeLabel, (uint) volumeLabel.Capacity, ref serialNumber, ref maxLength,
                    ref volumeFlags, fileSystemName, (uint) fileSystemName.Capacity);

                label = volumeLabel.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            if (label.Length == 0)
                label = dir.Name;

            return label;
        }

        public StreamTreeNode CreateNode(StreamTreeNode tParent, string tName, object data)
        {
            var subNode = new StreamTreeNode
            {
                ID = _treeNodeID++,
                Name = tName,
                Data = data,
                IsChecked = tParent.IsChecked,
                IsExpanded = tParent.IsExpanded,
                Parent = tParent,
                Children = new List<StreamTreeNode>()
            };
            subNode.PropertyChanged += TreeNodePropertyChanged;
            tParent.Children.Add(subNode);
           
            return subNode;
        }

        public EncoderProfile GetProfile(string pName, ProfileType pType)
        {
            var profHandler = new ProfilesHandler(_configService);
            return profHandler.FilteredList.Find(ep => (ep.Name == pName) && (ep.Type == pType));
        }

        public IEnumerable<StreamTreeNode> GetCheckedItems(StreamTreeNode streamTree)
        {
            var items = new List<StreamTreeNode>();

            if (streamTree.IsChecked)
                items.Add(streamTree);

            if (streamTree.Children.Count <= 0) return items;
            foreach (var child in streamTree.Children)
                items.AddRange(GetCheckedItems(child));

            return items;
        }

        public void CheckRootItem(StreamTreeNode item)
        {
            var iParent = item.Parent;
            StreamTreeNode topParent = null;
            if (iParent != null)
            {
                item.IsChecked = true;
                item.IsExpanded = true;

                iParent.IsChecked = true;
                iParent.IsExpanded = true;

                topParent = iParent.Parent;
            }

            if (topParent == null) return;
            topParent.IsExpanded = true;
            topParent.IsChecked = true;
        }

        public void CheckSubItems(StreamTreeNode node)
        {
            foreach (var childNode in node.Children)
            {
                childNode.IsChecked = node.IsChecked;
                childNode.IsExpanded = node.IsChecked;
            }
            node.IsExpanded = node.IsChecked;
        }

        [DllImport("kernel32.dll")]
        private static extern long GetVolumeInformation(
            string pathName,
            StringBuilder volumeNameBuffer,
            uint volumeNameSize,
            ref uint volumeSerialNumber,
            ref uint maximumComponentLength,
            ref uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            uint fileSystemNameSize);

        #endregion
    }
}
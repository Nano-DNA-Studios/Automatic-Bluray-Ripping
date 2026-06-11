using MediaInfo;

namespace Automatic_Bluray_Ripping
{
    public class MKVBackup
    {
        public string Name { get; set; }

        public string DirPath { get; set; }

        public string TranscodePreset { get; set; }

        public bool RemoveOnCompletion { get; set; }

        public Dictionary<string, VideoCategoryMetadata> VideoCategories { get; set; }

        public MKVBackup(string dirPath)
        {
            Name = Path.GetFileName(dirPath) ?? "";
            DirPath = dirPath;
            TranscodePreset = "";
            RemoveOnCompletion = true;
            VideoCategories = new Dictionary<string, VideoCategoryMetadata>();
        }

        public int GetTotalVideos ()
        {
            int total = 0;

            foreach (VideoCategoryMetadata metadata in VideoCategories.Values)
                total += metadata.Metadata.Count;

            return total;
        }
    }

    public class MediaScannerManager
    {
        public List<MKVBackup> Backups { get; set; }

        public string[] Presets { get; set; }

        public bool IsScanning { get; set; }

        private readonly ThumbnailQueue _thumbnailQueue;

        private readonly TranscodeQueueService _transcodeQueueService;

        private readonly MakeMKVManager _mkvManager;

        public event Action? OnProgressUpdated;

        public MediaScannerManager(MakeMKVManager mkvManager, ThumbnailQueue thumbnailQueue, TranscodeQueueService transcodeQueueService)
        {
            Backups = new List<MKVBackup>();
            Presets = [];
            IsScanning = true;

            _mkvManager = mkvManager;
            _thumbnailQueue = thumbnailQueue;
            _transcodeQueueService = transcodeQueueService;

            _thumbnailQueue.OnProgressUpdated += Update;
        }

        private void Update()
        {
            OnProgressUpdated?.Invoke();
        }

        public void LoadHandbrakePresets()
        {
            string? fullPath = Path.GetDirectoryName(Environment.ProcessPath);
            string presetDir = "HandbrakePresets";

            if (fullPath == null)
                return;

            if (!Directory.Exists(fullPath))
                return;

            fullPath = Path.Combine(fullPath, presetDir);

            if (!Directory.Exists(fullPath))
                return;

            Presets = Directory.GetFiles(fullPath);

            Update();
        }

        public void LoadMKVBackups()
        {
            IsScanning = true;

            Backups = new List<MKVBackup>();

            if (!Directory.Exists(DefaultSettings.DefaultMKVDirectory))
                return;

            foreach (string dir in Directory.GetDirectories(DefaultSettings.DefaultMKVDirectory))
            {
                MKVBackup backup = new MKVBackup(dir);

                if (_mkvManager.DiscBackups.Any((d) => d.Name == backup.Name))
                    continue;

                ComputeCategories(backup);

                Backups.Add(backup);
            }

            IsScanning = false;

            Update();
        }

        public void ComputeCategories(MKVBackup backup)
        {
            Dictionary<string, VideoCategoryMetadata> videoCategories = new Dictionary<string, VideoCategoryMetadata>();

            int index = 1;

            string[] MKVFiles = Directory.GetFiles(backup.DirPath, "*.mkv");

            foreach (string MKVFile in MKVFiles)
            {
                VideoMetadata metadata = new VideoMetadata(MKVFile);

                ParseMetadata(metadata);

                string signature = metadata.GetSignature(true, true, true, true);

                if (!videoCategories.ContainsKey(signature))
                {
                    videoCategories.Add(signature, new VideoCategoryMetadata($"Category {index}"));
                    index++;
                }

                videoCategories[signature].AddMetadata(metadata);
            }

            backup.VideoCategories = videoCategories;
        }

        private void ParseMetadata (VideoMetadata metadata)
        {
            if (!File.Exists(metadata.FilePath))
                return;

            MediaInfoWrapper mediaFile = new MediaInfoWrapper(metadata.FilePath);

            mediaFile.WriteInfo();

            metadata.Duration = mediaFile.Duration;

            List<VideoStreamItem> videoStreams = new List<VideoStreamItem>();
            List<AudioStreamItem> audioStreams = new List<AudioStreamItem>();
            List<SubtitleStreamItem> subtitleStreams = new List<SubtitleStreamItem>();
            List<ChapterStreamItem> chapterStreams = new List<ChapterStreamItem>();

            foreach (var video in mediaFile.VideoStreams)
                videoStreams.Add(new VideoStreamItem(video, mediaFile.Duration, videoStreams.Count + 1));

            foreach (var audio in mediaFile.AudioStreams)
                audioStreams.Add(new AudioStreamItem(audio, audioStreams.Count + 1));

            foreach (var subtitle in mediaFile.Subtitles)
                subtitleStreams.Add(new SubtitleStreamItem(subtitle, subtitleStreams.Count + 1));

            foreach (var chapter in mediaFile.Chapters)
                chapterStreams.Add(new ChapterStreamItem(chapter, chapterStreams.Count + 1));

            metadata.VideoStreams = videoStreams.ToArray();
            metadata.AudioStreams = audioStreams.ToArray();
            metadata.SubtitleStreams = subtitleStreams.ToArray();
            metadata.ChapterStreams = chapterStreams.ToArray();

            _thumbnailQueue.EnqueueJob(metadata);
        }

        public void AddToTranscodeQueue (MKVBackup backup)
        {
            string exportDir = Path.Combine(DefaultSettings.DefaultTranscodeDirectory, backup.Name);

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            foreach (string key in backup.VideoCategories.Keys)
            {
                if (!backup.VideoCategories[key].IsSelected)
                    continue;

                foreach (VideoMetadata metadata in backup.VideoCategories[key].Metadata)
                {
                    if (!metadata.IsSelected)
                        continue;

                    _transcodeQueueService.EnqueueJob(new TranscodeJob()
                    {
                        Name = metadata.Name,
                        InputFilePath = metadata.FilePath,
                        OutputFilePath = Path.Combine(exportDir, Path.GetFileName(metadata.Name) + ".mkv"),
                        Args = metadata.GetTranscodeArgs(),
                        ThumbnailBase64 = metadata.ThumbnailBase64,
                        TranscodePreset = backup.TranscodePreset,
                        RemoveOnCompletion = backup.RemoveOnCompletion
                    });
                }
            }

            Backups.Remove(backup);
        }
    }
}

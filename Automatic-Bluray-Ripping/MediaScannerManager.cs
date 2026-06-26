using MediaInfo;
using MediaInfo.Model;

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

        public int GetTotalVideos()
        {
            int total = 0;

            foreach (VideoCategoryMetadata metadata in VideoCategories.Values)
                total += metadata.Metadata.Count;

            return total;
        }
    }

    public class MediaScannerManager
    {
        public List<MKVBackup> UnremovedBackups { get; set; }

        public List<MKVBackup> Backups { get; set; }

        public string[] Presets { get; set; }

        public bool IsScanning { get; set; }

        private readonly ThumbnailQueue _thumbnailQueue;

        private readonly SubtitleQueue _subtitleQueue;

        private readonly TranscodeQueueService _transcodeQueueService;

        private readonly MakeMKVManager _mkvManager;

        private readonly DefaultSettings _settings;

        public event Action? OnProgressUpdated;

        public MediaScannerManager(MakeMKVManager mkvManager, ThumbnailQueue thumbnailQueue, SubtitleQueue subtitleQueue, TranscodeQueueService transcodeQueueService, DefaultSettings settings)
        {
            Backups = new List<MKVBackup>();
            UnremovedBackups = new List<MKVBackup>();
            Presets = [];
            IsScanning = true;

            _mkvManager = mkvManager;
            _thumbnailQueue = thumbnailQueue;
            _subtitleQueue = subtitleQueue;
            _transcodeQueueService = transcodeQueueService;

            _thumbnailQueue.OnProgressUpdated += Update;
            _subtitleQueue.OnChange += Update;
            _settings = settings;
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

            Console.WriteLine("Checking if exists");

            if (!Directory.Exists(_settings.DefaultMKVDirectory))
                return;

            Console.WriteLine("Line does exist");

            if (UnremovedBackups.Count > 0)
            {
                Console.WriteLine("Adding unremoved backups");
                Backups.AddRange(UnremovedBackups);
                UnremovedBackups.Clear();
            }

            Console.WriteLine("Going through directories");

            foreach (string dir in Directory.GetDirectories(_settings.DefaultMKVDirectory))
            {
                MKVBackup backup = new MKVBackup(dir);

                if (_mkvManager.DiscBackups.Any((d) => d.Name == backup.Name && d.IsConverting))
                {
                    Console.WriteLine("Skipping cause exist?");
                    continue;
                }

                if (Backups.Any((b) => b.Name == backup.Name))
                {
                    Console.WriteLine("Already exists");
                    continue;
                }

                Console.WriteLine("Computing the category");

                ComputeCategories(backup);

                Backups.Add(backup);
            }

            IsScanning = false;
            Backups.Sort((a, b) => a.Name.CompareTo(b.Name));

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

        private void ParseMetadata(VideoMetadata metadata)
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

            foreach (VideoStream video in mediaFile.VideoStreams)
                videoStreams.Add(new VideoStreamItem(video, mediaFile.Duration, videoStreams.Count + 1));

            foreach (AudioStream audio in mediaFile.AudioStreams)
                audioStreams.Add(new AudioStreamItem(audio, audioStreams.Count + 1, _settings));

            foreach (SubtitleStream subtitle in mediaFile.Subtitles)
                subtitleStreams.Add(new SubtitleStreamItem(subtitle, subtitleStreams.Count + 1));

            foreach (ChapterStream chapter in mediaFile.Chapters)
                chapterStreams.Add(new ChapterStreamItem(chapter, chapterStreams.Count + 1));

            metadata.VideoStreams = videoStreams.ToArray();
            metadata.AudioStreams = audioStreams.ToArray();
            metadata.SubtitleStreams = subtitleStreams.ToArray();
            metadata.ChapterStreams = chapterStreams.ToArray();

            _thumbnailQueue.EnqueueJob(metadata);
            _subtitleQueue.EnqueueJob(metadata);
        }

        private string GetTranscodeArgs(VideoMetadata metadata)
        {
            string args = "";

            AudioStreamItem[] selectedAudio = metadata.AudioStreams.Where(s => s.IsSelected).ToArray();

            if (selectedAudio.Length > 0)
            {
                args += $" -a " + string.Join(",", selectedAudio.Select(s => s.ID));
                args += $" -E " + string.Join(",", selectedAudio.Select(s => s.Codec));
                args += $" -6 " + string.Join(",", selectedAudio.Select(s => _settings.MixdownToCli[s.Mixdown]));
                args += $" -A " + string.Join(",", selectedAudio.Select(s => $"\"{s.Name}\""));
            }

            SubtitleStreamItem[] selectedSubtitle = metadata.SubtitleStreams.Where(s => s.IsSelected).ToArray();

            if (selectedSubtitle.Length > 0)
            {
                args += $" -s " + string.Join(",", selectedSubtitle.Select(s => s.ID));
                args += $" -S " + string.Join(",", selectedSubtitle.Select(s => $"\"{s.Name}\""));
            }

            return args;
        }

        public void AddToTranscodeQueue(MKVBackup backup)
        {
            string exportDir = Path.Combine(_settings.DefaultTranscodeDirectory, backup.Name);

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
                        Args = GetTranscodeArgs(metadata),
                        ThumbnailBase64 = metadata.ThumbnailBase64,
                        TranscodePreset = backup.TranscodePreset,
                        RemoveOnCompletion = backup.RemoveOnCompletion
                    });
                }
            }

            if (!backup.RemoveOnCompletion)
                UnremovedBackups.Add(backup);

            Backups.Remove(backup);
        }
    }
}

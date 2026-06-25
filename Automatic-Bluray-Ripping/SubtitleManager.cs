using NanoDNA.ProcessRunner;
using System.Diagnostics;

namespace Automatic_Bluray_Ripping
{
    public struct SubtitleJob
    {
        public int Index;

        public VideoMetadata Metadata;
    }

    public class SubtitleQueue
    {
        private readonly PriorityQueue<SubtitleJob, (int PriorityLevel, long SequenceNumber)> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly object _lockObj = new();

        private long _nextSequenceNumber = 0;

        public event Action? OnChange;

        public SubtitleStreamItem? ActiveStream { get; private set; }

        public SubtitleJob? ProcessingJob { get; private set; }

        public bool IsVisible { get; private set; }

        public void NotifyStateChanged() => OnChange?.Invoke();

        public int GetQueueCount () => _queue.Count;

        public void ShowPreview(VideoMetadata metadata, SubtitleStreamItem stream)
        {
            ActiveStream = stream;
            IsVisible = true;

            if (stream.Images.Length == 0)
                PriorityEnqueue(metadata, stream.ID - 1);

            NotifyStateChanged();
        }

        public void ClosePreview()
        {
            ActiveStream = null;
            IsVisible = false;
            NotifyStateChanged();
        }

        public void EnqueueJob(VideoMetadata metadata)
        {
            lock (_lockObj)
            {
                for (int i = 0; i < metadata.SubtitleStreams.Length; i++)
                {
                    _queue.Enqueue(new SubtitleJob { Metadata = metadata, Index = i }, (10, _nextSequenceNumber++));
                    _signal.Release();
                }
            }

            NotifyStateChanged();
        }

        public void PriorityEnqueue(VideoMetadata metadata, int index)
        {
            lock (_lockObj)
            {
                _queue.Enqueue(new SubtitleJob { Metadata = metadata, Index = index }, (0, _nextSequenceNumber++));
                _signal.Release();
            }

            NotifyStateChanged();
        }

        public async Task<SubtitleJob> DequeueJobAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _queue.TryDequeue(out var metadata, out _);
            ProcessingJob = metadata;
            NotifyStateChanged();
            return metadata!;
        }

        public void ClearProcessing()
        {
            ProcessingJob = null;
        }
    }

    public class SubtitleManager : BackgroundService
    {
        public SubtitleQueue _queueService;

        private DefaultSettings _settings { get; set; }

        public SubtitleManager(SubtitleQueue queue, DefaultSettings settings)
        {
            _queueService = queue;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SubtitleJob metadata = await _queueService.DequeueJobAsync(stoppingToken);

                await ExtractSubtitleImagesToBase64Async(metadata);

                _queueService.ClearProcessing();

                _queueService.NotifyStateChanged();
            }
        }

        public int GetSubtitleFrameCount(SubtitleJob subtitleJob)
        {
            ProcessRunner process = new ProcessRunner("ffprobe");

            string args = $"-v error -select_streams s:{subtitleJob.Index} -show_entries stream_tags -of default=noprint_wrappers=1 \"{subtitleJob.Metadata.FilePath}\"";
            int numOfFrames = 0;

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                if (!args.Data.Contains("TAG:NUMBER_OF_FRAMES"))
                    return;

                string frames = args.Data.Split("=")[1];

                numOfFrames = int.Parse(frames);
            };

            process.Run(args);

            return numOfFrames;
        }

        public async Task ExtractSubtitleImagesToBase64Async(SubtitleJob subtitleJob)
        {
            List<string> base64Images = new List<string>();

            int frames = GetSubtitleFrameCount(subtitleJob);

            string singlePassArgs;

            if (frames < _settings.SubtitleFramesExtracted)
                singlePassArgs = $"-probesize 20M -analyzeduration 20M -f lavfi -i color=c=black@0:s={subtitleJob.Metadata.Width}x{subtitleJob.Metadata.Height} -i \"{subtitleJob.Metadata.FilePath}\" -filter_complex \"[0:v][1:s:{subtitleJob.Index}]overlay=shortest=1,mpdecimate\" -vsync vfr -pix_fmt rgba -frames:v {frames - 1} -f image2pipe -c:v png pipe:1";
            else
                singlePassArgs = $"-probesize 20M -analyzeduration 20M -f lavfi -i color=c=black@0:s={subtitleJob.Metadata.Width}x{subtitleJob.Metadata.Height} -i \"{subtitleJob.Metadata.FilePath}\" -filter_complex \"[0:v][1:s:{subtitleJob.Index}]overlay=shortest=1,mpdecimate\" -vsync vfr -pix_fmt rgba -frames:v {_settings.SubtitleFramesExtracted} -f image2pipe -c:v png pipe:1";

            //ProcessRunner process = new ProcessRunner("ffmpeg");

            //await process.RunAsync(singlePassArgs);

            //byte[] allPngBytes = process.STDOutputBytes;

            //List<byte[]> individualImages = SplitPngStream(allPngBytes);

            //foreach (var imgBytes in individualImages)
            //    base64Images.Add($"data:image/png;base64,{Convert.ToBase64String(imgBytes)}");

            //subtitleJob.Metadata.SubtitleStreams[subtitleJob.Index].Images = base64Images.ToArray();

            //ProcessRunner process = new ProcessRunner("ffmpeg");

            //await process.RunAsync(singlePassArgs);

            //using (MemoryStream pngStream = (MemoryStream)process.StandardOutputReader.BaseStream)
            //{
            //    byte[] allPngBytes = pngStream.ToArray();

            //    List<byte[]> individualImages = SplitPngStream(allPngBytes);
            //    foreach (var imgBytes in individualImages)
            //        base64Images.Add($"data:image/png;base64,{Convert.ToBase64String(imgBytes)}");
            //}

            //subtitleJob.Metadata.SubtitleStreams[subtitleJob.Index].Images = base64Images.ToArray();

            ProcessStartInfo pngStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = singlePassArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process pngProcess = new Process { StartInfo = pngStartInfo })
            {
                pngProcess.Start();

                using (MemoryStream pngStream = new MemoryStream())
                {
                    Task readOutputTask = pngProcess.StandardOutput.BaseStream.CopyToAsync(pngStream);

                    await readOutputTask;
                    await pngProcess.WaitForExitAsync();

                    byte[] allPngBytes = pngStream.ToArray();

                    List<byte[]> individualImages = SplitPngStream(allPngBytes);
                    foreach (var imgBytes in individualImages)
                        base64Images.Add($"data:image/png;base64,{Convert.ToBase64String(imgBytes)}");
                }
            }

            subtitleJob.Metadata.SubtitleStreams[subtitleJob.Index].Images = base64Images.ToArray();
        }

        private List<byte[]> SplitPngStream(byte[] streamBytes)
        {
            List<byte[]> chunks = new List<byte[]>();
            if (streamBytes == null || streamBytes.Length == 0) return chunks;

            // PNG File Header Magic Bytes: \x89PNG\r\n\x1a\n
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            List<int> positions = new List<int>();

            for (int i = 0; i <= streamBytes.Length - pngHeader.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pngHeader.Length; j++)
                {
                    if (streamBytes[i + j] != pngHeader[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) positions.Add(i);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                if (i % 2 == 0)
                    continue;

                int start = positions[i];
                int end = (i + 1 < positions.Count) ? positions[i + 1] : streamBytes.Length;
                int length = end - start;

                byte[] pngFile = new byte[length];
                Buffer.BlockCopy(streamBytes, start, pngFile, 0, length);
                chunks.Add(pngFile);
            }

            return chunks;
        }
    }
}

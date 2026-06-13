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
        private readonly PriorityQueue<SubtitleJob, int> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly object _lockObj = new();

        public event Action? OnChange;

        public SubtitleStreamItem? ActiveStream { get; private set; }

        public bool IsVisible { get; private set; }

        public void NotifyStateChanged() => OnChange?.Invoke();

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
                for (int i = 0; i < metadata.SubtitleStreams.Length - 1; i++)
                {
                    _queue.Enqueue(new SubtitleJob { Metadata = metadata, Index = i }, 10);
                    _signal.Release();
                }
            }

            NotifyStateChanged();
        }

        public void PriorityEnqueue(VideoMetadata metadata, int index)
        {
            Console.WriteLine("Priority!");

            lock (_lockObj)
            {
                _queue.Enqueue(new SubtitleJob { Metadata = metadata, Index = index }, 0);
                _signal.Release();
            }

            NotifyStateChanged();
        }

        public async Task<SubtitleJob> DequeueJobAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _queue.TryDequeue(out var metadata, out int priority);
            NotifyStateChanged();
            return metadata!;
        }

    }

    public class SubtitleManager : BackgroundService
    {
        public SubtitleQueue _queueService;

        public SubtitleManager(SubtitleQueue queue)
        {
            _queueService = queue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SubtitleJob metadata = await _queueService.DequeueJobAsync(stoppingToken);

                await ExtractSubtitleImagesToBase64Async(metadata);

                _queueService.NotifyStateChanged();
            }
        }

        public async Task ExtractSubtitleImagesToBase64Async(SubtitleJob subtitleJob)
        {
            List<string> base64Images = new List<string>();

            string singlePassArgs = $"-probesize 50M -analyzeduration 50M -f lavfi -i color=c=black@0:s={subtitleJob.Metadata.Width}x{subtitleJob.Metadata.Height} -i \"{subtitleJob.Metadata.FilePath}\" -filter_complex \"[0:v][1:s:{subtitleJob.Index}]overlay,mpdecimate\" -vsync vfr -pix_fmt rgba -frames:v 50 -f image2pipe -c:v png pipe:1";

            Console.WriteLine(singlePassArgs);

            Console.WriteLine("Starting Combined Image Extraction");

            ProcessStartInfo pngStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\FFmpeg\App\bin\ffmpeg.exe",
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
                    //Task<string> readErrorTask = pngProcess.StandardError.ReadToEndAsync();

                    await readOutputTask;
                    await pngProcess.WaitForExitAsync();

                    Console.WriteLine("Finished Extracting");

                    byte[] allPngBytes = pngStream.ToArray();

                    // Slice the continuous pipe buffer into distinct individual PNG byte arrays
                    List<byte[]> individualImages = SplitPngStream(allPngBytes);
                    foreach (var imgBytes in individualImages)
                    {
                        base64Images.Add($"data:image/png;base64,{Convert.ToBase64String(imgBytes)}");
                    }
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

            // Locate the start indexes of each PNG file inside the master byte array
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

            // Extract individual byte slices
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

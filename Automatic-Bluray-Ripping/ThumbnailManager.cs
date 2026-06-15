using System.Collections.Concurrent;
using System.Diagnostics;

namespace Automatic_Bluray_Ripping
{
    public class ThumbnailQueue
    {
        private readonly ConcurrentQueue<VideoMetadata> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public event Action? OnProgressUpdated;

        public int GetQueueCount() => _queue.Count;

        public void EnqueueJob(VideoMetadata metadata)
        {
            _queue.Enqueue(metadata);
            _signal.Release();
            UpdateThumbnails();
        }

        public async Task<VideoMetadata> DequeueJobAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _queue.TryDequeue(out var metadata);
            UpdateThumbnails();
            return metadata!;
        }

        public void UpdateThumbnails()
        {
            OnProgressUpdated?.Invoke();
        }
    }

    public class ThumbnailManager : BackgroundService
    {
        private readonly ThumbnailQueue _queueService;

        public ThumbnailManager(ThumbnailQueue queueService)
        {
            _queueService = queueService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                VideoMetadata metadata = await _queueService.DequeueJobAsync(stoppingToken);

                await ExtractThumbnailToBase64Async(metadata);

                _queueService.UpdateThumbnails();
            }
        }

        public async Task ExtractThumbnailToBase64Async(VideoMetadata metadata)
        {
            if (!File.Exists(metadata.FilePath))
                return;

            string timeOffset = "00:01:00";

            if (metadata.Duration < 65000)
                timeOffset = $"{TimeSpan.FromMilliseconds(metadata.Duration/2):hh\\:mm\\:ss}";

            string arguments = $"-ss {timeOffset} -discard nokey -i \"{metadata.FilePath}\" -an -sn -frames:v 1 -f image2 -c:v mjpeg pipe:1";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = @"C:\FFmpeg\App\bin\ffmpeg.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                Task<string> error = process.StandardError.ReadToEndAsync();

                using (MemoryStream ms = new MemoryStream())
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(ms);
                    await process.WaitForExitAsync();

                    byte[] imageBytes = ms.ToArray();

                    string errorOutput = await error;

                    if (imageBytes.Length == 0 || process.ExitCode != 0)
                    {
                        Console.WriteLine($"Image Extraction failed for {metadata.Name}");
                        Console.WriteLine(errorOutput);
                        return;
                    }

                    metadata.ThumbnailBase64 = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}";
                }
            }
        }
    }
}

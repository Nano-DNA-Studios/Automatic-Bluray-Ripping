using System.Collections.Concurrent;
using System.Diagnostics;

namespace Automatic_Bluray_Ripping
{
    public class ThumbnailQueue
    {
        private readonly ConcurrentQueue<VideoMetadata> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public void EnqueueJob(VideoMetadata metadata)
        {
            _queue.Enqueue(metadata);
            _signal.Release();
        }

        public async Task<VideoMetadata> DequeueJobAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _queue.TryDequeue(out var metadata);
            return metadata!;
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
            }
        }

        public async Task ExtractThumbnailToBase64Async(VideoMetadata metadata)
        {
            if (!File.Exists(metadata.FilePath))
                return;

            string timeOffset = "00:01:00";

            if (metadata.Duration < 65)
            {
                timeOffset = "00:00:05";
            }

            string arguments = $"-ss {timeOffset} -i \"{metadata.FilePath}\" -vframes 1 -f image2 -c:v mjpeg pipe:1";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = @"C:\FFmpeg\App\bin\ffmpeg.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                using (MemoryStream ms = new MemoryStream())
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(ms);
                    await process.WaitForExitAsync();

                    byte[] imageBytes = ms.ToArray();

                    if (imageBytes.Length == 0 || process.ExitCode != 0)
                    {
                        Console.WriteLine($"Image Extraction failed for {metadata.Name}");
                        return;
                    }

                    metadata.ThumbnailBase64 = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}";
                }
            }
        }
    }
}

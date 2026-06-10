using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Automatic_Bluray_Ripping
{
    public class TranscodeManager : BackgroundService
    {
        public List<MKVBackup> Backups { get; set; }

        public string[] Presets { get; set; }

        public event Action? OnProgressUpdated;

        private readonly TranscodeQueueService _queueService;

        public TranscodeManager(TranscodeQueueService queueService)
        {
            Backups = new List<MKVBackup>();
            Presets = [];
            _queueService = queueService;
        }

        public void AddToTranscodeQueue(MKVBackup backup)
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

                    _queueService.EnqueueJob(new TranscodeJob()
                    {
                        Name = metadata.Name,
                        InputFilePath = metadata.FilePath,
                        OutputFilePath = Path.Combine(exportDir, Path.GetFileName(metadata.Name) + ".mkv"),
                        Args = metadata.GetTranscodeArgs(),
                        ThumbnailBase64 = metadata.ThumbnailBase64,
                        TranscodePreset = backup.TranscodePreset
                    });
                }
            }

            Backups.Remove(backup);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait until a job is added to the queue
                    var job = await _queueService.DequeueJobAsync(stoppingToken);

                    Console.WriteLine($"Starting transcode job for: {job.InputFilePath}");
                    await RunHandbrakeAsync(job, stoppingToken);
                    Console.WriteLine($"Finished transcode job for: {job.InputFilePath}");

                    _queueService.Completed(job);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred during transcoding background execution. {ex.Message}");
                }
            }
        }

        private async Task RunHandbrakeAsync(TranscodeJob job, CancellationToken cancellationToken)
        {
            Console.WriteLine("Run Handbrake Async");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\HandBrake\HandBrakeCLI.exe",
                Arguments = job.GetFullArgument(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                // 1. Attach the event handlers to stream output live to the console
                process.OutputDataReceived += (sender, e) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(e.Data) && e.Data.StartsWith("Encoding:"))
                        {
                            var match = Regex.Match(e.Data, @"(\d+(?:\.\d+)?)\s*%");

                            if (match.Success)
                            {
                                string percentValue = match.Groups[1].Value;

                                if (double.TryParse(percentValue, out double percentDbl))
                                {
                                    _queueService.UpdateProgress(percentDbl);
                                }
                            }
                        }
                    }
                    catch
                    {
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // HandBrake writes its progress ticks (e.g., % done) here
                        //Console.WriteLine($"[HandBrake LOG] {e.Data}");
                    }
                };

                // 2. Start the process
                process.Start();

                // 3. CRITICAL: Begin reading the streams asynchronously
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 4. Await the exit of the application safely
                await process.WaitForExitAsync();

                _queueService.UpdateProgress(100);
            }
        }
    }
}

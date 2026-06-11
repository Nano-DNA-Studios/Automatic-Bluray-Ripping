using NanoDNA.ProcessRunner;
using NanoDNA.ProcessRunner.Results;
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _queueService.DequeueJobAsync(stoppingToken);

                    Console.WriteLine($"Starting transcode job for: {job.InputFilePath}");
                    await RunHandbrakeAsync(job, stoppingToken);
                    Console.WriteLine($"Finished transcode job for: {job.InputFilePath}");

                    _queueService.Completed(job);

                    if (job.RemoveOnCompletion)
                        File.Delete(job.InputFilePath);

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
            ProcessRunner process = new ProcessRunner("HandBrakeCLI");

            process.STDOutputReceived += (sender, e) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;

                    if (!e.Data.StartsWith("Encoding:"))
                        return;

                    Match match = Regex.Match(e.Data, @"(\d+(?:\.\d+)?)\s*%");

                    if (!match.Success)
                        return;

                    string percentValue = match.Groups[1].Value;

                    if (double.TryParse(percentValue, out double percentDbl))
                        _queueService.UpdateProgress(percentDbl);
                }
                catch
                {
                    Console.WriteLine(e.Data);
                }
            };

            ProcessResult result = (await process.RunAsync(job.GetFullArgument())).Content;

            _queueService.UpdateProgress(100);
        }
    }
}

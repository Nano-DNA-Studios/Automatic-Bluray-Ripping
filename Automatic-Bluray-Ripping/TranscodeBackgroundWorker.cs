using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Automatic_Bluray_Ripping
{
    public class TranscodeQueueService
    {
        private readonly ConcurrentQueue<TranscodeJob> _completedQueue = new();
        private readonly ConcurrentQueue<TranscodeJob> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public event Action<double>? OnProgressUpdated;
        public event Action? OnQueueChanged; // Alerts UI when item starts/completes
        public double CurrentProgressPercent { get; private set; }
        public TranscodeJob? CurrentJob { get; private set; }

        public void EnqueueJob(TranscodeJob job)
        {
            _queue.Enqueue(job);
            _signal.Release();
            OnQueueChanged?.Invoke();
        }

        public async Task<TranscodeJob> DequeueJobAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _queue.TryDequeue(out var job);
            CurrentJob = job;
            OnQueueChanged?.Invoke();
            return job!;
        }

        public void Completed(TranscodeJob job)
        {
            _completedQueue.Enqueue(job);
            CurrentJob = null;
            OnQueueChanged?.Invoke();
        }

        public void UpdateProgress(double progress)
        {
            CurrentProgressPercent = progress;
            if (CurrentJob != null)
            {
                CurrentJob.Progress = progress;
            }
            OnProgressUpdated?.Invoke(progress);
        }

        public TranscodeJob[] GetJobs()
        {
            var jobs = _completedQueue.ToList();
            if (CurrentJob != null)
            {
                jobs.Add(CurrentJob);
            }
            jobs.AddRange(_queue.ToArray());
            return jobs.ToArray();
        }
    }

    public class TranscodeBackgroundWorker : BackgroundService
    {
        private readonly TranscodeQueueService _queueService;

        public double CurrentProgressPercent { get; set; }

        public TranscodeBackgroundWorker(TranscodeQueueService queueService)
        {
            _queueService = queueService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Running");
            Console.WriteLine(stoppingToken.IsCancellationRequested);

            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine("Running Loop");

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
                catch
                {
                    Console.WriteLine("Idk");
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

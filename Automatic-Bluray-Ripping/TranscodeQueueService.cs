using System.Collections.Concurrent;

namespace Automatic_Bluray_Ripping
{
    public class TranscodeQueueService
    {
        private readonly ConcurrentQueue<TranscodeJob> _completedQueue = new();
        private readonly ConcurrentQueue<TranscodeJob> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public event Action<double>? OnProgressUpdated;
        public event Action? OnQueueChanged;
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
                CurrentJob.Progress = progress;

            OnProgressUpdated?.Invoke(progress);
        }

        public TranscodeJob[] GetJobs()
        {
            var jobs = _completedQueue.ToList();

            if (CurrentJob != null)
                jobs.Add(CurrentJob);

            jobs.AddRange(_queue.ToArray());

            return jobs.ToArray();
        }
    }
}

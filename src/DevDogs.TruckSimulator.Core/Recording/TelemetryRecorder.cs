using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Recording;

/// <summary>
/// Records telemetry on a background thread, so writing to disk never delays SimHub's update loop.
/// Updates are queued; when the queue is full (the disk can't keep up) updates are dropped and
/// counted rather than blocking. Safe to call from any thread.
/// </summary>
/// <param name="queueCapacity">The most updates held in memory waiting to be written.</param>
public sealed class TelemetryRecorder(int queueCapacity = 600) : IDisposable
{
    private readonly object _gate = new();
    private volatile BlockingCollection<RecordedTick>? _queue;
    private Thread? _worker;
    private long _recorded;
    private long _dropped;

    /// <summary>Gets a value indicating whether a recording is in progress.</summary>
    public bool IsRecording => _queue is not null;

    /// <summary>Gets the number of updates written to the current or last recording.</summary>
    public long RecordedTicks => Interlocked.Read(ref _recorded);

    /// <summary>Gets the number of updates dropped from the current or last recording because the queue was full.</summary>
    public long DroppedTicks => Interlocked.Read(ref _dropped);

    /// <summary>Gets the error that stopped the last recording, if any.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Starts recording into <paramref name="destination"/>. Does nothing when already recording.
    /// </summary>
    /// <param name="destination">The stream to write to; the recorder disposes it when stopped.</param>
    /// <param name="pluginVersion">The plugin version recorded in the header.</param>
    /// <param name="startedAt">The UTC time recording started.</param>
    public void Start(
        Stream destination,
        string pluginVersion,
        DateTime startedAt)
    {
        lock (_gate)
        {
            if (_queue is not null)
            {
                destination.Dispose();
                return;
            }

            Interlocked.Exchange(ref _recorded, 0);
            Interlocked.Exchange(ref _dropped, 0);
            LastError = null;

            var queue = new BlockingCollection<RecordedTick>(queueCapacity);
            var writer = new TelemetryRecordingWriter(destination, pluginVersion, startedAt);
            _worker = new Thread(() => Drain(queue, writer)) { IsBackground = true, Name = "DevDogs.TruckSimulator recorder" };
            _worker.Start();
            _queue = queue;
        }
    }

    /// <summary>
    /// Queues one update when recording; otherwise does nothing.
    /// </summary>
    /// <param name="at">The UTC time of the update.</param>
    /// <param name="telemetry">The snapshot; immutable, so it is safe to hand to the writer thread.</param>
    public void Record(
        DateTime at,
        TruckTelemetry telemetry)
    {
        var queue = _queue;
        if (queue is null)
        {
            return;
        }

        try
        {
            if (!queue.TryAdd(new RecordedTick { At = at, Telemetry = telemetry }))
            {
                Interlocked.Increment(ref _dropped);
            }
        }
        catch (InvalidOperationException)
        {
            // Stop() completed the queue between the null check and TryAdd; the update is simply not recorded.
        }
    }

    /// <summary>
    /// Stops recording and waits for queued updates to be written. Does nothing when not recording.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            var queue = _queue;
            if (queue is null)
            {
                return;
            }

            _queue = null;
            queue.CompleteAdding();
            _worker?.Join();
            _worker = null;
            queue.Dispose();
        }
    }

    /// <summary>
    /// Stops any recording in progress.
    /// </summary>
    public void Dispose() => Stop();

    /// <summary>
    /// Writes queued updates until recording stops or writing fails.
    /// </summary>
    /// <param name="queue">The queue to drain.</param>
    /// <param name="writer">The recording writer; disposed when draining ends.</param>
    private void Drain(
        BlockingCollection<RecordedTick> queue,
        TelemetryRecordingWriter writer)
    {
        try
        {
            foreach (var tick in queue.GetConsumingEnumerable())
            {
                writer.Write(tick);
                Interlocked.Increment(ref _recorded);
            }
        }
        catch (Exception ex)
        {
            // A write failure (disk full, file removed) ends the recording; it must not crash SimHub.
            LastError = ex;
            _queue = null;
            queue.CompleteAdding();
        }
        finally
        {
            try
            {
                writer.Dispose();
            }
            catch (Exception ex)
            {
                LastError ??= ex;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Tracks a job through taken, loading, ongoing and completed or abandoned, inferred from the job
/// ids, the route advisor's distance and the speed limit. The original plugin's heuristic, kept for
/// compatibility; the game's own delivery and cancellation events are a candidate replacement.
/// </summary>
public sealed class JobStatusSection : ITelemetrySection
{
    /// <summary>Property: the job status name (None, Taken, Loading, Ongoing, Completed, Abandoned).</summary>
    public const string Status = "Job.Status";

    /// <summary>Property: the game reports that a job is active.</summary>
    public const string InProgress = "Job.InProgress";

    /// <summary>Action: forgets the tracked job so the status is worked out again.</summary>
    public const string JobStatusReset = "JobStatusReset";

    /// <summary>Event: a new job was taken.</summary>
    public const string JobTaken = "JobTaken";

    /// <summary>Event: the route to the cargo ended, so the cargo is being loaded.</summary>
    public const string JobLoading = "JobLoading";

    /// <summary>Event: the delivery route started.</summary>
    public const string JobOngoing = "JobOngoing";

    /// <summary>Event: the job was delivered.</summary>
    public const string JobCompleted = "JobCompleted";

    /// <summary>Event: the job ended without reaching the destination.</summary>
    public const string JobAbandoned = "JobAbandoned";

    /// <summary>Event: the tracked status was reset by the action.</summary>
    public const string JobReset = "JobReset";

    /// <summary>How long the route distance must stay at 0 before the route counts as ended.</summary>
    private static readonly TimeSpan _routeEndGrace = TimeSpan.FromSeconds(2);

    /// <summary>How long Completed or Abandoned is shown before returning to None.</summary>
    private static readonly TimeSpan _finishedHold = TimeSpan.FromSeconds(2);

    /// <summary>Route distance, in metres, that counts as having reached the destination.</summary>
    private const float NearDestinationMetres = 30f;

    /// <summary>A change in route distance, in metres, within the frame window that counts as a new route.</summary>
    private const float RouteJumpMetres = 300f;

    /// <summary>Number of recent route distances kept to detect a jump.</summary>
    private const int FrameWindow = 20;

    /// <summary>Zero distances are ignored as glitches unless at least this many are in the window.</summary>
    private const int ZeroFramesToKeep = 5;

    private enum JobStatus
    {
        None,
        Taken,
        Loading,
        Ongoing,
        Completed,
        Abandoned,
    }

    private readonly List<float> _recentDistances = [0f];

    private JobStatus _status;
    private string _trackedJob = "";
    private DateTime _holdStatusUntil = DateTime.MinValue;
    private bool _hasSeenSpeedLimit;
    private bool _hasBeenNearDestination;
    private bool _routeAtZero;
    private DateTime _routeEndsAt;
    private bool _routeJumped;
    private float _previousDistance;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(Status, nameof(JobStatus.None));
        host.AddProperty(InProgress, false);
        host.AddAction(JobStatusReset, Reset);
        host.AddEvent(JobTaken);
        host.AddEvent(JobLoading);
        host.AddEvent(JobOngoing);
        host.AddEvent(JobCompleted);
        host.AddEvent(JobAbandoned);
        host.AddEvent(JobReset);
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        Advance(telemetry, now, output);
        TrackRouteJumps(telemetry.Navigation.Distance);

        output.SetProperty(Status, _status.ToString());
        output.SetProperty(InProgress, telemetry.Job.OnJob);
    }

    /// <summary>
    /// Identifies a job by its cargo, companies and cities; empty when there is no job.
    /// </summary>
    /// <param name="job">The current job.</param>
    /// <returns>The lower-case job key, or an empty string.</returns>
    internal static string JobKey(JobState job)
    {
        string[] parts = [job.CargoId, job.CompanySourceId, job.CitySourceId, job.CompanyDestinationId, job.CityDestinationId];

        return parts.All(string.IsNullOrEmpty)
            ? ""
            : string.Join("__", parts).Replace(" ", "-").ToLowerInvariant();
    }

    /// <summary>
    /// Moves the state machine at most one step per update.
    /// </summary>
    /// <param name="telemetry">The current telemetry.</param>
    /// <param name="now">The current time.</param>
    /// <param name="output">Receives events.</param>
    private void Advance(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var job = JobKey(telemetry.Job);
        if (job.Length == 0)
        {
            _status = JobStatus.None;
            return;
        }

        var distance = telemetry.Navigation.Distance;
        var routeEnded = UpdateRouteEnded(distance, now);

        if (_status == JobStatus.None && job != _trackedJob && now > _holdStatusUntil)
        {
            _trackedJob = job;
            _hasSeenSpeedLimit = false;
            _hasBeenNearDestination = false;
            _holdStatusUntil = DateTime.MaxValue;
            MoveTo(JobStatus.Taken, JobTaken, output);
            return;
        }

        if (_status == JobStatus.Taken && routeEnded)
        {
            MoveTo(JobStatus.Loading, JobLoading, output);
            return;
        }

        if (_status is JobStatus.Taken or JobStatus.Loading && distance > 0)
        {
            MoveTo(JobStatus.Ongoing, JobOngoing, output);
            return;
        }

        if (_status == JobStatus.Ongoing)
        {
            _hasSeenSpeedLimit |= telemetry.Navigation.SpeedLimitMph > 0;
            _hasBeenNearDestination |= _hasSeenSpeedLimit && distance > 0 && distance < NearDestinationMetres;

            var jobChanged = job != _trackedJob;
            var reachedDestination = _hasSeenSpeedLimit && _hasBeenNearDestination;

            if (reachedDestination && (jobChanged || routeEnded || _routeJumped))
            {
                _holdStatusUntil = now + _finishedHold;
                MoveTo(JobStatus.Completed, JobCompleted, output);
                return;
            }

            if (!reachedDestination && jobChanged)
            {
                _holdStatusUntil = now + _finishedHold;
                MoveTo(JobStatus.Abandoned, JobAbandoned, output);
                return;
            }
        }

        if (_status is JobStatus.Completed or JobStatus.Abandoned && now > _holdStatusUntil)
        {
            _status = JobStatus.None;
            _holdStatusUntil = now + _finishedHold;
            _hasSeenSpeedLimit = false;
            _hasBeenNearDestination = false;
        }
    }

    /// <summary>
    /// A route counts as ended once its distance has stayed at 0 for the grace period.
    /// </summary>
    /// <param name="distance">The route distance left.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when the route has ended.</returns>
    private bool UpdateRouteEnded(
        float distance,
        DateTime now)
    {
        if (distance == 0 && !_routeAtZero)
        {
            _routeAtZero = true;
            _routeEndsAt = now + _routeEndGrace;
        }
        else if (distance > 0)
        {
            _routeAtZero = false;
        }

        return _routeAtZero && now > _routeEndsAt;
    }

    /// <summary>
    /// Flags a jump in route distance across the recent window, which happens when the route
    /// advisor switches to a new route. The window lags one update behind, as in the original.
    /// </summary>
    /// <param name="distance">The route distance left.</param>
    private void TrackRouteJumps(float distance)
    {
        if (_recentDistances.Count >= FrameWindow)
        {
            _recentDistances.RemoveAt(0);
        }

        _recentDistances.Add(_previousDistance);
        _previousDistance = distance;

        if (_recentDistances.Count(d => d == 0) < ZeroFramesToKeep)
        {
            _recentDistances.RemoveAll(d => d == 0);
        }

        if (_recentDistances.Count > 0)
        {
            _routeJumped = _recentDistances.Max() - _recentDistances.Min() > RouteJumpMetres;
        }
    }

    /// <summary>
    /// Forgets the tracked job; the next update detects the current job as newly taken.
    /// </summary>
    /// <param name="output">Receives the reset event and status.</param>
    private void Reset(ISectionOutput output)
    {
        _status = JobStatus.None;
        _trackedJob = "";
        _hasSeenSpeedLimit = false;
        _hasBeenNearDestination = false;
        _routeAtZero = false;
        _holdStatusUntil = DateTime.MinValue;

        output.SetProperty(Status, _status.ToString());
        output.TriggerEvent(JobReset);
    }

    /// <summary>
    /// Changes status and triggers the matching event.
    /// </summary>
    /// <param name="status">The new status.</param>
    /// <param name="eventName">The event to trigger.</param>
    /// <param name="output">Receives the event.</param>
    private void MoveTo(
        JobStatus status,
        string eventName,
        ISectionOutput output)
    {
        _status = status;
        output.TriggerEvent(eventName);
    }
}

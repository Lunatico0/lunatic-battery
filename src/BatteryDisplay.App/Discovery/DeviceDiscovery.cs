using System.Diagnostics;
using BatteryDisplay.App.Logging;
using BatteryDisplay.Core;

namespace BatteryDisplay.App.Discovery;

/// <summary>
/// Composes every detection mechanism into one list: sources in order, the first provider wins on a repeated Id,
/// and a source that throws anyway is logged and skipped so the others still show.
/// Providers that report the same identifying PnP container are one physical device seen by several sources: they
/// show once, as a <see cref="MergedProvider"/> of the most direct channel (vendor HID, then XInput, then Bluetooth;
/// the earlier source on a tie), in the place of the first of them. A provider without a container is never merged.
/// Sources run in parallel, each bounded by a timeout: one that does not answer in time adds its last successful
/// result (nothing if it never had one), so its devices keep their place, and it is not called again until its
/// pending call returns, so a stuck source never piles up threads. The first round logs what each source found and
/// how long it took; later rounds log only failures and timeouts.
/// </summary>
public sealed class DeviceDiscovery(IReadOnlyList<IDeviceSource> sources, Action<string>? log = null, TimeSpan? sourceTimeout = null)
    : IDeviceSource
{
    /// <summary>
    /// Discovery only reads the PnP store (HID and Bluetooth) or polls four XInput slots, which takes milliseconds,
    /// but CfgMgr32 and SetupAPI calls can wait seconds on the PnP lock while Windows installs a driver for a
    /// device that was just plugged in. Same bound as a battery read.
    /// </summary>
    public static readonly TimeSpan DefaultSourceTimeout = TimeSpan.FromSeconds(5);

    private readonly record struct Outcome(IReadOnlyList<IBatteryProvider> Providers, Exception? Error, TimeSpan Took);

    private readonly Action<string> _log = log ?? AppLog.For("discovery");
    private readonly TimeSpan _timeout = sourceTimeout ?? DefaultSourceTimeout;
    private readonly Task<Outcome>?[] _pending = new Task<Outcome>?[sources.Count];
    // Empty until a source first answers, so one that never did still adds nothing.
    private readonly IReadOnlyList<IBatteryProvider>[] _lastGood = Enumerable.Repeat<IReadOnlyList<IBatteryProvider>>([], sources.Count).ToArray();
    private readonly Lock _gate = new();
    private bool _firstRoundLogged;

    public IReadOnlyList<IBatteryProvider> Discover()
    {
        Task<Outcome>[] calls;
        lock (_gate)
        {
            calls = new Task<Outcome>[sources.Count];
            for (var i = 0; i < sources.Count; i++)
            {
                // A call that outlived its round is awaited again instead of starting a second one.
                if (_pending[i] is not { IsCompleted: false })
                    _pending[i] = Start(sources[i]);
                calls[i] = _pending[i]!;
            }
        }

        // Started together, so one shared deadline bounds each source by the timeout.
        Task.WaitAll(calls, _timeout);

        var found = new List<IBatteryProvider>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        lock (_gate)
        {
            var firstRound = !_firstRoundLogged;
            _firstRoundLogged = true;
            for (var i = 0; i < calls.Length; i++)
            {
                var name = sources[i].GetType().Name;
                IReadOnlyList<IBatteryProvider> providers;
                if (!calls[i].IsCompleted)
                {
                    _log($"{name}: discovery timed out after {_timeout.TotalSeconds:0.#} s, kept its last result");
                    providers = _lastGood[i];
                }
                else if (calls[i].Result.Error is { } ex)
                {
                    _log($"{name}: discovery failed: {ex.GetType().Name}: {ex.Message}");
                    continue;
                }
                else
                {
                    providers = _lastGood[i] = calls[i].Result.Providers;
                    if (firstRound)
                        _log($"{name}: {providers.Count} device(s) in {calls[i].Result.Took.TotalMilliseconds:0} ms");
                }

                foreach (var provider in providers)
                {
                    if (seen.Add(provider.Id))
                        found.Add(provider);
                }
            }
        }
        return Merge(found);
    }

    private static IReadOnlyList<IBatteryProvider> Merge(List<IBatteryProvider> found)
    {
        // Slot of each container's group in the result, so the merged device keeps the first member's place.
        var groups = new Dictionary<Guid, List<IBatteryProvider>>();
        var slots = new List<List<IBatteryProvider>>();
        foreach (var provider in found)
        {
            if (PhysicalIdentity.Identifies(provider.ContainerId) && groups.TryGetValue(provider.ContainerId!.Value, out var group))
            {
                group.Add(provider);
                continue;
            }
            List<IBatteryProvider> slot = [provider];
            if (PhysicalIdentity.Identifies(provider.ContainerId))
                groups[provider.ContainerId!.Value] = slot;
            slots.Add(slot);
        }

        return slots.Select(members =>
        {
            if (members.Count == 1)
                return members[0];
            // MinBy keeps the first of equals, so a tie goes to the earlier source.
            var primary = members.MinBy(p => p.Channel)!;
            return new MergedProvider(primary, members.Where(p => !ReferenceEquals(p, primary)).ToList());
        }).ToList();
    }

    // Never faults, so waiting on it cannot throw and the error is logged on the caller's thread.
    private static Task<Outcome> Start(IDeviceSource source) => Task.Run(() =>
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return new Outcome(source.Discover(), null, Stopwatch.GetElapsedTime(started));
        }
        catch (Exception ex)
        {
            return new Outcome([], ex, Stopwatch.GetElapsedTime(started));
        }
    });
}

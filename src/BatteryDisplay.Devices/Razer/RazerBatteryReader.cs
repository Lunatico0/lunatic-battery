using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Razer;

/// <param name="ResponseWait">Pause between sending a request and reading its answer.</param>
/// <param name="RetryDelay">Pause before a retry.</param>
/// <param name="MaxTries">Attempts per command before giving up.</param>
public sealed record RazerTiming(TimeSpan ResponseWait, TimeSpan RetryDelay, int MaxTries)
{
    // Same values OpenRazer uses for this mouse (RAZER_NEW_MOUSE_RECEIVER_WAIT_US, razer_send_payload).
    public static RazerTiming Default { get; } = new(TimeSpan.FromMilliseconds(31), TimeSpan.FromMilliseconds(10), 5);
}

/// <summary>Reads battery level and charging state with the two GET commands 0x07/0x80 and 0x07/0x84.</summary>
public sealed class RazerBatteryReader(IRazerTransport transport, byte transactionId, RazerTiming timing)
{
    private enum Outcome { Answered, Unsupported, NoAnswer }

    public async Task<BatteryStatus?> ReadAsync(CancellationToken ct)
    {
        var (levelOutcome, rawLevel) = await QueryAsync(RazerReport.BatteryLevel, ct);
        if (levelOutcome == Outcome.NoAnswer)
            return null;

        int? percent = levelOutcome == Outcome.Answered ? RazerReport.ToPercent(rawLevel) : null;

        var (chargeOutcome, rawCharging) = await QueryAsync(RazerReport.ChargingStatus, ct);
        var charge = chargeOutcome != Outcome.Answered
            ? ChargeState.Unknown
            : rawCharging == 0
                ? ChargeState.Discharging
                : percent == 100 ? ChargeState.Full : ChargeState.Charging;

        return new BatteryStatus(percent, charge);
    }

    private async Task<(Outcome, byte)> QueryAsync(RazerCommand command, CancellationToken ct)
    {
        var request = RazerReport.BuildRequest(transactionId, command);

        for (var attempt = 1; attempt <= timing.MaxTries; attempt++)
        {
            if (attempt > 1)
                await Task.Delay(timing.RetryDelay, ct);

            if (!transport.TrySend(request))
                return (Outcome.NoAnswer, 0);

            await Task.Delay(timing.ResponseWait, ct);

            var bytes = transport.TryReceive();
            if (bytes is null)
                return (Outcome.NoAnswer, 0);

            // A garbled answer or one meant for another request (Synapse shares the interface) is retried.
            if (!RazerReport.TryParse(bytes, out var response)
                || response.CommandClass != command.Class
                || response.CommandId != command.Id)
                continue;

            switch (response.Status)
            {
                case RazerStatus.Successful:
                    return (Outcome.Answered, response.Value);
                case RazerStatus.NotSupported:
                case RazerStatus.Failure:
                    return (Outcome.Unsupported, 0);
                default:
                    // Busy or Timeout: a busy read carries no reliable payload, and Timeout means the mouse is asleep.
                    continue;
            }
        }

        return (Outcome.NoAnswer, 0);
    }
}

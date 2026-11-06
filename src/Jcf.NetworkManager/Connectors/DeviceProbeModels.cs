using Jcf.NetworkManager.Domain;

namespace Jcf.NetworkManager.Connectors;

public sealed record DetectedMember(string Name, string Model, string? Zone, string? IpMasked, string Confidence);
public sealed record DetectedClient(string Mac,string? Ssid,string? Band,decimal? RssiDbm,decimal? SnrDb,int? Vlan);
public sealed record DetectedPort(string Name,string? State,long? RxBytes,long? TxBytes,long? RxErrors,long? TxErrors,decimal? PoeWatts);
public sealed record RfObservation(int? Channel,int? WidthMhz,decimal? UtilizationPct,decimal? NoiseFloorDbm,decimal? ClientRetryPct,string Source);
public sealed record ProbeResult(
    bool Connected, string? Firmware, int? ApCount, int? ClientCount,
    int? VlanCount, int? PortCount,
    Dictionary<string,string> Arm, Dictionary<string,bool> Fields,
    List<DetectedMember> Members, string? Issue,
    List<string> ApiChecks,RfObservation? Observation = null, List<DetectedPort>? PortReadings = null, List<DetectedClient>? Clients = null);

public interface IDeviceConnector
{
    bool Supports(DeviceKind kind);
    Task<ProbeResult> ProbeAsync(Device device, CancellationToken cancellationToken);
}

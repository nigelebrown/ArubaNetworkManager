using Jcf.NetworkManager.Domain;

namespace Jcf.NetworkManager.Connectors;

/// <summary>Deterministic simulator, never connects to real devices or writes configuration.</summary>
public sealed class DemoConnector : IDeviceConnector
{
    public bool Supports(DeviceKind kind) => true;
    public Task<ProbeResult> ProbeAsync(Device device,CancellationToken ct)
    {
        var ap=device.IsAccessPoint;
        return Task.FromResult(new ProbeResult(true,ap?"8.10.0.21":"10.16.1000",ap?2:null,ap?0:null,ap?null:9,
            ap?null:48,ap?new Dictionary<string,string>() { ["80mhz-support"]="enabled",["air-time-fairness-mode"]="default-access",
                ["band-steering-mode"]="prefer-higher-band",["min-tx-power"]="9",["max-tx-power"]="127" }:new Dictionary<string,string>(),
            ap?new Dictionary<string,bool>(){["ChannelUtilization"]=true,["NoiseFloor"]=true,["RSSI"]=false}:new Dictionary<string,bool>(),
            ap?new List<DetectedMember> {
                new("LAB-AP-505","AP-505","Test Lab","192.0.2.xxx","SIMULATED"),
                new("LAB-AP-535","AP-535","Test Lab","192.0.2.xxx","SIMULATED") }:new List<DetectedMember>(),null,
            new() {"SIMULATED login","SIMULATED read","NO device changes"},
            ap?new RfObservation(149,20,68,-92,null,"SIMULATED data — not live RF measurements"):null));
    }
}

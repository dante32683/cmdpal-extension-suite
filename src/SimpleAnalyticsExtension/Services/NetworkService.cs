using System;
using Windows.Networking.Connectivity;

namespace SimpleAnalyticsExtension;

internal sealed class NetworkInfo
{
    public bool Connected { get; init; }
    public bool IsLimited { get; init; }
    public bool IsWifi { get; init; }
    public string Ssid { get; init; } = string.Empty;
    public int SignalBars { get; init; }  // 0-5 raw Windows signal bars
    public double ReceiveMbps { get; init; }
    public double TransmitMbps { get; init; }
}

internal sealed class NetworkService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    public NetworkInfo GetNetworkInfo()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null)
                return new NetworkInfo();

            var level = profile.GetNetworkConnectivityLevel();
            var connected = level == NetworkConnectivityLevel.InternetAccess;
            var isLimited = level is NetworkConnectivityLevel.LocalAccess
                                  or NetworkConnectivityLevel.ConstrainedInternetAccess;

            var isWifi = profile.IsWlanConnectionProfile;
            var ssid = isWifi
                ? profile.WlanConnectionProfileDetails?.GetConnectedSsid() ?? string.Empty
                : string.Empty;

            var signalBars = (int)(profile.GetSignalBars() ?? 0);

            var adapter = profile.NetworkAdapter;
            double rxMbps = 0, txMbps = 0;
            if (adapter is not null)
            {
                rxMbps = adapter.InboundMaxBitsPerSecond / 1_000_000.0;
                txMbps = adapter.OutboundMaxBitsPerSecond / 1_000_000.0;
            }

            return new NetworkInfo
            {
                Connected = connected,
                IsLimited = isLimited,
                IsWifi = isWifi,
                Ssid = ssid,
                SignalBars = signalBars,
                ReceiveMbps = rxMbps,
                TransmitMbps = txMbps,
            };
        }
        catch
        {
            return new NetworkInfo();
        }
    }
}

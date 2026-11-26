using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Veltrix.Models;

namespace Veltrix.Services
{
    public class NetworkMonitorService
    {
        private readonly Dictionary<string, (long Received, long Sent)>
            _previous = new();

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private long _totalDownloaded;
        private long _totalUploaded;

        public string SelectedAdapterId { get; private set; }
            = AppSettingsService.LoadAdapter();

        public string? ActiveAutoAdapterId { get; private set; }

        public IReadOnlyList<NetworkAdapterInfo> Adapters
        { get; private set; } = Array.Empty<NetworkAdapterInfo>();

        private readonly HashSet<string> _monitoredIds = new();

        public NetworkMonitorService()
        {
            GetStats();
        }

        private static bool Supported(NetworkInterface a)
        {
            return a.NetworkInterfaceType is
                NetworkInterfaceType.Wireless80211 or
                NetworkInterfaceType.Ethernet or
                NetworkInterfaceType.GigabitEthernet or
                NetworkInterfaceType.FastEthernetFx or
                NetworkInterfaceType.FastEthernetT or
                NetworkInterfaceType.Ppp or
                NetworkInterfaceType.Tunnel;
        }

        private static bool IsPhysicalCandidate(NetworkInterface a)
        {
            if (a.NetworkInterfaceType is
                NetworkInterfaceType.Ppp or
                NetworkInterfaceType.Tunnel)
                return false;

            string text = (a.Name + " " + a.Description)
                .ToLowerInvariant();

            string[] excluded =
            {
                "virtual",
                "vmware",
                "virtualbox",
                "hyper-v",
                "vethernet",
                "wi-fi direct",
                "wifi direct",
                "loopback",
                "bluetooth",
                "wintun",
                "wireguard",
                "tap-windows",
                "vpn",
                "tailscale",
                "zerotier",
                "host-only",
                "npcap",
                "docker",
                "wsl"
            };

            return !excluded.Any(x => text.Contains(x));
        }

        private static bool HasGateway(NetworkInterface a)
        {
            try
            {
                return a.GetIPProperties()
                    .GatewayAddresses
                    .Any(g =>
                        !g.Address.Equals(IPAddress.Any) &&
                        !g.Address.Equals(IPAddress.IPv6Any) &&
                        !IPAddress.IsLoopback(g.Address));
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }

        private string? DetectAdapter(NetworkInterface[] interfaces)
        {
            var candidates = interfaces
                .Where(a =>
                    a.OperationalStatus == OperationalStatus.Up &&
                    IsPhysicalCandidate(a) &&
                    HasGateway(a))
                .ToArray();

            if (candidates.Length == 0)
                return null;

            // Windows routing එක භාවිත කරන local IP එක හඳුනාගැනීම
            try
            {
                using var socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Dgram,
                    ProtocolType.Udp);

                socket.Connect("1.1.1.1", 53);

                if (socket.LocalEndPoint is IPEndPoint local)
                {
                    var routed = candidates.FirstOrDefault(a =>
                        a.GetIPProperties().UnicastAddresses
                            .Any(x => x.Address.Equals(local.Address)));

                    if (routed != null)
                        return routed.Id;
                }
            }
            catch (SocketException)
            {
            }
            catch (NetworkInformationException)
            {
            }

            // Routing detection අසාර්ථක නම් පෙර adapter එක තබාගැනීම
            var previous = candidates.FirstOrDefault(
                a => a.Id == ActiveAutoAdapterId);

            if (previous != null)
                return previous.Id;

            return candidates
                .OrderByDescending(a => a.Speed)
                .FirstOrDefault()?.Id;
        }

        public bool IsSelected(string id)
        {
            return _monitoredIds.Contains(id);
        }

        public void SelectAdapter(string id)
        {
            AppSettingsService.SaveAdapter(id);
            SelectedAdapterId = id;

            _previous.Clear();
            _monitoredIds.Clear();

            _clock.Restart();
        }

        public NetworkStats GetStats()
        {
            double seconds = Math.Max(
                _clock.Elapsed.TotalSeconds, 0.001);

            _clock.Restart();

            long down = 0;
            long up = 0;

            bool connected = false;

            var current = new HashSet<string>();
            var adapters = new List<NetworkAdapterInfo>();

            var interfaces = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(Supported)
                .ToArray();

            string? previousAuto = ActiveAutoAdapterId;

            ActiveAutoAdapterId =
                SelectedAdapterId == "auto"
                    ? DetectAdapter(interfaces)
                    : null;

            bool autoChanged =
                SelectedAdapterId == "auto" &&
                previousAuto != ActiveAutoAdapterId;

            _monitoredIds.Clear();

            foreach (var a in interfaces)
            {
                var info = new NetworkAdapterInfo
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    Type = a.NetworkInterfaceType.ToString(),
                    Status = a.OperationalStatus.ToString()
                };

                adapters.Add(info);

                try
                {
                    var addresses = a.GetIPProperties()
                        .UnicastAddresses;

                    info.IPv4 = string.Join(", ",
                        addresses.Where(x =>
                            x.Address.AddressFamily ==
                            AddressFamily.InterNetwork)
                        .Select(x => x.Address.ToString()));

                    info.IPv6 = string.Join(", ",
                        addresses.Where(x =>
                            x.Address.AddressFamily ==
                            AddressFamily.InterNetworkV6)
                        .Select(x => x.Address.ToString()));

                    info.Mac = string.Join(":",
                        a.GetPhysicalAddress()
                            .GetAddressBytes()
                            .Select(x => x.ToString("X2")));

                    if (a.OperationalStatus != OperationalStatus.Up)
                        continue;

                    info.LinkSpeed = a.Speed > 0
                        ? $"{a.Speed / 1000000.0:0.##} Mbps"
                        : "—";

                    var s = a.GetIPStatistics();

                    current.Add(a.Id);

                    long d = 0;
                    long u = 0;

                    if (_previous.TryGetValue(a.Id, out var p))
                    {
                        d = Math.Max(0,
                            s.BytesReceived - p.Received);

                        u = Math.Max(0,
                            s.BytesSent - p.Sent);
                    }

                    _previous[a.Id] =
                        (s.BytesReceived, s.BytesSent);

                    info.DownloadSpeed = d / seconds;
                    info.UploadSpeed = u / seconds;

                    bool selected = SelectedAdapterId switch
                    {
                        "auto" => a.Id == ActiveAutoAdapterId,

                        "all" => IsPhysicalCandidate(a),

                        _ => SelectedAdapterId == a.Id
                    };

                    if (!selected)
                        continue;

                    _monitoredIds.Add(a.Id);
                    connected = true;

                    // Adapter මාරුවීමේ පළමු sample එක අතහැරීම
                    if (autoChanged)
                        continue;

                    down += d;
                    up += u;
                }
                catch (NetworkInformationException)
                {
                }
            }

            foreach (var id in _previous.Keys.ToArray())
            {
                if (!current.Contains(id))
                    _previous.Remove(id);
            }

            Adapters = adapters;

            _totalDownloaded += down;
            _totalUploaded += up;

            return new NetworkStats
            {
                DownloadSpeed = down / seconds,
                UploadSpeed = up / seconds,

                TotalDownloaded = _totalDownloaded,
                TotalUploaded = _totalUploaded,

                DownloadedDelta = down,
                UploadedDelta = up,

                IsConnected = connected
            };
        }
    }
}
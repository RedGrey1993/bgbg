
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

// Internal message types for local discovery and communication
[Serializable]
public class LocalPacket
{
    public string type; // "DiscoveryRequest", "DiscoveryResponse", "JoinRequest", "JoinAccept", "GameData", "Leave"
    public string payload;
}

[Serializable]
public class DiscoveryResponsePayload
{
    public string lobbyName;
    public int currentPlayers;
    public int maxPlayers;
    public bool hasPassword;
    public string ownerName;
    public string ownerId;
    public string hostEndpoint; // "ip:port"
}

[Serializable]
public class JoinAcceptPayload
{
    public LobbyInfo lobbyInfo;
    public List<PlayerInfo> players;
}

public class LocalUdpNetworkLayer : INetworkLayer
{
    public event Action<LobbyInfo> OnLobbyCreated;
    public event Action OnLobbyCreateFailed;
    public event Action<LobbyInfo> OnLobbyJoined;
    public event Action<string> OnLobbyJoinFailed;
    public event Action<List<LobbyInfo>> OnLobbyListUpdated;
    public event Action<PlayerInfo> OnPlayerJoined;
    public event Action<PlayerInfo> OnPlayerLeft;
    public event Action<byte[]> OnPacketReceived;
    public event Action OnLobbyLeft;
    public event Action<string, Texture2D> OnAvatarReady;
    public event Action<PlayerInfo> OnPlayerInfoUpdated;

    public bool IsHost { get; private set; }
    public PlayerInfo MyInfo { get; private set; }
    public HashSet<PlayerInfo> Players { get; } = new HashSet<PlayerInfo>();

    private UdpClient udpClient;
    private IPEndPoint hostEndpoint;
    private List<IPEndPoint> clientEndpoints = new List<IPEndPoint>();
    private readonly int[] gamePorts = { 7775, 7776, 7777, 7778, 7779, 7780, 7781, 7782 }; // Game ports, one will be chosen

    private LobbyInfo currentLobby;

    public bool Initialize()
    {
        int randomSuffix = UnityEngine.Random.Range(1000, 9999);
        MyInfo = new PlayerInfo { Id = "LocalUdpPlayer" + randomSuffix, Name = "Player " + randomSuffix };

        // Try to bind to any available game port to allow multiple clients on one machine
        foreach (var port in gamePorts)
        {
            try
            {
                udpClient = new UdpClient(port);
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.BeginReceive(OnUdpData, null);
                Debug.Log($"LocalUdpNetworkLayer: Initialized and listening on port {port}");
                return true; // Success
            }
            catch (Exception)
            {
                // Port in use, try next
                continue;
            }
        }

        Debug.LogError("LocalUdpNetworkLayer: Failed to initialize UDP client. All game ports are in use.");
        return false;
    }

    public void Shutdown()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        IsHost = false;
        Players.Clear();
        clientEndpoints.Clear();
        Debug.Log("LocalUdpNetworkLayer Shutdown.");
    }

    public void Tick() { /* Handled by async receive */ }

    public void CreateLobby(string roomName, string password, int maxPlayers)
    {
        // Don't close the client. Just transition to Host mode on the current port.
        IsHost = true;

        // The host endpoint is our own endpoint that we are already listening on.
        var localPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
        hostEndpoint = new IPEndPoint(GetLocalIPAddress(), localPort);

        // Update MyInfo with the correct network ID before adding to the list
        MyInfo = new PlayerInfo { Id = hostEndpoint.ToString(), Name = MyInfo.Name };
        Players.Add(MyInfo);

        // Add the host to the list of endpoints to ensure it receives broadcast messages
        clientEndpoints.Add(hostEndpoint);

        currentLobby = new LobbyInfo
        {
            Id = hostEndpoint.ToString(),
            OwnerId = hostEndpoint.ToString(),
            Name = roomName,
            CurrentPlayers = 1,
            MaxPlayers = maxPlayers,
            HasPassword = !string.IsNullOrEmpty(password),
            OwnerName = MyInfo.Name
        };

        // We are already receiving, so no need to call BeginReceive again.
        OnLobbyCreated?.Invoke(currentLobby);
        OnLobbyJoined?.Invoke(currentLobby); // Also trigger Joined event for the host
        Debug.Log($"Local UDP Host mode enabled on {hostEndpoint}");
    }

    public void RequestLobbyList()
    {
        // Broadcast a discovery request to all potential host ports
        var requestPacket = new LocalPacket { type = "DiscoveryRequest", payload = "" };
        var json = JsonUtility.ToJson(requestPacket);
        byte[] data = Encoding.UTF8.GetBytes(json);

        // Use the main UdpClient to send, so the response comes back to the correct listening port.
        udpClient.EnableBroadcast = true;
        foreach (var port in gamePorts)
        {
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
            try
            {
                udpClient.Send(data, data.Length, broadcastEndpoint);
            }
            catch(Exception e)
            {
                Debug.LogError($"Discovery broadcast error to {broadcastEndpoint}: {e.Message}");
            }
        }
        Debug.Log($"Sent discovery request broadcast to all game ports.");
    }

    public void JoinLobby(LobbyInfo lobbyInfo, string password)
    {
        if (TryParseIPEndPoint((string)lobbyInfo.Id, out IPEndPoint targetHostEndpoint))
        {
            hostEndpoint = targetHostEndpoint;
            // Send join request
            var joinRequest = new LocalPacket { type = "JoinRequest", payload = JsonUtility.ToJson(MyInfo) };
            var json = JsonUtility.ToJson(joinRequest);
            byte[] data = Encoding.UTF8.GetBytes(json);
            SendToEndpoint(hostEndpoint, data);
            // The host will respond and trigger OnLobbyJoined
        }
        else
        {
            OnLobbyJoinFailed?.Invoke("Invalid lobby ID format.");
        }
    }

    public void LeaveLobby()
    {
        string packetType = IsHost ? "LobbyClosed" : "Leave";
        var leavePacket = new LocalPacket { type = packetType, payload = JsonUtility.ToJson(MyInfo) };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(leavePacket));

        if (IsHost)
        {
            // Inform all clients that the lobby is closing
            SendToAll(data, true);
            IsHost = false;
        }
        else if (hostEndpoint != null)
        {
            // Inform host that this client is leaving
            SendToHost(data, true);
        }

        OnLobbyLeft?.Invoke();
    }

    public void SendToHost(byte[] data, bool reliable) => SendToEndpoint(hostEndpoint, data);

    public void SendToAll(byte[] data, bool reliable)
    {
        if (!IsHost) return;
        foreach (var client in clientEndpoints)
        {
            SendToEndpoint(client, data);
        }
    }

    public void SendToPlayer(PlayerInfo player, byte[] data, bool reliable)
    {
        if (TryParseIPEndPoint((string)player.Id, out IPEndPoint target))
        {
            SendToEndpoint(target, data);
        }
    }

    public void RequestAvatar(string playerId)
    {
        // Not implemented for Local UDP. Avatars will use the default UI style.
    }

    private bool TryParseIPEndPoint(string endpointString, out IPEndPoint result)
    {
        result = null;
        try
        {
            var parts = endpointString.Split(':');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0], out IPAddress address))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int port))
            {
                return false;
            }

            result = new IPEndPoint(address, port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SendToEndpoint(IPEndPoint endpoint, byte[] data)
    {
        if (udpClient == null || endpoint == null) return;
        try
        {
            udpClient.Send(data, data.Length, endpoint);
        }
        catch (Exception e)
        { 
            Debug.LogError($"UDP Send Error to {endpoint}: {e.Message}");
        }
    }

    private void OnUdpData(IAsyncResult result)
    {
        if (udpClient == null) return;
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedBytes = udpClient.EndReceive(result, ref remoteEP);
            
            // Queue the processing to the main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessPacket(receivedBytes, remoteEP));

            // Continue listening
            udpClient.BeginReceive(OnUdpData, null);
        }
        catch (ObjectDisposedException) { /* Client closed, do nothing */ }
        catch (Exception e)
        { 
            Debug.LogError($"UDP Receive Error: {e.Message}");
        }
    }

    private void ProcessPacket(byte[] data, IPEndPoint remoteEP)
    {
        // First, try to parse as a control packet used by this layer for connection management.
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var packet = JsonUtility.FromJson<LocalPacket>(json);

            if (packet != null && !string.IsNullOrEmpty(packet.type))
            {
                bool packetHandled = false;
                if (IsHost)
                {
                    // Host handles requests from clients
                    switch (packet.type)
                    {
                        case "DiscoveryRequest":
                            Debug.Log($"fhhtest, Received discovery request from {remoteEP}");
                            var responsePayload = new DiscoveryResponsePayload
                            {
                                lobbyName = currentLobby.Name,
                                currentPlayers = Players.Count,
                                maxPlayers = currentLobby.MaxPlayers,
                                hasPassword = currentLobby.HasPassword,
                                ownerName = currentLobby.OwnerName,
                                ownerId = (string)currentLobby.OwnerId,
                                hostEndpoint = hostEndpoint.ToString()
                            };
                            var responsePacket = new LocalPacket { type = "DiscoveryResponse", payload = JsonUtility.ToJson(responsePayload) };
                            byte[] responseData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(responsePacket));
                            SendToEndpoint(remoteEP, responseData);
                            Debug.Log($"fhhtest, Sent discovery response to {remoteEP}, {responsePayload.hostEndpoint}, {responsePayload.lobbyName}");
                            packetHandled = true;
                            break;

                        case "JoinRequest":
                            if (!clientEndpoints.Contains(remoteEP))
                            {
                                var playerInfoFromClient = JsonUtility.FromJson<PlayerInfo>(packet.payload);
                                var newPlayer = new PlayerInfo { Id = remoteEP.ToString(), Name = playerInfoFromClient.Name };

                                var playerJoinedPacket = new LocalPacket { type = "PlayerJoined", payload = JsonUtility.ToJson(newPlayer) };
                                byte[] playerJoinedData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(playerJoinedPacket));
                                SendToAll(playerJoinedData, true);

                                clientEndpoints.Add(remoteEP);
                                Players.Add(newPlayer);
                                OnPlayerJoined?.Invoke(newPlayer);

                                currentLobby.CurrentPlayers = Players.Count;
                                var joinAcceptPayload = new JoinAcceptPayload { lobbyInfo = currentLobby, players = Players.ToList() };
                                Debug.Log($"fhhtest, currentLobby {currentLobby.Name}, total players now {Players.Count}");
                                var joinAcceptPacket = new LocalPacket { type = "JoinAccept", payload = JsonUtility.ToJson(joinAcceptPayload) };
                                Debug.Log($"fhhtest, joinAcceptPacket payload: {joinAcceptPacket.payload}");
                                byte[] joinAcceptData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(joinAcceptPacket));
                                SendToEndpoint(remoteEP, joinAcceptData);
                                Debug.Log($"fhhtest, Player {newPlayer.Name} joined, Sent join accept to {remoteEP}, {joinAcceptData.Length},,, {JsonUtility.ToJson(joinAcceptPacket)}");
                            }
                            packetHandled = true;
                            break;
                        
                        case "Leave":
                            var leavingPlayerInfo = Players.First(p => p.Id.Equals(remoteEP.ToString()));
                            if (!leavingPlayerInfo.Equals(default(PlayerInfo)))
                            {
                                clientEndpoints.Remove(remoteEP);
                                Players.Remove(leavingPlayerInfo);
                                OnPlayerLeft?.Invoke(leavingPlayerInfo);

                                var playerLeftPacket = new LocalPacket { type = "PlayerLeft", payload = JsonUtility.ToJson(leavingPlayerInfo) };
                                byte[] playerLeftData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(playerLeftPacket));
                                SendToAll(playerLeftData, true);
                            }
                            packetHandled = true;
                            break;
                    }
                }
                else
                {
                    // Client handles responses from the host
                    switch (packet.type)
                    {
                        case "DiscoveryResponse":
                            Debug.Log($"fhhtest, Received discovery response from {remoteEP}");
                            var payload = JsonUtility.FromJson<DiscoveryResponsePayload>(packet.payload);
                            var lobbyInfo = new LobbyInfo { Id = payload.hostEndpoint, OwnerId = payload.ownerId, Name = payload.lobbyName, CurrentPlayers = payload.currentPlayers, MaxPlayers = payload.maxPlayers, HasPassword = payload.hasPassword, OwnerName = payload.ownerName };
                            OnLobbyListUpdated?.Invoke(new List<LobbyInfo> { lobbyInfo });
                            packetHandled = true;
                            break;
                        
                        case "JoinAccept":
                            try
                            {
                                Debug.Log($"fhhtest, Received JoinAccept from {remoteEP}");
                                var joinAcceptPayload = JsonUtility.FromJson<JoinAcceptPayload>(packet.payload);
                                currentLobby = joinAcceptPayload.lobbyInfo;
                                Players.Clear();
                                Players.UnionWith(joinAcceptPayload.players);
                                // MyInfo的ID会被Host修改为endpoint格式
                                MyInfo = Players.First(p => p.Name == MyInfo.Name);
                                Debug.Log($"fhhtest, call OnLobbyJoined, {OnLobbyJoined}");
                                OnLobbyJoined?.Invoke(currentLobby);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Error processing JoinAccept: {e.StackTrace}, payload: {packet.payload}");
                                OnLobbyJoinFailed?.Invoke("Failed to join lobby due to data error.");
                            }
                            packetHandled = true;
                            
                            break;

                        case "LobbyClosed":
                            OnLobbyLeft?.Invoke();
                            packetHandled = true;
                            break;

                        case "PlayerJoined":
                            var newPlayer = JsonUtility.FromJson<PlayerInfo>(packet.payload);
                            if (!Players.Any(p => p.Id.Equals(newPlayer.Id)))
                            {
                                Players.Add(newPlayer);
                                OnPlayerJoined?.Invoke(newPlayer);
                            }
                            packetHandled = true;
                            break;

                        case "PlayerLeft":
                            var leftPlayerInfo = JsonUtility.FromJson<PlayerInfo>(packet.payload);
                            var playerToRemove = Players.FirstOrDefault(p => p.Id.Equals(leftPlayerInfo.Id));
                            if (!playerToRemove.Equals(default(PlayerInfo)))
                            {
                                Players.Remove(playerToRemove);
                                OnPlayerLeft?.Invoke(playerToRemove);
                            }
                            packetHandled = true;
                            break;
                    }
                }

                if (packetHandled)
                {
                    return; // This was a control packet and we've handled it.
                }
            }
        }
        catch (Exception) { /* Not a LocalPacket, so it must be game data. Fall through. */ }

        // If we reach here, the packet was not a recognized control packet.
        // We assume it's game data (e.g., a GenericMessage) and pass it up to the game logic layer.
        OnPacketReceived?.Invoke(data);
    }

    private static IPAddress GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip;
            }
        }
        Debug.LogWarning("No network adapters with an IPv4 address in the system! Falling back to loopback.");
        return IPAddress.Loopback;
    }
}

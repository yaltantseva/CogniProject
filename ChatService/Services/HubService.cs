using System.Collections.Concurrent;
using System.Collections.Generic;
using ChatService.Abstractions;
using ChatService.Controllers;
using Cogni.Database.Context;
using Microsoft.AspNetCore.SignalR;

namespace ChatService.Services;

public class HubService : IHubService
{
    private readonly ILogger<HubService> _logger;

    private static readonly ConcurrentDictionary<string, string> ConnectionToUser = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserToConnections = new();
    private static readonly object UserConnectionsLock = new();

    private readonly IHubContext<ChatHubController> _hubContext;

    public HubService(IHubContext<ChatHubController> hubContext, ILogger<HubService> logger)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task SendMessage<T>(string method, string userId, T message)
    {

        string[] connectionIds;

        lock (UserConnectionsLock)
        {
            if (!UserToConnections.TryGetValue(userId, out var connections))
                return;

            connectionIds = connections.ToArray();
        }

        foreach (var connectionId in connectionIds)
        {
            _logger.LogInformation(
                "[{Method}] Sending message to connection: {ConnectionId}",
                method,
                connectionId);

            await _hubContext.Clients.Client(connectionId)
                .SendAsync(method, message);
        }
    }

    public void AddRel(string userId, string connectionId)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        lock (UserConnectionsLock)
        {
            ConnectionToUser[connectionId] = userId;

            if (!UserToConnections.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                UserToConnections[userId] = connections;
            }

            connections.Add(connectionId);
        }
    }

    public void RemoveRel(string connectionId)
    {

        lock (UserConnectionsLock)
        {
            if (!ConnectionToUser.TryRemove(connectionId, out var userId))
                return;

            if (!UserToConnections.TryGetValue(userId, out var connections))
                return;

            connections.Remove(connectionId);

            if (connections.Count == 0)
                UserToConnections.TryRemove(userId, out _);
        }

    }

    public string? GetUserId(string connectionId) => ConnectionToUser.TryGetValue(connectionId, out var userId) ? userId : null;
}
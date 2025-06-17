using System;
using System.Collections.Concurrent;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Hubs;

[Authorize]
public class ChatHub(UserManager<AppUser> userManager, AppDbContext context) : Hub
{
    // Suporta múltiplas conexões por usuário
    public static readonly ConcurrentDictionary<string, List<OnlineUserDto>> onlineUsers = new();

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User!.Identity!.Name!;
        var connectionId = Context.ConnectionId;
        var currentUser = await userManager.FindByNameAsync(userName);

        var userDto = new OnlineUserDto
        {
            
            ConnectionId = connectionId,
            UserName = userName,
            ProfilePicture = currentUser!.ProfilePicture,
            FullName = currentUser.FullName
        };

        lock (onlineUsers)
        {
            if (onlineUsers.TryGetValue(userName, out var connections))
            {
                connections.Add(userDto);
            }
            else
            {
                onlineUsers.TryAdd(userName, new List<OnlineUserDto> { userDto });
            }
        }

        await Clients.AllExcept(connectionId).SendAsync("Notify", currentUser);
        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());

        //var httpContext = Context.GetHttpContext();
        //var receiverId = httpContext?.Request.Query["senderId"].ToString();

        /*if (!string.IsNullOrEmpty(receiverId))
        {
            await LoadMessages(receiverId);
        }*/
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User!.Identity!.Name!;
        var connectionId = Context.ConnectionId;

        lock (onlineUsers)
        {
            if (onlineUsers.TryGetValue(userName, out var connections))
            {
                var connectionToRemove = connections.FirstOrDefault(c => c.ConnectionId == connectionId);
                if (connectionToRemove != null)
                    connections.Remove(connectionToRemove);

                if (connections.Count == 0)
                    onlineUsers.TryRemove(userName, out _);
            }
        }

        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
    }

    public async Task LoadMessages(string recipientId, int pageNumber = 1)
    {
        const int pageSize = 10;
        var userName = Context.User!.Identity!.Name!;
        var currentUser = await userManager.FindByNameAsync(userName);
        if (currentUser is null) return;

        Console.WriteLine($"Usuário logado: {currentUser.Id}");
        Console.WriteLine($"Recipiente: {recipientId}");
        
        var messages = await context.Messages
            .Where(x => (x.SenderId == currentUser.Id && x.ReceiverId == recipientId) ||
                        (x.SenderId == recipientId && x.ReceiverId == currentUser.Id))
            .OrderByDescending(x => x.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(x => x.CreatedDate)
            .Select(x => new MessageResponseDto
            {
                Id = x.Id,
                Content = x.Content,
                CreatedAt = x.CreatedDate,
                ReceiverId = x.ReceiverId,
                SenderId = x.SenderId
            })
            .ToListAsync();

        foreach (var msg in messages)
        {
            var m = await context.Messages.FindAsync(msg.Id);
            if (m != null && m.ReceiverId == currentUser.Id && !m.IsRead)
            {
                m.IsRead = true;
            }
        }

        await context.SaveChangesAsync();

        
        await Clients.Caller.SendAsync("ReceiveMessageList", messages);
    }

    public async Task SendMessage(MessageRequestDto message)
    {
        var senderUserName = Context.User!.Identity!.Name!;
        var sender = await userManager.FindByNameAsync(senderUserName);
        var receiver = await userManager.FindByIdAsync(message.ReceiverId!);

        var newMessage = new Message
        {
            Sender = sender,
            Receiver = receiver,
            Content = message.Content,
            CreatedDate = DateTime.UtcNow,
            IsRead = false
        };

        context.Messages.Add(newMessage);
        await context.SaveChangesAsync();

        var receiverConnections = GetUserConnections(receiver.UserName!);
        foreach (var connId in receiverConnections)
        {
            await Clients.Client(connId).SendAsync("ReceiveNewMessage", newMessage);
        }
    }

    public async Task NotifyTyping(string recipientUserName)
    {
        var recipientConnections = GetUserConnections(recipientUserName);
        foreach (var conn in recipientConnections)
        {
            await Clients.Client(conn).SendAsync("NotifyTypingToUser");
        }
    }
/*
    private async Task<IEnumerable<OnlineUserDto>> GetAllUsers()
    {
        var currentUserName = Context.User!.GetUserName();
        var onlineSet = onlineUsers.Keys.ToHashSet();

        var users = await userManager.Users
            .Select(u => new OnlineUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                ProfilePicture = u.ProfilePicture,
                IsOnline = onlineSet.Contains(u.UserName!),
                UnreadCount = context.Messages
                    .Count(m => m.ReceiverId == currentUserName && m.SenderId == u.Id && !m.IsRead)
            })
            .OrderByDescending(u => u.IsOnline)
            .ToListAsync();

        return users;
    }
*/
    private async Task<IEnumerable<OnlineUserDto>> GetAllUsers()
    {
        
           var allConnectedUserNames = onlineUsers.Keys.ToList();

            // Busca do banco e traz para a memória
            var usersInDb = await userManager.Users
                .Where(u => allConnectedUserNames.Contains(u.UserName!))
                .ToListAsync();

            // Agora podemos usar ?. normalmente
            var users = usersInDb.Select(u => new OnlineUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                ProfilePicture = u.ProfilePicture,
                IsOnline = true,
                ConnectionId = onlineUsers[u.UserName!].FirstOrDefault()?.ConnectionId
            });

            return users;

    }

    private List<string> GetUserConnections(string userName)
    {
        if (onlineUsers.TryGetValue(userName, out var connections))
        {
            return connections.Select(c => c.ConnectionId).ToList();
        }
        return new();
    }
}

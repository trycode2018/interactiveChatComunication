using System;
using API.Models;

namespace API.DTOs;

public class MessageResponseDto
{
    public int Id { get; set; }
    public string? SenderId { get; set; }
    public string? ReceiverId { get; set; }
    public string? Content { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt{ get; set; }
}

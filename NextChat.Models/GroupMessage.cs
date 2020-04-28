using System;

namespace NextChat.Models
{
    public class GroupMessage
    {
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
    }
}

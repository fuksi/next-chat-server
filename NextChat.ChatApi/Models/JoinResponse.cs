using NextChat.Models;

namespace NextChat.ChatApi.Models
{
    public class JoinResponse
    {
        public Group Group { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}

using NextChat.Models;

namespace NextChat.ChatApi.Models
{
    public class NewGroupResultResponse
    {
        public Group Group { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}

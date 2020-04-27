using NextChat.Models;

namespace NextChat.ChatApi.Models
{
    public class UserWssPayload
    {
        public string GroupId { get; set; }
        public string NewGroupName { get; set; }
        public string NewMessage { get; set; }
        public int Counter { get; set; }
    }
}

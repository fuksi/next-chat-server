using NextChat.Models;
using System.Collections.Generic;

namespace NextChat.ChatApi.Models
{
    public class NewMessageReponse
    {
        public string GroupId { get; set; }
        public List<GroupMessage> Messages { get; set; }
    }
}

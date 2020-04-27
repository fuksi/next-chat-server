using NextChat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Models
{
    public class NewMessageReponse
    {
        public string GroupId { get; set; }
        public List<GroupMessage> Messages { get; set; }
    }
}

using NextChat.Models;
using System.Collections.Generic;

namespace NextChat.ChatApi.Models
{
    public class InitialStateResponse
    {
        public IEnumerable<Group> UserGroups { get; set; }
        public IEnumerable<Group> OtherGroups { get; set; }
    }
}

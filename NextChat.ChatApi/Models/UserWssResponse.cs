using NextChat.Models;
using System.Collections.Generic;

namespace NextChat.ChatApi.Models
{
    public class UserWssResponse
    {
        public int Counter { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Group NewGroup { get; set; }
        public InitialState InitialState { get; set; }
    }

    public class InitialState
    {
        public IEnumerable<Group> UserGroups { get; set; }
        public IEnumerable<Group> OtherGroups { get; set; }
    }
}

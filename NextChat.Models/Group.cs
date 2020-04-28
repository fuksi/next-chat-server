using System.Collections.Generic;

namespace NextChat.Models
{
    public class Group
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<GroupMessage> Messages { get; set; }
        public HashSet<string> Users { get; set; }
        public bool IsFull { get; set; }
    }
}

using NextChat.ChatApi.Models;
using NextChat.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Services
{
    public interface IWssMessageHandler
    {
        Task<(IEnumerable<Group>, IEnumerable<Group>)> GetConnectionInitialStateAsync(string userId);
        Task<Group> GetGroupAsync(string groupId);
        Task<(bool, string)> NewGroupAsync(string userId, string groupName);
        Task<bool> JoinGroupAsync(string userId, string groupId);
        Task LeaveGroupAsync(string userId, string groupId);
        Task AddMessageAsync(string userId, UserWssPayload payload);
    }
}

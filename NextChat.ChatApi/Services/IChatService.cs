using NextChat.ChatApi.Models;
using NextChat.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Services
{
    public interface IChatService
    {
        Task<InitialStateResponse> GetConnectionInitialStateAsync(string userId);
        Task<Group> GetGroupAsync(string groupId);
        Task<(bool, string)> NewGroupAsync(string userId, string groupName);
        Task<bool> JoinGroupAsync(string userId, string groupId);
        Task LeaveGroupAsync(string userId, string groupId);
        Task AddMessageAsync(GroupMessage message, string groupId);
    }
}

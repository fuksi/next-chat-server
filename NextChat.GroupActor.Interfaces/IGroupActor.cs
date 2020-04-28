using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;
using NextChat.Models;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2_1, RemotingClientVersion = RemotingClientVersion.V2_1)]
namespace NextChat.GroupActor.Interfaces
{
    public interface IGroupActor : IActor
    {
        Task<HashSet<string>> GetMembersAsync();
        Task<bool> AddMemberAsync(string userId);
        Task RemoveMemberAsync(string userId);
        Task SetGroupNameAsync(string name);
        Task<string> GetGroupNameAsync();
        Task<bool> IsFullAsync();
        Task<List<GroupMessage>> GetMessagesAsync();
        Task AddMessageAsync(GroupMessage message);
    }
}

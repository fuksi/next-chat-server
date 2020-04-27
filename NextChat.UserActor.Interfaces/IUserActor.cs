using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2_1, RemotingClientVersion = RemotingClientVersion.V2_1)]
namespace NextChat.UserActor.Interfaces
{
    public interface IUserActor : IActor
    {
        Task<HashSet<string>> GetGroupIdsAsync();
        Task AddGroupAsync(string groupId);
        Task RemoveGroupAsync(string groupId);
    }
}

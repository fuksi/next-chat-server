using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using NextChat.UserActor.Interfaces;

namespace NextChat.UserActor
{
    [StatePersistence(StatePersistence.Persisted)]
    internal class UserActor : Actor, IUserActor
    {
        private const string GroupIdsKey = "groupIds";

        public UserActor(ActorService actorService, ActorId actorId) 
            : base(actorService, actorId)
        {
        }

        public async Task AddGroupAsync(string groupId)
        {
            var groupIds = await StateManager.GetStateAsync<HashSet<string>>(GroupIdsKey);
            groupIds.Add(groupId);

            await StateManager.AddOrUpdateStateAsync(GroupIdsKey, groupIds, (key, val) => groupIds);
        }

        public Task<HashSet<string>> GetGroupIdsAsync()
        {
            return StateManager.GetStateAsync<HashSet<string>>(GroupIdsKey);
        }

        public async Task RemoveGroupAsync(string groupId)
        {
            var groupIds = await StateManager.GetStateAsync<HashSet<string>>(GroupIdsKey);
            groupIds.Remove(groupId);

            await StateManager.AddOrUpdateStateAsync(GroupIdsKey, groupIds, (key, val) => groupIds);
        }

        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");
            return StateManager.TryAddStateAsync(GroupIdsKey, new HashSet<string>());
        }
    }
}

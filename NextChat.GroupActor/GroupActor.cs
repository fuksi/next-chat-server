using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodenameGenerator;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using NextChat.GroupActor.Interfaces;
using NextChat.Models;

namespace NextChat.GroupActor
{
    [StatePersistence(StatePersistence.Persisted)]
    internal class GroupActor : Actor, IGroupActor
    {
        private const string GroupMessagesKey = "messages";
        private const string GroupNameKey = "name";
        private const string GroupUsersKey = "users";
        private const int GroupCapacity = 2;

        public GroupActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            var generator = new Generator();
            var groupName = generator.Generate();
            var welcomeMessage = new GroupMessage
            {
                Content = "Welcome to the group",
                CreatedAt = DateTime.UtcNow,
                UserId = "NextChat bot!"
            };

            var activateTask = Task.WhenAll(
                StateManager.TryAddStateAsync(GroupMessagesKey, new List<GroupMessage> { welcomeMessage }),
                StateManager.TryAddStateAsync(GroupUsersKey, new HashSet<string>()),
                StateManager.TryAddStateAsync(GroupNameKey, groupName)
            );

            return activateTask;
        }

        public async Task<bool> IsFullAsync()
        {
            var groupUsers = await StateManager.GetStateAsync<HashSet<string>>(GroupUsersKey);
            return groupUsers.Count >= GroupCapacity;
        }

        public async Task<bool> AddMemberAsync(string userId)
        {
            var groupUsers = await StateManager.GetStateAsync<HashSet<string>>(GroupUsersKey);
            if (groupUsers.Count >= GroupCapacity)
            {
                return false;
            }

            groupUsers.Add(userId);
            await StateManager.AddOrUpdateStateAsync(GroupUsersKey, groupUsers, (key, val) => groupUsers);

            return true;
        }

        public Task<HashSet<string>> GetMembersAsync()
        {
            return StateManager.GetStateAsync<HashSet<string>>(GroupUsersKey);
        }

        public async Task RemoveMemberAsync(string userId)
        {
            var groupUsers = await StateManager.GetStateAsync<HashSet<string>>(GroupUsersKey);

            groupUsers.Remove(userId);
            await StateManager.AddOrUpdateStateAsync(GroupUsersKey, groupUsers, (key, val) => groupUsers);
        }

        public Task<List<GroupMessage>> GetMessagesAsync()
        {
            return StateManager.GetStateAsync<List<GroupMessage>>(GroupMessagesKey);
        }

        public async Task AddMessageAsync(GroupMessage message)
        {
            var messages = await StateManager.GetStateAsync<List<GroupMessage>>(GroupMessagesKey);
            messages.Add(message);

            await StateManager.AddOrUpdateStateAsync(GroupMessagesKey, messages, (key, val) => messages);
        }

        public Task<string> GetGroupNameAsync()
        {
            return StateManager.GetStateAsync<string>(GroupNameKey);
        }

        public Task SetGroupNameAsync(string name)
        {
            return StateManager.AddOrUpdateStateAsync(GroupNameKey, name, (key, val) => name);
        }
    }
}

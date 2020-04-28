using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Query;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using NextChat.ChatApi.Models;
using NextChat.GroupActor.Interfaces;
using NextChat.Models;
using NextChat.UserActor.Interfaces;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Services
{
    public class ChatService : IChatService
    {
        private const string UserActorServiceRemotingUri = "fabric:/NextChat/UserActorService";
        private const string GroupActorServiceRemotingUri = "fabric:/NextChat/GroupActorService";

        /// <summary>
        /// Add message to group
        /// </summary>
        public async Task AddMessageAsync(GroupMessage message, string groupId)
        {
            var group = GetGroupActor(groupId);
            await group.AddMessageAsync(message);
        }

        /// <summary>
        /// Get all of group state
        /// </summary>
        public async Task<Group> GetGroupAsync(string groupId)
        {
            var group = GetGroupActor(groupId);
            var messages = await group.GetMessagesAsync();
            var users = await group.GetMembersAsync();
            var name = await group.GetGroupNameAsync();

            return new Group 
            { 
                Id = groupId,
                Messages = messages,
                Users = users,
                Name = name
            };
        }

        /// <summary>
        /// Request to join group.
        /// If succeeded, group-user relation is persisted both in user & group actor
        /// </summary>
        public async Task<bool> JoinGroupAsync(string userId, string groupId)
        {
            var groupActor = GetGroupActor(groupId);
            var joinSucceeded = await groupActor.AddMemberAsync(userId);
            if (!joinSucceeded)
            {
                return false;
            }

            var userActor = GetUserActor(userId);
            await userActor.AddGroupAsync(groupId);

            return true;
        }

        /// <summary>
        /// Leave group, group-user relation is remove from both user & group actor
        /// </summary>
        public async Task LeaveGroupAsync(string userId, string groupId)
        {
            var groupActor = GetGroupActor(groupId);
            await groupActor.RemoveMemberAsync(userId);

            var userActor = GetUserActor(userId);
            await userActor.RemoveGroupAsync(groupId);
        }

        /// <summary>
        /// Create a new group.
        /// Name is the only restriction, return false if group name exists
        /// If succeeded, persist group-user relation in both user & group actor
        /// </summary>
        public async Task<(bool, string)> NewGroupAsync(string userId, string groupName)
        {
            var existingGroups = await GetAllGroupsInformationAsync();
            var groupNameAlreadyExists = existingGroups.Any(g => g.Name.Equals(groupName, StringComparison.InvariantCulture));
            if (groupNameAlreadyExists)
            {
                return (false, null);
            }

            var newGroupId = Guid.NewGuid().ToString();
            var newGroupActor = GetGroupActor(newGroupId);
            await newGroupActor.SetGroupNameAsync(groupName);
            await newGroupActor.AddMemberAsync(userId);
            
            var userActor = GetUserActor(userId);
            await userActor.AddGroupAsync(newGroupId);

            return (true, newGroupId);
        }

        /// <summary>
        /// Get all groups information by looping through all partitions
        /// https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-reliable-actors-enumerate
        /// TODO: persist this in cache
        /// </summary>
        private async Task<List<Group>> GetAllGroupsInformationAsync()
        {
            var allGroups = new List<Group>();

            var serviceName = new Uri(GroupActorServiceRemotingUri);
            using var client = new FabricClient();

            var partitions = await client.QueryManager.GetPartitionListAsync(serviceName);
            var partitionKeys = partitions.Select(i =>
            {
                var partitionInfo = (Int64RangePartitionInformation)i.PartitionInformation;
                return partitionInfo.LowKey;
            });

            var cancellationToken = new CancellationToken();
            
            foreach (var partitionKey in partitionKeys)
            {
                IActorService actorServiceProxy = ServiceProxy.Create<IActorService>(
                    new Uri(GroupActorServiceRemotingUri),
                    new ServicePartitionKey(partitionKey));

                ContinuationToken continuationToken = null;

                do
                {
                    PagedResult<ActorInformation> page = await actorServiceProxy.GetActorsAsync(continuationToken, cancellationToken);
                    foreach (var actorInfo in page.Items)
                    {
                        var groupId = actorInfo.ActorId.ToString();
                        var actor = GetGroupActor(groupId);
                        var groupName = await actor.GetGroupNameAsync();
                        allGroups.Add(new Group
                        {
                            Id = groupId,
                            Name = groupName
                        });

                    }
                    continuationToken = page.ContinuationToken;
                }

                while (continuationToken != null);
            }

            return allGroups;
        }

        /// <summary>
        /// Retrieve all groups. Divide into groups user belong to and the others.
        /// User's groups payload will include all data including members list
        /// </summary>
        public async Task<(IEnumerable<Group>, IEnumerable<Group>)> GetConnectionInitialStateAsync(string userId)
        {
            var allGroups = await GetAllGroupsInformationAsync();
            var userActor = GetUserActor(userId);
            var userGroupIds = await userActor.GetGroupIdsAsync();

            var userGroupsEnriched = userGroupIds.Select(id =>
            {
                var groupActor = GetGroupActor(id);
                var messages = groupActor.GetMessagesAsync().Result;
                var members = groupActor.GetMembersAsync().Result;
                var name = groupActor.GetGroupNameAsync().Result;
                return new Group
                {
                    Id = id,
                    Name = name,
                    Messages = messages,
                    Users = members,
                };
            });

            var otherGroups = allGroups.Where(g => !userGroupIds.Contains(g.Id));
            return (userGroupsEnriched, otherGroups);
        }

        private IUserActor GetUserActor(string userId)
        {
            return ActorProxy.Create<IUserActor>(
                new ActorId(userId),
                new Uri(UserActorServiceRemotingUri));
        }

        private IGroupActor GetGroupActor(string groupId)
        {
            return ActorProxy.Create<IGroupActor>(
                new ActorId(groupId),
                new Uri(GroupActorServiceRemotingUri));
        }
    }
}

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
    public class WssMessageHandler : IWssMessageHandler
    {
        public async Task AddMessageAsync(string userId, UserWssPayload payload)
        {
            var group = GetGroupActor(payload.GroupId);
            await group.AddMessageAsync(new GroupMessage
            {
                Content = payload.NewMessage,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

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

        public async Task LeaveGroupAsync(string userId, string groupId)
        {
            var groupActor = GetGroupActor(groupId);
            await groupActor.RemoveMemberAsync(userId);

            var userActor = GetUserActor(userId);
            await userActor.RemoveGroupAsync(groupId);
        }

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
        /// Join group. Add user-group relation to both user/group actor
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="payload"></param>
        /// <returns>Error Message is group is full, else group info and messages</returns>
        private async Task<UserWssResponse> OnJoinGroupActionAsync(string userId, UserWssPayload payload)
        {
            UserWssResponse res;
            var groupActor = GetGroupActor(payload.GroupId);
            var joinSucceeded = await groupActor.AddMemberAsync(userId);
            if (!joinSucceeded)
            {
                res = GetErrorResponse("Cannot join, group is full!");
                res.Counter = payload.Counter;
                return res;
            }

            var userActor = GetUserActor(userId);
            await userActor.AddGroupAsync(payload.GroupId);

            var groupName = await groupActor.GetGroupNameAsync();
            var messages = await groupActor.GetMessagesAsync();
            var members = await groupActor.GetMembersAsync();

            return new UserWssResponse
            {
                Counter = payload.Counter,
                Success = true,
                NewGroup = new Group
                {
                    Id = payload.GroupId,
                    Name = groupName,
                    Messages = messages,
                    Users = members
                }
            };
        }

        private async Task<List<Group>> GetAllGroupsInformationAsync()
        {
            List<Group> allGroups = new List<Group>();

            var serviceName = new Uri("fabric:/NextChat/GroupActorService");
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
                    new Uri("fabric:/NextChat/GroupActorService"),
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

        private UserWssResponse GetErrorResponse(string errorMessage)
        {
            return new UserWssResponse
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Create group if not exists. Add user-groupid relation to both user && group actor
        /// </summary>
        /// <param name="payload"></param>
        /// <returns>Group information</returns>
        private async Task<UserWssResponse> OnCreateGroupActionAsync(string userId, UserWssPayload payload)
        {
            var groupName = payload.NewGroupName;
            UserWssResponse res;

            if (string.IsNullOrEmpty(groupName))
            {
                res = GetErrorResponse("A group name is required!");
            }
            if (groupName.Length > 100)
            {
                res = GetErrorResponse("Group name max length is 100 characters");
            }

            var allGroups = await GetAllGroupsInformationAsync();
            if (allGroups.Any(g => g.Name.Equals(payload.NewGroupName, StringComparison.InvariantCulture)))
            {
                res = GetErrorResponse("Group name already exist!");
            }
            else
            {
                var newGroupId = Guid.NewGuid().ToString();

                var groupActor = GetGroupActor(newGroupId);
                await groupActor.SetGroupNameAsync(payload.NewGroupName);
                await groupActor.AddMemberAsync(userId);
                var messages = await groupActor.GetMessagesAsync();

                var userActor = GetUserActor(userId);
                await userActor.AddGroupAsync(newGroupId);

                res = new UserWssResponse
                {
                    NewGroup = new Group
                    {
                        Id = newGroupId,
                        Name = payload.NewGroupName,
                        Messages = messages
                    }
                };
            }

            res.Counter = payload.Counter;
            return res;
        }

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
                new Uri("fabric:/NextChat/UserActorService"));
        }

        private IGroupActor GetGroupActor(string groupId)
        {
            return ActorProxy.Create<IGroupActor>(
                new ActorId(groupId),
                new Uri("fabric:/NextChat/GroupActorService"));
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NextChat.ChatApi.Models;
using NextChat.ChatApi.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IWssMessageHandler _messageHandler;

        public ChatHub(IWssMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }

        public async Task InitializeState(UserWssPayload _)
        {
            var userId = Context.UserIdentifier;
            var (userGroups, otherGroups) = await _messageHandler.GetConnectionInitialStateAsync(userId);
            var res = new
            {
                userGroups,
                otherGroups
            };

            await Clients.Clients(Context.ConnectionId).SendAsync("InitialState", res);
        }


        public async Task NewGroup(UserWssPayload payload)
        {
            var groupName = payload.NewGroupName;
            var userId = Context.UserIdentifier;
            var (newGroupSucceeded, newGroupId) = await _messageHandler.NewGroupAsync(userId, groupName);

            if (!newGroupSucceeded)
            {
                var res = new NewGroupResultResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create group. Group name '{groupName}' already exists!"
                };
                await Clients.Clients(Context.ConnectionId).SendAsync("NewGroupResult", res);
            }
            else
            {
                // inform current connection
                var newGroup = await _messageHandler.GetGroupAsync(newGroupId);
                var res = new NewGroupResultResponse
                {
                    Group = newGroup,
                    Success = true
                };

                await Clients.Clients(Context.ConnectionId).SendAsync("NewGroupResult", res);

                // inform all the users of the new group
                var newGroupRes = new NewGroupResponse
                {
                    Group = newGroup
                };

                await Clients.All.SendAsync("NewGroup", newGroupRes);
            }
        }

        public async Task JoinGroup(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            var groupId = payload.GroupId;

            var joinSucceeded = await _messageHandler.JoinGroupAsync(userId, groupId);
            if (!joinSucceeded)
            {
                var res = new JoinResponse
                {
                    Success = false,
                    ErrorMessage = "Group is full, can't join!"
                };
                await Clients.Clients(Context.ConnectionId).SendAsync("JoinResult", res);
            }
            else
            {
                // inform current connection
                var updatedGroup = await _messageHandler.GetGroupAsync(groupId);
                var res = new JoinResponse
                {
                    Group = updatedGroup,
                    Success = true
                };

                await Clients.Clients(Context.ConnectionId).SendAsync("JoinResult", res);

                // inform the group of the new user
                var userList = updatedGroup.Users.ToList();
                var affectedClients = Clients.Users(userList);
                var newMememberRes = new NewMemberReponse
                {
                    UserId = userId,
                    GroupId = groupId
                };

                await affectedClients.SendAsync("NewMember", newMememberRes);
            }
        }

        public async Task LeaveGroup(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            var groupId = payload.GroupId;
            await _messageHandler.LeaveGroupAsync(userId, groupId);

            // inform current connection
            await Clients.Clients(Context.ConnectionId).SendAsync("LeaveSuccess", groupId);

            // inform in the group the user has left
            var updatedGroup = await _messageHandler.GetGroupAsync(groupId);
            var userList = updatedGroup.Users.ToList();
            var affectedClients = Clients.Users(userList);
            var memberLeftResponse = new MemberLeftReponse
            {
                UserId = userId,
                GroupId = groupId
            };

            await affectedClients.SendAsync("MemberLeft", memberLeftResponse);
        }

        public async Task NewMessage(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            await _messageHandler.AddMessageAsync(userId, payload);

            var updatedGroup = await _messageHandler.GetGroupAsync(payload.GroupId);
            var affectedClients = Clients.Users(updatedGroup.Users.ToList());
            var res = new NewMessageReponse
            {
                GroupId = payload.GroupId,
                Messages = updatedGroup.Messages
            };

            await affectedClients.SendAsync("NewMessage", res);
        }
    }
}

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
        // Multiple clients broadcast target
        private const string NewGroupMessage = "NewGroup";
        private const string NewMemberMessage = "NewMember";
        private const string MemberLeftMessage = "MemberLeft";
        private const string NewMessageMessage = "NewMessage";

        // Single client broadcast target
        private const string InitialStateMessage = "InitialState";
        private const string NewGroupResultMessage = "NewGroupResult";
        private const string JoinResultMessage = "JoinResult";
        private const string LeaveSuccessMessage = "LeaveSuccess";

        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Request initial state of the chat application for a user.
        /// Initial state is sent to the current connection
        /// </summary>
        public async Task InitializeState(UserWssPayload _)
        {
            var userId = Context.UserIdentifier;
            var (userGroups, otherGroups) = await _chatService.GetConnectionInitialStateAsync(userId);
            var res = new InitialStateResponse
            {
                UserGroups = userGroups,
                OtherGroups = otherGroups
            };

            await Clients.Clients(Context.ConnectionId).SendAsync(InitialStateMessage, res);
        }


        /// <summary>
        /// Create a new group
        /// Create result is sent to the current connection.
        /// If succeeded, all connected clients are notified of the new group
        /// </summary>
        public async Task NewGroup(UserWssPayload payload)
        {
            var groupName = payload.NewGroupName;
            var userId = Context.UserIdentifier;
            var (newGroupSucceeded, newGroupId) = await _chatService.NewGroupAsync(userId, groupName);

            if (!newGroupSucceeded)
            {
                var res = new NewGroupResultResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create group. Group name '{groupName}' already exists!"
                };
                await Clients.Clients(Context.ConnectionId).SendAsync(NewGroupResultMessage, res);
            }
            else
            {
                // inform current connection
                var newGroup = await _chatService.GetGroupAsync(newGroupId);
                var res = new NewGroupResultResponse
                {
                    Group = newGroup,
                    Success = true
                };

                await Clients.Clients(Context.ConnectionId).SendAsync(NewGroupResultMessage, res);

                // inform all the users of the new group
                var newGroupRes = new NewGroupResponse
                {
                    Group = newGroup
                };

                await Clients.All.SendAsync(NewGroupMessage, newGroupRes);
            }
        }

        /// <summary>
        /// Request to join group.
        /// Join result is sent to the current connection.
        /// If succeeded, said group's members are notified of the new member
        /// </summary>
        public async Task JoinGroup(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            var groupId = payload.GroupId;

            var joinSucceeded = await _chatService.JoinGroupAsync(userId, groupId);
            if (!joinSucceeded)
            {
                var res = new JoinResponse
                {
                    Success = false,
                    ErrorMessage = "Group is full, can't join!"
                };
                await Clients.Clients(Context.ConnectionId).SendAsync(JoinResultMessage, res);
            }
            else
            {
                // inform current connection
                var updatedGroup = await _chatService.GetGroupAsync(groupId);
                var res = new JoinResponse
                {
                    Group = updatedGroup,
                    Success = true
                };

                await Clients.Clients(Context.ConnectionId).SendAsync(JoinResultMessage, res);

                // inform the group of the new user
                var userList = updatedGroup.Users.ToList();
                var affectedClients = Clients.Users(userList);
                var newMememberRes = new NewMemberReponse
                {
                    UserId = userId,
                    GroupId = groupId
                };

                await affectedClients.SendAsync(NewMemberMessage, newMememberRes);
            }
        }

        /// <summary>
        /// Leave group. Current connect is notified that leave action went through
        /// Said group's members are notified of the members change
        /// </summary>
        public async Task LeaveGroup(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            var groupId = payload.GroupId;
            await _chatService.LeaveGroupAsync(userId, groupId);

            // inform current connection
            await Clients.Clients(Context.ConnectionId).SendAsync(LeaveSuccessMessage, groupId);

            // inform in the group the user has left
            var updatedGroup = await _chatService.GetGroupAsync(groupId);
            var userList = updatedGroup.Users.ToList();
            var affectedClients = Clients.Users(userList);
            var memberLeftResponse = new MemberLeftReponse
            {
                UserId = userId,
                GroupId = groupId
            };

            await affectedClients.SendAsync(MemberLeftMessage, memberLeftResponse);
        }

        /// <summary>
        /// Add message to a group.
        /// Said group's members are notified of the new message
        /// </summary>
        public async Task NewMessage(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            await _chatService.AddMessageAsync(userId, payload);

            var updatedGroup = await _chatService.GetGroupAsync(payload.GroupId);
            var affectedClients = Clients.Users(updatedGroup.Users.ToList());
            var res = new NewMessageReponse
            {
                GroupId = payload.GroupId,
                Messages = updatedGroup.Messages
            };

            await affectedClients.SendAsync(NewMessageMessage, res);
        }
    }
}

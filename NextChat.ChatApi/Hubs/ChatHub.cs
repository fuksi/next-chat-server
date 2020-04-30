using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NextChat.ChatApi.Models;
using NextChat.ChatApi.Services;
using NextChat.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        // Multiple clients broadcast target
        public static readonly string NewGroupMessage = "NewGroup";
        public static readonly string NewMemberMessage = "NewMember";
        public static readonly string MemberLeftMessage = "MemberLeft";
        public static readonly string NewMessageMessage = "NewMessage";

        // Single client broadcast target
        public static readonly string InitialStateMessage = "InitialState";
        public static readonly string NewGroupResultMessage = "NewGroupResult";
        public static readonly string JoinResultMessage = "JoinResult";
        public static readonly string LeaveSuccessMessage = "LeaveSuccess";

        public static readonly string UserEmailClaimType = "https://nextchat.me/email";

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
            var res = await _chatService.GetConnectionInitialStateAsync(userId);

            await Clients.Client(Context.ConnectionId).SendAsync(InitialStateMessage, res);
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
                await Clients.Client(Context.ConnectionId).SendAsync(NewGroupResultMessage, res);
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

                await Clients.Client(Context.ConnectionId).SendAsync(NewGroupResultMessage, res);

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
        /// If succeeded, notify all connection of relevant group propery change
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
                await Clients.Client(Context.ConnectionId).SendAsync(JoinResultMessage, res);
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

                await Clients.Client(Context.ConnectionId).SendAsync(JoinResultMessage, res);

                // inform all connection that group has changed
                var memberChangeRes = new MemberChangeResponse
                {
                    Group = updatedGroup
                };

                await Clients.All.SendAsync(NewMemberMessage, memberChangeRes);
            }
        }

        /// <summary>
        /// Leave group. Current connect is notified that leave action went through
        /// All connected clients are notified of group members change
        /// </summary>
        public async Task LeaveGroup(UserWssPayload payload)
        {
            var userId = Context.UserIdentifier;
            var groupId = payload.GroupId;
            await _chatService.LeaveGroupAsync(userId, groupId);

            // inform current connection
            await Clients.Client(Context.ConnectionId).SendAsync(LeaveSuccessMessage, groupId);

            // inform all that group members has changed
            var updatedGroup = await _chatService.GetGroupAsync(groupId);
            var memberChangeRes = new MemberChangeResponse
            {
                Group = updatedGroup
            };

            await Clients.All.SendAsync(MemberLeftMessage, memberChangeRes);
        }

        /// <summary>
        /// Add message to a group.
        /// Said group's members are notified of the new message
        /// </summary>
        public async Task NewMessage(UserWssPayload payload)
        {
            var newMessage = new GroupMessage
            {
                UserId = Context.UserIdentifier,
                Email = GetUserEmail(),
                CreatedAt = DateTime.UtcNow,
                Content = payload.NewMessage
            };
            await _chatService.AddMessageAsync(newMessage, payload.GroupId);

            var updatedGroup = await _chatService.GetGroupAsync(payload.GroupId);
            var affectedClients = Clients.Users(updatedGroup.Users.ToList());
            var res = new NewMessageReponse
            {
                GroupId = payload.GroupId,
                Messages = updatedGroup.Messages
            };

            await affectedClients.SendAsync(NewMessageMessage, res);
        }

        private string GetUserEmail()
        {
            return Context.User.Claims.Single(c => c.Type == UserEmailClaimType).Value;
        }
    }
}

using Microsoft.AspNetCore.SignalR;
using Moq;
using NextChat.ChatApi.Hubs;
using NextChat.ChatApi.Models;
using NextChat.ChatApi.Services;
using NextChat.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace NextChat.ChatApi.Tests.Unit
{
    public class ChatHubTests
    {
        private const string TestNewMessage = "newMessageTest";
        private const string TestUserId = "userid123";
        private const string TestUserEmail = "email456";
        private const string TestConnectionId = "connectionidabc";
        private const string TestGroupId = "groupidxyz";
        private const string TestGroupName = "groupnameabc";
        private HashSet<string> TestUsersGroup = new HashSet<string> { "usera", "userb" };

        private CancellationToken _defaultCancellationToken = CancellationToken.None;

        private Mock<IClientProxy> _currentConnClientProxyMock;
        private Mock<IClientProxy> _groupConnClientProxyMock;
        private Mock<IClientProxy> _allConnClientProxyMock;
        private Mock<IHubCallerClients> _hubCallClientsMock;

        private Mock<IChatService> _chatServiceMock;

        private ChatHub _target;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Mock user for hub context
            var testClaims = new List<Claim>
            {
                new Claim(ChatHub.UserEmailClaimType, TestUserEmail)
            };
            var claimPrincipalMock = new Mock<ClaimsPrincipal>();
            claimPrincipalMock.SetupGet(u => u.Claims).Returns(testClaims);

            // Mock hub context for target (ChatHub context)
            var hubContextMock = new Mock<HubCallerContext>();
            hubContextMock.SetupGet(m => m.ConnectionId).Returns(TestConnectionId);
            hubContextMock.SetupGet(m => m.UserIdentifier).Returns(TestUserId);
            hubContextMock.SetupGet(m => m.User).Returns(claimPrincipalMock.Object);

            // Mock Clients for ChatHub
            _currentConnClientProxyMock = new Mock<IClientProxy>();
            _allConnClientProxyMock = new Mock<IClientProxy>();
            _groupConnClientProxyMock = new Mock<IClientProxy>();

            _hubCallClientsMock = new Mock<IHubCallerClients>();
            _hubCallClientsMock
                .Setup(c => c.Client(It.Is<string>(connId => connId == TestConnectionId)))
                .Returns(_currentConnClientProxyMock.Object);
            _hubCallClientsMock
                .SetupGet(c => c.All)
                .Returns(_allConnClientProxyMock.Object);
            _hubCallClientsMock
                .Setup(c => c.Users(It.Is<List<string>>(u => u.TrueForAll(e => TestUsersGroup.Contains(e)))))
                .Returns(_groupConnClientProxyMock.Object);
            
            // Create test target
            _chatServiceMock = new Mock<IChatService>();
            _target = new ChatHub(_chatServiceMock.Object);
            _target.Context = hubContextMock.Object;
            _target.Clients = _hubCallClientsMock.Object;

        }

        [SetUp]
        public void Setup()
        {
            _chatServiceMock.Reset();
            _currentConnClientProxyMock.Reset();
            _allConnClientProxyMock.Reset();
            _groupConnClientProxyMock.Reset();
        }

        [Test]
        public async Task InitializeState_HasState_NotifyCurrentConnection()
        {
            var testPayload = new UserWssPayload();
            var testRes = new InitialStateResponse();
            _chatServiceMock
                .Setup(s => s.GetConnectionInitialStateAsync(TestUserId))
                .ReturnsAsync(testRes);

            await _target.InitializeState(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.InitialStateMessage,
                content => content == testRes);
        }

        [Test]
        public async Task NewGroup_Success_NotifyCurrentAndAllConnection()
        {
            var testPayload = new UserWssPayload { NewGroupName = TestGroupName };
            var testGroup = new Group { Id = TestGroupId };
            _chatServiceMock
                .Setup(s => s.NewGroupAsync(TestUserId, testPayload.NewGroupName))
                .ReturnsAsync((true, testGroup.Id));
            _chatServiceMock
                .Setup(s => s.GetGroupAsync(testGroup.Id))
                .ReturnsAsync(testGroup);

            await _target.NewGroup(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.NewGroupResultMessage,
                content => ((NewGroupResultResponse)content).Group == testGroup);
            AssertMessageSentToAllConnections(ChatHub.NewGroupMessage,
                content => ((NewGroupResponse)content).Group == testGroup);
        }

        [Test]
        public async Task NewGroup_Failed_NotifyCurrentConnection()
        {
            var testPayload = new UserWssPayload { NewGroupName = TestGroupName };
            _chatServiceMock
                .Setup(s => s.NewGroupAsync(TestUserId, testPayload.NewGroupName))
                .ReturnsAsync((false, null));

            await _target.NewGroup(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.NewGroupResultMessage,
                content => ((NewGroupResultResponse)content).Success == false);
        }

        [Test]
        public async Task JoinGroup_Success_NotifyCurrentAndAllConnection()
        {
            var testPayload = new UserWssPayload { GroupId = TestGroupId };
            var testGroup = new Group { Id = TestGroupId };
            _chatServiceMock
                .Setup(s => s.JoinGroupAsync(TestUserId, TestGroupId))
                .ReturnsAsync(true);
            _chatServiceMock
                .Setup(s => s.GetGroupAsync(testGroup.Id))
                .ReturnsAsync(testGroup);

            await _target.JoinGroup(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.JoinResultMessage,
                content => ((JoinResponse)content).Group == testGroup);
            AssertMessageSentToAllConnections(ChatHub.NewMemberMessage,
                content => ((MemberChangeResponse)content).Group == testGroup);
        }

        [Test]
        public async Task JoinGroup_Failed_NotifyCurrentConnection()
        {
            var testPayload = new UserWssPayload { GroupId = TestGroupId };
            _chatServiceMock
                .Setup(s => s.JoinGroupAsync(TestUserId, TestGroupId))
                .ReturnsAsync(false);

            await _target.JoinGroup(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.JoinResultMessage,
                content => ((JoinResponse)content).Success == false);
        }

        [Test]
        public async Task LeaveGroup_Success_NotifyCurrentAndAllConnection()
        {
            var testPayload = new UserWssPayload { GroupId = TestGroupId };
            var testGroup = new Group { Id = TestGroupId };
            _chatServiceMock
                .Setup(s => s.LeaveGroupAsync(TestUserId, TestGroupId))
                .Returns(Task.CompletedTask);
            _chatServiceMock
                .Setup(s => s.GetGroupAsync(testGroup.Id))
                .ReturnsAsync(testGroup);

            await _target.LeaveGroup(testPayload);

            AssertMessageSentToCurrentConnection(ChatHub.LeaveSuccessMessage,
                content => (string)content == TestGroupId);
            AssertMessageSentToAllConnections(ChatHub.MemberLeftMessage,
                content => ((MemberChangeResponse)content).Group == testGroup);
        }

        [Test]
        public async Task NewMessage_Success_NotifyMessageGroupUsersConnection()
        {
            var testGroup = new Group { Id = TestGroupId, Users = TestUsersGroup };
            var testPayload = new UserWssPayload
            {
                NewMessage = TestNewMessage,
                GroupId = TestGroupId
            };

            _chatServiceMock
                .Setup(s => s.AddMessageAsync(
                    It.Is<GroupMessage>(m => m.Content == TestNewMessage), 
                    TestGroupId))
                .Returns(Task.CompletedTask);
            _chatServiceMock
                .Setup(s => s.GetGroupAsync(TestGroupId))
                .ReturnsAsync(testGroup);

            await _target.NewMessage(testPayload);

            // message integrity should be tested in e2e instead
            AssertMessageSentToGroupUsersConnection(ChatHub.NewMessageMessage, content => true); 
        }

        private void AssertMessageSentToCurrentConnection(
            string target, Predicate<object> contentPredicate)
        {
            VerifySendCoreAsync(_currentConnClientProxyMock, target, contentPredicate);
        }

        private void AssertMessageSentToGroupUsersConnection(
            string target, Predicate<object> contentPredicate)
        {
            VerifySendCoreAsync(_groupConnClientProxyMock, target, contentPredicate);
        }

        private void AssertMessageSentToAllConnections(
            string target, Predicate<object> contentPredicate)
        {
            VerifySendCoreAsync(_allConnClientProxyMock, target, contentPredicate);
        }

        // Notice that we're using SendAsync not SendCoreAsync in the actual implementation
        // However, SendAsync is an extension method and can't (conveniently) be mocked/verified 
        // Fortunately SendAsync is some what a proxy to call SendCoreAsync, which can be verifed
        // More information here: https://github.com/aspnet/SignalR/issues/2239#issuecomment-407821452
        private void VerifySendCoreAsync(Mock<IClientProxy> clientProxyMock, string target,
            Predicate<object> contentPredicate)
        {
            clientProxyMock.Verify(m => m.SendCoreAsync(
                It.Is<string>(f => f == target),
                It.Is<object[]>(o => o.Length == 1 && contentPredicate(o[0])),
                _defaultCancellationToken), Times.Once());
        }

    }
}
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace NOSPortugal.Azure
{
    /// <summary>
    /// Helper functions static class.
    /// </summary>
    public static class UsersSyncHelper
    {
        /// <summary>
        /// Authenticates to Azure AD using a client secret that was generated for an App Registration.
        /// </summary>
        /// <param name="tenantId">The Azure tenant ID.</param>
        /// <param name="clientId">The App Registration client ID.</param>
        /// <param name="clientSecret">The App Registration client secret.</param>
        /// <param name="scopes">The Auth scopes.</param>
        /// <returns>MS GraphServiceClient instance.</returns>
        public static GraphServiceClient AuthClient(
            string tenantId,
            string clientId,
            string clientSecret,
            string[] scopes)
        {
            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            return new GraphServiceClient(clientSecretCredential, scopes);
        }

        /// <summary>
        /// Loads the respective source and destination group members.
        /// </summary>
        /// <param name="client">MS GraphServiceClient instance.</param>
        /// <param name="clientId">The Azure client ID.</param>
        /// <param name="clientSecret">The Azure client secret.</param>
        /// <param name="groupId">The Azure AD group ID.</param>
        /// <param name="scopes">The Auth scopes.</param>
        /// <param name="maxRetries">The number of request API restries.</param>
        /// <param name="token">The context cancellation token.</param>
        /// <returns>Dictionary with the AD group members.</returns>
        public static async Task<ConcurrentDictionary<string, User>> LoadGroupMembers(
            GraphServiceClient client,
            string clientId,
            string clientSecret,
            string groupId,
            int maxRetries,
            CancellationToken token)
        {

            var groupMembers = new ConcurrentDictionary<string, User>();
            var graphGroupMembers = await client.Groups[groupId].Members
                .Request()
                .WithMaxRetry(maxRetries)
                .GetAsync(token);

            foreach (var member in graphGroupMembers)
            {
                var memberId = member.Id.ToString();
                var user = await client.Users[memberId]
                    .Request()
                    .WithMaxRetry(maxRetries)
                    .GetAsync(token);

                // what if, the user does not have an e-mail? :(
                var key = user.Mail.ToString().ToLowerInvariant();
                groupMembers.TryAdd(key, user);
            }

            return groupMembers;
        }

        /// <summary>
        /// This function synchronizes users between two different tenants in Azure.
        /// </summary>
        /// <param name="srcClient">MS GraphServiceClient instance for the source tenant.</param>
        /// <param name="dstClient">MS GraphServiceClient instance for the destination tenant.</param>
        /// <param name="srcMembers">Dictionary with the AD source group members.</param>
        /// <param name="dstcMembers">Dictionary with the AD destination group members.</param>
        /// <param name="dstTenantId">The destination tenant ID.</param>
        /// <param name="dstGroupId">The destination group ID.</param>
        /// <param name="maxRetries">Request max retries to the Graph API.</param>
        /// <param name="token">Context cancellation token.</param>
        /// <returns>Tuple containing simple stats report of the synchronization result.</returns>
        public static async Task<(int usrAdded, int usrDel, int totalUsers)> SyncUsers(
            GraphServiceClient srcClient,
            GraphServiceClient dstClient,
            ConcurrentDictionary<string, User> srcMembers,
            ConcurrentDictionary<string, User> dstMembers,
            string dstTenantId,
            string dstGroupId,
            string inviteBaseUrl,
            int maxRetries,
            CancellationToken token)
        {
            var report = (usrAdded: 0, usrDel: 0, totalUsers: 0);
            // add users as guests in destination
            foreach (var srcMemberPair in srcMembers)
            {
                // if source user is not in dest add it
                if (!dstMembers.ContainsKey(srcMemberPair.Key))
                {
                    var srcUser = srcMemberPair.Value;
                    // invite the user to the new tenant & add it to the dest group
                    var mail = String.IsNullOrEmpty(srcUser.Mail.ToString()) ?
                     srcUser.UserPrincipalName.ToString() :
                     srcUser.Mail.ToString();

                    var name = srcUser.DisplayName.ToString();
                    var inviteUrl = string.Format("{0}/{1}", inviteBaseUrl, dstTenantId);
                    // send invitation to user
                    var invitation = new Invitation
                    {
                        InvitedUserDisplayName = name,
                        InvitedUserEmailAddress = mail,
                        SendInvitationMessage = true,
                        InviteRedirectUrl = inviteUrl
                    };

                    var newUser = await dstClient.Invitations
                        .Request()
                        .WithMaxRetry(maxRetries)
                        .AddAsync(invitation, token);

                    // adds user to dst group
                    await dstClient.Groups[dstGroupId].Members.References
                        .Request()
                        .WithMaxRetry(maxRetries)
                        .AddAsync(newUser.InvitedUser, token);

                    report.usrAdded += 1;
                }
            }

            // remove users from dest group
            foreach (var dstMemberPair in dstMembers)
            {
                // check if user is missing from source group
                if (!srcMembers.ContainsKey(dstMemberPair.Key))
                {
                    var dstUser = dstMemberPair.Value;
                    var dstUserId = dstUser.Id.ToString();

                    // remove it from the destination group
                    await dstClient.Groups[dstGroupId].Members[dstUserId].Reference
                        .Request()
                        .WithMaxRetry(maxRetries)
                        .DeleteAsync(token);

                    // Remove user from dest tenant
                    await dstClient.Users[dstUserId]
                        .Request()
                        .WithMaxRetry(maxRetries)
                        .DeleteAsync(token);

                    report.usrDel += 1;
                }
            }

            report.totalUsers = dstMembers.Count;

            return report;
        }

        /// <summary>
        /// Loads env variables values.
        /// </summary>
        /// <param name="name">The env variable name.</param>
        /// <returns>string containing the env variable value.</returns>
        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    /// <summary>
    /// Contains the logic to sync users from different Azure Tenants.
    /// </summary>
    public static class UsersSync
    {
        /// <summary>
        /// Main Azure Function.
        /// </summary>
        /// <param name="myTimer">The timer trigger schedule. Runs hourly.</param>
        /// <param name="log">The logging interface</param>
        /// <param name="cancellationToken">The context cancellation token.</param>
        /// <returns></returns>
        [FunctionName("UsersSync")]
        [ExponentialBackoffRetry(5, "00:00:04", "00:15:00")]
        public static async Task Run([TimerTrigger("%ScheduleTrigger%")] TimerInfo myTimer, ILogger log, CancellationToken cancellationToken)
        {
            var srcTenantId = UsersSyncHelper.GetEnvironmentVariable("SRC_TENANT_ID");
            var dstTenantId = UsersSyncHelper.GetEnvironmentVariable("DST_TENANT_ID");
            var srcGroupId = UsersSyncHelper.GetEnvironmentVariable("SRC_GROUP_ID");
            var dstGroupId = UsersSyncHelper.GetEnvironmentVariable("DST_GROUP_ID");
            var clientId = UsersSyncHelper.GetEnvironmentVariable("CLIENT_ID");
            var clientSecret = UsersSyncHelper.GetEnvironmentVariable("CLIENT_SECRET");
            var inviteBaseUrl = UsersSyncHelper.GetEnvironmentVariable("INVITE_BASE_URL");
            var requestMaxRetries = UsersSyncHelper.GetEnvironmentVariable("REQUEST_MAX_RETRIES");

            if (srcTenantId == null ||
                dstTenantId == null ||
                srcGroupId == null ||
                dstGroupId == null ||
                clientId == null ||
                clientSecret == null ||
                inviteBaseUrl == null ||
                requestMaxRetries == null) {
              log.LogError("Invalid function input! Exiting...");
              throw new ArgumentException("Invalid Function Input. Please check the app settings configuration!");
            }

            if (!Int32.TryParse(requestMaxRetries, out var maxRetries)) {
              log.LogError("Error parsing input variable requestMaxRetries! Exiting...");
              throw new ArgumentException("Invalid Function Request Max Retries. Please check requestMaxRetries configuration!");
            }

            try {
              log.LogInformation($"Loading group memebers using app registration '{clientId}'...");
              string[] scopes = { "https://graph.microsoft.com/.default" };

              var srcClient = UsersSyncHelper.AuthClient(srcTenantId, clientId, clientSecret, scopes);
              var dstClient = UsersSyncHelper.AuthClient(dstTenantId, clientId, clientSecret, scopes);
              var srcMembers = await UsersSyncHelper.LoadGroupMembers(srcClient, clientId, clientSecret, srcGroupId, maxRetries, cancellationToken);
              var dstMembers = await UsersSyncHelper.LoadGroupMembers(dstClient, clientId, clientSecret, dstGroupId, maxRetries, cancellationToken);

              log.LogInformation("Loading group members done!");
              log.LogInformation("Syncing Users...");

              var report = await UsersSyncHelper.SyncUsers(srcClient, dstClient, srcMembers, dstMembers, dstTenantId, dstGroupId, inviteBaseUrl, maxRetries, cancellationToken);

              log.LogInformation("Users synced!");
              log.LogInformation($"# Users added: {report.usrAdded}");
              log.LogInformation($"# Users deleted: {report.usrDel}");
              log.LogInformation($"# of Users: {report.totalUsers}");

            } catch (Exception ex) {
              if(ex is ServiceException) {
                var svcException = ex as ServiceException;
                log.LogError($"Error: {svcException.InnerException.Message.ToString()}\n");
              } else if (ex is OperationCanceledException) {
                log.LogError($"Function context was cancelled: {ex.InnerException.Message.ToString()}");
              } else {
                log.LogError($"Unknown Error: {ex.InnerException.Message.ToString()}");
              }
              throw;
            }
        }
    }
}

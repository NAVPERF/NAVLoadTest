using System;
using System.Collections.Concurrent;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Nav.UserSession;
using Microsoft.VisualStudio.TestTools.LoadTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Dynamics.Nav.TestUtilities
{
    /// <summary>
    /// UserContextManager provides user contexts for a given tenant/company/user
    /// This allows tests to reuse sessions for a given virtual user
    /// The purpose of the class is to ensure that there are as many active NAV sessions as there are virtual users
    /// </summary>
    public abstract class UserContextManager : IDisposable
    {
        private ConcurrentDictionary<int, UserContext> UserContextPool { get; set; }
        public string NAVServerUrl { get; private set; }
        public string TenantId { get; private set; }
        public string Company { get; private set; }
        public int? RoleCenterId { get; private set; }
        public string UICulture { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="navServerUrl">URL for NAV ClientService</param>
        /// <param name="tenantId">Tenant</param>
        /// <param name="companyName">Company</param>
        /// <param name="roleCenterId">Role Center to use for the users</param>
        public UserContextManager(string navServerUrl, string tenantId, string companyName, int? roleCenterId, string uiCulture = null)
        {
            this.UserContextPool = new ConcurrentDictionary<int, UserContext>();
            NAVServerUrl = navServerUrl;
            this.TenantId = tenantId;
            this.Company = companyName;
            this.UICulture = uiCulture;
            RoleCenterId = roleCenterId;
        }

        /// <summary>
        /// Thread safety for UserContext creation ensure we don't try and create users simultaneously
        /// </summary>
        private static readonly object Lockobj = new object();

        /// <summary>
        /// Create a new UserContext and instruments the session transactions 
        /// </summary>
        /// <param name="testContext">The current Test Context</param>
        /// <returns>a new UserContext</returns>
        private UserContext CreateSession(TestContext testContext)
        {
            lock (Lockobj)
            {
                var userContext = CreateUserContext(testContext);
                using (new TestTransaction(testContext, "InitializeSession"))
                {
                    userContext.InitializeSession(NAVServerUrl);
                }

                using (new TestTransaction(testContext, "OpenSession"))
                {
                    userContext.OpenSession(UICulture);
                }

                if (RoleCenterId.HasValue)
                {
                    using (new TestTransaction(testContext, "OpenRoleCenter"))
                    {
                        userContext.OpenRoleCenter(RoleCenterId.Value);
                    }
                }

                return userContext;
            }
        }
        
        /// <summary>
        /// Get the User Context for the test user and create a new user if it doesn't already exist
        /// </summary>
        /// <param name="testContext"></param>
        /// <returns></returns>
        public UserContext GetUserContext(TestContext testContext)
        {
            string userName = GetUserName(testContext);
            UserContext userContext = CreateUserContext(testContext);
            int userId = GetTestUserId(testContext);
            if (this.UserContextPool.TryRemove(userId, out userContext))
            {
                return userContext;
            }
            return CreateSession(testContext);
        }

        /// <summary>
        /// Get the UserName for the current virtual user
        /// </summary>
        /// <param name="testContext">current test context</param>
        /// <returns></returns>
        protected abstract string GetUserName(TestContext testContext);

        /// <summary>
        /// Create a new user context for the current virtual user
        /// </summary>
        /// <param name="testContext">current test context</param>
        /// <returns></returns>
        protected abstract UserContext CreateUserContext(TestContext testContext);
        
        /// <summary>
        /// Return the user context to the pool
        /// </summary>
        /// <param name="testContext"></param>
        /// <param name="userContext"></param>
        public void ReturnUserContext(TestContext testContext, UserContext userContext)
        {
            if (userContext != null)
            {
                int userId = GetTestUserId(testContext);
                this.UserContextPool.TryAdd(userId, userContext);
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseAllSessions();
            }
        }

        public void CloseAllSessions()
        {
            // close any open sessions
            foreach (var context in UserContextPool)
            {
                context.Value.CloseSession();
            }
        }

        /// <summary>
        /// Get a unique id for the current virtual user or 0 if there is no load test context
        /// </summary>
        /// <param name="testContext">current test context</param>
        /// <returns></returns>
        protected static int GetTestUserId(TestContext testContext)
        {
            LoadTestUserContext loadTestUserContext = testContext.GetLoadTestUserContext();
            return (loadTestUserContext != null) ? loadTestUserContext.UserId : 0;
        }
                
    }
}

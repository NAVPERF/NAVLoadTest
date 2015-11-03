using System.Security.Principal;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Nav.UserSession;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Dynamics.Nav.TestUtilities
{
    /// <summary>
    /// WindowsUserContextManager manages user contexts for a given tenant/company/user
    /// All virtual users use the current Windows Identity 
    /// </summary>
    public class WindowsUserContextManager : UserContextManager
    {
        /// <summary>
        /// Creates the WindowsUserContextManager for a given tenant/company/user
        /// </summary>
        /// <param name="navServerUrl">URL for NAV ClientService</param>
        /// <param name="tenantId">Tenant</param>
        /// <param name="companyName">Company</param>
        /// <param name="roleCenterId">Role Center to use for the users</param>
        public WindowsUserContextManager(string navServerUrl, string tenantId, string companyName, int? roleCenterId, string uiCulture = null)
            : base(navServerUrl, tenantId, companyName, roleCenterId, uiCulture){}


        protected override UserContext CreateUserContext(TestContext testContext)
        {
            var userName = GetUserName(testContext);
            var userContext = new UserContext(TenantId, Company, AuthenticationScheme.Windows, userName);
            return userContext;
        }

        protected override string GetUserName(TestContext testContext)
        {
            return WindowsIdentity.GetCurrent().Name;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.Dynamics.Framework.UI.Client;

namespace Microsoft.Dynamics.Nav.UserSession
{
    public class UserContext
    {
        public UserContext(string tenantId, string company, AuthenticationScheme authenticationScheme, string userName = null, string password = null)
        {
            TenantId = tenantId;
            Company = company;
            AuthenticationScheme = authenticationScheme;
            UserName = userName;
            Password = password;
        }

        public string TenantId { get; private set; }

        public string Company { get; private set; }

        public AuthenticationScheme AuthenticationScheme { get; private set; }

        public string UserName { get; private set; }

        public string Password { get; private set; }

        private ClientSession clientSession;

        private ClientSession ClientSession
        {
            get
            {
                if (clientSession == null)
                {
                    throw new InvalidOperationException("Client Session Not Initialized");
                }
                return clientSession;
            }
        }

        public event EventHandler<ClientDialogToShowEventArgs> DialogHandler;

        public ClientLogicalForm RoleCenterPage { get; private set; }

        /// <summary>Opens the session synchronously.</summary>
        public void OpenSession(string uiCulture = null)
        {
            this.ClientSession.OpenSession(uiCulture);
            this.ClientSession.DialogToShow += clientSession_DialogToShow;
        }

        /// <summary>Closes the session synchronously.</summary>
        public void CloseSession()
        {
            this.CloseAllForms();
            this.ClientSession.CloseSession();
            this.ClientSession.DialogToShow -= clientSession_DialogToShow;
        }

        /// <summary>Invokes the interaction synchronously.</summary>
        /// <param name="interaction">The interaction.</param>
        public void InvokeInteraction(ClientInteraction interaction)
        {
            this.ClientSession.InvokeInteraction(interaction);
        }

        private void clientSession_DialogToShow(object sender, ClientDialogToShowEventArgs e)
        {
            if (DialogHandler != null)
                DialogHandler(sender, e);
        }

        /// <summary>Closes the froms in the session synchronously.</summary>
        public void CloseAllForms()
        {
            this.ClientSession.CloseAllForms();
            this.ClientSession.AwaitAllFormsAreClosedAndSessionIsReady();
        }

        public void WaitForReady()
        {
            this.ClientSession.AwaitSessionIsReady();
        }

        public IEnumerable<ClientLogicalForm> OpenedForms
        {
            get { return this.ClientSession.OpenedForms; }
        }

        /// <summary>"Catches" a new form opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The catch form. If no such form exists, returns null.</returns>
        public ClientLogicalForm CatchForm(Action action)
        {
            return this.ClientSession.CatchForm(action);
        }

        /// <summary>"Catches" a new lookup form opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The catch lookup form. If no such lookup form exists, returns null.</returns>
        public ClientLogicalForm CatchLookupForm(Action action)
        {
            return this.ClientSession.CatchLookupForm(action);
        }

        /// <summary>"Catches" a Uri to show (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The catch uri to show. If no such URI exists, returns null.</returns>
        public string CatchUriToShow(Action action)
        {
            return this.ClientSession.CatchUriToShow(action);
        }

        /// <summary>"Catches" a new dialog opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The catch dialog. If no such dialog exists, returns null.</returns>
        public ClientLogicalForm CatchDialog(Action action)
        {
            return this.ClientSession.CatchDialog(action);
        }

        /// <summary>Inititialies a new <see cref="ClientSession"/>.</summary>
        /// <param name="serviceAddress">The service Address.</param>
        /// <param name="authentication">The authentication.</param>
        /// <param name="password">The password.</param>
        /// <returns>The initialize session.</returns>
        public void InitializeSession(string serviceAddress)
        {
            this.clientSession = ClientSessionExtensions.InitializeSession(serviceAddress, this.TenantId, this.Company,
                AuthenticationScheme, this.UserName, this.Password);
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="clientSession"/> is open.
        /// </summary>
        /// <returns><c>true</c> is <see cref="clientSession"/> is open <c>false</c> otherwise</returns>
        public bool IsReadyOrBusy()
        {
            return this.ClientSession.IsReadyOrBusy();
        }

        /// <summary>
        /// Open a form.
        /// </summary>
        /// <param name="formId">The id of the form to open.</param>
        /// <returns>The form opened.</returns>
        public ClientLogicalForm OpenForm(string formId)
        {
            return this.ClientSession.OpenForm(formId);
        }

        /// <summary>
        /// Open a form and closes the Cronus dialog if it is shown. If another dialog is shown this will thron an exception.
        /// </summary>
        /// <param name="formId">The id of the form to open.</param>
        /// <returns>The form opened.</returns>
        /// <exception cref="InvalidOperationException">If a dialog is shown that is not the Cronus dialog.</exception>
        public ClientLogicalForm OpenInitialForm(string formId)
        {
            return this.ClientSession.OpenInitialForm(formId);
        }

        public void ValidateForm(ClientLogicalForm form)
        {
            ObservableCollection<ClientValidationResultItem> prevalidate = form.ValidationResults;
            if (prevalidate.Count == 0)
            {
                return;
            }

            foreach (ClientValidationResultItem clientValidationResultItem in prevalidate)
            {
                throw new Exception(clientValidationResultItem.Description);
            }
        }

        public ClientLogicalForm EnsurePage(int expectedPageNo, ClientLogicalForm page)
        {
            if (expectedPageNo == 0)
            {
                if (page != null)
                {
                    if (page.ControlIdentifier.Equals("00000000-0000-0000-0800-0000836bd2d2"))
                    {
                        throw new Exception("ERROR: " +
                                            page.Children[0].Children[0].Children[1].Caption.Replace(
                                                Environment.NewLine, " "));
                    }
                    var actualPageNo = GetActualPageNo(page);
                    if (actualPageNo != expectedPageNo)
                    {
                        throw new Exception(String.Format("Not expecting any page to open, but got Page No. {0}",
                            actualPageNo));
                    }
                }
            }
            else
            {
                if (page == null)
                {
                    throw new Exception(String.Format("Expecting Page No. {0} to open, instead no page was opened",
                        expectedPageNo));
                }
                if (page.ControlIdentifier.Equals("00000000-0000-0000-0800-0000836bd2d2"))
                {
                    throw new Exception("ERROR: " +
                                        page.Children[0].Children[0].Children[1].Caption.Replace(Environment.NewLine,
                                            " "));
                }

                int actualPageNo;
                try
                {
                    actualPageNo = GetActualPageNo(page);
                }
                catch
                {
                    throw new Exception(
                        String.Format(
                            "Expecting Page No. {0} to open, instead got a page with ControlIdentifier={1}, Caption={2}",
                            expectedPageNo, page.ControlIdentifier, page.Caption));
                }
                if (actualPageNo != expectedPageNo)
                {
                    throw new Exception(String.Format("Expecting Page No. {0} to open, instead got Page No. {1}",
                        expectedPageNo, actualPageNo));
                }
            }
            return page;
        }

        public static int GetActualPageNo(ClientLogicalForm form)
        {
            int actualPageNo;
            try
            {
                actualPageNo = Convert.ToInt32(form.ControlIdentifier.Substring(1, 8), 16);
            }
            catch
            {
                throw new Exception(
                    String.Format(
                        "Not expecting any page to open, but got a page with ControlIdentifier={0}, Caption={1}",
                        form.ControlIdentifier, form.Caption));
            }
            return actualPageNo;
        }

        public int GetOpenFormCount()
        {
            return OpenedForms.Count();
        }

        public void CheckOpenForms(int initalFormCount)
        {
            if (this.OpenedForms.Count() > initalFormCount)
            {
                string captions = "";
                foreach (var form in this.OpenedForms)
                    captions += (String.IsNullOrEmpty(captions) ? "" : ",") + "'" + form.Caption + "/" +
                                form.ControlIdentifier +
                                "'";
                throw new Exception(String.Format("Not all forms were closed ({0})", captions));
            }
        }

        public void OpenRoleCenter(int roleCenterId)
        {
            this.RoleCenterPage = this.OpenInitialForm(roleCenterId.ToString(CultureInfo.InvariantCulture));
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;
using Microsoft.Dynamics.Framework.UI.Client.WcfClient;

namespace Microsoft.Dynamics.Nav.UserSession
{
    /// <summary>
    /// Helper methods for testing with Client Service
    /// </summary>
    public static partial class ClientSessionExtensions
    {
        private const int AwaitForeverDuration = -1;

        private const int AwaitDuration = 500;

        private const int AwaitAllFormsAreClosedDuration = 10000;

        /// <summary>Invokes the interaction synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="interaction">The interaction.</param>
        public static void InvokeInteraction(this ClientSession clientSession, ClientInteraction interaction)
        {
            clientSession.AwaitReady(() => clientSession.InvokeInteractionAsync(interaction));
        }

        /// <summary>Opens the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void OpenSession(this ClientSession clientSession, string uiCulture = null)
        {
            var sessionParameters = new ClientSessionParameters { CultureId = CultureInfo.CurrentCulture.Name };
            if (uiCulture != null)
                sessionParameters.UICultureId = CultureInfo.GetCultureInfoByIetfLanguageTag(uiCulture).Name;
            sessionParameters.AdditionalSettings.Add("IncludeControlIdentifier", true);
            clientSession.AwaitReady(() => clientSession.OpenSessionAsync(sessionParameters));
        }

        /// <summary>Closes the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseSession(this ClientSession clientSession)
        {
            if (clientSession.State == ClientSessionState.Closed)
            {
                return;
            }

            clientSession.AwaitReady(clientSession.CloseSessionAsync, session => session.State == ClientSessionState.Closed, true, AwaitForeverDuration);
        }

        /// <summary>Closes the froms in the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseAllForms(this ClientSession clientSession)
        {
            clientSession.AwaitReady(
                () => clientSession.InvokeInteractionsAsync(
                    clientSession.OpenedForms.Select(clientLogicalForm => new CloseFormInteraction(clientLogicalForm))
                    ),
                    session => session.State == ClientSessionState.Ready && !session.OpenedForms.Any(),
                    false,
                    AwaitForeverDuration
                );
        }

        /// <summary>Closes the froms in the session asynchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseAllFormsAsync(this ClientSession clientSession)
        {
            clientSession.InvokeInteractionsAsync(clientSession.OpenedForms.Select(clientLogicalForm => new CloseFormInteraction(clientLogicalForm)));
        }

        /// <summary>Awaits until all the forms are closed.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static bool AwaitSessionIsReady(this ClientSession clientSession)
        {
            return clientSession.AwaitReady(() => { }, session => session.State == ClientSessionState.Ready, false, AwaitDuration);
        }

        /// <summary>Awaits until all the forms are closed.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static bool AwaitAllFormsAreClosedAndSessionIsReady(this ClientSession clientSession)
        {
            return clientSession.AwaitReady(() => { }, session => session.State == ClientSessionState.Ready && !session.OpenedForms.Any(), false, AwaitAllFormsAreClosedDuration);
        }

        /// <summary>"Catches" a new form opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <returns>The catch form. If no such form exists, returns null.</returns>
        public static ClientLogicalForm CatchForm(this ClientSession clientSession, Action action)
        {
            ClientLogicalForm form = null;
            EventHandler<ClientFormToShowEventArgs> clientSessionOnFormToShow = delegate(object sender, ClientFormToShowEventArgs args) { form = args.FormToShow; };
            clientSession.FormToShow += clientSessionOnFormToShow;
            try
            {
                action();
            }
            finally
            {
                clientSession.FormToShow -= clientSessionOnFormToShow;
            }

            return form;
        }

        /// <summary>"Catches" a new lookup form opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <returns>The catch lookup form. If no such lookup form exists, returns null.</returns>
        public static ClientLogicalForm CatchLookupForm(this ClientSession clientSession, Action action)
        {
            ClientLogicalForm form = null;
            EventHandler<ClientLookupFormToShowEventArgs> clientSessionOnLookupFormToShow = delegate(object sender, ClientLookupFormToShowEventArgs args) { form = args.LookupFormToShow; };
            clientSession.LookupFormToShow += clientSessionOnLookupFormToShow;
            try
            {
                action();
            }
            finally
            {
                clientSession.LookupFormToShow -= clientSessionOnLookupFormToShow;
            }

            return form;
        }

        /// <summary>"Catches" a Uri to show (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <returns>The catch uri to show. If no such URI exists, returns null.</returns>
        public static string CatchUriToShow(this ClientSession clientSession, Action action)
        {
            string uri = null;
            EventHandler<ClientUriToShowEventArgs> clientSessionOnUriToShow = delegate(object sender, ClientUriToShowEventArgs args)
            {
                if (uri != null)
                {
                    throw new Exception("UriToShow fired more than once.");
                }

                uri = args.UriToShow;
            };
            clientSession.UriToShow += clientSessionOnUriToShow;
            try
            {
                action();
            }
            finally
            {
                clientSession.UriToShow -= clientSessionOnUriToShow;
            }

            return uri;
        }

        /// <summary>"Catches" a new dialog opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <returns>The catch dialog. If no such dialog exists, returns null.</returns>
        public static ClientLogicalForm CatchDialog(this ClientSession clientSession, Action action)
        {
            ClientLogicalForm dialog = null;
            EventHandler<ClientDialogToShowEventArgs> clientSessionOnDialogToShow =
                (sender, args) => dialog = args.DialogToShow;

            bool wasSuspended = false;

            // If there is an instance of UnexpectedDialogHandler registered in the client session
            // make sure the UnexpectedDialogHandler is suspended while we catch a dialog
            UnexpectedDialogHandler unexpectedDialogHandler = GetUnexpectedDialogHandler(clientSession);
            if (unexpectedDialogHandler != null)
            {
                wasSuspended = unexpectedDialogHandler.IsSuspended;
                unexpectedDialogHandler.IsSuspended = true;
            }

            clientSession.DialogToShow += clientSessionOnDialogToShow;
            try
            {
                action();
            }
            finally
            {
                clientSession.DialogToShow -= clientSessionOnDialogToShow;
                if (unexpectedDialogHandler != null)
                {
                    unexpectedDialogHandler.IsSuspended = wasSuspended;
                }
            }

            return dialog;
        }

        /// <summary>Inititialies a new <see cref="ClientSession"/>.</summary>
        /// <param name="serviceAddress">The service Address.</param>
        /// <param name="tenantId">The optional tenant id.</param>
        /// <param name="company">The company to open</param>
        /// <param name="authentication">The authentication.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The initialize session.</returns>
        public static ClientSession InitializeSession(string serviceAddress, string tenantId = null, string company = null, AuthenticationScheme? authentication = null, string username = null, string password = null)
        {
            if (string.IsNullOrWhiteSpace(serviceAddress))
            {
                throw new ArgumentNullException("serviceAddress");
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                serviceAddress += "?tenant=" + tenantId;
            }

            if (!string.IsNullOrEmpty(company))
            {
                serviceAddress += (string.IsNullOrEmpty(tenantId) ? "?" : "&") + "company=" + Uri.EscapeDataString(company);
            }

            if (authentication == null)
            {
                // Discover Authentication settings
                ServiceSettings serviceSettings = new ServiceSettings(new DiscoverSettingsProvider(AsyncClientDiscoveryFactory.Create(new Uri(serviceAddress))));
                authentication = serviceSettings.AuthenticationScheme;
            }

            var binding = new BasicHttpBinding();
            var clientServiceClient = new WcfServiceClient(binding, new EndpointAddress(serviceAddress));

            Uri addressUri = new Uri(serviceAddress);
            binding.Security.Mode = addressUri.Scheme == Uri.UriSchemeHttps ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.TransportCredentialOnly;

            binding.AllowCookies = true;
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferSize = int.MaxValue;
            binding.MaxBufferPoolSize = int.MaxValue;
            binding.ReaderQuotas.MaxStringContentLength = 10240000;
            binding.ReaderQuotas.MaxDepth = 64;
            binding.UseDefaultWebProxy = true;
            binding.OpenTimeout = TimeSpan.FromMinutes(10);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(10);
            binding.SendTimeout = TimeSpan.FromMinutes(10);
            binding.CloseTimeout = TimeSpan.FromMinutes(10);

            switch (authentication)
            {
               case AuthenticationScheme.Ntlm:
                    binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Ntlm;
                    binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
                    clientServiceClient.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                    clientServiceClient.ChannelFactory.Credentials.Windows.ClientCredential = System.Net.CredentialCache.DefaultNetworkCredentials;
                    break;
                case AuthenticationScheme.Windows:
                    binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                    clientServiceClient.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                    clientServiceClient.ChannelFactory.Credentials.Windows.ClientCredential = System.Net.CredentialCache.DefaultNetworkCredentials;
                    break;
                case AuthenticationScheme.UserNamePassword:
                    binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
                    binding.Security.Message = new BasicHttpMessageSecurity { ClientCredentialType = BasicHttpMessageCredentialType.UserName };
                    clientServiceClient.ClientCredentials.UserName.UserName = username;
                    clientServiceClient.ClientCredentials.UserName.Password = password;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("authentication");
            }

            return new ClientSession(new AsyncWcfClientService(clientServiceClient), new NonDispatcher(), new TimerFactory<ThreadTimer>());
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="clientSession"/> is open.
        /// </summary>
        /// <param name="clientSession">The client Session.</param>
        /// <returns><c>true</c> is <see cref="clientSession"/> is open <c>false</c> otherwise</returns>
        public static bool IsReadyOrBusy(this ClientSession clientSession)
        {
            return clientSession.State == ClientSessionState.Ready || clientSession.State == ClientSessionState.Busy;
        }

        /// <summary>
        /// Open a form.
        /// </summary>
        /// <param name="clientSession">The <see cref="ClientSession"/>.</param>
        /// <param name="formId">The id of the form to open.</param>
        /// <returns>The form opened.</returns>
        public static ClientLogicalForm OpenForm(this ClientSession clientSession, string formId)
        {
            return clientSession.CatchForm(() => clientSession.InvokeInteraction(new OpenFormInteraction { Page = formId }));
        }

        /// <summary>
        /// Open a form and closes the Cronus dialog if it is shown. If another dialog is shown this will throw an exception.
        /// </summary>
        /// <param name="clientSession">The <see cref="ClientSession"/>.</param>
        /// <param name="formId">The id of the form to open.</param>
        /// <returns>The form opened.</returns>
        /// <exception cref="InvalidOperationException">If a dialog is shown that is not the Cronus dialog.</exception>
        public static ClientLogicalForm OpenInitialForm(this ClientSession clientSession, string formId)
        {
            return clientSession.CatchForm(delegate
            {
                ClientLogicalForm dialog =
                    clientSession.CatchDialog(
                        () => clientSession.InvokeInteraction(new OpenFormInteraction { Page = formId }));
                if (dialog != null)
                {
                    if (ClientLogicalFormExtensions.IsCronusDemoDialog(dialog))
                    {
                        clientSession.InvokeInteraction(new CloseFormInteraction(dialog));
                    }
                    else
                    {
                        string exceptionMessage = "Unexpected dialog shown: " + dialog.Caption;

                        ClientStaticStringControl staticStringControl =
                            dialog.ContainedControls.OfType<ClientStaticStringControl>().FirstOrDefault();
                        if (staticStringControl != null)
                        {
                            exceptionMessage += " - " + staticStringControl.StringValue;
                        }

                        throw new InvalidOperationException(exceptionMessage);
                    }
                }
            });
        }

        /// <summary>Awaits that the <see cref="ClientSession"/> reached the ready state.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        internal static void AwaitReady(this ClientSession clientSession, Action action)
        {
            AwaitReady(clientSession, action, session => session.State == ClientSessionState.Ready, false, AwaitForeverDuration);
        }

        /// <summary>
        /// Awaits that the <see cref="ClientSession"/> reached the ready state.
        /// </summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <param name="readyCondition">The ready Condition.</param>
        /// <param name="allowClosed">if set to <c>true</c> allows session state to be closed.</param>
        /// <param name="maxDuration">Max await duration.</param>
        internal static bool AwaitReady(this ClientSession clientSession, Action action, Func<ClientSession, bool> readyCondition, bool allowClosed, int maxDuration)
        {
            bool waitForever = maxDuration < 0;

            using (var awaitContext = new AwaitClientContext(clientSession, readyCondition))
            {
                action();
                while (!readyCondition(clientSession))
                {
                    if (awaitContext.CommunicationErrors.Any())
                    {
                        throw new InvalidOperationException("Communication error was thrown:\n" + awaitContext.CommunicationErrors.First(), awaitContext.CommunicationErrors.First());
                    }

                    if (awaitContext.UnhandledExceptions.Any())
                    {
                        if (awaitContext.UnhandledExceptions.Any(e => e is TestAbortException))
                        {
                            throw awaitContext.UnhandledExceptions.First(e => e is TestAbortException);
                        }

                        throw new InvalidOperationException("Unhandled exception was thrown:\n" + awaitContext.UnhandledExceptions.First(), awaitContext.UnhandledExceptions.First());
                    }

                    if (awaitContext.Messages.Any())
                    {
                        throw new InvalidOperationException("Message was shown:\n" + awaitContext.Messages.First());
                    }

                    if (!string.IsNullOrEmpty(awaitContext.InvalidCredentialMessage))
                    {
                        throw new InvalidCredentialsException(awaitContext.InvalidCredentialMessage);
                    }

                    switch (clientSession.State)
                    {
                        case ClientSessionState.InError:
                            throw new InvalidOperationException("ClientSession entered Error state without raising error events.");
                        case ClientSessionState.TimedOut:
                            throw new InvalidOperationException("ClientSession has timed out.");
                        case ClientSessionState.Closed:
                            if (!allowClosed)
                            {
                                throw new InvalidOperationException("ClientSession has been closed unexpectedly.");
                            }

                            break;
                    }

                    if (waitForever || maxDuration > 0)
                    {
                        awaitContext.Wait(AwaitDuration);

                        maxDuration -= AwaitDuration;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static UnexpectedDialogHandler GetUnexpectedDialogHandler(ClientSession clientSession)
        {
            object dialogHandlerObj;
            if (clientSession.Attributes.TryGetValue(UnexpectedDialogHandler.UnexpectedDialogHandlerKey, out dialogHandlerObj))
            {
                UnexpectedDialogHandler unexpectedDialogHandler = dialogHandlerObj as UnexpectedDialogHandler;
                if (unexpectedDialogHandler != null)
                {
                    return unexpectedDialogHandler;
                }
            }

            return null;
        }
    }
}
using System;
using System.Globalization;
using System.Linq;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;
using System.Net;

namespace Microsoft.Dynamics.Nav.UserSession
{
    /// <summary>
    /// Helper methods for testing with Client Service
    /// </summary>
    public static partial class ClientSessionExtensions
    {
        private const int AwaitInteractionDuration = 5000;
        private const int AwaitFormOpenDuration = 5000;
        private const int AwaitAllFormsAreClosedDuration = 10000;
        private const int AwaitSessionDuration = 10000;

        /// <summary>Invokes the interaction synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="interaction">The interaction.</param>
        public static void InvokeInteraction(
            this ClientSession clientSession,
            ClientInteraction interaction,
            int duration = AwaitInteractionDuration)
        {
            clientSession.AwaitReady(() => 
                clientSession.InvokeInteractionAsync(interaction),
                session => session.State == ClientSessionState.Ready,
                false,
                AwaitInteractionDuration);
        }

        /// <summary>Opens the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="uiCultureId">The language culture Id. For example "da-DK"</param>
        public static void OpenSession(
            this ClientSession clientSession,
            string uiCultureId)
        {
            var sessionParameters = new ClientSessionParameters
            {
                CultureId = CultureInfo.CurrentCulture.Name,
                UICultureId = uiCultureId
            };
            sessionParameters.AdditionalSettings.Add("IncludeControlIdentifier", true);
            clientSession.AwaitReady(
                () => clientSession.OpenSessionAsync(sessionParameters),
                session => session.State == ClientSessionState.Ready,
                false,
                AwaitSessionDuration);
        }

        /// <summary>Closes the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseSession(this ClientSession clientSession)
        {
            if (clientSession.State == ClientSessionState.Closed)
            {
                return;
            }

            clientSession.AwaitReady(
                clientSession.CloseSessionAsync,
                session => session.State == ClientSessionState.Closed, 
                true,
                AwaitSessionDuration);
        }

        /// <summary>Closes the froms in the session synchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseAllForms(this ClientSession clientSession)
        {
            clientSession.AwaitReady(() => 
                    CloseAllFormsAsync(clientSession),
                    session => session.State == ClientSessionState.Ready && !session.OpenedForms.Any(),
                    false,
                    AwaitAllFormsAreClosedDuration);
        }

        /// <summary>Closes the froms in the session asynchronously.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void CloseAllFormsAsync(this ClientSession clientSession)
        {
            clientSession.InvokeInteractionsAsync(
                clientSession.OpenedForms.Select(
                    clientLogicalForm => new CloseFormInteraction(clientLogicalForm)));
        }

        /// <summary>Awaits until all the forms are closed.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void AwaitSessionIsReady(this ClientSession clientSession)
        {
            clientSession.AwaitReady(
                () => { }, 
                session => session.State == ClientSessionState.Ready,
                false, 
                AwaitSessionDuration);
        }

        /// <summary>Awaits until all the forms are closed.</summary>
        /// <param name="clientSession">The client Session.</param>
        public static void AwaitAllFormsAreClosedAndSessionIsReady(this ClientSession clientSession)
        {
            clientSession.AwaitReady(
                () => { }, 
                session => session.State == ClientSessionState.Ready && !session.OpenedForms.Any(), 
                false, 
                AwaitAllFormsAreClosedDuration);
        }

        /// <summary>"Catches" a new form opened (if any) during executions of <paramref name="action"/>.</summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <returns>The catch form. If no such form exists, returns null.</returns>
        public static ClientLogicalForm CatchForm(this ClientSession clientSession, Action action)
        {
            ClientLogicalForm form = null;
            EventHandler<ClientFormToShowEventArgs> clientSessionOnFormToShow = delegate (object sender, ClientFormToShowEventArgs args) { form = args.FormToShow; };
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
            EventHandler<ClientLookupFormToShowEventArgs> clientSessionOnLookupFormToShow = delegate (object sender, ClientLookupFormToShowEventArgs args) { form = args.LookupFormToShow; };
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
            EventHandler<ClientUriToShowEventArgs> clientSessionOnUriToShow = delegate (object sender, ClientUriToShowEventArgs args)
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

            Uri addressUri = ServiceAddressProvider.ServiceAddress(new Uri(serviceAddress));
            ICredentials credentials = null;
            if (authentication.GetValueOrDefault() == AuthenticationScheme.UserNamePassword)
            {
                credentials = new NetworkCredential(username, password);
            }

            var jsonClient = new JsonHttpClient(addressUri, credentials, authentication.GetValueOrDefault());
            return new ClientSession(jsonClient, new NonDispatcher(), new TimerFactory<TaskTimer>());
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
            return clientSession.CatchForm(() => 
                clientSession.InvokeInteraction(
                    new OpenFormInteraction { Page = formId },
                    AwaitFormOpenDuration));
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
                        () => clientSession.InvokeInteraction(
                            new OpenFormInteraction { Page = formId },
                            AwaitFormOpenDuration));
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

        /// <summary>
        /// Awaits that the <see cref="ClientSession"/> reached the ready state.
        /// </summary>
        /// <param name="clientSession">The client Session.</param>
        /// <param name="action">The action.</param>
        /// <param name="readyCondition">The ready Condition.</param>
        /// <param name="allowClosed">if set to <c>true</c> allows session state to be closed.</param>
        /// <param name="maxDuration">Max await duration.</param>
        private static void AwaitReady(
            this ClientSession clientSession,
            Action action, 
            Func<ClientSession, bool> readyCondition, 
            bool allowClosed, 
            int maxDuration)
        {
            const int AwaitStepDuration = 500;
            var remainingTime = maxDuration;
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

                    if (remainingTime > 0)
                    {
                        awaitContext.Wait(AwaitStepDuration);

                        remainingTime -= AwaitStepDuration;
                    }
                    else
                    {
                        throw new TimeoutException($"Timeout occurred after {maxDuration} ms waiting for action to complete.");
                    }
                }
            }
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
using System;
using System.Globalization;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;
using Microsoft.Dynamics.Nav.UserSession;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Dynamics.Nav.TestUtilities
{
    public class TestScenario
    {

        /// <summary>
        /// Run an end-to-end test scenario
        /// Ensure any opened forms are closed at the end of the scenario
        /// </summary>
        /// <param name="manager">UserContextManager</param>
        /// <param name="testContext">current Test Context</param>
        /// <param name="action">function to perform the user actions</param>
        /// <param name="actionName">the name of the scenario being performed</param>
        public static void Run(UserContextManager manager, TestContext testContext, Action<UserContext> action, string actionName = null)
        {
            var userContext = manager.GetUserContext(testContext);
            var formCount = userContext.GetOpenFormCount();
            try
            {
                action(userContext);
                userContext.CheckOpenForms(formCount);
                userContext.WaitForReady();
                manager.ReturnUserContext(testContext, userContext);
            }
            catch (Exception)
            {
                // if error occurs we close the session and don't return to the pool
                userContext.WaitForReady();
                CloseSession(userContext, testContext);
                throw;
            }
        }

        /// <summary>
        /// Open a page run an action and close the page
        /// </summary>
        /// <param name="testContext">current Test Context</param>
        /// <param name="context">current user Context</param>
        /// <param name="pageId">page to open</param>
        /// <param name="formAction">action to run</param>
        public static void RunPageAction(TestContext testContext, UserContext context, int pageId, Action<ClientLogicalForm> formAction = null)
        {
            var form = OpenPage(testContext, context, pageId);

            if (formAction != null)
            {
                formAction(form);
            }

            ClosePage(testContext, context, form);
        }

        public static ClientLogicalForm OpenPage(TestContext testContext, UserContext context, int pageId)
        {
            ClientLogicalForm form;
            using (new TestTransaction(testContext, String.Format("OpenPage{0}", pageId)))
            {
                form = context.OpenForm(pageId.ToString(CultureInfo.InvariantCulture));
                context.EnsurePage(pageId, form);
            }

            DelayTiming.SleepDelay(DelayTiming.OpenFormDelay);
            return form;
        }

        public static void ClosePage(TestContext testContext, UserContext context, ClientLogicalForm form)
        {
            if (form.State == ClientLogicalFormState.Open)
            {
                using (new TestTransaction(testContext, String.Format("ClosePage{0}", UserContext.GetActualPageNo(form))))
                {
                    context.InvokeInteraction(new CloseFormInteraction(form));
                }
            }
        }
        private static void CloseSession(UserContext userContext, TestContext testContext)
        {
            try
            {
                using (new TestTransaction(testContext, "CloseSession"))
                {
                    userContext.CloseSession();
                }
            }
            catch (Exception exception)
            {
                testContext.WriteLine("Error occurred on CloseSession {0}", exception.Message);
            }
        }


        public static void SaveValueWithDelay(ClientLogicalControl control, object value)
        {
            control.SaveValue(value);
            DelayTiming.SleepDelay(DelayTiming.EntryDelay);
        }

        /// <summary>
        /// Save the specified value with entry delay and ignore any warning dialogs
        /// </summary>
        /// <param name="context">the current test contest</param>
        /// <param name="userContext">the user context</param>
        /// <param name="control">the control to be updated</param>
        /// <param name="value">the value</param>
        /// <param name="ignoreAction">the name of the action, default is Yes</param>
        public static void SaveValueAndIgnoreWarning(TestContext context, UserContext userContext, ClientLogicalControl control, string value, string ignoreAction = "Yes")
        {
            var dialog = userContext.CatchDialog(() => SaveValueWithDelay(control, value));
            if (dialog != null)
            {
                try
                {
                    var action = dialog.Action(ignoreAction);
                    if (action != null)
                    {
                        action.Invoke();
                        context.WriteLine("Dialog Caption: {0} Message: {1} was ignored with Action: {2} ", dialog.Caption, dialog.FindMessage(), ignoreAction);
                    }
                }
                catch (InvalidOperationException)
                {
                    context.WriteLine("Dialog Caption: {0} Message: {1} Action: {2} was not found.", dialog.Caption, dialog.FindMessage(), ignoreAction);
                    throw;
                }
            }
        }

        public static string SelectRandomRecordFromListPage(
            TestContext testContext,
            UserContext context,
            int pageId,
            string keyFieldCaption)
        {
            string randomKey = null;
            RunPageAction(testContext, context, pageId, form =>
            {
                randomKey = SelectRandomRecord(form, keyFieldCaption);
                testContext.WriteLine(
                "Selected Random Record from Page:{0} Key:{1} Value:{2}",
                pageId,
                keyFieldCaption,
                randomKey);
            });
            return randomKey;
        }

        public static string SelectRandomRecordFromLookup(
            TestContext testContext,
            UserContext context,
            ClientLogicalControl control,
            string keyFieldCaption)
        {
            var form = control.InvokeCatchLookup();
            if (form == null)
            {
                throw new InvalidOperationException("No Lookup Form found");
            }
            try
            {
                var randomKey = SelectRandomRecord(form, keyFieldCaption);
                testContext.WriteLine(
                "Selected Random Record from Lookup:{0} Key:{1} Value:{2}",
                control.Caption,
                keyFieldCaption,
                randomKey);
                return randomKey;
            }
            finally
            {
                ClosePage(testContext, context, form);
            }
        }

        public static string SelectRandomRecord(
            ClientLogicalForm form,
            string keyFieldCaption)
        {
            // selects a random row from the repeater
            var rowCount = form.Repeater().DefaultViewport.Count;
            var rowToSelect = SafeRandom.GetRandomNext(rowCount);
            var rowControl = form.Repeater().DefaultViewport[rowToSelect];
            var randomKey = rowControl.Control(keyFieldCaption).StringValue;
            return randomKey;
        }

        public static void ApplyColumnFilter(
            TestContext testContext,
            UserContext userContext,
            ClientRepeaterColumnControl column,
            string value)
        {
            using (new TestTransaction(testContext, "FilterBy" + column.Caption))
            {
                var filterForm = column.Action("Filter...").InvokeCatchDialog();
                var filterControl = filterForm.Control(column.Caption);
                filterControl.SaveValue(value);
                userContext.InvokeInteraction(
                    new InvokeActionInteraction(
                        filterForm.Action("OK")));
            }
        }
    }
}

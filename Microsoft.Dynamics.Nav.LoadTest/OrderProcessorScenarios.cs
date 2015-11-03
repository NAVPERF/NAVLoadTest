using System.Globalization;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;
using Microsoft.Dynamics.Nav.LoadTest.Properties;
using Microsoft.Dynamics.Nav.TestUtilities;
using Microsoft.Dynamics.Nav.UserSession;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Dynamics.Nav.LoadTest
{
    [TestClass]
    public class OrderProcessorScenarios
    {
        public TestContext TestContext { get; set; }

        private const int OrderProcessorRoleCenterId = 9006;
        private const int CustomerListPageId = 22;
        private const int ItemListPageId = 31;
        private const int SalesOrderListPageId = 9305;
        private const int SalesOrderPageId = 42;

        private static UserContextManager orderProcessorUserContextManager;

        public UserContextManager OrderProcessorUserContextManager
        {
            get
            {
                return orderProcessorUserContextManager ?? CreateUserContextManager();
            }
        }

        private UserContextManager CreateUserContextManager()
        {
            // Use the current windows user 
            orderProcessorUserContextManager = new WindowsUserContextManager(
                    NAVClientService,
                    null,
                    null,
                    OrderProcessorRoleCenterId,
                    UICulture);

            // to use NAV User Password authentication for multiple users uncomment the following
            // orderProcessorUserContextManager = new NAVUserContextManager(
            //        NavServerUrl,
            //        null,
            //        null,
            //        OrderProcessorRoleCenterId,
            //        NAVUserName,
            //        NAVPassword);

            return orderProcessorUserContextManager;
        }

        public string NAVPassword
        {
            get
            {
                return Settings.Default.NAVUserPassword;
            }
        }

        public string NAVUserName
        {
            get
            {
                return Settings.Default.NAVUserName;
            }
        }

        public string NAVClientService
        {
            get
            {
                return Settings.Default.NAVClientService;
            }
        }

        public string UICulture
        {
            get
            {
                return Settings.Default.UICulture;
            }
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            if (orderProcessorUserContextManager != null)
            {
                orderProcessorUserContextManager.CloseAllSessions();
            }
        }

        [TestMethod]
        public void OpenSalesOrderList()
        {
            // Open Page "Sales Order List" which contains a list of all sales orders
            TestScenario.Run(OrderProcessorUserContextManager, TestContext,
                userContext => TestScenario.RunPageAction(TestContext, userContext, SalesOrderListPageId));
        }

        [TestMethod]
        public void OpenCustomerList()
        {
            // Open Customers
            TestScenario.Run(OrderProcessorUserContextManager, TestContext,
                userContext => TestScenario.RunPageAction(TestContext, userContext, CustomerListPageId));
        }

        [TestMethod]
        public void OpenItemList()
        {
            // Open Customers
            TestScenario.Run(OrderProcessorUserContextManager, TestContext,
                userContext => TestScenario.RunPageAction(TestContext, userContext, ItemListPageId));
        }

        [TestMethod]
        public void LookupRandomCustomer()
        {
            TestScenario.Run(OrderProcessorUserContextManager, TestContext,
                userContext =>
                {
                    string custNo = TestScenario.SelectRandomRecordFromListPage(this.TestContext, CustomerListPageId, userContext, "No.");
                    Assert.IsNotNull(custNo, "No customer selected");
                });
        }

        [TestMethod]
        public void CreateAndPostSalesOrder()
        {
            TestScenario.Run(OrderProcessorUserContextManager, TestContext, RunCreateAndPostSalesOrder);
        }

        public void RunCreateAndPostSalesOrder(UserContext userContext)
        {
            // select a random customer
            var custno = TestScenario.SelectRandomRecordFromListPage(TestContext, CustomerListPageId, userContext, "No.");

            // Invoke using the new sales order action on Role Center
            var newSalesOrderPage = userContext.EnsurePage(SalesOrderPageId, userContext.RoleCenterPage.Action("Sales Order").InvokeCatchForm());

            // Start in the No. field
            newSalesOrderPage.Control("No.").Activate();

            // Navigate to Sell-to Customer No. field in order to create record
            newSalesOrderPage.Control("Sell-to Customer No.").Activate();

            var newSalesOrderNo = newSalesOrderPage.Control("No.").StringValue;
            TestContext.WriteLine("Created Sales Order No. {0}", newSalesOrderNo);

            // Set Sell-to Customer No. to a Random Customer and ignore any credit warning
            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, newSalesOrderPage.Control("Sell-to Customer No."), custno);

            TestScenario.SaveValueWithDelay(newSalesOrderPage.Control("External Document No."), custno);

            userContext.ValidateForm(newSalesOrderPage);

            // Add a random number of lines between 2 and 5
            int noOfLines = SafeRandom.GetRandomNext(2, 6);
            for (int line = 0; line < noOfLines; line++)
            {
                AddSalesOrderLine(userContext, newSalesOrderPage, line);
            }

            // Check Validation errors
            userContext.ValidateForm(newSalesOrderPage);

            PostSalesOrder(userContext, newSalesOrderPage);

            // Close the page
            TestScenario.ClosePage(TestContext, userContext, newSalesOrderPage);
        }

        private void PostSalesOrder(UserContext userContext, ClientLogicalForm newSalesOrderPage)
        {
            ClientLogicalForm postConfirmationDialog;
            using (new TestTransaction(TestContext, "Post"))
            {
                postConfirmationDialog = newSalesOrderPage.Action("Post...").InvokeCatchDialog();
            }

            if (postConfirmationDialog == null)
            {
                userContext.ValidateForm(newSalesOrderPage);
                Assert.Inconclusive("Post dialog can't be found");
            }

            using (new TestTransaction(TestContext, "ConfirmShipAndInvoice"))
            {
                ClientLogicalForm dialog = userContext.CatchDialog(postConfirmationDialog.Action("OK").Invoke);
                if (dialog != null)
                {
                    // after confiming the post we dont expect more dialogs
                    Assert.Fail("Unexpected Dialog on Post - Caption: {0} Message: {1}", dialog.Caption, dialog.FindMessage());
                }
            }
        }

        private void AddSalesOrderLine(UserContext userContext, ClientLogicalForm newSalesOrderPage, int line)
        {
            // Get Line
            var itemsLine = newSalesOrderPage.Repeater().DefaultViewport[line];

            // Activate Type field
            itemsLine.Control("Type").Activate();

            // set Type = Item
            TestScenario.SaveValueWithDelay(itemsLine.Control("Type"), "Item");

            // Set Item No.
            var itemNo = TestScenario.SelectRandomRecordFromListPage(TestContext, ItemListPageId, userContext, "No.");
            TestScenario.SaveValueWithDelay(itemsLine.Control("No."), itemNo);

            string qtyToOrder = SafeRandom.GetRandomNext(1, 10).ToString(CultureInfo.InvariantCulture);

            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, itemsLine.Control("Quantity"), qtyToOrder);

            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, itemsLine.Control("Qty. to Ship"), qtyToOrder, "OK");

            // Look at the line for 1 seconds.
            DelayTiming.SleepDelay(DelayTiming.ThinkDelay);
        }
    }
}

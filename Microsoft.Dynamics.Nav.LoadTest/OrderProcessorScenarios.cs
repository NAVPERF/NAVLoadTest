﻿using System.Globalization;
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
            // use NAV User Password authentication
            orderProcessorUserContextManager = new NAVUserContextManager(
                   NAVClientService,
                   null,
                   null,
                   OrderProcessorRoleCenterId,
                   NAVUserName,
                   NAVPassword);

            // Use the current windows user uncomment the following
            //orderProcessorUserContextManager = new WindowsUserContextManager(
            //        NAVClientService,
            //        null,
            //        null,
            //        OrderProcessorRoleCenterId);

            // to use NAV User Password authentication for multiple tenants uncomment the following
            //orderProcessorUserContextManager = new NAVTenantUserContextManager(
            //       NAVClientService,
            //       "default",
            //       null,
            //       OrderProcessorRoleCenterId,
            //       NAVUserName,
            //       NAVPassword);

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
                    string custNo = TestScenario.SelectRandomRecordFromListPage(this.TestContext, userContext, CustomerListPageId, "No.");
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
            // Invoke using the new sales order action on Role Center
            var newSalesOrderPage = userContext.EnsurePage(SalesOrderPageId, userContext.RoleCenterPage.Action("Sales Order").InvokeCatchForm());

            // Start in the No. field
            newSalesOrderPage.Control("No.").Activate();

            // Navigate to Sell-to Customer No. field in order to create record
            var custNameControl = newSalesOrderPage.Control("Customer Name");
            custNameControl.Activate();

            // select a random customer from Sell-to Customer No. lookup
            var custName = TestScenario.SelectRandomRecordFromLookup(TestContext, userContext, custNameControl, "Name");
            
            // Set Customer Name to a Random Customer and ignore any credit warning
            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, custNameControl, custName);

            TestScenario.SaveValueWithDelay(newSalesOrderPage.Control("External Document No."), custName);
            var newSalesOrderNo = newSalesOrderPage.Control("No.").StringValue;
            userContext.ValidateForm(newSalesOrderPage);
            TestContext.WriteLine("Created Sales Order No. {0} for Customer {1}", newSalesOrderNo, custName);

            // Add a random number of lines between 2 and 25
            var noOfLines = SafeRandom.GetRandomNext(2, 25);
            for (var line = 0; line < noOfLines; line++)
            {
                AddSalesOrderLine(userContext, newSalesOrderPage, line);
            }

            // Check Validation errors
            userContext.ValidateForm(newSalesOrderPage);

            // Post the order
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

            ClientLogicalForm openPostedInvoiceDialog;
            using (new TestTransaction(TestContext, "ConfirmShipAndInvoice"))
            {
                openPostedInvoiceDialog = userContext.CatchDialog(postConfirmationDialog.Action("OK").Invoke);
            }

            ClientLogicalForm postedInvoicePage;
            using (new TestTransaction(TestContext, "OpenPostedInvoice"))
            {
                if (openPostedInvoiceDialog != null)
                {
                    postedInvoicePage = userContext.CatchForm(openPostedInvoiceDialog.Action("Yes").Invoke);
                    var newSalesInvoiceNo = postedInvoicePage.Control("No.").StringValue;
                    TestContext.WriteLine("Posted Sales Invoice No. {0}", newSalesInvoiceNo);
                    TestScenario.ClosePage(TestContext, userContext, postedInvoicePage);
                }
            }
        }

        private void AddSalesOrderLine(UserContext userContext, ClientLogicalForm newSalesOrderPage, int index)
        {
            var repeater = newSalesOrderPage.Repeater();
            var rowCount = repeater.Offset + repeater.DefaultViewport.Count;
            if (index >= rowCount)
            {
                // scroll to the next viewport
                userContext.InvokeInteraction(new ScrollRepeaterInteraction(repeater, 1));
            }

            var rowIndex = (int)(index - repeater.Offset);
            var itemsLine = repeater.DefaultViewport[rowIndex];

            // Activate Type field
            itemsLine.Control("Type").Activate();

            // set Type = Item
            TestScenario.SaveValueWithDelay(itemsLine.Control("Type"), "Item");

            // Set Item No. from random lookup
            var itemNoControl = itemsLine.Control("No.");
            var itemNo = TestScenario.SelectRandomRecordFromLookup(TestContext, userContext, itemNoControl, "No.");
            TestScenario.SaveValueWithDelay(itemNoControl, itemNo);

            var qtyToOrder = SafeRandom.GetRandomNext(1, 10).ToString(CultureInfo.InvariantCulture);

            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, itemsLine.Control("Quantity"), qtyToOrder);

            TestScenario.SaveValueAndIgnoreWarning(TestContext, userContext, itemsLine.Control("Qty. to Ship"), qtyToOrder, "OK");

            // Look at the line for 1 seconds.
            DelayTiming.SleepDelay(DelayTiming.ThinkDelay);
        }
    }
}

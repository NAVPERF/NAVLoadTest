using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Dynamics.Framework.UI.Client;
using Microsoft.Dynamics.Framework.UI.Client.Interactions;

namespace Microsoft.Dynamics.Nav.UserSession
{
    /// <summary>
    /// Test Extensions to <see cref="ClientLogicalControl"/>.
    /// </summary>
    public static class ClientLogicalControlExtensions
    {
        /// <summary>Activates the control.</summary>
        /// <param name="control">The control.</param>
        public static void Activate(this ClientLogicalControl control)
        {
            control.GetRootForm().Session.InvokeInteraction(new ActivateControlInteraction(control));
        }

        /// <summary>Invokes the specified control.</summary>
        /// <remarks>The consumer specifies the expected form type to retrieve. If this could not be matched, the invocation is performed but null is returned.</remarks>
        /// <param name="control">The control.</param>
        public static void Invoke(this ClientLogicalControl control)
        {
            control.GetRootForm().Session.InvokeInteraction(new InvokeActionInteraction(control));
        }

        /// <summary>Invokes the specified control and returns the expected form.</summary>
        /// <remarks>The consumer specifies the expected form type to retrieve. If this could not be matched, the invocation is performed but null is returned.</remarks>
        /// <param name="control">The control.</param>
        /// <returns>The expected form.</returns>
        public static ClientLogicalForm InvokeCatchForm(this ClientLogicalControl control)
        {
            var rootForm = control.GetRootForm();
            return rootForm.Session.CatchForm(() => rootForm.Session.InvokeInteraction(new InvokeActionInteraction(control)));
        }

        /// <summary>Invokes the specified control and returns the expected dialog.</summary>
        /// <remarks>The consumer specifies the expected form type to retrieve. If this could not be matched, the invocation is performed but null is returned.</remarks>
        /// <param name="control">The control.</param>
        /// <returns>The expected dialog.</returns>
        public static ClientLogicalForm InvokeCatchDialog(this ClientLogicalControl control)
        {
            var rootForm = control.GetRootForm();
            return rootForm.Session.CatchDialog(() => rootForm.Session.InvokeInteraction(new InvokeActionInteraction(control)));
        }

        public static ClientLogicalForm InvokeCatchLookup(this ClientLogicalControl control)
        {
            var rootForm = control.GetRootForm();
            return rootForm.Session.CatchLookupForm(() => 
                rootForm.Session.InvokeInteraction(
                    new InvokeActionInteraction(control, SystemAction.Lookup)));
        }
        
        /// <summary>Save a value for the control.</summary>
        /// <param name="control">The control.</param>
        /// <param name="newValue">The new Value.</param>
        public static void SaveValue(this ClientLogicalControl control, object newValue)
        {
            control.GetRootForm().Session.InvokeInteraction(new SaveValueInteraction(control, newValue));
        }

        /// <summary>
        /// Gets the root form of a control.
        /// </summary>
        /// <remarks>
        /// This is needed because the <see cref="ClientLogicalForm.Session"/> property is not set on intermediary forms in the hierarchy.
        /// </remarks>
        /// <param name="control">The control.</param>
        /// <returns>The root form.</returns>
        private static ClientLogicalForm GetRootForm(this ClientLogicalControl control)
        {
            while (control.Parent != null && control != control.Parent)
            {
                control = control.Parent;
            }

            return (ClientLogicalForm)control;
        }


        public static ClientActionControl Action(this ClientLogicalControl control, string actionCaption)
        {
            try
            {
                return control
                    .ContainedControls
                    .OfType<ClientActionControl>()
                    .First(c => c.HasCaption(actionCaption));
            }
            catch (InvalidOperationException exception)
            {
                control.WriteControlCaptions<ClientActionControl>();
                throw new ArgumentOutOfRangeException(string.Format("Could not find an Action with caption {0}", actionCaption), exception);
            }
        }

        public static void WriteControlCaptions<T>(this ClientLogicalControl control) where T : ClientLogicalControl
        {
            var all = control.ContainedControls.OfType<T>();
            foreach (var c in all)
            {
                Debug.WriteLine(c.Caption);
            }
        }

        public static ClientLogicalControl Control(this ClientLogicalControl control, string controlCaption)
        {
            try
            {
                return control
                    .ContainedControls
                    .First(c => c.HasCaption(controlCaption));
            }
            catch (InvalidOperationException exception)
            {
                control.WriteControlCaptions<ClientLogicalControl>();
                throw new ArgumentOutOfRangeException(string.Format("Could not find a control with caption {0}",controlCaption), exception);
            }
        }

        private static bool HasCaption(this ClientLogicalControl control, string caption)
        {
            return control.Caption.Replace("&", "").Equals(caption);
        }
    }
}
using System;

namespace BugSense {
    public class BugSenseUnhandledExceptionEventArgs : EventArgs {

        /// <summary>
        /// Cancel the error handling by BugSense. Should be used with UnhandledException event of <see cref="BugSenseHandler"/>.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// A custom message for the exception that occured. ex: A username, user options etc.
        /// </summary>
        public string Comment { get; set; }

        public Exception Exception { get; set; }

        public BugSenseUnhandledExceptionEventArgs(Exception ex)
        {
            Exception = ex;
        }
    }
}

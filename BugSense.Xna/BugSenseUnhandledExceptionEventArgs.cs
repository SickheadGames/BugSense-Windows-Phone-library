using System;

namespace BugSense {
    public class BugSenseUnhandledExceptionEventArgs : EventArgs {

        /// <summary>
        /// Cancel the error handling by BugSense. Should be used with UnhandledException event of <see cref="BugSenseHandler"/>.
        /// </summary>
        public bool Cancel { get; set; }
        
        [Obsolete("Not supported in MonoTouch, but used in other platforms.")]
        public bool Handled { get; set; }

        /// <summary>
        /// A custom message for the exception that occured. ex: A username, user options etc.
        /// </summary>
        public string Comment { get; set; }

        public Exception ExceptionObject { get; set; }

        public BugSenseUnhandledExceptionEventArgs(Exception ex)
        {
            ExceptionObject = ex;
        }
    }
}

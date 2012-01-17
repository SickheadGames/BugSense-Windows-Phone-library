using System;
using BugSense.Internal;

namespace BugSense
{
    public class BugSenseLogErrorCompletedEventArgs : EventArgs {
        public BugSenseLogErrorCompletedEventArgs(BugSenseRequest request, Exception exception)
        {
            RequestData = request;
            Exception = exception;
        }

        public BugSenseRequest RequestData { get; set; }
        public Exception Exception { get; set; }
        //public bool ExitApp { get; set; }
    }
}
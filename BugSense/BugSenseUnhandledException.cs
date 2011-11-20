using System;

namespace BugSense {
    public class BugSenseUnhandledException : Exception {
        public BugSenseUnhandledException(string message)
            : base(message) { }
        public BugSenseUnhandledException() { }
        public BugSenseUnhandledException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}

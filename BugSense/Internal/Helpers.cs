using System;
using BugSense.Internal;

namespace BugSense.Extensions {
    internal static class Helpers {
        public static BugSenseEx ToBugSenseEx(this Exception ex, string comment = null)
        {
            BugSenseEx be = new BugSenseEx();
            be.Comment = comment;
            be.ClassName = ex.GetType().FullName;
            be.DateOccured = DateTime.Now;
            be.Name = ex.Message;
            be.StackTrace = ex.StackTrace;
            be.Where = "NA";
            return be;
        }
    }
}

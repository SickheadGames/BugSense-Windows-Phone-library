using System;
using System.Text;
using BugSense.Internal;

namespace BugSense.Extensions {
    internal static class Helpers {
        public static BugSenseEx ToBugSenseEx(this Exception ex)
        {
            return ToBugSenseEx(ex, null);
        }
        public static BugSenseEx ToBugSenseEx(this Exception ex, string comment)
        {
            BugSenseEx be = new BugSenseEx();
            be.Comment = comment;
            be.klass = ex.GetType().FullName;
            be.occured_at = DateTime.Now;
            be.message = ex.Message;
            be.backtrace = GetStackTrace(ex);
            be.where = "NA";
            return be;
        }

        private static string GetStackTrace(Exception ex)
        {
            StringBuilder sb = new StringBuilder(string.IsNullOrEmpty(ex.StackTrace)
                ? "not available" : ex.StackTrace);
            var innerEx = ex.InnerException;
            while (innerEx != null) {
                sb.AppendLine(string.IsNullOrEmpty(ex.StackTrace)
                                  ? "not available" : ex.StackTrace);
                innerEx = innerEx.InnerException;
            }

            return sb.ToString().Trim();
        }
    }
}

using System;
using System.Text;
using BugSense.Internal;

namespace BugSense.Extensions {
    internal static class Helpers {
        public static BugSenseEx ToBugSenseEx(this Exception ex)
        {
            return ToBugSenseEx(ex, string.Empty);
        }
        
        
        public static BugSenseEx ToBugSenseEx(this Exception ex, string comment)
        {
            BugSenseEx be = new BugSenseEx();
            be.Comment = comment;
            be.klass = ex.GetType().FullName;
            be.occured_at = DateTime.Now;
            be.message = ex.Message;
            be.backtrace = GetStackTrace(ex);
            
#if iOS
            try
            {
                // It's likely the Exception message's will be useless...
                // try to pase the class/method name from the stacktrace.
                var traceString = ex.StackTrace.ToString();
                if (!string.IsNullOrEmpty(traceString))
                {
                    var parenthidx = traceString.IndexOf(')');
                    var periodIdx = traceString.IndexOf('.');
                    var length = parenthidx - periodIdx;
                    
                    be.where = traceString.Substring(periodIdx + 1, length);
                }
            }
            catch (Exception) { }
#else
            be.where = "NA";
#endif
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

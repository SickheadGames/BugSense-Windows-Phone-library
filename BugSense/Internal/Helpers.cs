using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
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
            be.StackTrace = GetStackTrace(ex);
            be.Where = "NA";
            be.OriginalException = ex;
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

        //Based on: http://bjorn.kuiper.nu/2011/10/01/wp7-notify-user-of-new-application-version/
        public static string[] GetVersion()
        {
            Uri manifest = new Uri("WMAppManifest.xml", UriKind.Relative);
            string version = "0.0.0.0";
            string title = string.Empty;
            try {
                var si = Application.GetResourceStream(manifest);
                if (si != null) {
                    using (StreamReader sr = new StreamReader(si.Stream)) {
                        while (!sr.EndOfStream) {
                            string line = sr.ReadLine();
                            if (line != null) {
                                int i = line.IndexOf("Title=\"", StringComparison.InvariantCulture);
                                if (i >= 0) {
                                    line = line.Substring(i + 7);
                                    int z = line.IndexOf("\"", StringComparison.Ordinal);
                                    if (z >= 0) {
                                        title = line.Substring(0, z);
                                    }
                                }
                            }
                            if (line != null) {
                                int y = line.IndexOf("Version=\"", StringComparison.InvariantCulture);
                                if (y >= 0) {
                                    int z = line.IndexOf("\"", y + 9, StringComparison.InvariantCulture);
                                    if (z >= 0) {
                                        version = line.Substring(y + 9, z - y - 9);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) {
            }
            return new[] { title, version };
        }

        public static void Log(string message)
        {
            //TODO: Implement better VS logging
            Debugger.Log(3, "BugSense", message);
        }
    }
}

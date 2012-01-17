using System;
using System.Runtime.Serialization;

namespace BugSense.Internal {
    [DataContract]
    public class BugSenseEx {
        [DataMember(Name = "message")]
        public string message { get; set; }
        [DataMember(Name = "backtrace")]
        public string backtrace { get; set; }
        [DataMember(Name = "occured_at")]
        public DateTime occured_at { get; set; }
        [DataMember(Name = "klass")]
        public string klass { get; set; }
        [DataMember(Name = "where")]
        public string where { get; set; }
        public string Comment { get; set; }
    }
    
    public class AppEnvironment {
        [DataMember(Name = "phone")]
        public string phone { get; set; }
        [DataMember(Name = "appver")]
        public string appver { get; set; }
        [DataMember(Name = "appname")]
        public string appname { get; set; }
        [DataMember(Name = "osver")]
        public string osver { get; set; }
        [DataMember(Name = "wifi_on")]
        public string wifi_on { get; set; }
        [DataMember(Name = "gps_on")]
        public string gps_on { get; set; }
        [DataMember(Name = "screen:width")]
        public double ScreenWidth { get; set; }
        [DataMember(Name = "screen:height")]
        public double ScreenHeight { get; set; }
        [DataMember(Name = "screen:orientation")]
        public string ScreenOrientation { get; set; }
        [DataMember(Name = "screen_dpi(x:y)")]
        public string ScreenDpi { get; set; }
    }

    [DataContract]
    public class BugSenseRequest {
        public BugSenseRequest() { }
        public BugSenseRequest(BugSenseEx ex, AppEnvironment environment)
        {
            client = new BugSenseClient();
            request = new BugSenseInternalRequest();
            request.comment = string.IsNullOrEmpty(ex.Comment) ? ex.message : ex.Comment;
            exception = ex;
            this.application_environment = environment;
        }
        [DataMember(Name = "exception")]
        public BugSenseEx exception { get; set; }
        [DataMember(Name = "application_environment")]
        public AppEnvironment application_environment { get; set; }
        [DataMember(Name = "client")]
        public BugSenseClient client { get; set; }
        [DataMember(Name = "request")]
        public BugSenseInternalRequest request { get; set; }
    }

    [DataContract]
    public class BugSenseClient {

        public BugSenseClient()
        {
            version = "bugsense-version-0.6";
            name = "bugsense-wp7";
        }

        [DataMember(Name = "version")]
        public string version { get; set; }
        [DataMember(Name = "name")]
        public string name { get; set; }
    }

    [DataContract]
    public class BugSenseInternalRequest {
        [DataMember(Name = "comment")]
        public string comment { get; set; }
    }
}

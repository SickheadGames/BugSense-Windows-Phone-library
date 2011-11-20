using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using BugSense.Extensions;
using BugSense.Internal;
using Microsoft.Phone.Info;
using Microsoft.Phone.Net.NetworkInformation;
using Microsoft.Phone.Reactive;
using BugSense.Notifications;

namespace BugSense {
    public sealed class BugSenseHandler {

        #region [ Singleton ]

        BugSenseHandler()
        {

        }

        static BugSenseHandler()
        {
            _jsonSerializer = new DataContractJsonSerializer(typeof(BugSenseRequest));
        }

        public static BugSenseHandler Instance
        {
            get
            {
                return Nested.instance;
            }
        }

        class Nested {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly BugSenseHandler instance = new BugSenseHandler();
        }

        #endregion

        #region [ Fields ]

        private const string s_FolderName = "BugSense_Exceptions";
        private const string s_FileName = "{0}_BugSense_Ex_{1}.dat";
        private const int s_MaxExceptions = 3;
        private NotificationOptions _options;
        private Application _application;
        private bool _initialized;
        private string _appVersion;
        private string _appName;
        private static readonly DataContractJsonSerializer _jsonSerializer;
        public event EventHandler<BugSenseUnhandledExceptionEventArgs> UnhandledException;

        #endregion

        #region [ Public Methods ]

        /// <summary>
        /// Use this method inside a catch block or when you want to send error details sto BugSense
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="comment"></param>
        /// <param name="options"></param>
        public static void HandleError(Exception ex, string comment = null, NotificationOptions options = null)
        {
            if (Instance == null || !Instance._initialized)
                throw new InvalidOperationException("BugSense Handler is not initialized.");
            Instance.Handle(ex, comment, options ?? Instance._options);
        }

        /// <summary>
        /// Initialized the BugSense handler. Must be called at App constructor.
        /// </summary>
        /// <param name="application">The Windows Phone application.</param>
        /// <param name="apiKey">The Api Key that can be retrieved at bugsense.com</param>
        /// <param name="options">Optional Options</param>
        public void Init(Application application, string apiKey, NotificationOptions options = null)
        {
            if (_initialized)
                return;

            //General Initializations
            _options = options ?? DefaultOptions();
            _application = application;
            G.API_KEY = apiKey;

            //Getting version and app details
            var nameHelper = new AssemblyName(Assembly.GetCallingAssembly().FullName);
            _appVersion = nameHelper.Version.ToString();
            _appName = nameHelper.Name;

            //Attaching the handler
            _application.UnhandledException += OnUnhandledException;

            //Get a list with exceptions stored from previoys crashes
            var storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (storage.DirectoryExists(s_FolderName)) {
                try {
                    var fileNames = storage.GetFileNames(s_FolderName + "\\*").OrderByDescending(s => s).ToList();
                    int counter = 0;
                    foreach (var fileName in fileNames) {
                        if (string.IsNullOrEmpty(fileName))
                            continue;
                        string filePath = Path.Combine(s_FolderName, fileName);
                        //If there are more exceptions in the pool we just delete them.
                        if (counter < s_MaxExceptions) {
                            using (var fileStream = storage.OpenFile(filePath, FileMode.Open)) {
                                using (StreamReader sr = new StreamReader(fileStream)) {
                                    string data = sr.ReadToEnd();
                                    ExecuteRequest(data, false);
                                }
                            }
                        }
                        counter++;
                        storage.DeleteFile(filePath);
                    }
                }
                //If this fails it probably due to an issue with the Isolated Storage.
                catch (Exception e) { /* Swallow like a fish - Not much that we can do here */}
            }

            //Just in case Init is called again
            _initialized = true;
        }

        /// <summary>
        /// Gets default options for error handling
        /// </summary>
        /// <returns></returns>
        public static NotificationOptions DefaultOptions()
        {
            return new NotificationOptions {
                Title = Labels.DefaultNotificationTitle,
                Text = Labels.DefaultNotificationText_MessageBox,
                Type = enNotificationType.MessageBox
            };
        }

        #endregion

        #region [ Private Core Methods ]

        private void OnBugSenseUnhandledException(BugSenseUnhandledExceptionEventArgs e)
        {
            EventHandler<BugSenseUnhandledExceptionEventArgs> handler = UnhandledException;
            if (handler != null)
                handler(this, e);
        }

        private void OnUnhandledException(object sender, ApplicationUnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is BugSenseUnhandledException)
                return;
            var e = new BugSenseUnhandledExceptionEventArgs(args.ExceptionObject, args.Handled);
            OnBugSenseUnhandledException(e);
            args.Handled = e.Handled;
            if (!args.Handled) {
                Handle(args.ExceptionObject, e.Comment, _options);
                args.Handled = true;
            }
        }

        private void Handle(Exception e, string comment, NotificationOptions options)
        {
            var request = new BugSenseRequest(e.ToBugSenseEx(comment), GetEnvironment());
            try {
                switch (options.Type) {
                    case enNotificationType.MessageBox:
                        if (!NotificationBox.IsOpen())
                            NotificationBox.Show(options.Title, options.Text,
                                                       new NotificationBoxCommand(Labels.OkMessage, () => { }));
                        Send(request);
                        break;
                    case enNotificationType.MessageBoxConfirm:
                        if (!NotificationBox.IsOpen())
                            Scheduler.Dispatcher.Schedule(
                                () => {
                                    try {
                                        if (!NotificationBox.IsOpen())
                                            NotificationBox.Show(options.Title, options.Text,
                                                                 new NotificationBoxCommand(Labels.OkMessage, () => Send(request)),
                                                                 new NotificationBoxCommand(Labels.CancelMessage,
                                                                                            () => { }));
                                    }
                                    catch { }
                                });
                        break;
                    default:
                        Send(request);
                        break;
                }
            }
            catch { }

        }

        private void Send(BugSenseRequest request)
        {
            string json = GetJson(request);
            Scheduler.NewThread.Schedule(() => ExecuteRequest(json), TimeSpan.FromSeconds(1));
        }

        #endregion

        #region [ Private Helper Methods ]

        private string GetJson(BugSenseRequest request)
        {
            MemoryStream ms = new MemoryStream();
            _jsonSerializer.WriteObject(ms, request);
            var array = ms.ToArray();
            string json = Encoding.UTF8.GetString(array, 0, array.Length);
            return json;
        }

        private AppEnvironment GetEnvironment()
        {
            AppEnvironment environment = new AppEnvironment();
            environment.AppName = _appName;
            environment.AppVersion = _appVersion;
            environment.OsVersion = Environment.OSVersion.Version.ToString();
            string result = string.Empty;
            object manufacturer;
            if (DeviceExtendedProperties.TryGetValue("DeviceManufacturer", out manufacturer))
                result = manufacturer.ToString();
            object theModel;
            if (DeviceExtendedProperties.TryGetValue("DeviceName", out theModel))
                result = result + theModel;

            environment.PhoneModel = result;
            try {
                environment.ScreenHeight = _application.RootVisual.RenderSize.Height;
                environment.ScreenWidth = _application.RootVisual.RenderSize.Width;
            }
            catch { /* If the exception is not in the UIThread we don't have access to above */ }

            environment.GpsOn = "unavailable";
            environment.ScreenDpi = "unavailable";
            environment.ScreenOrientation = "unavailable";
            environment.WifiOn = NetworkInterface.NetworkInterfaceType.ToString();
            return environment;
        }

        private static void ExecuteRequest(string postData, bool encode = true)
        {
            try {
                if (encode)
                    postData = "data=" + HttpUtility.UrlEncode(postData);
                var request = WebRequest.CreateHttp(G.URL);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "WP7";
                request.Headers["X-BugSense-Api-Key"] = G.API_KEY;
                request.BeginGetRequestStream(ar => {
                    var requestStream = request.EndGetRequestStream(ar);
                    using (var sw = new StreamWriter(requestStream)) {
                        sw.Write(ar.AsyncState);
                    }
                    request.BeginGetResponse(a => {
                        try {
                            request.EndGetResponse(a);
                        }
                        catch (WebException e) {
                            if (e.Response is HttpWebResponse && ((HttpWebResponse)(e.Response)).StatusCode != HttpStatusCode.InternalServerError) {
                                string data = ar.AsyncState as string;
                                if (!string.IsNullOrEmpty(data)) {
                                    SaveToFile(data);
                                }
                            }
                        }
                        catch (Exception e) {
                            string data = ar.AsyncState as string;
                            if (!string.IsNullOrEmpty(data)) {
                                SaveToFile(data);
                            }
                        }
                    }, null);
                }, postData);
            }
            catch {
                //Save to send another day!
                SaveToFile(postData);
            }
        }

        private static void SaveToFile(string postData)
        {
            var storage = IsolatedStorageFile.GetUserStoreForApplication();
            if (!storage.DirectoryExists(s_FolderName))
                storage.CreateDirectory(s_FolderName);

            string fileName = string.Format(s_FileName, DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Guid.NewGuid());
            using (var fileStream = storage.CreateFile(Path.Combine(s_FolderName, fileName))) {
                using (StreamWriter sw = new StreamWriter(fileStream)) {
                    sw.Write(postData);
                }
            }
        }

        #endregion

    }
}
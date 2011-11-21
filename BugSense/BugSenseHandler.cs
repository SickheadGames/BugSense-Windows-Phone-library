using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
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
            ProccessSavedErrors();

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
            if (e.Cancel)
                return;
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
            catch (Exception ex1) {
                if (options.Type != enNotificationType.MessageBoxConfirm) {
                    Send(request);
                }
            }
        }

        private void Send(BugSenseRequest request)
        {
            string json = GetJson(request);
            SaveToFile(json);
            Scheduler.NewThread.Schedule(ProccessSavedErrors);
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

        private void ProccessFile(string filePath, IsolatedStorageFile storage)
        {
            using (var fileStream = storage.OpenFile(filePath, FileMode.Open)) {
                using (StreamReader sr = new StreamReader(fileStream)) {
                    string data = sr.ReadToEnd();
                    ExecuteRequestAsync(data, filePath);
                }
            }
        }

        private static void ExecuteRequestAsync(string errorJson, string filePath)
        {
            try {
                errorJson = "data=" + HttpUtility.UrlEncode(errorJson);
                var request = WebRequest.CreateHttp(G.URL);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "WP7";
                request.Headers["X-BugSense-Api-Key"] = G.API_KEY;
                string contextFilePath = filePath;
                request.BeginGetRequestStream(ar => {
                    try {
                        var requestStream = request.EndGetRequestStream(ar);
                        using (var sw = new StreamWriter(requestStream)) {
                            sw.Write(ar.AsyncState);
                        }
                        request.BeginGetResponse(a => {
                            try {
                                request.EndGetResponse(a);
                                //Error sent! Delete it!
                                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                                    storage.DeleteFile(contextFilePath);
                                }
                            }
                            catch { }
                        }, null);
                    }
                    catch {

                    }
                }, errorJson);
            }
            catch { /* Error is already saved so next time the app starts will try to send it again*/ }
        }

        private static void SaveToFile(string postData)
        {
            try {
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                    if (!storage.DirectoryExists(s_FolderName))
                        storage.CreateDirectory(s_FolderName);

                    string fileName = string.Format(s_FileName, DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Guid.NewGuid());
                    using (var fileStream = storage.CreateFile(Path.Combine(s_FolderName, fileName))) {
                        using (StreamWriter sw = new StreamWriter(fileStream)) {
                            sw.Write(postData);
                        }
                    }
                }
            }
            catch { /* Getting in here means the phone is about to explode */
            }
        }

        private void ProccessSavedErrors()
        {
            try {
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                    if (storage.DirectoryExists(s_FolderName)) {
                        var fileNames = storage.GetFileNames(s_FolderName + "\\*").OrderByDescending(s => s).ToList();
                        int counter = 0;
                        foreach (var fileName in fileNames) {
                            if (string.IsNullOrEmpty(fileName))
                                continue;
                            string filePath = Path.Combine(s_FolderName, fileName);
                            //If there are more exceptions in the pool we just delete them.
                            if (counter < s_MaxExceptions)
                                ProccessFile(filePath, storage);
                            else
                                storage.DeleteFile(filePath);
                            counter++;
                            //
                        }
                    }
                }
            }
            //If this fails it probably due to an issue with the Isolated Storage.
            catch (Exception e) { /* Swallow like a fish - Not much that we can do here */}
        }

        #endregion

    }
}
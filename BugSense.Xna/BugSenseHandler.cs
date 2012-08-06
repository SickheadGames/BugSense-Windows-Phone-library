using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using BugSense.Extensions;
using BugSense.Internal;
using Microsoft.Xna.Framework;

#if WINDOWS_PHONE
using System.IO.IsolatedStorage;

using Microsoft.Phone.Info;
using Microsoft.Phone.Reactive;
using ServiceStack.Text;
#endif

#if WINDOWS_RT
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Graphics.Display;
using System.Runtime.Serialization.Json;
using Windows.ApplicationModel.Core;
#endif

namespace BugSense {
    public sealed class BugSenseHandler {

        #region [ Singleton ]

        BugSenseHandler()
        {

        }

        static BugSenseHandler()
        {

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
        private bool _initialized;
        private string _appVersion;
        private string _appName;
        private Game _application;
        public event EventHandler<BugSenseUnhandledExceptionEventArgs> UnhandledException;

        #endregion

        #region [ Public Methods ]

        /// <summary>
        /// Use this method inside a catch block or when you want to send error details sto BugSense
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="comment"></param>
        /// <param name="options"></param>
        public static void LogError(Exception ex, string comment)
        {
            //if (Instance == null || !Instance._initialized)
            //    throw new InvalidOperationException("BugSense Handler is not initialized.");
            Instance.Handle(ex, comment);
        }

        /// <summary>
        /// Initialized the BugSense handler. Must be called at App constructor.
        /// </summary>
        /// <param name="application">The Windows Phone application.</param>
        /// <param name="apiKey">The Api Key that can be retrieved at bugsense.com</param>
        /// <param name="options">Optional Options</param>
        public void Init(Game application, string apiKey)
        {
            if (_initialized)
                return;

            //General Initializations
            _application = application;
            G.API_KEY = apiKey;

            //Getting version and app details
#if WINDOWS_RT
            var nameHelper = new AssemblyName(application.GetType().GetTypeInfo().Assembly.FullName);
#else
            var nameHelper = new AssemblyName(Assembly.GetCallingAssembly().FullName);
#endif
            _appVersion = nameHelper.Version.ToString();
            _appName = nameHelper.Name;

            //Get a list with exceptions stored from previoys crashes
            ProccessSavedErrors();

            //Just in case Init is called again
            _initialized = true;
        }

        #endregion

        #region [ Private Core Methods ]

        //private void OnBugSenseUnhandledException(BugSenseUnhandledExceptionEventArgs e)
        //{
        //    EventHandler<BugSenseUnhandledExceptionEventArgs> handler = UnhandledException;
        //    if (handler != null)
        //        handler(this, e);
        //}

        private void Handle(Exception e, string comment)
        {
            var request = new BugSenseRequest(e.ToBugSenseEx(comment), GetEnvironment());
            try {
                Send(request);
            }
            catch (Exception ex1) {
            }
        }

        private void Send(BugSenseRequest request)
        {
            string json = GetJson(request);
            if (!string.IsNullOrEmpty(json)) {
                SaveToFile(json);
#if WINDOWS_RT
                Task.Run( () => ProccessSavedErrors() );
#else
                Scheduler.NewThread.Schedule(ProccessSavedErrors);
#endif
            }
        }

        #endregion

        #region [ Private Helper Methods ]

        private string GetJson(BugSenseRequest request)
        {
            try {
                Log("Sending json ");
                using (MemoryStream ms = new MemoryStream()) {
#if WINDOWS_RT
                    new DataContractJsonSerializer(typeof(BugSenseRequest)).WriteObject(ms,request);
#else
                    JsonSerializer.SerializeToStream(request, typeof(BugSenseRequest), ms);
#endif
                    var array = ms.ToArray();
                    string json = Encoding.UTF8.GetString(array, 0, array.Length);
                    json = json.Replace("ScreenDpi", "screen_dpi(x:y)")
                        .Replace("ScreenOrientation", "screen:orientation")
                        .Replace("ScreenHeight", "screen:height")
                        .Replace("ScreenWidth", "screen:width");
                    return json;
                }
            }
            catch {
                Log("Error during BugSenseRequest serialization");
                return string.Empty;
            }
        }

        private AppEnvironment GetEnvironment()
        {
            AppEnvironment environment = new AppEnvironment();
            environment.appname = _appName;
            environment.appver = _appVersion;
#if WINDOWS_RT
            // TODO: Currently there is no way to get the OS version
            // even for error logging purposes... which i suspect will
            // change before too long.
            environment.osver = "Windows 8 Metro";
#else
            environment.osver = Environment.OSVersion.Version.ToString();
#endif
            string result = string.Empty;

#if WINDOWS_RT
#else
            object manufacturer;
            //TODO: Find model
            if (DeviceExtendedProperties.TryGetValue("DeviceManufacturer", out manufacturer))
                result = manufacturer.ToString();
            object theModel;
            if (DeviceExtendedProperties.TryGetValue("DeviceName", out theModel))
                result = result + theModel;
#endif

            environment.phone = result;
            try {
                environment.ScreenHeight = _application.Window.ClientBounds.Height;
                environment.ScreenWidth = _application.Window.ClientBounds.Width;
            }
            catch { /* If the exception is not in the UIThread we don't have access to above */ }

            environment.gps_on = "unavailable";
#if WINDOWS_RT
            environment.ScreenDpi = DisplayProperties.LogicalDpi.ToString();
#else
            environment.ScreenDpi = "unavailable";
#endif
            environment.ScreenOrientation = _application.Window.CurrentOrientation.ToString();
            environment.wifi_on = NetworkInterface.GetIsNetworkAvailable() ? bool.TrueString : bool.FalseString;
            return environment;
        }

        private static void ProccessFile(Stream fileStream, string filePath)
        {
            using (StreamReader sr = new StreamReader(fileStream)) {
                string data = sr.ReadToEnd();
                ExecuteRequestAsync(data, filePath);
            }
        }

        private static void ExecuteRequestAsync(string errorJson, string filePath)
        {
            try {
                errorJson = "data=" + Uri.EscapeDataString(errorJson);
                var request = WebRequest.CreateHttp(G.URL);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
#if WINDOWS_RT
                request.Headers["User-Agent"] = "WinRT";
#else
                request.UserAgent = "WP7";                
#endif
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

#if WINDOWS_RT

                                Task.Run(
                                    async () =>
                                    {
                                        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(contextFilePath);
                                        file.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                    }).Wait();
                               
#else
                                //Error sent! Delete it!
                                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                                    if (storage.FileExists(contextFilePath))
                                        storage.DeleteFile(contextFilePath);
                                }
#endif
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
#if WINDOWS_RT

                Task.Run(
                    async () =>
                    {
                        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(s_FolderName, CreationCollisionOption.OpenIfExists);

                        var fileName = string.Format(s_FileName, DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Guid.NewGuid());
                        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                        using (var fileStream = await file.OpenStreamForWriteAsync())
                        {
                            using (var sw = new StreamWriter(fileStream))
                                sw.Write(postData);
                        }

                    }).Wait();

#else
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
#endif
            }
            catch { /* Getting in here means the phone is about to explode */
            }
        }

        private void ProccessSavedErrors()
        {
            try {
#if WINDOWS_RT

                Task.Run(
                    async () =>
                    {
                        var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(s_FolderName);
                        var files = (await folder.GetFilesAsync()).OrderByDescending( s => s.Name ).ToList();

                        int counter = 0;
                        foreach (var file in files)
                        {
                            // If there are more exceptions in the pool we just delete them.
                            if (counter < s_MaxExceptions)
                                using (var fileStream = await file.OpenStreamForReadAsync())
                                    ProccessFile(fileStream, file.Path);
                            else
                                file.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            counter++;
                        }
                    });

#else
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
                                using (var fileStream = storage.OpenFile(filePath, FileMode.Open)) {
                                    ProccessFile(fileStream, filePath);
                            else
                                storage.DeleteFile(filePath);
                            counter++;
                            //
                        }
                    }
                }
#endif
            }
            //If this fails it probably due to an issue with the Isolated Storage.
            catch (Exception e) { /* Swallow like a fish - Not much that we can do here */}
        }

        private void Log(string message)
        {
            //TODO: Implement better VS logging
#if WINDOWS_RT
            Debug.WriteLine("BugSense: " + message);
#else
            Debugger.Log(3, "BugSense", message);
#endif
        }

        #endregion

    }
}
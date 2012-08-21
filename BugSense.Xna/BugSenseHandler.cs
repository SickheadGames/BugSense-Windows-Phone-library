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

#if WINDOWS_PHONE
using System.IO.IsolatedStorage;
using System.Windows;
using Microsoft.Phone.Info;
using Microsoft.Phone.Reactive;
using ServiceStack.Text;
#endif

#if iOS
using ServiceStack.Text;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
#endif

#if WINDOWS_RT
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Graphics.Display;
using System.Runtime.Serialization.Json;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Foundation;
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
        private static string _dataPath;

        public string ScreenOrientation = string.Empty;
        public Point ScreenSize = new Point();

        //private Game _application;
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
            HandleError(ex, comment);
        }

        /// <summary>
        /// Initialized the BugSense handler. Must be called at App constructor.
        /// </summary>
        /// <param name="application">The Windows Phone application.</param>
        /// <param name="apiKey">The Api Key that can be retrieved at bugsense.com</param>
        /// <param name="options">Optional Options</param>
        public void Init(string apiKey)
        {
            if (_initialized)
                return;

            G.API_KEY = apiKey;

            //Getting version and app details
#if WINDOWS_RT
            var nameHelper = new AssemblyName(Application.Current.GetType().GetTypeInfo().Assembly.FullName);
#else
            var nameHelper = new AssemblyName(Assembly.GetCallingAssembly().FullName);
#endif
            _appVersion = nameHelper.Version.ToString();
            _appName = nameHelper.Name;
            
#if iOS
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), s_FolderName);
            
            if (!Directory.Exists(_dataPath))
                Directory.CreateDirectory(_dataPath);
#endif
            
            //Get a list with exceptions stored from previoys crashes
            ProccessSavedErrors();

            //Just in case Init is called again
            _initialized = true;
            
            // Setup our unhandled exception handler.
#if WINDOWS_RT || WINDOWS_PHONE
            Application.Current.UnhandledException += OnUnhandledException;
#else
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
#endif
        }
        
#if WINDOWS_PHONE
        private void OnUnhandledException(object sender, ApplicationUnhandledExceptionEventArgs a)
#else
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs a)
#endif
        {
#if WINDOWS_RT
            var ex = (Exception)a.Exception;
#else
            var ex = (Exception)a.ExceptionObject;
#endif
            
            var bsException = ex.ToBugSenseEx();
            var bugsenseArgs = new BugSenseUnhandledExceptionEventArgs(ex);
            
            var handler = UnhandledException;
            if (handler != null)
                handler(this, bugsenseArgs);
            
            // Cancel reporting if we've been told to
            if (bugsenseArgs.Cancel)
                return;
            
            // Report the exception
            LogError(ex, bugsenseArgs.Comment);
        }

        #endregion

        #region [ Private Core Methods ]

        /// <summary>
        /// Make sure to set screen size/orientation before calling this!
        /// </summary>
        public static void HandleError(Exception e, string comment)
        {
            var request = new BugSenseRequest(e.ToBugSenseEx(comment), Instance.GetEnvironment());
            try
            {
                Instance.Send(request);
            }
            catch (Exception ex1)
            {
            }
        }

        private void Send(BugSenseRequest request)
        {
            string json = GetJson(request);
            if (!string.IsNullOrEmpty(json))
            {
                SaveToFile(json);
#if WINDOWS_RT
                Task.Run( () => ProccessSavedErrors() );
#elif iOS
                // TODO: Thread this.
                
                ProccessSavedErrors();
                //UIApplication.SharedApplication.BeginInvokeOnMainThread(ProccessSavedErrors);
#else
                Scheduler.NewThread.Schedule(ProccessSavedErrors);
#endif
            }
        }

        #endregion

        #region [ Private Helper Methods ]

        private string GetJson(BugSenseRequest request)
        {
            try
            {
                Log("Sending json ");
                using (MemoryStream ms = new MemoryStream())
                {
#if WINDOWS_RT
                    new DataContractJsonSerializer(typeof(BugSenseRequest)).WriteObject(ms, request);
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
            catch
            {
                Log("Error during BugSenseRequest serialization");
                return string.Empty;
            }
        }

        internal AppEnvironment GetEnvironment()
        {
            AppEnvironment environment = new AppEnvironment();
            environment.appname = _appName;
            environment.appver = _appVersion;
#if WINDOWS_RT
            // TODO: Currently there is no way to get the OS version
            // even for error logging purposes... which i suspect will
            // change before too long.
            environment.osver = "Windows 8 Metro";
#elif iOS
            environment.osver = UIDevice.CurrentDevice.SystemVersion;
#else
            environment.osver = Environment.OSVersion.Version.ToString();
#endif
            string result = string.Empty;

            object manufacturer;
            
#if WINDOWS_RT
#elif iOS
            result = DeviceIdentifier.Version.ToString();
#else
            //TODO: Find model
            if (DeviceExtendedProperties.TryGetValue("DeviceManufacturer", out manufacturer))
                result = manufacturer.ToString();
            object theModel;
            if (DeviceExtendedProperties.TryGetValue("DeviceName", out theModel))
                result = result + theModel;
#endif

            environment.phone = result;

            Debug.Assert(ScreenSize.X * ScreenSize.Y > 0, "Screen size was not set.");

            environment.ScreenWidth = ScreenSize.X;
            environment.ScreenHeight = ScreenSize.Y;

            environment.gps_on = "unavailable";
#if WINDOWS_RT
            environment.ScreenDpi = DisplayProperties.LogicalDpi.ToString();
#else
            environment.ScreenDpi = "unavailable";
#endif
            environment.ScreenOrientation = ScreenOrientation;
            environment.wifi_on = NetworkInterface.GetIsNetworkAvailable() ? bool.TrueString : bool.FalseString;
            return environment;
        }

        private static void ProccessFile(Stream fileStream, string filePath)
        {
            using (StreamReader sr = new StreamReader(fileStream))
            {
                string data = sr.ReadToEnd();
                ExecuteRequestAsync(data, filePath);
            }
        }

        private static void ExecuteRequestAsync(string errorJson, string filePath)
        {
            try
            {

                errorJson = "data=" + Uri.EscapeDataString(errorJson);
#if WINDOWS_RT

                var content = new StringContent(errorJson);

                var client = new HttpClient();
                //client.Headers.UserAgent.ParseAdd("WinRT");
                client.DefaultRequestHeaders.Add("User-Agent", "WinRT");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                content.Headers.Add("X-BugSense-Api-Key", G.API_KEY);

                var response = client.PostAsync(G.URL, content);
                response.Wait();
#else
                var request = HttpWebRequest.Create(new Uri(G.URL)) as HttpWebRequest;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
#if iOS
                request.UserAgent = "iOS";
#else
                request.UserAgent = "WP7";                
#endif // #if iOS
                
                request.Headers["X-BugSense-Api-Key"] = G.API_KEY;
                string contextFilePath = filePath;
                request.BeginGetRequestStream(ar =>
                {
                    try
                    {
                        var requestStream = request.EndGetRequestStream(ar);
                        using (var sw = new StreamWriter(requestStream))
                        {
                            sw.Write(ar.AsyncState);
                        }
                        
                        request.BeginGetResponse(a =>
                        {
                            try
                            {
                                var response = request.EndGetResponse(a);
#endif // #if WINDOWS_RT

#if WINDOWS_RT
                Task.Run(
                            async () =>
                            {
                                var fileName = filePath;
                                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                                await file.DeleteAsync();

                            }).Wait();
#else
                                //Error sent! Delete it!
                                if (File.Exists(contextFilePath))
                                    File.Delete(contextFilePath);
                            }
                            catch (Exception exc) { }
                        }, null);
                    }
                    catch (Exception ex) { }
                }, errorJson);
#endif
            }
            catch (Exception e) { /* Error is already saved so next time the app starts will try to send it again*/ }
        }

        private static void SaveToFile(string postData)
        {
            try
            {
                var fileName = string.Format(s_FileName, DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Guid.NewGuid());

#if WINDOWS_RT
                Task.Run( async () =>
                {
                    var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(s_FolderName, CreationCollisionOption.OpenIfExists);
                    var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                    using (var fileStream = await file.OpenStreamForWriteAsync())
                    {
                        using (var sw = new StreamWriter(fileStream))
                            sw.Write(postData);
                    }

                }).Wait();

#elif iOS
                var fullPath = Path.Combine(_dataPath, fileName);
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    using (var sw = new StreamWriter(fs))
                        sw.Write(postData);
                }
#else

                using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!storage.DirectoryExists(s_FolderName))
                        storage.CreateDirectory(s_FolderName);

                    using (var fileStream = storage.CreateFile(Path.Combine(s_FolderName, fileName)))
                    {
                        using (StreamWriter sw = new StreamWriter(fileStream))
                            sw.Write(postData);
                    }
                }
#endif
            }
            catch (Exception e) { /* Getting in here means the phone is about to explode */ }
        }

        private void ProccessSavedErrors()
        {
            try
            {
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
                            {
                                file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }

                            counter++;
                        }
                    });
#elif iOS
                    var path = _dataPath;
                    var fileNames = Directory.GetFiles(path);
                    
                    foreach (var file in fileNames)
                    {
                        // don't mess around with the lame DS_Store file
                        if (file.Contains("DS_Store"))
                            continue;
                        
                        int counter = 0;
                        
                        //If there are more exceptions in the pool we just delete them.
                        if (counter < s_MaxExceptions)
                        {
                            using (var fileStream = new FileStream(file, FileMode.Open))
                            {
                                ProccessFile(fileStream, file);
                            }
                        }    
                        else
                            File.Delete(file);
                        
                        counter++;
                    }   
#else

                using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (storage.DirectoryExists(s_FolderName))
                        {
                            var fileNames = storage.GetFileNames(s_FolderName + "\\*").OrderByDescending(s => s).ToList();
                            int counter = 0;
                            foreach (var fileName in fileNames)
                            {
                                if (string.IsNullOrEmpty(fileName))
                                    continue;

                                string filePath = Path.Combine(s_FolderName, fileName);
                                //If there are more exceptions in the pool we just delete them.
                                if (counter < s_MaxExceptions)
                                {
                                    using (var fileStream = storage.OpenFile(filePath, FileMode.Open))
                                    {
                                        ProccessFile(fileStream, filePath);
                                        counter++;
                                    }
                                }
                                else
                                    storage.DeleteFile(filePath);
                            }
                        }
                    }
#endif
            } catch (Exception e) { /* Swallow like a fish - Not much that we can do here */ }
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

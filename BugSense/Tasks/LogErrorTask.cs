using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using BugSense.Coroutines;
using BugSense.Extensions;
using BugSense.Internal;
using Path = System.IO.Path;

namespace BugSense.Tasks {
    internal class LogErrorTask : IResult {
        private readonly BugSenseRequest _request;
        private static readonly DataContractJsonSerializer _jsonSerializer;
        static LogErrorTask()
        {
            _jsonSerializer = new DataContractJsonSerializer(typeof(BugSenseRequest));
        }

        public LogErrorTask(BugSenseRequest request)
        {
            _request = request;
        }

        public void Execute(ActionExecutionContext context)
        {
            if (_request == null)
                return;
            string json = GetJson(_request);
            if (!string.IsNullOrEmpty(json)) {
                string fileName = SaveToFile(json);
                context["fileName"] = fileName;
            }
            TaskCompleted();
        }

        public event EventHandler<ResultCompletionEventArgs> Completed;

        public void TaskCompleted()
        {
            EventHandler<ResultCompletionEventArgs> handler = Completed;
            if (handler != null) handler(this, new ResultCompletionEventArgs());
        }



        private string GetJson(BugSenseRequest request)
        {
            try {
                Helpers.Log("Sending json ");
                using (MemoryStream ms = new MemoryStream()) {
                    _jsonSerializer.WriteObject(ms, request);
                    var array = ms.ToArray();
                    string json = Encoding.UTF8.GetString(array, 0, array.Length);
                    return json;
                }
            }
            catch {
                Helpers.Log("Error during BugSenseRequest serialization");
                return string.Empty;
            }
        }

        private static string SaveToFile(string postData)
        {
            try {
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                    if (!storage.DirectoryExists(G.FolderName))
                        storage.CreateDirectory(G.FolderName);

                    string fileName = string.Format(G.FileName, DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Guid.NewGuid());
                    using (var fileStream = storage.CreateFile(Path.Combine(G.FolderName, fileName))) {
                        using (StreamWriter sw = new StreamWriter(fileStream)) {
                            sw.Write(postData);
                        }
                    }
                    return fileName;
                }
            }
            catch { /* Getting in here means the phone is about to explode */
            }
            return string.Empty;
        }
    }
}

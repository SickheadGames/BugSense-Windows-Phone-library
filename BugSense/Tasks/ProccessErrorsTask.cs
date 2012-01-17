using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using BugSense.Coroutines;
using BugSense.Internal;

namespace BugSense.Tasks {
    internal class ProccessErrorsTask : IResult {
        public void Execute(ActionExecutionContext context)
        {
            try {
                var taskList = new List<IResult>();
                using (var storage = IsolatedStorageFile.GetUserStoreForApplication()) {
                    if (storage.DirectoryExists(G.FolderName)) {
                        var fileNames = storage.GetFileNames(G.FolderName + "\\*").OrderByDescending(s => s).ToList();
                        int counter = 0;
                        foreach (var fileName in fileNames) {
                            if (string.IsNullOrEmpty(fileName))
                                continue;
                            string filePath = Path.Combine(G.FolderName, fileName);
                            //If there are more exceptions in the pool we just delete them.
                            if (counter < G.MaxExceptions)
                                taskList.Add(new SendErrorTask(filePath));
                            else
                                storage.DeleteFile(filePath);
                            counter++;
                            //
                        }
                    }
                }
                Coroutine.BeginExecute(taskList.GetEnumerator());
            }
            //If this fails it probably due to an issue with the Isolated Storage.
            catch (Exception e) { /* Swallow like a fish - Not much that we can do here */}
            finally {
                TaskCompleted();
                
            }
        }

        public event EventHandler<ResultCompletionEventArgs> Completed;

        public void TaskCompleted()
        {
            EventHandler<ResultCompletionEventArgs> handler = Completed;
            if (handler != null) handler(this, new ResultCompletionEventArgs());
        }
    }
}

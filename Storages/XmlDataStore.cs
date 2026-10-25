using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Rocket.API;
using Rocket.Core.Assets;
using Rocket.Core.Logging;

namespace RFRocketLibrary.Storages
{
    public class XmlDataStore<T> : Asset<T> where T : class, IDefaultable
    {
        #region Methods

        public override void Load(AssetLoaded<T>? callback = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(DataPath) && File.Exists(DataPath))
                {
                    using var stream = File.OpenText(DataPath);
                    instance = (T) Serializer.Deserialize(stream);
                }

                if (callback == null)
                    return;

                callback(this);
            }
            catch (Exception e)
            {
                ReportError("Load", e);
            }
        }

        public Task LoadAsync(AssetLoaded<T>? callback = null)
        {
            // Return the actual background task so awaiting callers observe completion;
            // the previous implementation returned an already-completed task.
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    Load();
                }
                catch (Exception e)
                {
                    ReportError("LoadAsync", e);
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }

        public override T Save()
        {
            try
            {
                var directoryName = Path.GetDirectoryName(DataPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);

                if (instance == null)
                {
                    if (DefaultInstance == null)
                    {
                        instance = Activator.CreateInstance<T>();
                        instance.LoadDefaults();
                    }
                    else
                        instance = DefaultInstance;
                }

                var tempPath = DataPath + ".tmp";
                try
                {
                    using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    using (var streamWriter = new StreamWriter(stream))
                        Serializer.Serialize(streamWriter, instance);

                    if (File.Exists(DataPath))
                        File.Replace(tempPath, DataPath, null);
                    else
                        File.Move(tempPath, DataPath);
                }
                catch (IOException)
                {
                    // Destination locked or replace unsupported on this volume: preserve the
                    // historical truncate-and-write behavior instead of failing the save.
                    using (var stream = new FileStream(DataPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    using (var streamWriter = new StreamWriter(stream))
                        Serializer.Serialize(streamWriter, instance);

                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                        // A leftover .tmp file is harmless; the next save overwrites it.
                    }
                }

                return instance;
            }
            catch (Exception e)
            {
                ReportError("Save", e);
                return null;
            }
        }

        public Task<T> SaveAsync()
        {
            var source = new TaskCompletionSource<T>();
            Task.Factory.StartNew(() =>
            {
                try
                {
                    source.SetResult(Save());
                }
                catch (Exception e)
                {
                    source.SetException(e);
                    ReportError("SaveAsync", e);
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            return source.Task;
        }

        /// <summary>
        /// Reports a storage failure through the Rocket log. The report is itself guarded:
        /// Rocket's logging chain bottoms out in the game console (SDG.Unturned.UnturnedLog,
        /// which subscribes to Unity engine callbacks), so on hosts without the game the
        /// report would otherwise replace the storage failure with a crash. The log output
        /// format is unchanged on a regular server.
        /// </summary>
        private static void ReportError(string operation, Exception e)
        {
            try
            {
                var caller = new StackTrace().GetFrame(2).GetMethod().ReflectedType.Assembly.GetName().Name;
                Logger.LogError($"[{caller}] [ERROR] XmlDataStore {operation}: {e.Message}");
                Logger.LogError($"[{caller}] [ERROR] Details: {e}");
            }
            catch
            {
                // The storage result matters more than the log line.
            }
        }

        #endregion

        public XmlDataStore(string dir, string fileName, Type[]? extraTypes = null, T? defaultInstance = null)
        {
            DataPath = Path.Combine(dir, fileName);
            Serializer = new XmlSerializer(typeof (T), extraTypes ?? Type.EmptyTypes);
            DefaultInstance = defaultInstance;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(DataPath))
                File.CreateText(DataPath).Dispose();
        }

        private string DataPath { get; }
        private XmlSerializer Serializer { get; }
        private T? DefaultInstance { get; }
    }
}

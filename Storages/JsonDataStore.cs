using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rocket.Core.Logging;

namespace RFRocketLibrary.Storages
{
    public class JsonDataStore<T> where T : class
    {
        #region Methods

        public T? Load()
        {
            try
            {
                if (!File.Exists(DataPath))
                    return null;

                string dataText;
                using (var stream = File.OpenText(DataPath))
                    dataText = stream.ReadToEnd();

                return JsonConvert.DeserializeObject<T>(dataText);
            }
            catch (Exception e)
            {
                ReportError("Load", e);
                return null;
            }
        }

        public async Task<T?> LoadAsync()
        {
            try
            {
                if (!File.Exists(DataPath))
                    return null;
                string dataText;
                using (var stream = File.OpenText(DataPath))
                    dataText = await stream.ReadToEndAsync();

                return JsonConvert.DeserializeObject<T>(dataText);
            }
            catch (Exception e)
            {
                ReportError("LoadAsync", e);
                return null;
            }
        }

        public bool Save(T obj)
        {
            return Save(obj, Formatting.Indented);
        }

        public bool Save(T obj, Formatting format = Formatting.Indented)
        {
            try
            {
                var objData = JsonConvert.SerializeObject(obj, format);
                WriteAllTextAtomic(DataPath, objData);

                return true;
            }
            catch (Exception e)
            {
                ReportError("Save", e);
                return false;
            }
        }
        public async Task<bool> SaveAsync(T obj)
        {
            return await SaveAsync(obj, Formatting.Indented);
        }
        public async Task<bool> SaveAsync(T obj, Formatting format = Formatting.Indented)
        {
            try
            {
                // Serialization can be CPU-heavy for large datasets; keep it off the caller's thread.
                var objData = await Task.Run(() => JsonConvert.SerializeObject(obj, format));
                await Task.Run(() => WriteAllTextAtomic(DataPath, objData));

                return true;
            }
            catch (Exception e)
            {
                ReportError("SaveAsync", e);
                return false;
            }
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
                Logger.LogError($"[{caller}] [ERROR] JsonDataStore {operation}: {e.Message}");
                Logger.LogError($"[{caller}] [ERROR] Details: {e}");
            }
            catch
            {
                // The storage result matters more than the log line.
            }
        }

        /// <summary>
        /// Writes the file without truncating the destination first: the payload goes to a
        /// temporary file in the same directory, which is then swapped in via File.Replace /
        /// File.Move. A crash mid-write therefore leaves the previous content intact instead
        /// of a truncated file. If the destination is exclusively locked by another process
        /// (Replace is not permitted), fall back to the historical direct-write behavior so
        /// existing integrations keep working.
        /// </summary>
        private static void WriteAllTextAtomic(string dataPath, string contents)
        {
            var directory = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = dataPath + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var streamWriter = new StreamWriter(stream))
                    streamWriter.Write(contents);

                if (File.Exists(dataPath))
                    File.Replace(tempPath, dataPath, null);
                else
                    File.Move(tempPath, dataPath);
            }
            catch (IOException)
            {
                // Destination locked or replace unsupported on this volume: preserve the old
                // truncate-and-write semantics instead of failing the save outright.
                using (var stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var streamWriter = new StreamWriter(stream))
                    streamWriter.Write(contents);

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
        }

        #endregion

        public JsonDataStore(string dir, string fileName)
        {
            DataPath = Path.Combine(dir, fileName);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(DataPath))
                File.Create(DataPath).Dispose();
        }

        private string DataPath { get; set; }
    }
}
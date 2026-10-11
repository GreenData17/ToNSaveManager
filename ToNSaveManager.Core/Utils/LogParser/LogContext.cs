using System.Text;

namespace ToNSaveManager.Utils.LogParser {
    public class LogContext {
        public bool IsRecent;

        public long Length;
        public long Position;
        public long ReadPos;
        public bool Initialized { get; private set; }
        public virtual void OnInit() {
            Initialized = true;
        }

        public string FilePath { get; private set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;
        public string DateKey { get; private set;  } = string.Empty;
        public string? DisplayName;
        public string? UserID;
        public bool Authenticated;

        // Recent Instance info
        public bool IsLeavingInstance { get; private set; }
        public long InstanceReadPos { get; set; }
        public string? WorldName { get; private set; }

        public string? InstanceID { get; private set; }
        public bool IsToNWorld { get; private set; }

        public DateTime InstanceDate { get; private set; }
        public readonly HashSet<LogPlayer> Players = new HashSet<LogPlayer>();

        /*
        // Hold temporal data in this log instance
        private Dictionary<string, object> Data = new Dictionary<string, object>();
        public bool TryGet<T>(string key, out T? result) {
            if (HasKey(key)) {
                result = (T?)Data[key];
                return true;
            }

            result = default;
            return false;
        }
        public bool HasKey(string key) => Data.ContainsKey(key);
        public T? Get<T>(string key) {
            return HasKey(key) ? (T)Data[key] : default;
        }
        public void Set<T>(string key, T? value) {
            if (value == null) {
                Rem(key);
                return;
            }

            Data[key] = value;
        }
        public void Rem(string key) {
            if (HasKey(key)) Data.Remove(key);
        }
        */

        public virtual void OnAwake() { }

        public static T CreateContext<T>(string fileName, string filePath, bool isRecent) where T : LogContext {
            T context = Activator.CreateInstance<T>();
            context.FilePath = filePath;
            context.FileName = fileName;
            context.DateKey = fileName.Substring(11, 19);
            context.IsRecent = isRecent;
            context.OnAwake();
            return context;
        }

        /// <summary>
        /// Called when a player joins the instance.
        /// </summary>
        public virtual void Join(LogPlayer player) {
            if (!Players.Contains(player))
                Players.Add(player);
        }

        /// <summary>
        /// Called when a player leaves the instance.
        /// </summary>
        public virtual void Leave(LogPlayer player) {
            if (Players.Contains(player))
                Players.Remove(player);
        }

        /// <summary>
        /// Called when user enters a world, before the instance is known.
        /// </summary>
        public virtual void Enter(string name, DateTime date) {
            WorldName = name;
            InstanceDate = date;
            Players.Clear();

            Logger.Info("Entering world: " + name);
            IsLeavingInstance = false;
        }
        /// <summary>
        /// Called when user is leaving the instance.
        /// </summary>
        public virtual void Exit() {
            Logger.Info("Leaving instance...");
            IsLeavingInstance = true;
        }
        public virtual void Enter(string instanceID, bool isToNWorld) {
            InstanceID = instanceID;
            IsToNWorld = isToNWorld;

            Logger.Info($"Instance [{isToNWorld}] : {instanceID}");
            IsLeavingInstance = false;
        }

        public virtual void Pickup(string name) {

        }
        public virtual void Drop(string name) {

        }

        public virtual bool Validate(long position, FileInfo fileInfo) {
            return true;
        }

        /// <summary>
        /// Get's a list of players in this instance as a string.
        /// </summary>
        public string GetPlayerList(bool lineBreak = true) {
            StringBuilder sb = new StringBuilder();
            const string start = "- ";
            sb.Append(start);
            sb.AppendJoin(lineBreak ? Environment.NewLine + start : ", ", Players);
            return sb.ToString();
        }

        public string GetInstanceLogs() {
            string content;

            using (FileStream fileStream = new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                fileStream.Seek(InstanceReadPos, SeekOrigin.Begin);

                using (StreamReader reader = new StreamReader(fileStream)) {
                    content = reader.ReadToEnd();
                }
            }

            return content;
        }
    }
}

using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace War3Trainer
{
    [Serializable]
    public class UnkonwnGameVersionExpection
        : Exception, ISerializable
    {
        public int ProcessId { get; private set; }
        public string GameVersion { get; private set; }

        public UnkonwnGameVersionExpection()
            : base("Game version can not be recognized.")
        {

        }

        public UnkonwnGameVersionExpection(string message)
            : base(message)
        {

        }

        public UnkonwnGameVersionExpection(string message, Exception innerException)
            : base(message, innerException)
        {

        }

        public UnkonwnGameVersionExpection(int processId, string gameVersion)
        {
            ProcessId = processId;
            GameVersion = gameVersion;
        }

        public UnkonwnGameVersionExpection(string message, int processId, string gameVersion)
            : base(message)
        {
            ProcessId = processId;
            GameVersion = gameVersion;
        }

        public UnkonwnGameVersionExpection(string message, int processId, string gameVersion, Exception innerException)
            : base(message, innerException)
        {
            ProcessId = processId;
            GameVersion = gameVersion;
        }

        protected UnkonwnGameVersionExpection(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ProcessId = info.GetInt32("ProcessId");
            GameVersion = info.GetString("GameVersion");
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            base.GetObjectData(info, context);
            info.AddValue("ProcessId", ProcessId, typeof(int));
            info.AddValue("GameVersion", GameVersion, typeof(string));
        }

        public override string Message
        {
            get
            {
                string message = base.Message;
                if (ProcessId != 0)
                {
                    return message
                        + string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            "{0}ProcessId = {1}{0}GameVersion = {2}",
                            Environment.NewLine,
                            ProcessId,
                            GameVersion);
                }
                return message;
            }
        }
    }
}

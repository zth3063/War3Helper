using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace War3Trainer.WindowsApi
{
    [Serializable]
    public class BadProcessIdException
        : Exception, ISerializable
    {
        public int ProcessId { get; private set; }

        public BadProcessIdException()
            : base("Process ID can not be feteched or incorrect.")
        {

        }

        public BadProcessIdException(string message)
            : base(message)
        {

        }

        public BadProcessIdException(string message, Exception innerException)
            : base(message, innerException)
        {

        }

        public BadProcessIdException(int processId)
        {
            ProcessId = processId;
        }

        public BadProcessIdException(string message, int processId)
            : base(message)
        {
            ProcessId = processId;
        }

        public BadProcessIdException(string message, int processId, Exception innerException)
            : base(message, innerException)
        {
            ProcessId = processId;
        }

        protected BadProcessIdException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ProcessId = info.GetInt32("ProcessId");
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
        }

        public override string Message
        {
            get
            {
                string message = base.Message;
                if (ProcessId != 0)
                {
                    return message
                        + Environment.NewLine
                        + string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            "ProcessId = {0}",
                            ProcessId);
                }
                return message;
            }
        }
    }
}

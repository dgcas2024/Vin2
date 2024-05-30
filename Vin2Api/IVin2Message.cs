namespace Vin2Api
{
    public interface IVin2Message
    {
        public void Message(string message);
        public void Error(string message);
        public void Warn(string message);
        public void Success(string message);
        public void Info(string message);
        public void ReSendMessageStorage();
        public void SendObject(object obj);
    }
}

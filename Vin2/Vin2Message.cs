using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System;
using Vin2Api;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Vin2
{
    public class Vin2Hub : Hub
    {
        private readonly IVin2Message _vin2Message;
        public Vin2Hub(IVin2Message vin2Message) 
        {
            _vin2Message = vin2Message;
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public void TakeMessage()
        {
            _vin2Message.ReSendMessageStorage();
        }
    }

    public class Vin2Message : IVin2Message
    {
        private readonly IHubContext<Vin2Hub> _vin2HubContext;

        private readonly ConcurrentQueue<KeyValuePair<string, string>> _messageStorage = new();
        private ConcurrentQueue<KeyValuePair<string, string>> MessageStorage
        {
            get
            {
                while (_messageStorage.Count >= 5000)
                {
                    _messageStorage.TryDequeue(out _);
                }
                return _messageStorage;
            }
        }

        public Vin2Message(IHubContext<Vin2Hub> vin2HubContext)
        {
            _vin2HubContext = vin2HubContext;
        }

        private void Send(string function, string message, bool isResend)
        {
            if (!isResend)
            {
                message = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}: {message}";
                MessageStorage.Enqueue(new KeyValuePair<string, string>(function, message));
            }
            _vin2HubContext.Clients.All.SendAsync(function, message);
        }

        public void Message(string message)
        {
            Send("message", message, false);
        }

        public void Error(string message)
        {
            Send("error", message, false);
        }

        public void Info(string message)
        {
            Send("info", message, false);
        }

        public void Success(string message)
        {
            Send("success", message, false);
        }

        public void Warn(string message)
        {
            Send("warn", message, false);
        }

        public void ReSendMessageStorage()
        {
            var messages = MessageStorage.ToArray();
            foreach (var message in messages)
            {
                Send(message.Key, message.Value, true);
            }
        }

        public void SendObject(object obj)
        {
            _vin2HubContext.Clients.All.SendAsync("receiveObject", obj);
        }
    }
}

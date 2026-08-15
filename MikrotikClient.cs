using System;
using System.Collections.Generic;
using RouterOS;

namespace MikrotikSwitch
{
    public class PortInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Running { get; set; }
        public bool Disabled { get; set; }
    }

    public class MikrotikClient : IDisposable
    {
        private Client _client;
        private readonly object _lock = new object();

        public MikrotikClient(string address, string user, string pass)
        {
            var parts = address.Split(':');
            string host = parts[0];
            int port = int.Parse(parts[1]);

            _client = new Client(host, port, user, pass);
            // Проверка соединения
            _client.SendCommand("/system/identity/print");
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                var reply = _client.SendCommand("/interface/ethernet/print");
                var result = new List<PortInfo>();

                // reply – это массив словарей
                foreach (var sentence in reply)
                {
                    if (sentence.TryGetValue(".id", out string id))
                    {
                        result.Add(new PortInfo
                        {
                            Id = id,
                            Name = sentence.GetValueOrDefault("name", ""),
                            Running = sentence.GetValueOrDefault("running") == "true",
                            Disabled = sentence.GetValueOrDefault("disabled") == "true"
                        });
                    }
                }
                return result;
            }
        }

        public void SetPortEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                string cmd = enabled ? "/interface/enable" : "/interface/disable";
                _client.SendCommand(cmd, $"=.id={id}");
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using Mikrotik.RouterOS;

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
        private Connection _connection;
        private readonly string _address;
        private readonly string _user;
        private readonly string _pass;
        private readonly object _lock = new object();

        public MikrotikClient(string address, string user, string pass)
        {
            _address = address;
            _user = user;
            _pass = pass;
            Connect();
        }

        private void Connect()
        {
            var parts = _address.Split(':');
            string host = parts[0];
            int port = int.Parse(parts[1]);

            _connection = new Connection(host, port, _user, _pass);
            _connection.Open();
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                try
                {
                    var reply = _connection.SendCommand("/interface/ethernet/print");
                    var result = new List<PortInfo>();
                    foreach (var sentence in reply.Sentences)
                    {
                        var dict = sentence.ToDictionary();
                        if (dict.ContainsKey(".id"))
                        {
                            var pi = new PortInfo
                            {
                                Id = dict[".id"],
                                Name = dict.ContainsKey("name") ? dict["name"] : "",
                                Running = dict.ContainsKey("running") && dict["running"] == "true",
                                Disabled = dict.ContainsKey("disabled") && dict["disabled"] == "true"
                            };
                            result.Add(pi);
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception("Ошибка при получении портов: " + ex.Message);
                }
            }
        }

        public void SetPortEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                string cmd = enabled ? "/interface/enable" : "/interface/disable";
                _connection.SendCommand(cmd, $"=.id={id}");
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}

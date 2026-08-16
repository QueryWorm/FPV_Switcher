using System;
using System.Collections.Generic;
using tik4net;

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
        private ITikConnection _connection;
        private readonly object _lock = new object();

        public MikrotikClient(string address, string user, string pass)
        {
            var parts = address.Split(':');
            string host = parts[0];
            int port = int.Parse(parts[1]);

            _connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            _connection.Open(host, user, pass, port);
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                var result = new List<PortInfo>();

                try
                {
                    var reply = _connection.Call("/interface/ethernet/print");

                    foreach (var sentence in reply)
                    {
                        if (sentence.ContainsKey(".id"))
                        {
                            result.Add(new PortInfo
                            {
                                Id = sentence[".id"],
                                Name = sentence.ContainsKey("name") ? sentence["name"] : "",
                                Running = sentence.ContainsKey("running") && sentence["running"] == "true",
                                Disabled = sentence.ContainsKey("disabled") && sentence["disabled"] == "true"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error listing ethernet ports: {ex.Message}", ex);
                }

                return result;
            }
        }

        public void SetPortEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                try
                {
                    string cmd = enabled ? "/interface/enable" : "/interface/disable";
                    _connection.Call(cmd, new Dictionary<string, string> { { ".id", id } });
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error setting port state: {ex.Message}", ex);
                }
            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                try
                {
                    _connection.Close();
                }
                catch { }
                _connection.Dispose();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
            _connection.Open(host, user, pass);
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                var result = new List<PortInfo>();

                try
                {
                    var reply = _connection.CallCommandSync("/interface/ethernet/print");

                    foreach (var sentence in reply)
                    {
                        if (sentence.Words.ContainsKey(".id"))
                        {
                            result.Add(new PortInfo
                            {
                                Id = sentence.Words[".id"],
                                Name = sentence.Words.ContainsKey("name") ? sentence.Words["name"] : "",
                                Running = sentence.Words.ContainsKey("running") && sentence.Words["running"] == "true",
                                Disabled = sentence.Words.ContainsKey("disabled") && sentence.Words["disabled"] == "true"
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
                    _connection.CallCommandSync(cmd, new[] { new KeyValuePair<string, string>(".id", id) });
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

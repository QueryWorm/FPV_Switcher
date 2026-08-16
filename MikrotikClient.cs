using System;
using System.Collections.Generic;
using System.Linq;
using tik4net;
using tik4net.Objects.Interface;

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

            _connection = TikConnectionFactory.CreateConnection(TikConnectorType.Api);
            _connection.Open(host, user, pass, port);
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                var result = new List<PortInfo>();

                try
                {
                    var interfaces = _connection.CreateQuery<Interface>().ToList();

                    foreach (var iface in interfaces)
                    {
                        result.Add(new PortInfo
                        {
                            Id = iface.Id,
                            Name = iface.Name ?? "",
                            Running = iface.Running == true,
                            Disabled = iface.Disabled == true
                        });
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
                    var query = _connection.CreateQuery<Interface>();
                    var iface = query.FirstOrDefault(i => i.Id == id);

                    if (iface != null)
                    {
                        iface.Disabled = !enabled;
                        _connection.SaveEntity(iface);
                    }
                    else
                    {
                        throw new Exception($"Interface with id {id} not found");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error setting port state: {ex.Message}", ex);
                }
            }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}

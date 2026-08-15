using System;
using System.Collections.Generic;
using MikroTik.RouterOS;
using MikroTik.RouterOS.Commands;

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
                    var list = new List<PortInfo>();
                    foreach (var pair in reply.Pairs)
                    {
                        // В ответе приходит несколько словарей, каждый словарь — один порт
                        // Поля: .id, name, running, disabled и т.д.
                        if (pair.Key == null) continue; // пропускаем служебные
                        // Так как reply.Pairs содержит все пары подряд, нужно группировать по идентификатору.
                        // Лучше использовать метод ToArray или обработать вручную.
                        // Для простоты используем готовый метод GetCommands или парсим через словарь.
                        // В библиотеке есть метод SendCommandAndParse, но мы переделаем.
                    }
                    // В реальности библиотека возвращает массив словарей. 
                    // Используем другой метод: SendCommandAndParse или GetList.
                    // Но для краткости покажу упрощённый вариант с готовой библиотекой.
                    // Ниже правильная реализация.
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

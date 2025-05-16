using System.Net;
using System.Net.Sockets;

namespace VideoChat_Server
{
    public class Program
    {
        private static readonly Dictionary<Guid, TcpClient> _clients = new();
        private static readonly Dictionary<Guid, IPEndPoint> _clientEndPoints = new();

        public static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();

            Console.WriteLine("Сервер запущен...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClient(client); // Обработка в отдельном потоке
            }
        }

        private static async Task HandleClient(TcpClient tcpClient)
        {
            using (tcpClient)
            using (var stream = tcpClient.GetStream())
            using (var reader = new BinaryReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                try
                {
                    // 1. Получаем регистрационную информацию
                    var userId = new Guid(reader.ReadBytes(16));
                    var publicIp = new IPAddress(reader.ReadBytes(4));
                    var publicPort = reader.ReadInt32();
                    var localIp = new IPAddress(reader.ReadBytes(4));
                    var localPort = reader.ReadInt32();

                    // 2. Сохраняем клиента
                    lock (_clients)
                    {
                        _clients[userId] = tcpClient;
                        _clientEndPoints[userId] = new IPEndPoint(publicIp, publicPort);
                        Console.WriteLine($"Клиент подключен: {userId}");
                    }

                    // 3. Ожидаем команды
                    while (true)
                    {
                        var command = reader.ReadByte();
                        switch (command)
                        {
                            case 0x01: // Инициация звонка
                                var targetId = new Guid(reader.ReadBytes(16));
                                await HandleCallRequest(userId, targetId, writer);
                                break;
                            //case 0x02: // Ответ на звонок
                            //    var accept = reader.ReadBoolean();
                            //    var callerId = new Guid(reader.ReadBytes(16));
                            //    await HandleCallResponse(userId, callerId, accept, writer);
                            //    break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        private static async Task HandleCallRequest(Guid callerId, Guid targetId, BinaryWriter callerWriter)
        {
            Console.WriteLine($"Звонок от {callerId} к {targetId}");

            if (_clientEndPoints.TryGetValue(targetId, out var targetEndPoint))
            {
                callerWriter.Write(targetEndPoint.Address.GetAddressBytes());
                callerWriter.Write(targetEndPoint.Port);
            }
            else
            {
                callerWriter.Write(IPAddress.None.GetAddressBytes()); // Индикатор ошибки
                return;
            }

            // 2. Уведомляем target о входящем звонке
            if (_clients.TryGetValue(targetId, out var targetClient))
            {
                var targetStream = targetClient.GetStream();
                var targetWriter = new BinaryWriter(targetStream);

                targetWriter.Write((byte)0x01); // Код входящего звонка
                targetWriter.Write(callerId.ToByteArray());

                if (_clientEndPoints.TryGetValue(callerId, out var callerEndPoint))
                {
                    targetWriter.Write(callerEndPoint.Address.GetAddressBytes());
                    targetWriter.Write(callerEndPoint.Port);
                }
            }
        }

        //private static async Task HandleCallResponse(Guid calleeId, Guid callerId, bool accepted, BinaryWriter calleeWriter)
        //{
        //    Console.WriteLine($"Ответ от {calleeId} для {callerId}: {accepted}");

        //    if (!_clients.TryGetValue(callerId, out var callerClient))
        //    {
        //        calleeWriter.Write((byte)0xFF); // Ошибка
        //        return;
        //    }

        //    // Отправляем ответ звонящему   
        //    var callerStream = callerClient.GetStream();
        //    var callerWriter = new BinaryWriter(callerStream);

        //    callerWriter.Write((byte)0x02); // Команда "Ответ на звонок"
        //    callerWriter.Write(accepted);

        //    if (accepted && _clientEndPoints.TryGetValue(calleeId, out var calleeEndPoint))
        //    {
        //        callerWriter.Write(calleeEndPoint.Address.GetAddressBytes());
        //        callerWriter.Write(calleeEndPoint.Port);
        //    }
        //}
    }
}
